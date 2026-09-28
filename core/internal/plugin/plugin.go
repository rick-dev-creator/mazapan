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

	// Targets are files the plugin generates. The core renders and writes
	// them; plugins never touch the filesystem themselves.
	Targets []Target `toml:"targets"`
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
}

func (p *Plugin) ID() string { return p.Meta.ID }

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
	for i, t := range p.Targets {
		if t.Template == "" || t.Output == "" {
			return nil, fmt.Errorf("%s: targets[%d] needs template and output", path, i)
		}
		if _, err := os.Stat(filepath.Join(p.Dir, t.Template)); err != nil {
			return nil, fmt.Errorf("%s: targets[%d]: %w", path, i, err)
		}
	}
	return p, nil
}
