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
	return t
}

func renderOne(t *testing.T, tmpl string) (string, error) {
	t.Helper()
	dir := t.TempDir()
	os.WriteFile(filepath.Join(dir, "f.tmpl"), []byte(tmpl), 0o644)
	p := &plugin.Plugin{Dir: dir, Targets: []plugin.Target{{Template: "f.tmpl", Output: filepath.Join(dir, "out")}}}
	p.Meta.ID = "p"
	files, err := All([]*plugin.Plugin{p}, testTheme())
	if err != nil {
		return "", err
	}
	return string(files[0].Content), nil
}

func TestColorFuncs(t *testing.T) {
	cases := map[string]string{
		`{{c "accent"}}`:             "#ffb000",
		`{{hex (c "accent")}}`:       "ffb000",
		`{{rgb (c "accent")}}`:       "rgb(ffb000)",
		`{{rgba (c "accent") 0.5}}`:  "rgba(ffb00080)",
		`{{cssa (c "accent") 0.36}}`: "rgba(255, 176, 0, 0.36)",
		`{{speed 0.5}}`:              "0.90",
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
