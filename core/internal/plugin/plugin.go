// Package plugin discovers plugins and reads their plugin.toml manifests.
//
// Built-in plugins use exactly this API; there is no private path for them.
package plugin

import (
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"

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
		// Requires: other plugins it needs, "id" or "id >= 1.2".
		Requires []string `toml:"requires"`
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
	// app's own; only a change to its keys counts as an edit. "prefs" is
	// the same for Firefox's user.js (user_pref("name", value); lines);
	// "lines" makes sure the template's lines are in the file (an @import
	// in someone's userChrome.css), at the top when missing; "json" sets the
	// template's leaves in a JSON object (Chromium's Preferences).
	Merge string `toml:"merge"`
	// Each: glob patterns of a file that marks a place ("~/.config/mozilla/
	// firefox/*/prefs.js": a Firefox profile). The target is written into
	// every such directory, Output being relative to it; templates see the
	// directory as .Place.
	Each []string `toml:"each"`
	// Busy: a file, relative to each place, that says its app is running
	// ("../SingletonLock" for a Chromium profile); then the file is left
	// for the next apply, since the app would write its own copy back.
	Busy string `toml:"busy"`
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

// Discover loads every plugin under dirs; a plugin's folder is named after
// its id. An id found in an earlier directory shadows the same id in later
// ones (user dir before built-ins). A plugin that doesn't load is returned
// in broken, by id, and still shadows: a broken plugin from git must not
// quietly fall back to a built-in.
func Discover(dirs []string) (plugins []*Plugin, broken map[string]error) {
	byID := map[string]*Plugin{}
	broken = map[string]error{}
	for _, d := range dirs {
		entries, err := os.ReadDir(d)
		if err != nil {
			continue
		}
		for _, e := range entries {
			id := e.Name()
			path := filepath.Join(d, id, "plugin.toml")
			if _, err := os.Stat(path); err != nil {
				continue
			}
			if _, seen := byID[id]; seen {
				continue
			}
			if _, seen := broken[id]; seen {
				continue
			}
			p, err := load(path)
			if err == nil && p.ID() != id {
				err = fmt.Errorf("%s: id %q, but its folder is %q: they must match", path, p.ID(), id)
			}
			if err != nil {
				broken[id] = err
				continue
			}
			byID[id] = p
		}
	}
	for _, p := range byID {
		plugins = append(plugins, p)
	}
	sort.Slice(plugins, func(i, j int) bool { return plugins[i].ID() < plugins[j].ID() })
	return plugins, broken
}

// Reserved says whether path is myarch's own: its config, plugins.lock,
// the plugins and themes it loads, what it remembers. No plugin writes
// there (bin/, for the scripts plugins ship, is the exception).
func Reserved(path string) bool {
	home, _ := os.UserHomeDir()
	path = filepath.Clean(path)
	for _, r := range []string{".config/myarch", ".local/state/myarch", ".local/share/myarch"} {
		r = filepath.Join(home, r)
		if path == r || strings.HasPrefix(path, r+"/") {
			return !strings.HasPrefix(path, filepath.Join(home, ".local/share/myarch/bin")+"/")
		}
	}
	return false
}

// Load reads the plugin in dir.
func Load(dir string) (*Plugin, error) { return load(filepath.Join(dir, "plugin.toml")) }

// An id names the plugin's folder and its [plugins.<id>] table.
var idPattern = regexp.MustCompile(`^[a-z0-9][a-z0-9-]*$`)

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
	case !idPattern.MatchString(p.Meta.ID):
		return nil, fmt.Errorf("%s: [plugin] id %q: lowercase letters, digits and dashes", path, p.Meta.ID)
	case p.Meta.API != APIVersion:
		return nil, fmt.Errorf("%s: api = %d, this core supports api = %d", path, p.Meta.API, APIVersion)
	}
	if _, err := parseVersion(p.Meta.Version); err != nil {
		return nil, fmt.Errorf("%s: [plugin] %w", path, err)
	}
	for _, r := range p.Meta.Requires {
		req, err := ParseRequirement(r)
		if err != nil {
			return nil, fmt.Errorf("%s: [plugin] %w", path, err)
		}
		if req.ID == p.Meta.ID {
			return nil, fmt.Errorf("%s: [plugin] requires itself", path)
		}
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
		if t.Merge != "" && t.Merge != "ini" && t.Merge != "prefs" && t.Merge != "lines" && t.Merge != "json" {
			return nil, fmt.Errorf("%s: targets[%d]: merge = %q: \"ini\", \"prefs\", \"lines\" or \"json\"", path, i, t.Merge)
		}
		if len(t.Each) > 0 && (filepath.IsAbs(t.Output) || strings.HasPrefix(t.Output, "~")) {
			return nil, fmt.Errorf("%s: targets[%d]: with each, output is relative to each place", path, i)
		}
		if len(t.Each) == 0 && !filepath.IsAbs(t.Output) && !strings.HasPrefix(t.Output, "~/") {
			return nil, fmt.Errorf("%s: targets[%d]: output starts with ~/ or / (or use each)", path, i)
		}
		for _, part := range strings.Split(t.Output, "/") {
			if part == ".." {
				return nil, fmt.Errorf("%s: targets[%d]: output can't go up (..)", path, i)
			}
		}
		for _, e := range t.Each {
			// The part before the first wildcard is where every place is.
			fixed := e
			if i := strings.IndexAny(e, "*?["); i >= 0 {
				fixed = filepath.Dir(e[:i+1])
			}
			home, _ := os.UserHomeDir()
			if strings.HasPrefix(fixed, "~/") {
				fixed = filepath.Join(home, fixed[2:])
			}
			if Reserved(fixed) || Reserved(filepath.Join(fixed, "x")) {
				return nil, fmt.Errorf("%s: targets[%d]: each %s is in myarch's own folders", path, i, e)
			}
		}
		if len(t.Each) == 0 {
			home, _ := os.UserHomeDir()
			out := t.Output
			if strings.HasPrefix(out, "~/") {
				out = filepath.Join(home, out[2:])
			}
			if Reserved(out) {
				return nil, fmt.Errorf("%s: targets[%d]: %s is myarch's own", path, i, t.Output)
			}
		}
		if _, err := os.Stat(filepath.Join(p.Dir, t.Template)); err != nil {
			return nil, fmt.Errorf("%s: targets[%d]: %w", path, i, err)
		}
	}
	// Commands must say what they run (see Capabilities).
	if _, err := p.capabilities(); err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}
	return p, nil
}
