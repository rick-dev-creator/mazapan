package locale

import (
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"
)

func TestClean(t *testing.T) {
	for in, want := range map[string]string{
		"es_MX.UTF-8":      "es_MX",
		"de_DE.UTF-8@euro": "de_DE",
		"en":               "en",
		"C":                "",
		"POSIX":            "",
		"":                 "",
	} {
		if got := clean(in); got != want {
			t.Errorf("clean(%q) = %q, want %q", in, got, want)
		}
	}
}

func TestDetectOrder(t *testing.T) {
	t.Setenv("LC_ALL", "")
	t.Setenv("LC_MESSAGES", "fr_FR.UTF-8")
	t.Setenv("LANG", "es_MX.UTF-8")
	if got := Detect(""); got != "fr_FR" {
		t.Errorf("LC_MESSAGES should win over LANG, got %q", got)
	}
	if got := Detect("pt_BR"); got != "pt_BR" {
		t.Errorf("config override should win, got %q", got)
	}
}

func TestCandidates(t *testing.T) {
	for in, want := range map[string][]string{
		"es_MX": {"es_MX", "es", "en"},
		"es":    {"es", "en"},
		"en_US": {"en_US", "en"},
		"":      {"en"},
	} {
		if got := Candidates(in); !reflect.DeepEqual(got, want) {
			t.Errorf("Candidates(%q) = %v, want %v", in, got, want)
		}
	}
}

func writeLocales(t *testing.T, files map[string]string) string {
	t.Helper()
	dir := t.TempDir()
	os.MkdirAll(filepath.Join(dir, "locales"), 0o755)
	for name, body := range files {
		os.WriteFile(filepath.Join(dir, "locales", name+".toml"), []byte(body), 0o644)
	}
	return dir
}

func TestFallbackChain(t *testing.T) {
	dir := writeLocales(t, map[string]string{
		"en":    "a = 'A'\nb = 'B'\nc = 'C'\n",
		"es":    "a = 'A-es'\nb = 'B-es'\n",
		"es_MX": "a = 'A-mx'\n",
	})
	c, err := Load("p", dir, "es_MX")
	if err != nil {
		t.Fatal(err)
	}
	for key, want := range map[string]string{"a": "A-mx", "b": "B-es", "c": "C"} {
		if got, _ := c.T(key); got != want {
			t.Errorf("T(%q) = %q, want %q", key, got, want)
		}
	}
	if _, err := c.T("nope"); err == nil {
		t.Error("unknown key must be an error")
	}
}

func TestKeyNotInEnglishIsAnError(t *testing.T) {
	dir := writeLocales(t, map[string]string{
		"en": "hello = 'hello'\n",
		"es": "helo = 'hola'\n",
	})
	_, err := Load("p", dir, "es")
	if err == nil || !strings.Contains(err.Error(), `locales/es.toml has "helo"`) {
		t.Fatalf("want a typo error naming es.toml, got %v", err)
	}
}
