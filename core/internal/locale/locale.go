// Package locale works out the language to render in and loads plugin
// translations.
//
// A plugin keeps its user-facing text in locales/<lang>.toml, flat
// key = "text" tables. en.toml is required and is the fallback; other
// languages may leave keys out. The language comes from the OS unless
// config.toml sets one.
package locale

import (
	"bufio"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"github.com/BurntSushi/toml"
)

// Fallback is the language every plugin must provide.
const Fallback = "en"

// Detect returns the language as a POSIX locale name without encoding or
// modifier ("es_MX", "en"): override if set, else LC_ALL, LC_MESSAGES,
// LANG, else LANG from /etc/locale.conf, else Fallback.
func Detect(override string) string {
	if override != "" {
		return clean(override)
	}
	for _, v := range []string{"LC_ALL", "LC_MESSAGES", "LANG"} {
		if l := clean(os.Getenv(v)); l != "" {
			return l
		}
	}
	if l := clean(fromLocaleConf("/etc/locale.conf")); l != "" {
		return l
	}
	return Fallback
}

// clean turns "es_MX.UTF-8@euro" into "es_MX"; "C" and "POSIX" mean no
// language at all.
func clean(l string) string {
	l = strings.TrimSpace(l)
	if i := strings.IndexAny(l, ".@"); i >= 0 {
		l = l[:i]
	}
	if l == "C" || l == "POSIX" {
		return ""
	}
	return l
}

func fromLocaleConf(path string) string {
	f, err := os.Open(path)
	if err != nil {
		return ""
	}
	defer f.Close()
	sc := bufio.NewScanner(f)
	for sc.Scan() {
		if v, ok := strings.CutPrefix(strings.TrimSpace(sc.Text()), "LANG="); ok {
			return strings.Trim(v, `"'`)
		}
	}
	return ""
}

// Candidates lists the translation files to try, most specific first:
// "es_MX" -> es_MX, es, en.
func Candidates(lang string) []string {
	out := []string{}
	add := func(l string) {
		for _, x := range out {
			if x == l {
				return
			}
		}
		out = append(out, l)
	}
	if lang != "" {
		add(lang)
		if base, _, ok := strings.Cut(lang, "_"); ok {
			add(base)
		}
	}
	add(Fallback)
	return out
}

// Catalog is one plugin's translations for one language, with fallbacks.
type Catalog struct {
	plugin string
	names  []string            // language of each chain entry
	chain  []map[string]string // most specific first, en last
}

// Load reads dir/locales for lang. A plugin without a locales directory
// gets an empty catalog: any lookup is an error, which is what a plugin
// that shows text but ships no translations deserves.
func Load(plugin, dir, lang string) (*Catalog, error) {
	c := &Catalog{plugin: plugin}
	var en map[string]string
	for _, l := range Candidates(lang) {
		m, err := read(filepath.Join(dir, "locales", l+".toml"))
		if err != nil {
			return nil, fmt.Errorf("plugin %s: %w", plugin, err)
		}
		if m == nil {
			continue
		}
		if l == Fallback {
			en = m
		}
		c.names = append(c.names, l)
		c.chain = append(c.chain, m)
	}
	// A key other languages have but en doesn't is a typo somewhere.
	if en != nil {
		for i, m := range c.chain[:len(c.chain)-1] {
			for k := range m {
				if _, ok := en[k]; !ok {
					return nil, fmt.Errorf("plugin %s: locales/%s.toml has %q, which en.toml doesn't", plugin, c.names[i], k)
				}
			}
		}
	}
	return c, nil
}

func read(path string) (map[string]string, error) {
	var m map[string]string
	_, err := toml.DecodeFile(path, &m)
	if os.IsNotExist(err) {
		return nil, nil
	}
	if err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}
	return m, nil
}

// T returns the text for key in the most specific language that has it.
func (c *Catalog) T(key string) (string, error) {
	for _, m := range c.chain {
		if v, ok := m[key]; ok {
			return v, nil
		}
	}
	return "", fmt.Errorf("plugin %s: no text for %q in locales/%s.toml", c.plugin, key, Fallback)
}
