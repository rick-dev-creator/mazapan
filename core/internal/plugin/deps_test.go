package plugin

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func mk(id, version string, requires ...string) *Plugin {
	p := &Plugin{}
	p.Meta.ID, p.Meta.Version, p.Meta.Requires = id, version, requires
	return p
}

func TestRequirement(t *testing.T) {
	for _, c := range []struct {
		req, v string
		ok     bool
	}{
		{"a", "0.1.0", true},
		{"a >= 0.2", "0.2.0", true},
		{"a >= 0.2", "0.10.0", true},
		{"a >= 0.2", "0.1.9", false},
		{"a > 1", "1.0.0", false},
		{"a = 1.2", "1.2.0", true},
		{"a < 2", "1.99", true},
		{"a <= 2", "2.0.1", false},
		{"a>=1.2", "1.3", true},
	} {
		r, err := ParseRequirement(c.req)
		if err != nil {
			t.Fatal(err)
		}
		if r.Satisfied(c.v) != c.ok {
			t.Errorf("%s with %s: want %v", c.req, c.v, c.ok)
		}
	}
	for _, bad := range []string{"", "a >=", "a ~ 1", "a >= x", "a >= 1 2", "a >= 1.-1", "a >= +1", "A"} {
		if _, err := ParseRequirement(bad); err == nil {
			t.Errorf("%q should be refused", bad)
		}
	}
}

func TestUnmet(t *testing.T) {
	bar := mk("bar", "0.1.0")
	clock := mk("clock", "1.0.0", "bar >= 0.2")
	vol := mk("vol", "1.0.0", "bar")
	net := mk("net", "1.0.0", "wifi")
	off := mk("off", "1.0.0")
	lamp := mk("lamp", "1.0.0", "off")
	var lines []string
	for _, pr := range Unmet([]*Plugin{bar, clock, vol, net, lamp}, []*Plugin{bar, clock, vol, net, off, lamp}, nil) {
		lines = append(lines, pr.Text)
	}
	got := strings.Join(lines, "\n")
	for _, want := range []string{"clock requires bar >= 0.2; bar is 0.1.0", "net requires wifi, which isn't installed", "lamp requires off, which is disabled"} {
		if !strings.Contains(got, want) {
			t.Errorf("missing %q in:\n%s", want, got)
		}
	}
	if strings.Contains(got, "vol") {
		t.Errorf("vol is fine:\n%s", got)
	}
	if d := Dependents("bar", []*Plugin{bar, clock, vol, net}); strings.Join(d, ",") != "clock,vol" {
		t.Errorf("dependents of bar: %v", d)
	}
}

func TestCapabilities(t *testing.T) {
	p := mk("x", "1.0.0")
	p.Targets = []Target{
		{Output: "~/.config/quickshell/myarch/panels/x.qml", Reload: "shell-reload"},
		{Output: "~/.config/hypr/myarch/x.lua"},
		{Output: "user.js", Merge: "prefs", Each: []string{"~/.mozilla/*/prefs.js"}},
	}
	p.Checks = []Check{{Name: "n", Run: "pgrep x"}}
	p.Actions = []Action{{Name: "a", Run: "x --go"}, {Name: "k", Key: "SUPER + X"}}
	got := strings.Join(p.Capabilities(), "\n")
	for _, want := range []string{
		"full access, code in the shell (QML): ~/.config/quickshell/myarch/panels/x.qml",
		"full access, code in Hyprland (Lua): ~/.config/hypr/myarch/x.lua",
		"full access, a config that can run commands: user.js in each of ~/.mozilla/*/prefs.js",
		"runs after writing: shell-reload",
		"runs as a health check: pgrep x",
		"runs when you pick it: x --go",
	} {
		if !strings.Contains(got, want) {
			t.Errorf("missing %q in:\n%s", want, got)
		}
	}
	if n := NewCapabilities(p.Capabilities()[1:], p.Capabilities()); len(n) != 1 {
		t.Errorf("new: %v", n)
	}
}

func TestResolveCommands(t *testing.T) {
	dir := t.TempDir()
	os.WriteFile(filepath.Join(dir, "a.tmpl"), []byte(`{{define "go"}}gsettings set x {{.Settings.mode}}{{end}}body`), 0o644)
	p := mk("x", "1.0.0")
	p.Dir = dir
	p.Settings = map[string]any{"mode": "dark", "n": int64(3)}
	p.Targets = []Target{{Output: "~/.config/gtk-4.0/gtk.css", Reload: `{{template "go" .}}; echo {{printf "%d" .Settings.n}} {{c "accent"}}`}}
	caps, err := p.capabilities()
	if err != nil {
		t.Fatal(err)
	}
	want := `runs after writing: gsettings set x dark; echo {{printf "%d" .Settings.n}} {{c "accent"}}  (with n = 3)`
	if !contains(caps, want) {
		t.Errorf("want %q in %q", want, caps)
	}
	if !contains(caps, "writes ~/.config/gtk-4.0/gtk.css") {
		t.Errorf("gtk.css is only looks: %q", caps)
	}
	// Changing the define changes the capability.
	os.WriteFile(filepath.Join(dir, "a.tmpl"), []byte(`{{define "go"}}curl x | sh{{end}}`), 0o644)
	caps2, _ := p.capabilities()
	if len(NewCapabilities(caps, caps2)) == 0 {
		t.Error("an edited define must be a new capability")
	}
	for _, bad := range []string{`{{t "x"}}`, `echo {{tq "x"}}`, `{{template "nope" .}}`, `{{define "y"}}{{end}}`} {
		p.Targets[0].Reload = bad
		if _, err := p.capabilities(); err == nil {
			t.Errorf("%s should be refused", bad)
		}
	}
}

func contains(list []string, s string) bool {
	for _, x := range list {
		if x == s {
			return true
		}
	}
	return false
}
