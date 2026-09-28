// Package render turns plugin templates plus a theme into file contents.
package render

import (
	"bytes"
	"encoding/json"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"text/template"

	"myarch/internal/locale"
	"myarch/internal/plugin"
	"myarch/internal/theme"
)

// Output is everything the plugins produce for one theme and language.
type Output struct {
	Files   []File
	Checks  []Check
	Actions []Action
}

// Action is one plugin action, rendered: what the command palette lists.
type Action struct {
	Plugin   string `json:"plugin"`
	Name     string `json:"name"`
	Run      string `json:"run,omitempty"`
	Key      string `json:"key,omitempty"`
	Terminal bool   `json:"terminal,omitempty"`
	Keywords string `json:"keywords,omitempty"`
}

// Check is one plugin check, rendered.
type Check struct {
	Plugin  string
	Name    string
	Run     string
	Timeout int  // seconds
	Session bool // needs the graphical session
}

// File is one rendered target.
type File struct {
	Plugin  string
	Path    string // absolute
	Content []byte
	Reload  string // rendered reload command, may be empty
	// Merge: a file shared with its app or person; only these keys are
	// myarch's (see apply).
	Merge string
	// Busy: its app is running (the target's busy marker exists): left
	// for the next apply.
	Busy bool
}

// Data is what every template sees as ".".
type Data struct {
	Theme    *theme.Theme
	Plugin   string
	Home     string
	Settings map[string]any // plugin defaults merged with config overrides
	Lang     string         // POSIX locale name: "es_MX", "en"
	LangCode string         // just the language: "es", "en"
	// Every plugin's actions, rendered: what a command palette lists.
	Actions []Action
	// For a target with `each`: the directory this copy goes into (a
	// browser profile); "" otherwise.
	Place string
}

// Places are where a target goes: once at its output, or, with `each`,
// into every directory holding a file its patterns match (every Firefox
// profile has a prefs.js). A place that appears later is taken on the
// next apply; one that's gone takes its file with it (the dir is gone).
func Places(tg plugin.Target) []string {
	if len(tg.Each) == 0 {
		return []string{""}
	}
	seen := map[string]bool{}
	var out []string
	for _, pattern := range tg.Each {
		matches, _ := filepath.Glob(ExpandHome(pattern))
		for _, m := range matches {
			d := filepath.Dir(m)
			// ~/.mozilla/firefox linked to ~/.config/mozilla/firefox: one
			// profile, one copy.
			real := d
			if r, err := filepath.EvalSymlinks(d); err == nil {
				real = r
			}
			if !seen[real] {
				seen[real] = true
				out = append(out, d)
			}
		}
	}
	sort.Strings(out)
	return out
}

// landing resolves the symlinks of path's nearest existing folder, to see
// where a write would land.
func landing(path string) string {
	dir, rest := filepath.Dir(path), filepath.Base(path)
	for {
		if r, err := filepath.EvalSymlinks(dir); err == nil {
			return filepath.Join(r, rest)
		}
		parent := filepath.Dir(dir)
		if parent == dir {
			return path
		}
		dir, rest = parent, filepath.Join(filepath.Base(dir), rest)
	}
}

// ExpandHome turns a leading "~/" into the user's home directory.
func ExpandHome(p string) string {
	if strings.HasPrefix(p, "~/") {
		home, _ := os.UserHomeDir()
		return filepath.Join(home, p[2:])
	}
	return p
}

// All renders every target of every plugin. Output paths are collected first
// so templates can see each other's outputs through the "under" function.
// overrides holds per-plugin settings from config.toml, keyed by plugin id;
// lang is the language to render text in (see locale.Detect).
func All(plugins []*plugin.Plugin, t *theme.Theme, overrides map[string]map[string]any, lang string) (*Output, error) {
	var outputs []string
	owner := map[string]string{}
	for _, p := range plugins {
		for _, tg := range p.Targets {
			for _, d := range Places(tg) {
				out := ExpandHome(tg.Output)
				if d != "" {
					out = filepath.Join(d, tg.Output)
				}
				if plugin.Reserved(out) || plugin.Reserved(landing(out)) {
					return nil, fmt.Errorf("plugin %s: %s is myarch's own; no plugin writes there", p.ID(), out)
				}
				if other, dup := owner[out]; dup {
					return nil, fmt.Errorf("%s: both %s and %s generate it", out, other, p.ID())
				}
				owner[out] = p.ID()
				outputs = append(outputs, out)
			}
		}
	}
	sort.Strings(outputs)

	home, _ := os.UserHomeDir()
	out := &Output{}
	code, _, _ := strings.Cut(lang, "_")

	// First every plugin's checks and actions, so that templates can see
	// all the actions (the command palette lists them).
	type prepared struct {
		p    *plugin.Plugin
		tmpl *template.Template
		data Data
	}
	var ready []prepared
	for _, p := range plugins {
		if len(p.Targets) == 0 && len(p.Checks) == 0 && len(p.Actions) == 0 {
			continue
		}
		cat, err := locale.Load(p.ID(), p.Dir, lang)
		if err != nil {
			return nil, err
		}
		tmpl := template.New(p.ID()).
			Funcs(funcs(t, outputs, cat)).
			Option("missingkey=error")
		if len(p.Targets) > 0 {
			if tmpl, err = tmpl.ParseGlob(filepath.Join(p.Dir, "*.tmpl")); err != nil {
				return nil, fmt.Errorf("plugin %s: %w", p.ID(), err)
			}
		}
		settings, err := p.Resolve(overrides[p.ID()])
		if err != nil {
			return nil, err
		}
		data := Data{Theme: t, Plugin: p.ID(), Home: home, Settings: settings, Lang: lang, LangCode: code}
		for _, c := range p.Checks {
			name, err := renderString(tmpl, c.Name, data)
			if err != nil {
				return nil, fmt.Errorf("plugin %s: check name: %w", p.ID(), err)
			}
			run, err := renderString(tmpl, c.Run, data)
			if err != nil {
				return nil, fmt.Errorf("plugin %s: check %q: %w", p.ID(), name, err)
			}
			timeout := c.Timeout
			if timeout <= 0 {
				timeout = 15
			}
			out.Checks = append(out.Checks, Check{Plugin: p.ID(), Name: name, Run: run, Timeout: timeout, Session: c.Session})
		}
		for n, a := range p.Actions {
			var fields [4]string
			for i, src := range []string{a.Name, a.Run, a.Key, a.Keywords} {
				if fields[i], err = renderString(tmpl, src, data); err != nil {
					return nil, fmt.Errorf("plugin %s: actions[%d]: %w", p.ID(), n, err)
				}
			}
			// A key set to "" in config.toml unbinds it: an action with
			// neither a command nor a key left has nothing to offer.
			if strings.TrimSpace(fields[1]) == "" && strings.TrimSpace(fields[2]) == "" {
				continue
			}
			out.Actions = append(out.Actions, Action{Plugin: p.ID(), Name: fields[0], Run: fields[1],
				Key: fields[2], Terminal: a.Terminal, Keywords: fields[3]})
		}
		ready = append(ready, prepared{p, tmpl, data})
	}

	for _, r := range ready {
		r.data.Actions = out.Actions
		for _, tg := range r.p.Targets {
			for _, place := range Places(tg) {
				data := r.data
				data.Place = place
				path := ExpandHome(tg.Output)
				if place != "" {
					path = filepath.Join(place, tg.Output)
				}
				var buf bytes.Buffer
				if err := r.tmpl.ExecuteTemplate(&buf, tg.Template, data); err != nil {
					return nil, fmt.Errorf("plugin %s: %w", r.p.ID(), err)
				}
				reload, err := renderString(r.tmpl, tg.Reload, data)
				if err != nil {
					return nil, fmt.Errorf("plugin %s: reload: %w", r.p.ID(), err)
				}
				busy := false
				if tg.Busy != "" && place != "" {
					_, err := os.Lstat(filepath.Join(place, tg.Busy))
					busy = err == nil
				}
				out.Files = append(out.Files, File{
					Plugin:  r.p.ID(),
					Path:    path,
					Content: buf.Bytes(),
					Reload:  reload,
					Merge:   tg.Merge,
					Busy:    busy,
				})
			}
		}
	}
	return out, nil
}

func renderString(base *template.Template, s string, data Data) (string, error) {
	if s == "" {
		return "", nil
	}
	t, err := base.Clone()
	if err != nil {
		return "", err
	}
	t, err = t.New("reload").Parse(s)
	if err != nil {
		return "", err
	}
	var buf bytes.Buffer
	err = t.Execute(&buf, data)
	return buf.String(), err
}

func funcs(t *theme.Theme, outputs []string, cat *locale.Catalog) template.FuncMap {
	return template.FuncMap{
		// t "preview.empty" -> the plugin's text in the current language.
		"t": cat.T,
		// tq "preview.empty" -> the same, as a quoted string literal that is
		// valid in QML/JS and Lua: "workspace %1 · vacío"
		"tq": func(key string) (string, error) {
			v, err := cat.T(key)
			return strconv.Quote(v), err
		},
		// c "accent" -> "#ffb000"; fails the render on unknown tokens.
		"c": t.Color,
		// hex "#ffb000" -> "ffb000"
		"hex": func(c string) string { return strings.TrimPrefix(c, "#") },
		// rgb "#ffb000" -> "rgb(ffb000)"  (Hyprland color syntax)
		"rgb": func(c string) string { return "rgb(" + strings.TrimPrefix(c, "#")[:6] + ")" },
		// rgba "#ffb000" 0.5 -> "rgba(ffb00080)"  (Hyprland color syntax)
		"rgba": func(c string, a float64) string {
			return fmt.Sprintf("rgba(%s%02x)", strings.TrimPrefix(c, "#")[:6], alphaByte(a))
		},
		// cssa "#ffb000" 0.5 -> "rgba(255, 176, 0, 0.5)"  (CSS syntax)
		"cssa": func(c string, a float64) (string, error) {
			r, g, b, err := channels(c)
			if err != nil {
				return "", err
			}
			return fmt.Sprintf("rgba(%d, %d, %d, %s)", r, g, b, strconv.FormatFloat(a, 'f', -1, 64)), nil
		},
		// speed 0.6 -> Hyprland animation speed (deciseconds) for 60% of the
		// theme's base duration.
		"speed": func(factor float64) string {
			return strconv.FormatFloat(float64(t.Motion.DurationMS)*factor/100, 'f', 2, 64)
		},
		// under "~/.config/hypr/myarch/" -> sorted outputs of all plugins
		// below that directory. Lets an entry-point file include fragments
		// without knowing which plugins exist.
		"under": func(prefix string) []string {
			prefix = ExpandHome(prefix)
			var out []string
			for _, o := range outputs {
				if strings.HasPrefix(o, prefix) {
					out = append(out, o)
				}
			}
			return out
		},
		// csv "#ffb000" -> "255,176,0"  (KDE color syntax)
		"csv": func(c string) (string, error) {
			r, g, b, err := channels(c)
			return fmt.Sprintf("%d,%d,%d", r, g, b), err
		},
		// spring 1.2 0.9 -> Hyprland spring curve fields for the theme's
		// spring with its response scaled by 1.2 and damping 0.9 (0 keeps
		// the theme's damping). A mass-spring with period T and damping
		// ratio z has stiffness (2*pi/T)^2 and dampening 2*z*(2*pi/T).
		"spring": func(responseFactor, damping float64) string {
			if damping == 0 {
				damping = t.Motion.Damping
			}
			omega := 2 * math.Pi / (float64(t.Motion.ResponseMS) * responseFactor / 1000)
			return fmt.Sprintf("mass = 1, stiffness = %.2f, dampening = %.2f", omega*omega, 2*damping*omega)
		},
		"num": func(f float64) string { return strconv.FormatFloat(f, 'f', -1, 64) },
		// pct 0.9 -> "90"
		"pct": func(f float64) string { return strconv.Itoa(int(math.Round(f * 100))) },
		// json .Actions -> a JSON value, which is also a valid QML/JS
		// literal: data for a template's code.
		"json": func(v any) (string, error) {
			b, err := json.Marshal(v)
			return string(b), err
		},
		// camel "bg_alt" -> "bgAlt"  (QML/JS property names)
		// base "/x/y.default" -> "y.default" (a profile's name, from .Place)
		"base": filepath.Base,
		// mix (c "bg") (c "success") 0.18 -> the second over the first at
		// 18%: "#2a3322" (a diff's added line)
		"mix": func(a, b string, t float64) (string, error) {
			ar, ag, ab, err := channels(a)
			if err != nil {
				return "", err
			}
			br, bg, bb, err := channels(b)
			if err != nil {
				return "", err
			}
			m := func(x, y int) int { return int(math.Round(float64(x) + float64(y-x)*t)) }
			return fmt.Sprintf("#%02x%02x%02x", m(ar, br), m(ag, bg), m(ab, bb)), nil
		},
		// solid "#ffb00080" -> "#ffb000" (for apps with no alpha: Neovim, btop)
		"solid": func(c string) string {
			if len(c) > 7 {
				return c[:7]
			}
			return c
		},
		// list "a" "b" -> [a b], to range over
		"list": func(s ...string) []string { return s },
		"camel": func(s string) string {
			parts := strings.Split(s, "_")
			for i := 1; i < len(parts); i++ {
				if parts[i] != "" {
					parts[i] = strings.ToUpper(parts[i][:1]) + parts[i][1:]
				}
			}
			return strings.Join(parts, "")
		},
	}
}

func alphaByte(a float64) int {
	switch {
	case a <= 0:
		return 0
	case a >= 1:
		return 255
	}
	return int(a*255 + 0.5)
}

func channels(c string) (r, g, b int, err error) {
	h := strings.TrimPrefix(c, "#")
	if len(h) < 6 {
		return 0, 0, 0, fmt.Errorf("bad color %q", c)
	}
	v, err := strconv.ParseUint(h[:6], 16, 32)
	if err != nil {
		return 0, 0, 0, fmt.Errorf("bad color %q", c)
	}
	return int(v >> 16 & 0xff), int(v >> 8 & 0xff), int(v & 0xff), nil
}
