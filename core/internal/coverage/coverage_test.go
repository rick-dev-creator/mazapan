package coverage

import (
	"os"
	"path/filepath"
	"testing"
)

func write(t *testing.T, dir, name, body string) {
	t.Helper()
	os.MkdirAll(dir, 0o755)
	if err := os.WriteFile(filepath.Join(dir, name), []byte(body), 0o644); err != nil {
		t.Fatal(err)
	}
}

func TestScan(t *testing.T) {
	user, system := t.TempDir(), t.TempDir()
	write(t, system, "term.desktop", "[Desktop Entry]\nType=Application\nName=Term\nName[es]=Terminal\nExec=env FOO=1 \"myterm\" %U\n")
	write(t, system, "hidden.desktop", "[Desktop Entry]\nType=Application\nName=Hidden\nExec=x\nNoDisplay=true\n")
	write(t, system, "link.desktop", "[Desktop Entry]\nType=Link\nName=Link\nURL=https://x\n")
	write(t, system, "tui.desktop", "[Desktop Entry]\nType=Application\nName=Top\nExec=top\nTerminal=true\n[Desktop Action new]\nExec=other\n")
	// The person's own copy wins over the system's.
	write(t, user, "tui.desktop", "[Desktop Entry]\nType=Application\nName=My top\nExec=htop\nTerminal=true\n")
	apps := Scan([]string{user, system}, "es_MX")
	if len(apps) != 2 {
		t.Fatalf("apps = %+v", apps)
	}
	byID := map[string]App{}
	for _, a := range apps {
		byID[a.ID] = a
	}
	if a := byID["term"]; a.Name != "Terminal" || a.Exec != "myterm" {
		t.Errorf("term = %+v", a)
	}
	if a := byID["tui"]; a.Name != "My top" || a.Exec != "htop" || !a.Terminal {
		t.Errorf("tui = %+v", a)
	}
}

func TestReport(t *testing.T) {
	dir := t.TempDir()
	write(t, dir, "a.desktop", "[Desktop Entry]\nType=Application\nName=A tui\nExec=top\nTerminal=true\n")
	write(t, dir, "kitty.desktop", "[Desktop Entry]\nType=Application\nName=kitty\nExec=kitty\n")
	write(t, dir, "foot.desktop", "[Desktop Entry]\nType=Application\nName=Foot\nExec=foot\n")
	apps := Report([]string{dir}, "en", []Covers{{Plugin: "theme-foot", Apps: []string{"foot"}, Toolkits: []string{Terminal}}})
	if len(apps) != 3 || apps[0].Name != "kitty" || apps[0].Plugin != "" {
		t.Fatalf("uncovered first: %+v", apps)
	}
	for _, a := range apps[1:] {
		if a.Plugin != "theme-foot" {
			t.Errorf("%s should be covered by theme-foot: %+v", a.Name, a)
		}
	}
}

func TestToolkitFromScript(t *testing.T) {
	dir := t.TempDir()
	for name, body := range map[string]string{
		"py-gtk4": "#!/usr/bin/python\ngi.require_version('Gtk', '4.0')\n",
		"py-gtk3": "#!/usr/bin/python\nfrom gi.repository import Gtk\n",
		"py-qt6":  "#!/usr/bin/python\nfrom PyQt6 import QtWidgets\n",
		"el":      "#!/bin/sh\nexec electron32 /usr/lib/app.asar \"$@\"\n",
	} {
		p := filepath.Join(dir, name)
		os.WriteFile(p, []byte(body), 0o755)
		want := map[string]string{"py-gtk4": GTK4, "py-gtk3": GTK3, "py-qt6": Qt6, "el": Electron}[name]
		if got := fromBinary(p); got != want {
			t.Errorf("%s: got %q, want %q", name, got, want)
		}
	}
}
