// Package plugin discovers plugins and reads their plugin.toml manifests.
//
// Built-in plugins use exactly this API; there is no private path for them.
package plugin

import (
	"fmt"
	"os"
	"path/filepath"
	"sort"

	"github.com/BurntSushi/toml"
)

// APIVersion is the manifest API this core understands.
const APIVersion = 1

type Plugin struct {
	Dir string `toml:"-"`

	Meta struct {
		ID          string `toml:"id"`
		Name        string `toml:"name"`
		Version     string `toml:"version"`
		API         int    `toml:"api"`
		Description string `toml:"description"`
	} `toml:"plugin"`

	// Packages the plugin needs at runtime. Checked, not installed (yet).
	Packages struct {
		Pacman []string `toml:"pacman"`
	} `toml:"packages"`

	// Settings are the plugin's knobs with their defaults. People override
	// them in config.toml under [plugins.<id>]; templates read the result
	// as .Settings.
	Settings map[string]any `toml:"settings"`

	// Targets are files the plugin generates. The core renders and writes
	// them; plugins never touch the filesystem themselves.
	Targets []Target `toml:"targets"`

	// Checks say whether what the plugin is responsible for still works.
	// `myarch doctor` runs them on demand; `myarch update` runs them after
	// updating and rolls back when one fails.
	Checks []Check `toml:"checks"`

	// Actions are what the plugin lets you do: the command palette lists
	// every plugin's, each with the command it runs and its keybinding.
	Actions []Action `toml:"actions"`

	// Coverage says which apps the plugin themes, so `myarch coverage`
	// can tell which installed apps the theme doesn't reach.
	Coverage struct {
		// .desktop ids or executable names: "foot", "org.gnome.Nautilus".
		Apps []string `toml:"apps"`
		// Whole toolkits: terminal, gtk4, gtk3, qt6, qt5, electron,
		// chromium, firefox, flatpak, web.
		Toolkits []string `toml:"toolkits"`
	} `toml:"coverage"`
}

var toolkits = map[string]bool{"terminal": true, "gtk4": true, "gtk3": true, "qt6": true, "qt5": true,
	"electron": true, "chromium": true, "firefox": true, "flatpak": true, "web": true}

type Action struct {
	// Name, Run and Key are rendered as templates: the name can be
	// translated, the command and key can come from settings.
	Name string `toml:"name"`
	// Run is a shell command, shown next to the action so it can be
	// learned. Empty for a keybinding that only makes sense as a key (a
	// mouse drag, "hold to…").
	Run string `toml:"run"`
	// Key is the keybinding that does the same, as the plugin binds it
	// ("SUPER + SHIFT + M"). Listing it here is how the palette knows every
	// keybinding: the list is always right.
	Key string `toml:"key"`
	// Terminal: run it in a terminal, for commands that ask or print
	// (myarch update).
	Terminal bool `toml:"terminal"`
	// Keywords help find it: other words people use for it.
	Keywords string `toml:"keywords"`
}

type Check struct {
	// Name and Run are rendered as templates, so the name can be
	// translated (t "…") and the command can use settings.
	Name string `toml:"name"`
	// Run is a shell command; exit status 0 means healthy. Its output is
	// shown when it fails.
	Run string `toml:"run"`
	// Timeout in seconds (default 15): a check that hangs has failed.
	Timeout int `toml:"timeout"`
	// Session: the check needs the graphical session (Hyprland, the bar,
	// the user's PipeWire). Outside it (a TTY, SSH) it's skipped, never
	// failed: a skipped check must not roll back a good update.
	Session bool `toml:"session"`
}

type Target struct {
	// Template file name inside the plugin directory. All *.tmpl files of a
	// plugin are parsed together, so they can share {{define}} blocks.
	Template string `toml:"template"`
	// Output path; "~/" is expanded.
	Output string `toml:"output"`
	// Reload is a shell command run once after any of the plugin's files
	// changed. It is rendered as a template too.
	Reload string `toml:"reload"`
	// Merge = "ini": the app writes this file too (qt6ct.conf, kdeglobals).
	// myarch only manages the keys the template renders and keeps the
	// app's own; only a change to its keys counts as an edit.
	Merge string `toml:"merge"`
}

func (p *Plugin) ID() string { return p.Meta.ID }

// Resolve merges overrides from config.toml onto the plugin's defaults.
// Unknown keys and type mismatches are errors: a typo in config.toml must
// not be silently ignored.
func (p *Plugin) Resolve(overrides map[string]any) (map[string]any, error) {
	out := make(map[string]any, len(p.Settings))
	for k, v := range p.Settings {
		out[k] = v
	}
	for k, v := range overrides {
		def, ok := p.Settings[k]
		if !ok {
			return nil, fmt.Errorf("[plugins.%s] %s: unknown setting (known: %s)", p.ID(), k, keys(p.Settings))
		}
		v, ok = coerce(def, v)
		if !ok {
			return nil, fmt.Errorf("[plugins.%s] %s = %v: want a %T like the default %v", p.ID(), k, v, def, def)
		}
		out[k] = v
	}
	return out, nil
}

// coerce accepts v if it has the default's type. An integer is accepted
// where the default is a float (min_width = 480 for a 480.0 default).
func coerce(def, v any) (any, bool) {
	switch def.(type) {
	case float64:
		switch n := v.(type) {
		case float64:
			return n, true
		case int64:
			return float64(n), true
		}
		return v, false
	case int64:
		n, ok := v.(int64)
		return n, ok
	case string:
		s, ok := v.(string)
		return s, ok
	case bool:
		b, ok := v.(bool)
		return b, ok
	}
	return v, fmt.Sprintf("%T", def) == fmt.Sprintf("%T", v)
}

func keys(m map[string]any) string {
	ks := make([]string, 0, len(m))
	for k := range m {
		ks = append(ks, k)
	}
	sort.Strings(ks)
	if len(ks) == 0 {
		return "none"
	}
	return fmt.Sprint(ks)
}

// Discover loads every plugin under dirs. A plugin id found in an earlier
// directory shadows the same id in later ones (user dir before built-ins).
func Discover(dirs []string) ([]*Plugin, error) {
	byID := map[string]*Plugin{}
	for _, d := range dirs {
		entries, err := os.ReadDir(d)
		if err != nil {
			continue
		}
		for _, e := range entries {
			path := filepath.Join(d, e.Name(), "plugin.toml")
			if _, err := os.Stat(path); err != nil {
				continue
			}
			p, err := load(path)
			if err != nil {
				return nil, err
			}
			if _, dup := byID[p.ID()]; !dup {
				byID[p.ID()] = p
			}
		}
	}
	out := make([]*Plugin, 0, len(byID))
	for _, p := range byID {
		out = append(out, p)
	}
	sort.Slice(out, func(i, j int) bool { return out[i].ID() < out[j].ID() })
	return out, nil
}

func load(path string) (*Plugin, error) {
	p := &Plugin{Dir: filepath.Dir(path)}
	md, err := toml.DecodeFile(path, p)
	if err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}
	if u := md.Undecoded(); len(u) > 0 {
		return nil, fmt.Errorf("%s: unknown keys: %v", path, u)
	}
	switch {
	case p.Meta.ID == "":
		return nil, fmt.Errorf("%s: [plugin] id is required", path)
	case p.Meta.API != APIVersion:
		return nil, fmt.Errorf("%s: api = %d, this core supports api = %d", path, p.Meta.API, APIVersion)
	}
	for i, c := range p.Checks {
		if c.Name == "" || c.Run == "" {
			return nil, fmt.Errorf("%s: checks[%d] needs name and run", path, i)
		}
	}
	for _, t := range p.Coverage.Toolkits {
		if !toolkits[t] {
			return nil, fmt.Errorf("%s: coverage: unknown toolkit %q", path, t)
		}
	}
	for i, a := range p.Actions {
		if a.Name == "" || (a.Run == "" && a.Key == "") {
			return nil, fmt.Errorf("%s: actions[%d] needs a name, and run or key", path, i)
		}
	}
	for i, t := range p.Targets {
		if t.Template == "" || t.Output == "" {
			return nil, fmt.Errorf("%s: targets[%d] needs template and output", path, i)
		}
		if t.Merge != "" && t.Merge != "ini" {
			return nil, fmt.Errorf("%s: targets[%d]: merge = %q, only \"ini\" is supported", path, i, t.Merge)
		}
		if _, err := os.Stat(filepath.Join(p.Dir, t.Template)); err != nil {
			return nil, fmt.Errorf("%s: targets[%d]: %w", path, i, err)
		}
	}
	return p, nil
}
