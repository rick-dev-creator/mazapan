// Package theme loads and validates theme.toml files.
//
// A theme is a set of semantic tokens. Plugins read tokens by name, so every
// name in RequiredColors and RequiredANSI is part of the plugin API: removing
// or renaming one is a breaking change.
package theme

import (
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"

	"github.com/BurntSushi/toml"
)

var RequiredColors = []string{
	"bg", "bg_alt", "surface", "surface_raised", "border",
	"fg", "fg_muted", "fg_subtle",
	"accent", "accent_fg", "accent_text", "accent_deep", "selection",
	"success", "warning", "danger", "info",
}

var RequiredANSI = []string{
	"black", "red", "green", "yellow", "blue", "magenta", "cyan", "white",
	"bright_black", "bright_red", "bright_green", "bright_yellow",
	"bright_blue", "bright_magenta", "bright_cyan", "bright_white",
}

type Theme struct {
	ID  string `toml:"-"`
	Dir string `toml:"-"`

	Meta struct {
		Name        string `toml:"name"`
		Mode        string `toml:"mode"`
		Description string `toml:"description"`
	} `toml:"meta"`

	Colors map[string]string `toml:"colors"`
	ANSI   map[string]string `toml:"ansi"`

	Font struct {
		Mono string  `toml:"mono"`
		UI   string  `toml:"ui"`
		Size float64 `toml:"size"`
	} `toml:"font"`

	Shape struct {
		Radius int `toml:"radius"`
		Border int `toml:"border"`
		GapIn  int `toml:"gap_in"`
		GapOut int `toml:"gap_out"`
	} `toml:"shape"`

	Motion struct {
		Enabled bool `toml:"enabled"`
		// Short transitions (fades, border color): a cubic-bezier.
		DurationMS int        `toml:"duration_ms"`
		Curve      [4]float64 `toml:"curve"`
		// Movement (windows, workspaces): a spring, described the way
		// tablet UIs do (SwiftUI's spring(response:dampingFraction:)).
		ResponseMS int     `toml:"response_ms"` // period of the spring
		Damping    float64 `toml:"damping"`     // 1 = no overshoot, lower = bouncier
	} `toml:"motion"`
}

var hexColor = regexp.MustCompile(`^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$`)

// Load finds theme id in the first directory of dirs that has it.
func Load(dirs []string, id string) (*Theme, error) {
	for _, d := range dirs {
		dir := filepath.Join(d, id)
		path := filepath.Join(dir, "theme.toml")
		if _, err := os.Stat(path); err != nil {
			continue
		}
		t := &Theme{ID: id, Dir: dir}
		md, err := toml.DecodeFile(path, t)
		if err != nil {
			return nil, fmt.Errorf("%s: %w", path, err)
		}
		if u := md.Undecoded(); len(u) > 0 {
			return nil, fmt.Errorf("%s: unknown keys: %v", path, u)
		}
		if err := t.Validate(); err != nil {
			return nil, fmt.Errorf("%s: %w", path, err)
		}
		return t, nil
	}
	return nil, fmt.Errorf("theme %q not found in %s", id, strings.Join(dirs, ", "))
}

// List returns the ids of every theme found in dirs, first match wins.
func List(dirs []string) []string {
	seen := map[string]bool{}
	var ids []string
	for _, d := range dirs {
		entries, _ := os.ReadDir(d)
		for _, e := range entries {
			if !e.IsDir() || seen[e.Name()] {
				continue
			}
			if _, err := os.Stat(filepath.Join(d, e.Name(), "theme.toml")); err == nil {
				seen[e.Name()] = true
				ids = append(ids, e.Name())
			}
		}
	}
	sort.Strings(ids)
	return ids
}

func (t *Theme) Validate() error {
	var problems []string
	check := func(table string, m map[string]string, required []string) {
		for _, k := range required {
			v, ok := m[k]
			switch {
			case !ok:
				problems = append(problems, fmt.Sprintf("[%s] missing %q", table, k))
			case !hexColor.MatchString(v):
				problems = append(problems, fmt.Sprintf("[%s] %s = %q is not #rrggbb or #rrggbbaa", table, k, v))
			}
		}
	}
	check("colors", t.Colors, RequiredColors)
	check("ansi", t.ANSI, RequiredANSI)
	if t.Meta.Mode != "dark" && t.Meta.Mode != "light" {
		problems = append(problems, fmt.Sprintf("[meta] mode = %q must be \"dark\" or \"light\"", t.Meta.Mode))
	}
	if t.Motion.ResponseMS <= 0 || t.Motion.Damping <= 0 || t.Motion.Damping > 2 {
		problems = append(problems, "[motion] needs response_ms > 0 and 0 < damping <= 2")
	}
	if t.Font.Mono == "" || t.Font.UI == "" || t.Font.Size <= 0 {
		problems = append(problems, "[font] needs mono, ui and a positive size")
	}
	if len(problems) > 0 {
		return fmt.Errorf("invalid theme:\n  %s", strings.Join(problems, "\n  "))
	}
	return nil
}

// Color returns a token from [colors], falling back to [ansi].
func (t *Theme) Color(name string) (string, error) {
	if v, ok := t.Colors[name]; ok {
		return v, nil
	}
	if v, ok := t.ANSI[name]; ok {
		return v, nil
	}
	return "", fmt.Errorf("theme %q has no color %q", t.ID, name)
}
