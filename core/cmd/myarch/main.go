// Command myarch applies a theme and the enabled plugins to the desktop.
package main

import (
	"errors"
	"flag"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strings"

	"myarch/internal/apply"
	"myarch/internal/config"
	"myarch/internal/locale"
	"myarch/internal/plugin"
	"myarch/internal/render"
	"myarch/internal/theme"
)

const usage = `usage: myarch <command> [flags]

commands:
  apply [--theme ID] [--dry-run] [--adopt]
                 render every enabled plugin with the theme and write the files
  plugins        list plugins and what they generate
  themes         list available themes
`

func main() {
	if len(os.Args) < 2 {
		fmt.Fprint(os.Stderr, usage)
		os.Exit(2)
	}
	var err error
	switch os.Args[1] {
	case "apply":
		err = cmdApply(os.Args[2:])
	case "plugins":
		err = cmdPlugins()
	case "themes":
		err = cmdThemes()
	case "-h", "--help", "help":
		fmt.Print(usage)
	default:
		fmt.Fprint(os.Stderr, usage)
		os.Exit(2)
	}
	if err != nil {
		fmt.Fprintln(os.Stderr, "myarch:", err)
		os.Exit(1)
	}
}

// root is the directory holding the built-in plugins/ and themes/:
// $MYARCH_ROOT, or the parent of the directory the binary lives in.
func root() string {
	if r := os.Getenv("MYARCH_ROOT"); r != "" {
		return r
	}
	exe, err := os.Executable()
	if err != nil {
		return "."
	}
	exe, _ = filepath.EvalSymlinks(exe)
	return filepath.Dir(filepath.Dir(exe))
}

// Search paths: the user's directory first, so it can shadow built-ins.
func pluginDirs() []string {
	return []string{render.ExpandHome("~/.local/share/myarch/plugins"), filepath.Join(root(), "plugins")}
}

func themeDirs() []string {
	return []string{render.ExpandHome("~/.local/share/myarch/themes"), filepath.Join(root(), "themes")}
}

func enabledPlugins(cfg *config.Config) ([]*plugin.Plugin, error) {
	all, err := plugin.Discover(pluginDirs())
	if err != nil {
		return nil, err
	}
	var out []*plugin.Plugin
	for _, p := range all {
		if !cfg.IsDisabled(p.ID()) {
			out = append(out, p)
		}
	}
	return out, nil
}

func cmdApply(args []string) error {
	fs := flag.NewFlagSet("apply", flag.ExitOnError)
	themeID := fs.String("theme", "", "switch to this theme (saved in config)")
	dryRun := fs.Bool("dry-run", false, "show what would change, write nothing")
	adopt := fs.Bool("adopt", false, "back up and take over files myarch didn't write")
	fs.Parse(args)

	cfg, err := config.Load()
	if err != nil {
		return err
	}
	if *themeID != "" {
		cfg.Theme = *themeID
	}
	if cfg.Theme == "" {
		return fmt.Errorf("no theme selected; available: %s\n  myarch apply --theme <id>",
			strings.Join(theme.List(themeDirs()), ", "))
	}
	t, err := theme.Load(themeDirs(), cfg.Theme)
	if err != nil {
		return err
	}
	plugins, err := enabledPlugins(cfg)
	if err != nil {
		return err
	}
	warnMissingPackages(plugins)

	if err := checkOverrides(cfg); err != nil {
		return err
	}
	lang := locale.Detect(cfg.Language)
	files, err := render.All(plugins, t, cfg.Plugins, lang)
	if err != nil {
		return err
	}
	owned, err := apply.LoadOwned(apply.StatePath())
	if err != nil {
		return err
	}
	changes, orphans, err := apply.Plan(files, owned)
	if err != nil {
		return err
	}

	fmt.Printf("theme %s, language %s, %d plugins\n", t.ID, lang, len(plugins))
	for _, c := range changes {
		fmt.Printf("  %-9s %-16s %s\n", c.State, c.Plugin, tilde(c.Path))
	}
	for _, o := range orphans {
		fmt.Printf("  %-9s %-16s %s\n", "orphan", "-", tilde(o))
	}
	if *dryRun {
		return nil
	}

	res, err := apply.Execute(changes, orphans, owned, *adopt)
	var ce *apply.ConflictError
	if errors.As(err, &ce) {
		return err
	}
	// Record whatever was written, even after a partial failure.
	if serr := owned.Save(apply.StatePath()); serr != nil && err == nil {
		err = serr
	}
	if err != nil {
		return err
	}
	if *themeID != "" {
		if err := cfg.Save(); err != nil {
			return err
		}
	}

	for p, bak := range res.Backups {
		fmt.Printf("backed up %s -> %s\n", tilde(p), filepath.Base(bak))
	}
	for _, p := range res.Removed {
		fmt.Printf("removed %s (plugin no longer enabled)\n", tilde(p))
	}
	for _, p := range res.Kept {
		fmt.Printf("left %s in place: it was edited, no longer managed\n", tilde(p))
	}
	for cmd, e := range apply.Reload(res.Written) {
		fmt.Fprintf(os.Stderr, "warning: reload %q failed: %v\n", cmd, e)
	}
	fmt.Printf("%d written\n", len(res.Written))
	return nil
}

func cmdPlugins() error {
	cfg, err := config.Load()
	if err != nil {
		return err
	}
	all, err := plugin.Discover(pluginDirs())
	if err != nil {
		return err
	}
	for _, p := range all {
		state := "enabled"
		if cfg.IsDisabled(p.ID()) {
			state = "disabled"
		}
		fmt.Printf("%-16s %-8s %-8s %s\n", p.ID(), p.Meta.Version, state, p.Meta.Description)
		settings, err := p.Resolve(cfg.Plugins[p.ID()])
		if err != nil {
			return err
		}
		for _, k := range sortedKeys(settings) {
			mark := ""
			if _, set := cfg.Plugins[p.ID()][k]; set {
				mark = "  (config.toml)"
			}
			fmt.Printf("  %s = %#v%s\n", k, settings[k], mark)
		}
		for _, t := range p.Targets {
			fmt.Printf("  -> %s\n", t.Output)
		}
	}
	return nil
}

func cmdThemes() error {
	cfg, _ := config.Load()
	for _, id := range theme.List(themeDirs()) {
		mark := " "
		if id == cfg.Theme {
			mark = "*"
		}
		fmt.Println(mark, id)
	}
	return nil
}

// checkOverrides rejects [plugins.<id>] sections for plugins that don't
// exist, so a misspelled id doesn't silently do nothing.
func checkOverrides(cfg *config.Config) error {
	all, err := plugin.Discover(pluginDirs())
	if err != nil {
		return err
	}
	known := map[string]bool{}
	for _, p := range all {
		known[p.ID()] = true
	}
	for id := range cfg.Plugins {
		if !known[id] {
			return fmt.Errorf("%s: [plugins.%s]: no such plugin", config.Path(), id)
		}
	}
	return nil
}

func sortedKeys(m map[string]any) []string {
	ks := make([]string, 0, len(m))
	for k := range m {
		ks = append(ks, k)
	}
	sort.Strings(ks)
	return ks
}

func warnMissingPackages(plugins []*plugin.Plugin) {
	if _, err := exec.LookPath("pacman"); err != nil {
		return
	}
	for _, p := range plugins {
		for _, pkg := range p.Packages.Pacman {
			if exec.Command("pacman", "-Q", pkg).Run() != nil {
				fmt.Fprintf(os.Stderr, "warning: plugin %s needs package %s (not installed)\n", p.ID(), pkg)
			}
		}
	}
}

func tilde(p string) string {
	home, _ := os.UserHomeDir()
	if strings.HasPrefix(p, home+"/") {
		return "~" + p[len(home):]
	}
	return p
}
