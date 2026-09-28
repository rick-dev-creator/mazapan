// Package render turns plugin templates plus a theme into file contents.
package render

import (
	"bytes"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"text/template"

	"myarch/internal/plugin"
	"myarch/internal/theme"
)

// File is one rendered target.
type File struct {
	Plugin  string
	Path    string // absolute
	Content []byte
	Reload  string // rendered reload command, may be empty
}

// Data is what every template sees as ".".
type Data struct {
	Theme  *theme.Theme
	Plugin string
	Home   string
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
func All(plugins []*plugin.Plugin, t *theme.Theme) ([]File, error) {
	var outputs []string
	owner := map[string]string{}
	for _, p := range plugins {
		for _, tg := range p.Targets {
			out := ExpandHome(tg.Output)
			if other, dup := owner[out]; dup {
				return nil, fmt.Errorf("%s: both %s and %s generate it", out, other, p.ID())
			}
			owner[out] = p.ID()
			outputs = append(outputs, out)
		}
	}
	sort.Strings(outputs)

	home, _ := os.UserHomeDir()
	var files []File
	for _, p := range plugins {
		if len(p.Targets) == 0 {
			continue
		}
		tmpl, err := template.New(p.ID()).
			Funcs(funcs(t, outputs)).
			Option("missingkey=error").
			ParseGlob(filepath.Join(p.Dir, "*.tmpl"))
		if err != nil {
			return nil, fmt.Errorf("plugin %s: %w", p.ID(), err)
		}
		data := Data{Theme: t, Plugin: p.ID(), Home: home}
		for _, tg := range p.Targets {
			var buf bytes.Buffer
			if err := tmpl.ExecuteTemplate(&buf, tg.Template, data); err != nil {
				return nil, fmt.Errorf("plugin %s: %w", p.ID(), err)
			}
			reload, err := renderString(tmpl, tg.Reload, data)
			if err != nil {
				return nil, fmt.Errorf("plugin %s: reload: %w", p.ID(), err)
			}
			files = append(files, File{
				Plugin:  p.ID(),
				Path:    ExpandHome(tg.Output),
				Content: buf.Bytes(),
				Reload:  reload,
			})
		}
	}
	return files, nil
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

func funcs(t *theme.Theme, outputs []string) template.FuncMap {
	return template.FuncMap{
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
		"num": func(f float64) string { return strconv.FormatFloat(f, 'f', -1, 64) },
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
