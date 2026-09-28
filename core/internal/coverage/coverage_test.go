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
	// Not installed anymore, or for another desktop: not shown.
	write(t, system, "gone.desktop", "[Desktop Entry]\nType=Application\nName=Gone\nExec=gone\nTryExec=/nonexistent/gone\n")
	write(t, system, "gnome.desktop", "[Desktop Entry]\nType=Application\nName=G\nExec=g\nOnlyShowIn=GNOME;\n")
	// In a subdirectory: its id carries the directory.
	write(t, filepath.Join(system, "vendor"), "app.desktop", "[Desktop Entry]\nType=Application\nName=V\nExec=v\n")
	t.Setenv("XDG_CURRENT_DESKTOP", "Hyprland")
	apps := Scan([]string{user, system}, "es_MX")
	if len(apps) != 3 {
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
	if _, ok := byID["vendor-app"]; !ok {
		t.Errorf("vendor/app.desktop should be vendor-app: %+v", apps)
	}
}

func TestProgram(t *testing.T) {
	for line, want := range map[string]struct {
		prog     string
		terminal bool
		flatpak  string
	}{
		`firefox %u`:                             {"firefox", false, ""},
		`env -u FOO BAR=1 "/opt/My App/app" --x`: {"/opt/My App/app", false, ""},
		`xdg-terminal-exec --app-id=TUI.float -e bash -c "dua i /"`: {"dua", true, ""},
		`foot -e htop`:                {"htop", true, ""},
		`sh -c "exec obsidian --foo"`: {"obsidian", false, ""},
		`/usr/bin/flatpak run --branch=stable org.gimp.GIMP @@u %U @@`: {"/usr/bin/flatpak", false, "org.gimp.GIMP"},
	} {
		prog, term, fp := program(line)
		if prog != want.prog || term != want.terminal || fp != want.flatpak {
			t.Errorf("%s: got %q %v %q, want %+v", line, prog, term, fp, want)
		}
	}
}

func TestFromDeps(t *testing.T) {
	for want, deps := range map[string][]string{
		Electron: {"gtk3", "electron32", "nss"},
		Chromium: {"gtk3", "nss", "alsa-lib"},
		Other:    {"gtk3", "webkit2gtk-4.1"},
		GTK4:     {"gtk4>=4.14", "glib2"},
		GTK3:     {"gtk3", "gtk4-layer-shell"},
		"":       {"gtk4-layer-shell", "wayland"},
	} {
		if got := fromDeps(deps); got != want {
			t.Errorf("%v: got %q, want %q", deps, got, want)
		}
	}
}

func TestReport(t *testing.T) {
	dir := t.TempDir()
	write(t, dir, "yt.desktop", "[Desktop Entry]\nType=Application\nName=YouTube\nExec=omarchy-launch-webapp https://youtube.com/\n")
	write(t, dir, "a.desktop", "[Desktop Entry]\nType=Application\nName=A tui\nExec=top\nTerminal=true\n")
	write(t, dir, "kitty.desktop", "[Desktop Entry]\nType=Application\nName=kitty\nExec=kitty\n")
	write(t, dir, "foot.desktop", "[Desktop Entry]\nType=Application\nName=Foot\nExec=foot\n")
	apps := Report([]string{dir}, "en", []Covers{{Plugin: "theme-foot", Apps: []string{"foot"}, Toolkits: []string{Terminal}}})
	if len(apps) != 4 || apps[0].Name != "kitty" || apps[0].Plugin != "" || apps[1].Toolkit != Web {
		t.Fatalf("uncovered first: %+v", apps)
	}
	for _, a := range apps[2:] {
		if a.Plugin != "theme-foot" {
			t.Errorf("%s should be covered by theme-foot: %+v", a.Name, a)
		}
	}
}

func TestToolkitFromScript(t *testing.T) {
	dir := t.TempDir()
	os.WriteFile(filepath.Join(dir, "target"), []byte("#!/usr/bin/python\nfrom gi.repository import Gtk\n"), 0o755)
	for name, body := range map[string]string{
		"py-gtk4": "#!/usr/bin/python\ngi.require_version('Gtk', '4.0')\n",
		"py-gtk3": "#!/usr/bin/python\nfrom gi.repository import Gtk\n",
		"py-qt6":  "#!/usr/bin/python\nfrom PyQt6 import QtWidgets\n",
		"el":      "#!/bin/sh\nexec electron32 /usr/lib/app.asar \"$@\"\n",
		"prose":   "#!/bin/sh\n# STMicroelectronics chips; Adwaita; Gtk immodule\necho hi\n",
		"wrapper": "#!/bin/bash\nexport X=1\nexec " + filepath.Join(dir, "target") + " \"$@\"\n",
	} {
		p := filepath.Join(dir, name)
		os.WriteFile(p, []byte(body), 0o755)
		want := map[string]string{"py-gtk4": GTK4, "py-gtk3": GTK3, "py-qt6": Qt6, "el": Electron, "prose": "", "wrapper": GTK3}[name]
		if got := fromBinary(p, 0); got != want {
			t.Errorf("%s: got %q, want %q", name, got, want)
		}
	}
}
