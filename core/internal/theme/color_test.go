package theme

import (
	"fmt"
	"strings"
	"testing"
)

func TestAccentTokensMatchPhosphor(t *testing.T) {
	colors := map[string]string{"bg": "#1a1917", "surface_raised": "#2d2b28", "fg": "#dcd3bf"}
	// Phosphor's hand-picked accent tokens, from its accent and colors.
	got, err := AccentTokens("#7847eb", colors, "dark")
	if err != nil {
		t.Fatal(err)
	}
	if got["accent"] != "#7847eb" || got["accent_fg"] != "#ffffff" || got["selection"] != "#39285d" {
		t.Errorf("got %v", got)
	}
	if r, _ := Contrast(got["accent_deep"], "#21143d"); r > 1.1 {
		t.Errorf("accent_deep %s is far from #21143d (%.2f)", got["accent_deep"], r)
	}
}

func bundled(t *testing.T) []*Theme {
	t.Helper()
	var out []*Theme
	for _, id := range List([]string{"../../../themes"}) {
		th, err := Load([]string{"../../../themes"}, id)
		if err != nil {
			t.Fatal(err)
		}
		out = append(out, th)
	}
	if len(out) == 0 {
		t.Fatal("no themes found")
	}
	return out
}

func TestBundledThemesPassContrast(t *testing.T) {
	for _, th := range bundled(t) {
		for _, p := range th.Contrast() {
			if !p.OK() {
				t.Errorf("%s: %s on %s = %.3f < %.1f", th.ID, p.Fg, p.Bg, p.Ratio, p.Min)
			}
		}
	}
}

// Any accent in any bundled theme keeps every pair the theme promises:
// its suggestions, and a grid over the whole color cube.
func TestAnyAccentKeepsEveryPair(t *testing.T) {
	var grid []string
	for r := 0; r < 256; r += 51 {
		for g := 0; g < 256; g += 51 {
			for b := 0; b < 256; b += 51 {
				grid = append(grid, fmt.Sprintf("#%02x%02x%02x", r, g, b))
			}
		}
	}
	for _, base := range bundled(t) {
		for _, a := range append(append([]string{}, base.Meta.Accents...), grid...) {
			th, _ := Load([]string{"../../../themes"}, base.ID)
			if err := th.WithAccent(a); err != nil {
				t.Fatal(err)
			}
			var bad []string
			for _, p := range th.Contrast() {
				if !p.OK() {
					bad = append(bad, fmt.Sprintf("%s/%s %.2f<%.1f", p.Fg, p.Bg, p.Ratio, p.Min))
				}
			}
			if len(bad) > 0 {
				t.Errorf("%s with accent %s: %s", base.ID, a, strings.Join(bad, ", "))
			}
		}
	}
}

func TestOwnAccentKeepsHandPickedTokens(t *testing.T) {
	th, _ := Load([]string{"../../../themes"}, "phosphor")
	th.WithAccent("#7847EB")
	if th.Colors["accent_text"] != "#a68af9" {
		t.Errorf("the theme's own accent must keep its tokens, got %s", th.Colors["accent_text"])
	}
}
