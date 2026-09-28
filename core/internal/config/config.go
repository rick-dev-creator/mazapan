// Package config reads and writes ~/.config/myarch/config.toml, the only
// file a person edits by hand.
package config

import (
	"bytes"
	"errors"
	"io/fs"
	"os"
	"path/filepath"

	"github.com/BurntSushi/toml"

	"myarch/internal/render"
)

type Config struct {
	Theme string `toml:"theme"`
	// Accent color (#rrggbb) instead of the theme's; its other accent
	// tokens are derived from it. Empty = the theme's own.
	Accent string `toml:"accent,omitempty"`
	// Language to render text in ("es", "es_MX"); empty = the OS's.
	Language string   `toml:"language,omitempty"`
	Disabled []string `toml:"disabled_plugins,omitempty"`
	// Per-plugin setting overrides: [plugins.<id>] key = value
	Plugins map[string]map[string]any `toml:"plugins,omitempty"`
}

func Path() string { return render.ExpandHome("~/.config/myarch/config.toml") }

func Load() (*Config, error) {
	c := &Config{}
	_, err := toml.DecodeFile(Path(), c)
	if errors.Is(err, fs.ErrNotExist) {
		return c, nil
	}
	return c, err
}

func (c *Config) Save() error {
	var buf bytes.Buffer
	buf.WriteString("# myarch configuration. Apply changes with: myarch apply\n\n")
	if err := toml.NewEncoder(&buf).Encode(c); err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(Path()), 0o755); err != nil {
		return err
	}
	return os.WriteFile(Path(), buf.Bytes(), 0o644)
}

func (c *Config) IsDisabled(id string) bool {
	for _, d := range c.Disabled {
		if d == id {
			return true
		}
	}
	return false
}
