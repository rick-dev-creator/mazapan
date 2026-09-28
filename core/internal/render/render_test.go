package render

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"myarch/internal/plugin"
	"myarch/internal/theme"
)

func testTheme() *theme.Theme {
	t := &theme.Theme{ID: "t", Colors: map[string]string{"accent": "#ffb000"}}
	t.Motion.DurationMS = 180
	t.Motion.ResponseMS = 400
	t.Motion.Damping = 0.8
	return t
}

func renderOne(t *testing.T, tmpl string) (string, error) {
	t.Helper()
	dir := t.TempDir()
	os.WriteFile(filepath.Join(dir, "f.tmpl"), []byte(tmpl), 0o644)
	os.MkdirAll(filepath.Join(dir, "locales"), 0o755)
	os.WriteFile(filepath.Join(dir, "locales", "en.toml"), []byte("hello = \"hello\"\nbye = \"bye\"\n"), 0o644)
	os.WriteFile(filepath.Join(dir, "locales", "es.toml"), []byte("hello = 'hola \"tú\"'\n"), 0o644)
	p := &plugin.Plugin{Dir: dir, Targets: []plugin.Target{{Template: "f.tmpl", Output: filepath.Join(dir, "out")}}}
	p.Meta.ID = "p"
	out, err := All([]*plugin.Plugin{p}, testTheme(), nil, "es_MX")
	if err != nil {
		return "", err
	}
	return string(out.Files[0].Content), nil
}

func TestColorFuncs(t *testing.T) {
	cases := map[string]string{
		`{{c "accent"}}`:             "#ffb000",
		`{{hex (c "accent")}}`:       "ffb000",
		`{{rgb (c "accent")}}`:       "rgb(ffb000)",
		`{{rgba (c "accent") 0.5}}`:  "rgba(ffb00080)",
		`{{cssa (c "accent") 0.36}}`: "rgba(255, 176, 0, 0.36)",
		`{{speed 0.5}}`:              "0.90",
		// omega = 2*pi/0.4 = 15.708: k = omega^2, c = 2*z*omega
		`{{spring 1 0}}`:             "mass = 1, stiffness = 246.74, dampening = 25.13",
		`{{spring 1 1}}`:             "mass = 1, stiffness = 246.74, dampening = 31.42",
		`{{spring 0.5 0}}`:           "mass = 1, stiffness = 986.96, dampening = 50.27",
		`{{camel "bright_magenta"}}`: "brightMagenta",
		`{{camel "bg"}}`:             "bg",
		`{{t "hello"}}`:              `hola "tú"`,
		`{{tq "hello"}}`:             `"hola \"tú\""`,
		`{{t "bye"}}`:                "bye", // missing in es: falls back to en
		`{{.Lang}} {{.LangCode}}`:    "es_MX es",
	}
	for in, want := range cases {
		got, err := renderOne(t, in)
		if err != nil || got != want {
			t.Errorf("%s = %q (%v), want %q", in, got, err, want)
		}
	}
}

func TestUnknownTokenFails(t *testing.T) {
	_, err := renderOne(t, `{{c "acent"}}`)
	if err == nil || !strings.Contains(err.Error(), `no color "acent"`) {
		t.Fatalf("typo in a token must fail the render, got %v", err)
	}
}

func TestChecksAreRendered(t *testing.T) {
	dir := t.TempDir()
	os.MkdirAll(filepath.Join(dir, "locales"), 0o755)
	os.WriteFile(filepath.Join(dir, "locales", "en.toml"), []byte("check = 'bar loads'\n"), 0o644)
	p := &plugin.Plugin{Dir: dir, Checks: []plugin.Check{{Name: `{{t "check"}}`, Run: `test {{.Settings.n}} = 3`}}}
	p.Meta.ID = "p"
	p.Settings = map[string]any{"n": int64(3)}
	out, err := All([]*plugin.Plugin{p}, testTheme(), nil, "en")
	if err != nil {
		t.Fatal(err)
	}
	c := out.Checks[0]
	if c.Name != "bar loads" || c.Run != "test 3 = 3" || c.Timeout != 15 || c.Plugin != "p" {
		t.Fatalf("check = %+v", c)
	}
}

// A target with `each` goes into every directory its marker matches, and
// its template sees which one.
func TestEachPlace(t *testing.T) {
	dir := t.TempDir()
	for _, p := range []string{"a.default", "b.work", "Crash Reports"} {
		os.MkdirAll(filepath.Join(dir, "profiles", p), 0o755)
	}
	os.WriteFile(filepath.Join(dir, "profiles", "a.default", "prefs.js"), nil, 0o644)
	os.WriteFile(filepath.Join(dir, "profiles", "b.work", "prefs.js"), nil, 0o644)
	os.MkdirAll(filepath.Join(dir, "plugin"), 0o755)
	os.WriteFile(filepath.Join(dir, "plugin", "f.tmpl"), []byte(`{{base .Place}}`), 0o644)
	p := &plugin.Plugin{Dir: filepath.Join(dir, "plugin"), Targets: []plugin.Target{{
		Template: "f.tmpl", Output: "chrome/x.css", Each: []string{filepath.Join(dir, "profiles", "*", "prefs.js")},
	}}}
	p.Meta.ID = "p"
	out, err := All([]*plugin.Plugin{p}, testTheme(), nil, "en")
	if err != nil {
		t.Fatal(err)
	}
	if len(out.Files) != 2 || out.Files[0].Path != filepath.Join(dir, "profiles", "a.default", "chrome", "x.css") || string(out.Files[1].Content) != "b.work" {
		t.Fatalf("files: %+v", out.Files)
	}
}

func TestActionsAreSeenByEveryTemplate(t *testing.T) {
	dir := t.TempDir()
	os.MkdirAll(filepath.Join(dir, "a", "locales"), 0o755)
	os.WriteFile(filepath.Join(dir, "a", "locales", "en.toml"), []byte("open = 'Open monitors'\n"), 0o644)
	a := &plugin.Plugin{Dir: filepath.Join(dir, "a"), Actions: []plugin.Action{
		{Name: `{{t "open"}}`, Run: "qs ipc call x", Key: "{{.Settings.key}}"},
		// Its key unbound in config.toml, and no command: left out.
		{Name: "only a key", Key: "{{.Settings.other}}"},
	}}
	a.Meta.ID = "a"
	a.Settings = map[string]any{"key": "SUPER + M", "other": ""}
	// "palette" sorts before nothing in particular: it must see a's
	// actions whatever the order.
	os.MkdirAll(filepath.Join(dir, "palette"), 0o755)
	os.WriteFile(filepath.Join(dir, "palette", "p.tmpl"), []byte(`{{json .Actions}}`), 0o644)
	pal := &plugin.Plugin{Dir: filepath.Join(dir, "palette"), Targets: []plugin.Target{{Template: "p.tmpl", Output: filepath.Join(dir, "out")}}}
	pal.Meta.ID = "palette"
	out, err := All([]*plugin.Plugin{pal, a}, testTheme(), nil, "en")
	if err != nil {
		t.Fatal(err)
	}
	want := `[{"plugin":"a","name":"Open monitors","run":"qs ipc call x","key":"SUPER + M"}]`
	if got := string(out.Files[0].Content); got != want {
		t.Fatalf("got %s\nwant %s", got, want)
	}
}
