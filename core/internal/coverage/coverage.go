// Package coverage finds the installed apps and tells which ones the theme
// reaches: an app is covered when an enabled plugin themes it, by name or
// by the toolkit it's built with (GTK 4, Qt 6, a terminal…).
package coverage

import (
	"bufio"
	"bytes"
	"debug/elf"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"sort"
	"strings"
)

// Toolkits an app can be built with. Plugins declare the ones they theme.
const (
	Terminal = "terminal" // runs in a terminal: the terminal's colors
	GTK4     = "gtk4"     // libadwaita included
	GTK3     = "gtk3"
	Qt6      = "qt6"
	Qt5      = "qt5"
	Electron = "electron"
	Chromium = "chromium"
	Firefox  = "firefox"
	Flatpak  = "flatpak" // sandboxed: host theme files don't reach it
	Web      = "web"     // a web app or page: it looks as the browser does
	Other    = "other"   // its own UI (Flutter, a web view, a game, kitty's GL…)
)

type App struct {
	ID       string `json:"id"` // the .desktop file's id
	Name     string `json:"name"`
	Exec     string `json:"exec"`
	Toolkit  string `json:"toolkit"`
	Plugin   string `json:"plugin,omitempty"` // what themes it; "" = nothing yet
	Terminal bool   `json:"-"`
	Flatpak  string `json:"-"` // its Flatpak id
	WebApp   bool   `json:"-"` // opens a URL
}

// Covers is what a plugin themes.
type Covers struct {
	Plugin   string
	Apps     []string // .desktop ids or executable names
	Toolkits []string
}

// Report lists every app shown in menus, each with its toolkit and the
// plugin that themes it, uncovered first. Covers are taken in order: when
// two plugins claim the same app or toolkit, the first one gets it.
func Report(dirs []string, lang string, covers []Covers) []App {
	apps := Scan(dirs, lang)
	byApp, byToolkit := map[string]string{}, map[string]string{}
	for _, c := range covers {
		for _, a := range c.Apps {
			if byApp[a] == "" {
				byApp[a] = c.Plugin
			}
		}
		for _, t := range c.Toolkits {
			if byToolkit[t] == "" {
				byToolkit[t] = c.Plugin
			}
		}
	}
	// Every toolkit first (the ones only a package can tell come from two
	// pacman calls for all of them), then who themes each app.
	d := newDetector()
	for i := range apps {
		apps[i].Toolkit = d.toolkit(&apps[i])
	}
	d.resolvePackages(apps)
	for i := range apps {
		a := &apps[i]
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

// Scan reads the .desktop files in dirs (subdirectories too: vendor/x.desktop
// is "vendor-x"); the first file with an id wins, as in menus, even when it
// hides the app. lang ("es_MX") picks the translated name.
func Scan(dirs []string, lang string) []App {
	desktops := strings.Split(os.Getenv("XDG_CURRENT_DESKTOP"), ":")
	seen := map[string]bool{}
	var out []App
	for _, d := range dirs {
		var files []string
		filepath.WalkDir(d, func(p string, e os.DirEntry, err error) error {
			if err == nil && !e.IsDir() && strings.HasSuffix(p, ".desktop") {
				files = append(files, p)
			}
			return nil
		})
		sort.Strings(files)
		for _, f := range files {
			rel, _ := filepath.Rel(d, f)
			id := strings.ReplaceAll(strings.TrimSuffix(rel, ".desktop"), string(filepath.Separator), "-")
			if seen[id] {
				continue
			}
			seen[id] = true
			if a, ok := parse(f, lang, desktops); ok {
				a.ID = id
				out = append(out, a)
			}
		}
	}
	return out
}

func parse(path, lang string, desktops []string) (App, bool) {
	f, err := os.Open(path)
	if err != nil {
		return App{}, false
	}
	defer f.Close()
	code, _, _ := strings.Cut(lang, "_")
	var a App
	names := map[string]string{}
	var typ, tryExec, onlyShowIn, notShowIn string
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
		case k == "Name" || strings.HasPrefix(k, "Name["):
			names[k] = v
		case k == "Exec":
			a.Exec, a.Terminal, a.Flatpak = program(v)
			// omarchy-launch-webapp https://…, xdg-open http://…
			a.WebApp = strings.Contains(v, "http://") || strings.Contains(v, "https://")
		case k == "TryExec":
			tryExec = v
		case k == "Terminal":
			a.Terminal = a.Terminal || v == "true"
		case k == "X-Flatpak":
			a.Flatpak = v
		case k == "OnlyShowIn":
			onlyShowIn = v
		case k == "NotShowIn":
			notShowIn = v
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
	if tryExec != "" {
		if _, err := exec.LookPath(tryExec); err != nil {
			return App{}, false // not installed anymore
		}
	}
	if onlyShowIn != "" && !anyIn(onlyShowIn, desktops) || notShowIn != "" && anyIn(notShowIn, desktops) {
		return App{}, false
	}
	return a, typ == "Application" && !hidden && a.Exec != "" && a.Name != ""
}

// anyIn: is one of desktops in list ("GNOME;KDE;")?
func anyIn(list string, desktops []string) bool {
	for _, x := range strings.Split(list, ";") {
		for _, d := range desktops {
			if x != "" && strings.EqualFold(x, d) {
				return true
			}
		}
	}
	return false
}

// fields splits an Exec line the way the spec quotes it: "…" keeps spaces.
func fields(s string) []string {
	var out []string
	var cur strings.Builder
	quoted, any := false, false
	for i := 0; i < len(s); i++ {
		c := s[i]
		switch {
		case c == '"':
			quoted, any = !quoted, true
		case c == '\\' && quoted && i+1 < len(s):
			i++
			cur.WriteByte(s[i])
		case (c == ' ' || c == '\t') && !quoted:
			if any || cur.Len() > 0 {
				out = append(out, cur.String())
			}
			cur.Reset()
			any = false
		default:
			cur.WriteByte(c)
		}
	}
	if any || cur.Len() > 0 {
		out = append(out, cur.String())
	}
	return out
}

// terminals start what follows -e (or --) in themselves.
var terminals = map[string]bool{
	"xdg-terminal-exec": true, "foot": true, "footclient": true, "kitty": true, "alacritty": true,
	"wezterm": true, "ghostty": true, "konsole": true, "gnome-terminal": true, "xterm": true,
}

// program is the executable an Exec line really runs: past `env`, shells
// (`sh -c "…"`) and terminals (`xdg-terminal-exec -e top`: a terminal app),
// or a Flatpak's id.
func program(execLine string) (prog string, terminal bool, flatpak string) {
	f := fields(execLine)
	for len(f) > 0 {
		base := filepath.Base(f[0])
		switch {
		case base == "env":
			f = f[1:]
			for len(f) > 0 && (strings.Contains(f[0], "=") || strings.HasPrefix(f[0], "-")) {
				if f[0] == "-u" && len(f) > 1 {
					f = f[1:]
				}
				f = f[1:]
			}
		case base == "exec" && len(f) > 1:
			f = f[1:]
		case base == "flatpak" && len(f) > 1 && f[1] == "run":
			for _, x := range f[2:] {
				if !strings.HasPrefix(x, "-") {
					return f[0], false, x
				}
			}
			return f[0], false, ""
		case (base == "sh" || base == "bash" || base == "zsh") && len(f) > 2 && f[1] == "-c":
			f = fields(f[2])
		case terminals[base] && len(f) > 1:
			i := indexOf(f, "-e")
			if i < 0 {
				i = indexOf(f, "--")
			}
			if i < 0 || i+1 >= len(f) {
				return f[0], false, ""
			}
			inner, _, _ := program(strings.Join(quoteAll(f[i+1:]), " "))
			return inner, true, ""
		default:
			return f[0], false, ""
		}
	}
	return "", false, ""
}

func indexOf(f []string, s string) int {
	for i, x := range f {
		if x == s {
			return i
		}
	}
	return -1
}

func quoteAll(f []string) []string {
	out := make([]string, len(f))
	for i, x := range f {
		out[i] = `"` + strings.ReplaceAll(x, `"`, `\"`) + `"`
	}
	return out
}

// known are apps whose toolkit their binary doesn't tell.
var known = map[string]string{
	"firefox": Firefox, "librewolf": Firefox, "zen-browser": Firefox, "zen-bin": Firefox, "zen": Firefox,
	"floorp": Firefox, "mullvad-browser": Firefox, "thunderbird": Firefox, "waterfox": Firefox,
	"chromium": Chromium, "google-chrome-stable": Chromium, "brave": Chromium, "vivaldi-stable": Chromium,
	"microsoft-edge-stable": Chromium, "spotify": Chromium,
	"code": Electron, "codium": Electron, "cursor": Electron, "discord": Electron, "slack": Electron,
	"obsidian": Electron, "signal-desktop": Electron,
	"kitty": Other, "alacritty": Other, "wezterm": Other, "ghostty": Other,
}

type detector struct {
	byPath map[string]string // resolved path -> toolkit ("" = ask pacman)
}

func newDetector() *detector { return &detector{byPath: map[string]string{}} }

func (d *detector) toolkit(a *App) string {
	switch {
	case a.Flatpak != "":
		return Flatpak
	case a.Terminal:
		return Terminal
	case a.WebApp:
		return Web
	}
	if t, ok := known[filepath.Base(a.Exec)]; ok {
		return t
	}
	path, err := exec.LookPath(a.Exec)
	if err != nil {
		return Other
	}
	if real, err := filepath.EvalSymlinks(path); err == nil {
		path = real
	}
	if t, ok := d.byPath[path]; ok {
		return t
	}
	t := fromBinary(path, 0)
	d.byPath[path] = t
	return t // "": decided later from its package (resolvePackages)
}

// Web engines first: Electron, Chromium and Firefox builds link GTK for
// their dialogs, but a GTK theme doesn't reach what they draw.
func fromBinary(path string, depth int) string {
	if t, ok := known[filepath.Base(path)]; ok {
		return t
	}
	dir := filepath.Dir(path)
	has := func(names ...string) bool {
		for _, n := range names {
			if _, err := os.Stat(filepath.Join(dir, n)); err == nil {
				return true
			}
		}
		return false
	}
	if f, err := elf.Open(path); err == nil {
		defer f.Close()
		libs, _ := f.ImportedLibraries()
		all := strings.Join(libs, " ")
		switch {
		case has("resources/app.asar", "resources/app"):
			return Electron
		case strings.Contains(all, "libffmpeg") || strings.Contains(all, "libcef") || has("v8_context_snapshot.bin", "chrome_100_percent.pak"):
			return Chromium
		case has("libxul.so", "application.ini"):
			return Firefox
		case strings.Contains(all, "libflutter") || strings.Contains(all, "webkit"):
			return Other
		}
		return fromLibs(all)
	}
	// A script: what it imports, or the program it hands over to.
	f, err := os.Open(path)
	if err != nil {
		return ""
	}
	defer f.Close()
	b, _ := io.ReadAll(io.LimitReader(f, 1<<16))
	if !bytes.HasPrefix(b, []byte("#!")) {
		return ""
	}
	s := string(b)
	switch {
	case webAppRun.MatchString(s):
		return Web
	case electronRun.MatchString(s):
		return Electron
	case gtk4Import.MatchString(s):
		return GTK4
	case qtImport.MatchString(s):
		if strings.Contains(qtImport.FindString(s), "6") {
			return Qt6
		}
		return Qt5
	case gtkImport.MatchString(s):
		return GTK3
	}
	if m := execTarget.FindStringSubmatch(s); m != nil && depth < 2 {
		return fromBinary(m[1], depth+1)
	}
	return ""
}

var (
	webAppRun   = regexp.MustCompile(`--app=|launch-webapp`)
	electronRun = regexp.MustCompile(`(?m)^\s*(exec\s+)?\S*/?electron[0-9]*\s`)
	gtk4Import  = regexp.MustCompile(`require_version\(\s*['"](Gtk['"]\s*,\s*['"]4|Adw['"])`)
	gtkImport   = regexp.MustCompile(`from gi\.repository import[^\n]*\bGtk\b|require_version\(\s*['"]Gtk`)
	qtImport    = regexp.MustCompile(`(?m)^\s*(from|import)\s+(PyQt[56]|PySide[26])`)
	execTarget  = regexp.MustCompile(`(?m)^\s*exec\s+"?(/[^\s"]+)`)
)

// fromLibs: the toolkit's own libraries, or ones built on it
// (libKF6…, libFcitx5Qt6…).
func fromLibs(s string) string {
	switch {
	case strings.Contains(s, "libgtk-4") || strings.Contains(s, "libadwaita"):
		return GTK4
	case strings.Contains(s, "libgtk-3"):
		return GTK3
	case strings.Contains(s, "Qt6") || strings.Contains(s, "libKF6"):
		return Qt6
	case strings.Contains(s, "Qt5") || strings.Contains(s, "libKF5"):
		return Qt5
	}
	return ""
}

// resolvePackages decides, from what their pacman packages depend on, the
// toolkit of the apps their binary didn't tell: two pacman calls in all.
func (d *detector) resolvePackages(apps []App) {
	var paths []string
	for p, t := range d.byPath {
		if t == "" {
			paths = append(paths, p)
		}
	}
	if len(paths) == 0 {
		return
	}
	sort.Strings(paths)
	owner := map[string]string{} // path -> package
	out, _ := pacman(append([]string{"-Qo"}, paths...)...)
	for _, line := range strings.Split(out, "\n") {
		// "/usr/bin/x is owned by pkg 1.0-1"
		if p, rest, ok := strings.Cut(line, " is owned by "); ok {
			if f := strings.Fields(rest); len(f) > 0 {
				owner[p] = f[0]
			}
		}
	}
	pkgs := map[string]bool{}
	for _, p := range owner {
		pkgs[p] = true
	}
	deps := map[string]string{} // package -> toolkit
	if len(pkgs) > 0 {
		args := []string{"-Qi"}
		for p := range pkgs {
			args = append(args, p)
		}
		out, _ := pacman(args...)
		name := ""
		for _, line := range strings.Split(out, "\n") {
			k, v, _ := strings.Cut(line, ":")
			switch strings.TrimSpace(k) {
			case "Name":
				name = strings.TrimSpace(v)
			case "Depends On":
				deps[name] = fromDeps(strings.Fields(v))
			}
		}
	}
	for p := range d.byPath {
		if d.byPath[p] == "" {
			d.byPath[p] = Other
			if t := deps[owner[p]]; t != "" {
				d.byPath[p] = t
			}
		}
	}
	for i := range apps {
		if apps[i].Toolkit != "" {
			continue
		}
		path, _ := exec.LookPath(apps[i].Exec)
		if real, err := filepath.EvalSymlinks(path); err == nil {
			path = real
		}
		apps[i].Toolkit = d.byPath[path]
		if apps[i].Toolkit == "" {
			apps[i].Toolkit = Other
		}
	}
}

// pacman runs in English: the field names it prints ("Depends On") are
// translated otherwise.
func pacman(args ...string) (string, error) {
	cmd := exec.Command("pacman", args...)
	cmd.Env = append(os.Environ(), "LC_ALL=C")
	out, err := cmd.Output()
	return string(out), err
}

// fromDeps: web engines before GTK (they depend on gtk3 for dialogs).
func fromDeps(deps []string) string {
	has := func(prefixes ...string) bool {
		for _, d := range deps {
			name, _, _ := strings.Cut(d, ">") // "gtk3>=3.24"
			name, _, _ = strings.Cut(name, "=")
			for _, p := range prefixes {
				if name == p || strings.HasPrefix(p, "electron") && strings.HasPrefix(name, "electron") {
					return true
				}
			}
		}
		return false
	}
	switch {
	case has("electron"):
		return Electron
	case has("nss") && has("gtk3"):
		return Chromium // Chromium and CEF builds: nss + gtk3
	case has("webkit2gtk", "webkit2gtk-4.1", "webkitgtk-6.0", "flutter"):
		return Other
	case has("gtk4", "libadwaita"):
		return GTK4
	case has("gtk3"):
		return GTK3
	case has("qt6-base"):
		return Qt6
	case has("qt5-base"):
		return Qt5
	}
	return ""
}
