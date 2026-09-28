// Command myarch applies a theme and the enabled plugins to the desktop.
package main

import (
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"math"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"sort"
	"strings"

	"myarch/internal/apply"
	"myarch/internal/config"
	"myarch/internal/coverage"
	"myarch/internal/install"
	"myarch/internal/locale"
	"myarch/internal/plugin"
	"myarch/internal/render"
	"myarch/internal/theme"
)

const usage = `usage: myarch <command> [flags]

commands:
  apply [--theme ID] [--dry-run] [--adopt]
                 render every enabled plugin with the theme and write the files
  update [-y] [--check] [--no-rollback]
                 update the system: preview, upgrade, re-apply, check, and
                 roll back on its own if a check fails
  doctor         run every plugin's checks now
  history        past updates and how they went
  rollback [ID]  put back the packages and files from before an update
                 (the last one by default)
  plugins [list]  plugins, where they come from, and their state
  plugins show ID
                 what a plugin needs and does: requirements, settings,
                 files, commands, code
  plugins enable|disable ID...
  plugins add URL[#REF] [-y]
                 install a plugin from git, after approving what it can do
  plugins update [ID[#REF]...] [-y]
                 update plugins from git (to another branch or tag with
                 #REF); asks before any new capability
  plugins remove ID...
  plugins sync   install exactly what plugins.lock says (another machine)
  coverage [--json]
                 installed apps, and whether the theme reaches them
  themes [--json]
                 list themes, with their contrast problems
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
	case "update":
		err = cmdUpdate(os.Args[2:])
	case "doctor":
		err = cmdDoctor()
	case "history":
		err = cmdHistory()
	case "rollback":
		err = cmdRollback(os.Args[2:])
	case "plugins":
		err = cmdPlugins(os.Args[2:])
	case "coverage":
		err = cmdCoverage(os.Args[2:])
	case "themes":
		err = cmdThemes(os.Args[2:])
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
	return []string{install.Dir(), filepath.Join(root(), "plugins")}
}

func themeDirs() []string {
	return []string{render.ExpandHome("~/.local/share/myarch/themes"), filepath.Join(root(), "themes")}
}

// enabledPlugins are the plugins to apply. It refuses when one doesn't
// load, needs a plugin that isn't there, or is a plugin from git that isn't
// what plugins.lock says (moved, edited, or needing more than was
// approved): applying would run what nobody agreed to.
func enabledPlugins(cfg *config.Config) ([]*plugin.Plugin, error) {
	all, broken := plugin.Discover(pluginDirs())
	lock, err := install.LoadLock()
	if err != nil {
		return nil, err
	}
	var out, failed []*plugin.Plugin
	var problems []string
	for _, id := range sortedKeys(broken) {
		if !cfg.IsDisabled(id) {
			problems = append(problems, broken[id].Error())
		}
	}
	found := map[string]bool{}
	for _, p := range all {
		found[p.ID()] = true
		if cfg.IsDisabled(p.ID()) {
			continue
		}
		if err := trusted(lock, p); err != nil {
			problems = append(problems, err.Error())
			failed = append(failed, p)
			continue
		}
		out = append(out, p)
	}
	for _, e := range lock.Plugins {
		if !found[e.ID] && broken[e.ID] == nil && !cfg.IsDisabled(e.ID) {
			problems = append(problems, e.ID+" is in plugins.lock but not installed (run: myarch plugins sync)")
		}
	}
	// Those that failed still count as there: their dependents' problem is
	// theirs, not a missing plugin.
	for _, pr := range plugin.Unmet(append(append([]*plugin.Plugin{}, out...), failed...), all, broken) {
		problems = append(problems, pr.Text)
	}
	if len(problems) > 0 {
		return nil, errors.New("plugins:\n  " + strings.Join(problems, "\n  "))
	}
	return out, nil
}

// trusted: built-ins and your own plugins are; one from git must be what
// plugins.lock says.
func trusted(lock *install.Lock, p *plugin.Plugin) error {
	e := lock.Get(p.ID())
	fromGit := filepath.Dir(p.Dir) == install.Dir()
	switch {
	case e != nil && !fromGit:
		return fmt.Errorf("%s is in plugins.lock but not installed (run: myarch plugins sync)", p.ID())
	case e == nil && fromGit && isCheckout(p.Dir):
		return fmt.Errorf("%s is a git checkout that isn't in plugins.lock; add it with myarch plugins add, or delete %s",
			p.ID(), tilde(p.Dir))
	case e != nil:
		return install.Verify(p.Dir, e, p)
	}
	return nil
}

func isCheckout(dir string) bool {
	_, err := os.Lstat(filepath.Join(dir, ".git"))
	return err == nil
}

// lockedEntry is p's plugins.lock entry when p is the plugin installed
// from git; a plugin elsewhere with the same id isn't.
func lockedEntry(lock *install.Lock, p *plugin.Plugin) *install.Entry {
	e := lock.Get(p.ID())
	if e == nil || filepath.Dir(p.Dir) != install.Dir() {
		return nil
	}
	return e
}

// session is everything loaded and rendered for one run: config, theme,
// the enabled plugins, the language, and what they produce.
type session struct {
	cfg     *config.Config
	theme   *theme.Theme
	plugins []*plugin.Plugin
	lang    string
	out     *render.Output
}

// load reads the config, the theme and the enabled plugins, and renders
// everything.
func load() (*session, error) {
	return loadWith(nil)
}

// loadWith is load with config changes that aren't saved yet (switching
// theme or accent), so they're only saved once they render.
func loadWith(change func(*config.Config)) (*session, error) {
	cfg, err := config.Load()
	if err != nil {
		return nil, err
	}
	if change != nil {
		change(cfg)
	}
	if cfg.Theme == "" {
		return nil, fmt.Errorf("no theme selected; available: %s\n  myarch apply --theme <id>",
			strings.Join(theme.List(themeDirs()), ", "))
	}
	t, err := theme.Load(themeDirs(), cfg.Theme)
	if err != nil {
		return nil, err
	}
	if cfg.Accent != "" {
		if err := t.WithAccent(cfg.Accent); err != nil {
			return nil, fmt.Errorf("accent: %w", err)
		}
	}
	plugins, err := enabledPlugins(cfg)
	if err != nil {
		return nil, err
	}
	if err := checkOverrides(cfg); err != nil {
		return nil, err
	}
	lang := locale.Detect(cfg.Language)
	out, err := render.All(plugins, t, cfg.Plugins, lang)
	if err != nil {
		return nil, err
	}
	return &session{cfg: cfg, theme: t, plugins: plugins, lang: lang, out: out}, nil
}

// plan compares the rendered files with the disk.
func (s *session) plan() ([]apply.Change, []string, apply.Owned, error) {
	owned, err := apply.LoadOwned(apply.StatePath())
	if err != nil {
		return nil, nil, nil, err
	}
	changes, orphans, err := apply.Plan(s.out.Files, owned)
	return changes, orphans, owned, err
}

// write applies the plan, records ownership, runs reload commands and
// prints what happened.
func (s *session) write(changes []apply.Change, orphans []string, owned apply.Owned, adopt bool) error {
	res, err := apply.Execute(changes, orphans, owned, adopt)
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
	for p, bak := range res.Backups {
		fmt.Printf("backed up %s -> %s\n", tilde(p), filepath.Base(bak))
	}
	for _, p := range res.Removed {
		fmt.Printf("removed %s (no plugin generates it anymore)\n", tilde(p))
	}
	for _, p := range res.Kept {
		fmt.Printf("left %s in place: it was edited, no longer managed\n", tilde(p))
	}
	for _, p := range res.Shared {
		fmt.Printf("left %s in place: it's its app's file too, no longer managed\n", tilde(p))
	}
	for cmd, e := range apply.Reload(res.Written) {
		fmt.Fprintf(os.Stderr, "warning: reload %q failed: %v\n", cmd, e)
	}
	fmt.Printf("%d written\n", len(res.Written))
	return nil
}

func printPlan(changes []apply.Change, orphans []string, onlyChanges bool) {
	for _, c := range changes {
		if onlyChanges && c.State == apply.Unchanged {
			continue
		}
		fmt.Printf("  %-10s %-16s %s\n", c.State, c.Plugin, tilde(c.Path))
	}
	for _, o := range orphans {
		fmt.Printf("  %-10s %-16s %s\n", "orphan", "-", tilde(o))
	}
	busy, unreadable := false, false
	for _, c := range changes {
		busy = busy || c.State == apply.Busy
		unreadable = unreadable || c.State == apply.Unreadable
	}
	if busy {
		fmt.Printf("  %sbusy: its app is running and would write its own copy back; close it and apply again%s\n", dim, reset)
	}
	if unreadable {
		fmt.Printf("  %sunreadable: not plain JSON (comments?), left as it is; set what myarch would by hand%s\n", dim, reset)
	}
}

func cmdApply(args []string) error {
	fs := flag.NewFlagSet("apply", flag.ExitOnError)
	themeID := fs.String("theme", "", "switch to this theme (saved in config)")
	accent := fs.String("accent", "", "use this accent color, #rrggbb; \"theme\" for the theme's own (saved in config)")
	dryRun := fs.Bool("dry-run", false, "show what would change, write nothing")
	adopt := fs.Bool("adopt", false, "back up and take over files myarch didn't write")
	fs.Parse(args)
	if *accent != "" && *accent != "theme" && !accentPattern.MatchString(*accent) {
		return fmt.Errorf("--accent %q: use #rrggbb, or \"theme\" for the theme's own", *accent)
	}

	s, err := loadWith(func(c *config.Config) {
		if *themeID != "" {
			c.Theme = *themeID
		}
		switch *accent {
		case "":
		case "theme":
			c.Accent = ""
		default:
			c.Accent = strings.ToLower(*accent)
		}
	})
	if err != nil {
		return err
	}
	warnMissingPackages(s.plugins)
	changes, orphans, owned, err := s.plan()
	if err != nil {
		return err
	}
	fmt.Printf("theme %s, language %s, %d plugins\n", s.theme.ID, s.lang, len(s.plugins))
	printPlan(changes, orphans, false)
	if *dryRun {
		return nil
	}
	if err := s.write(changes, orphans, owned, *adopt); err != nil {
		return err
	}
	if *themeID != "" || *accent != "" {
		return s.cfg.Save()
	}
	return nil
}

var accentPattern = regexp.MustCompile(`^#[0-9a-fA-F]{6}$`)

func cmdCoverage(args []string) error {
	fs := flag.NewFlagSet("coverage", flag.ExitOnError)
	asJSON := fs.Bool("json", false, "for the theme picker")
	fs.Parse(args)
	cfg, err := config.Load()
	if err != nil {
		return err
	}
	plugins, err := enabledPlugins(cfg)
	if err != nil {
		return err
	}
	var covers []coverage.Covers
	for _, p := range plugins {
		covers = append(covers, coverage.Covers{Plugin: p.ID(), Apps: p.Coverage.Apps, Toolkits: p.Coverage.Toolkits})
	}
	apps := coverage.Report(coverage.Dirs(), locale.Detect(cfg.Language), covers)
	if apps == nil {
		apps = []coverage.App{}
	}
	if *asJSON {
		b, err := json.Marshal(apps)
		if err != nil {
			return err
		}
		fmt.Println(string(b))
		return nil
	}
	covered := 0
	for _, a := range apps {
		if a.Plugin != "" {
			covered++
		}
	}
	fmt.Printf("The theme reaches %d of %d apps\n", covered, len(apps))
	for _, a := range apps {
		mark, by := green+"✓"+reset, dim+a.Plugin+reset
		if a.Plugin == "" {
			mark, by = amber+"✗"+reset, amber+missingHint[a.Toolkit]+reset
		}
		name := a.Name
		if r := []rune(name); len(r) > 30 {
			name = string(r[:29]) + "…"
		}
		fmt.Printf("  %s %-30s %-9s %s\n", mark, name, toolkitName[a.Toolkit], by)
	}
	return nil
}

// What a plugin for each toolkit would take, for the ones nothing themes.
var missingHint = map[string]string{
	coverage.Terminal: "no plugin themes the terminal yet",
	coverage.GTK4:     "no plugin themes GTK 4 yet",
	coverage.GTK3:     "no plugin themes GTK 3 yet",
	coverage.Qt6:      "no plugin themes Qt yet (a qt6ct + Kvantum plugin would)",
	coverage.Qt5:      "no plugin themes Qt yet (a qt5ct + Kvantum plugin would)",
	coverage.Electron: "Electron: only follows dark/light, needs its own plugin",
	coverage.Chromium: "has its own theming: needs its own plugin",
	coverage.Firefox:  "has its own theming (userChrome.css): needs its own plugin",
	coverage.Flatpak:  "Flatpak: sandboxed, the theme's files don't reach it",
	coverage.Web:      "a web app: it looks as the browser (and the site) make it",
	coverage.Other:    "draws its own UI: needs a plugin for its config",
}

var toolkitName = map[string]string{
	coverage.Terminal: "terminal", coverage.GTK4: "GTK 4", coverage.GTK3: "GTK 3", coverage.Qt6: "Qt 6",
	coverage.Qt5: "Qt 5", coverage.Electron: "Electron", coverage.Chromium: "Chromium",
	coverage.Firefox: "Firefox", coverage.Flatpak: "Flatpak", coverage.Web: "web app", coverage.Other: "own UI",
}

func cmdThemes(args []string) error {
	fs := flag.NewFlagSet("themes", flag.ExitOnError)
	asJSON := fs.Bool("json", false, "everything about every theme, for the theme picker")
	fs.Parse(args)
	cfg, _ := config.Load()
	all := []themeInfo{} // [] in JSON, never null
	for _, id := range theme.List(themeDirs()) {
		t, err := theme.Load(themeDirs(), id)
		if err != nil {
			if !*asJSON {
				fmt.Printf("  %-14s %v\n", id, err)
			}
			continue
		}
		info := describe(t, cfg)
		all = append(all, info)
		if *asJSON {
			continue
		}
		mark := " "
		shown := info.Problems
		if id == cfg.Theme {
			mark = "*"
			// As you have it: with your accent.
			for _, a := range info.Accents {
				if cfg.Accent != "" && a.Color == strings.ToLower(cfg.Accent) {
					shown = a.Problems
				}
			}
		}
		problems := ""
		for _, p := range shown {
			problems += fmt.Sprintf("  %s on %s %.2f < %.1f", p.Fg, p.Bg, p.Ratio, p.Min)
		}
		if problems == "" {
			problems = "  contrast ok"
		}
		fmt.Printf("%s %-14s %-5s %s%s\n", mark, id, t.Meta.Mode, t.Meta.Name, problems)
	}
	if *asJSON {
		b, err := json.Marshal(map[string]any{"current": cfg.Theme, "accent": cfg.Accent, "themes": all})
		if err != nil {
			return err
		}
		fmt.Println(string(b))
	}
	return nil
}

// themeInfo is a theme as the picker needs it: its tokens, the accents it
// suggests (each with its derived tokens) and its contrast problems.
type themeInfo struct {
	ID          string            `json:"id"`
	Name        string            `json:"name"`
	Mode        string            `json:"mode"`
	Description string            `json:"description"`
	Colors      map[string]string `json:"colors"`
	ANSI        map[string]string `json:"ansi"`
	Font        string            `json:"font"`
	Radius      int               `json:"radius"`
	Border      int               `json:"border"`
	Opacity     float64           `json:"terminal_opacity"`
	Blur        bool              `json:"blur"`
	Wallpaper   string            `json:"wallpaper"`
	Accents     []accentInfo      `json:"accents"`
	Problems    []problem         `json:"problems"`
}

type accentInfo struct {
	Color    string            `json:"color"`    // as chosen (what config.toml keeps)
	Tokens   map[string]string `json:"tokens"`   // what it becomes in this theme
	Problems []problem         `json:"problems"` // the theme's, with this accent
}

type problem struct {
	Fg    string  `json:"fg"`
	Bg    string  `json:"bg"`
	Ratio float64 `json:"ratio"`
	Min   float64 `json:"min"`
}

func describe(t *theme.Theme, cfg *config.Config) themeInfo {
	info := themeInfo{ID: t.ID, Name: t.Meta.Name, Mode: t.Meta.Mode, Description: t.Meta.Description,
		Colors: t.Colors, ANSI: t.ANSI, Font: t.Font.Mono, Radius: t.Shape.Radius, Border: t.Shape.Border,
		Opacity: t.Effects.TerminalOpacity, Blur: t.Effects.Blur, Wallpaper: t.Effects.Wallpaper,
		Accents: []accentInfo{}, Problems: problems(t)}
	// The theme's own accent first, then its suggestions, then the one in
	// config.toml if it's none of those.
	seen := map[string]bool{}
	for _, a := range append([]string{t.Colors["accent"]}, append(t.Meta.Accents, cfg.Accent)...) {
		a = strings.ToLower(a)
		if a == "" || seen[a] {
			continue
		}
		seen[a] = true
		with, err := theme.Load(themeDirs(), t.ID)
		if err != nil || with.WithAccent(a) != nil {
			continue
		}
		tokens := map[string]string{}
		for _, k := range []string{"accent", "accent_fg", "accent_text", "accent_deep", "selection"} {
			tokens[k] = with.Colors[k]
		}
		info.Accents = append(info.Accents, accentInfo{Color: a, Tokens: tokens, Problems: problems(with)})
	}
	return info
}

// problems are the contrast pairs a theme fails, rounded down for showing
// (2.9997 is not 3.00).
func problems(t *theme.Theme) []problem {
	out := []problem{}
	for _, p := range t.Contrast() {
		if !p.OK() {
			out = append(out, problem{p.Fg, p.Bg, math.Floor(p.Ratio*100) / 100, p.Min})
		}
	}
	return out
}

// checkOverrides rejects [plugins.<id>] sections for plugins that don't
// exist, so a misspelled id doesn't silently do nothing.
func checkOverrides(cfg *config.Config) error {
	all, broken := plugin.Discover(pluginDirs())
	for id := range cfg.Plugins {
		if broken[id] != nil {
			continue
		}
		known := false
		for _, p := range all {
			known = known || p.ID() == id
		}
		if !known {
			return fmt.Errorf("%s: [plugins.%s]: no such plugin", config.Path(), id)
		}
	}
	return nil
}

func sortedKeys[V any](m map[string]V) []string {
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
