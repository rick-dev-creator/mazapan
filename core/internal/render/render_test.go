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
