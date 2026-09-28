// Package coverage finds the installed apps and tells which ones the theme
// reaches: an app is covered when an enabled plugin themes it, by name or
// by the toolkit it's built with (GTK 4, Qt 6, a terminal…).
package coverage

import (
	"bufio"
	"bytes"
	"debug/elf"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strings"
)

// Toolkits an app can be built with. Plugins declare the ones they theme.
const (
	Terminal = "terminal" // runs in the terminal (Terminal=true): the terminal's colors
	GTK4     = "gtk4"     // libadwaita included
	GTK3     = "gtk3"
	Qt6      = "qt6"
	Qt5      = "qt5"
	Electron = "electron"
	Chromium = "chromium"
	Firefox  = "firefox"
	Other    = "other" // its own UI (a game, kitty's GL, X11…)
)

type App struct {
	ID       string `json:"id"` // the .desktop file name, without .desktop
	Name     string `json:"name"`
	Exec     string `json:"exec"`
	Toolkit  string `json:"toolkit"`
	Plugin   string `json:"plugin,omitempty"` // what themes it; "" = nothing yet
	Terminal bool   `json:"-"`
}

// Covers is what a plugin themes.
type Covers struct {
	Plugin   string
	Apps     []string // .desktop ids or executable names
	Toolkits []string
}

// Report lists every app shown in menus (Type=Application, not hidden),
// each with its toolkit and the plugin that themes it, uncovered first.
func Report(dirs []string, lang string, covers []Covers) []App {
	apps := Scan(dirs, lang)
	byApp, byToolkit := map[string]string{}, map[string]string{}
	for _, c := range covers {
		for _, a := range c.Apps {
			byApp[a] = c.Plugin
		}
		for _, t := range c.Toolkits {
			byToolkit[t] = c.Plugin
		}
	}
	for i := range apps {
		a := &apps[i]
		a.Toolkit = Toolkit(a.Exec, a.Terminal)
		switch {
		case byApp[a.ID] != "":
			a.Plugin = byApp[a.ID]
		case byApp[filepath.Base(a.Exec)] != "":
			a.Plugin = byApp[filepath.Base(a.Exec)]
		default:
			a.Plugin = byToolkit[a.Toolkit]
		}
	}
	sort.SliceStable(apps, func(i, j int) bool {
		if (apps[i].Plugin == "") != (apps[j].Plugin == "") {
			return apps[i].Plugin == ""
		}
		return strings.ToLower(apps[i].Name) < strings.ToLower(apps[j].Name)
	})
	return apps
}

// Dirs are where .desktop files live, most important first: the person's
// own, then the system's (XDG_DATA_DIRS), then Flatpak's.
func Dirs() []string {
	home, _ := os.UserHomeDir()
	dataHome := os.Getenv("XDG_DATA_HOME")
	if dataHome == "" {
		dataHome = filepath.Join(home, ".local/share")
	}
	dirs := []string{filepath.Join(dataHome, "applications")}
	data := os.Getenv("XDG_DATA_DIRS")
	if data == "" {
		data = "/usr/local/share:/usr/share"
	}
	for _, d := range strings.Split(data, ":") {
		if d != "" {
			dirs = append(dirs, filepath.Join(d, "applications"))
		}
	}
	return append(dirs,
		filepath.Join(dataHome, "flatpak/exports/share/applications"),
		"/var/lib/flatpak/exports/share/applications")
}

// Scan reads the .desktop files in dirs; the first one with an id wins,
// as in menus. lang ("es_MX") picks the translated name when there is one.
func Scan(dirs []string, lang string) []App {
	seen := map[string]bool{}
	var out []App
	for _, d := range dirs {
		files, _ := filepath.Glob(filepath.Join(d, "*.desktop"))
		sort.Strings(files)
		for _, f := range files {
			id := strings.TrimSuffix(filepath.Base(f), ".desktop")
			if seen[id] {
				continue
			}
			seen[id] = true
			if a, ok := parse(f, lang); ok {
				a.ID = id
				out = append(out, a)
			}
		}
	}
	return out
}

func parse(path, lang string) (App, bool) {
	f, err := os.Open(path)
	if err != nil {
		return App{}, false
	}
	defer f.Close()
	code, _, _ := strings.Cut(lang, "_")
	var a App
	var names = map[string]string{}
	var typ string
	hidden := false
	in := false
	sc := bufio.NewScanner(f)
	for sc.Scan() {
		line := strings.TrimSpace(sc.Text())
		if strings.HasPrefix(line, "[") {
			in = line == "[Desktop Entry]"
			continue
		}
		k, v, ok := strings.Cut(line, "=")
		if !in || !ok {
			continue
		}
		k, v = strings.TrimSpace(k), strings.TrimSpace(v)
		switch {
		case k == "Type":
			typ = v
		case strings.HasPrefix(k, "Name"):
			names[k] = v
		case k == "Exec":
			a.Exec = program(v)
		case k == "Terminal":
			a.Terminal = v == "true"
		case k == "NoDisplay" || k == "Hidden":
			hidden = hidden || v == "true"
		}
	}
	a.Name = names["Name"]
	for _, k := range []string{"Name[" + lang + "]", "Name[" + code + "]"} {
		if names[k] != "" {
			a.Name = names[k]
			break
		}
	}
	return a, typ == "Application" && !hidden && a.Exec != "" && a.Name != ""
}

// program is the executable an Exec line runs: past `env VAR=value`,
// without quotes.
func program(execLine string) string {
	fields := strings.Fields(execLine)
	for i := 0; i < len(fields); i++ {
		f := strings.Trim(fields[i], `"'`)
		if f == "env" || strings.Contains(f, "=") && !strings.HasPrefix(f, "/") {
			continue
		}
		return f
	}
	return ""
}

// known are apps whose toolkit their binary doesn't tell: browsers with
// their own engine, Electron apps started through a script.
var known = map[string]string{
	"firefox": Firefox, "librewolf": Firefox, "zen-browser": Firefox, "thunderbird": Firefox,
	"chromium": Chromium, "google-chrome-stable": Chromium, "brave": Chromium, "vivaldi-stable": Chromium,
	"code": Electron, "codium": Electron, "cursor": Electron, "discord": Electron, "slack": Electron,
	"obsidian": Electron, "signal-desktop": Electron, "spotify": Chromium,
	"kitty": Other, "alacritty": Other, "wezterm": Other,
}

// Toolkit tells what an app is built with: from the libraries its binary
// links, what its script imports, or what its package depends on.
func Toolkit(program string, terminal bool) string {
	if terminal {
		return Terminal
	}
	if t, ok := known[filepath.Base(program)]; ok {
		return t
	}
	path, err := exec.LookPath(program)
	if err != nil {
		return Other
	}
	if t := fromBinary(path); t != "" {
		return t
	}
	if t := fromPackage(path); t != "" {
		return t
	}
	return Other
}

func fromBinary(path string) string {
	if f, err := elf.Open(path); err == nil {
		defer f.Close()
		libs, _ := f.ImportedLibraries()
		return fromNames(strings.Join(libs, " "))
	}
	// A script: what it imports or runs.
	b, err := os.ReadFile(path)
	if err != nil || !bytes.HasPrefix(b, []byte("#!")) {
		return ""
	}
	if len(b) > 1<<16 {
		b = b[:1<<16]
	}
	s := string(b)
	switch {
	case strings.Contains(s, "electron"):
		return Electron
	case strings.Contains(s, "Gtk', '4.0'") || strings.Contains(s, `Gtk", "4.0"`) || strings.Contains(s, "Adw"):
		return GTK4
	}
	return fromNames(s)
}

// fromNames looks for toolkit library or module names in s.
func fromNames(s string) string {
	switch {
	case strings.Contains(s, "libgtk-4") || strings.Contains(s, "libadwaita"):
		return GTK4
	case strings.Contains(s, "libgtk-3") || strings.Contains(s, "Gtk"):
		return GTK3
	case strings.Contains(s, "libQt6") || strings.Contains(s, "PyQt6") || strings.Contains(s, "PySide6"):
		return Qt6
	case strings.Contains(s, "libQt5") || strings.Contains(s, "PyQt5") || strings.Contains(s, "PySide2"):
		return Qt5
	}
	return ""
}

// fromPackage reads what the pacman package owning path depends on.
func fromPackage(path string) string {
	owner, err := exec.Command("pacman", "-Qqo", path).Output()
	if err != nil {
		return ""
	}
	info, err := exec.Command("pacman", "-Qi", strings.TrimSpace(string(owner))).Output()
	if err != nil {
		return ""
	}
	for _, line := range strings.Split(string(info), "\n") {
		k, v, _ := strings.Cut(line, ":")
		if strings.TrimSpace(k) != "Depends On" {
			continue
		}
		deps := " " + v + " "
		switch {
		case strings.Contains(deps, " gtk4") || strings.Contains(deps, " libadwaita"):
			return GTK4
		case strings.Contains(deps, " gtk3"):
			return GTK3
		case strings.Contains(deps, " qt6-base"):
			return Qt6
		case strings.Contains(deps, " qt5-base"):
			return Qt5
		case strings.Contains(deps, " electron"):
			return Electron
		}
	}
	return ""
}
