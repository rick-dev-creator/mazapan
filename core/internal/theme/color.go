package theme

import (
	"fmt"
	"math"
	"strconv"
	"strings"
)

// Colors as sRGB channels in 0..1.
type rgb struct{ r, g, b float64 }

func parseHex(s string) (rgb, error) {
	h := strings.TrimPrefix(s, "#")
	if len(h) != 6 && len(h) != 8 {
		return rgb{}, fmt.Errorf("bad color %q", s)
	}
	v, err := strconv.ParseUint(h[:6], 16, 32)
	if err != nil {
		return rgb{}, fmt.Errorf("bad color %q", s)
	}
	return rgb{float64(v>>16&0xff) / 255, float64(v>>8&0xff) / 255, float64(v&0xff) / 255}, nil
}

func (c rgb) hex() string {
	ch := func(x float64) int { return int(math.Round(math.Max(0, math.Min(1, x)) * 255)) }
	return fmt.Sprintf("#%02x%02x%02x", ch(c.r), ch(c.g), ch(c.b))
}

func linear(x float64) float64 {
	if x <= 0.04045 {
		return x / 12.92
	}
	return math.Pow((x+0.055)/1.055, 2.4)
}

func gamma(x float64) float64 {
	if x <= 0.0031308 {
		return x * 12.92
	}
	return 1.055*math.Pow(x, 1/2.4) - 0.055
}

// luminance is WCAG's relative luminance.
func (c rgb) luminance() float64 {
	return 0.2126*linear(c.r) + 0.7152*linear(c.g) + 0.0722*linear(c.b)
}

func ratio(a, b rgb) float64 {
	la, lb := a.luminance(), b.luminance()
	if la < lb {
		la, lb = lb, la
	}
	return (la + 0.05) / (lb + 0.05)
}

// Contrast is the WCAG contrast ratio of two #rrggbb colors (1 to 21).
func Contrast(a, b string) (float64, error) {
	ca, err := parseHex(a)
	if err != nil {
		return 0, err
	}
	cb, err := parseHex(b)
	if err != nil {
		return 0, err
	}
	return ratio(ca, cb), nil
}

// mix goes from a (t = 0) to b (t = 1), in sRGB, like painting one over the
// other with opacity t.
func mix(a, b rgb, t float64) rgb {
	return rgb{a.r + (b.r-a.r)*t, a.g + (b.g-a.g)*t, a.b + (b.b-a.b)*t}
}

// OKLab: lightness that looks even, so a color can get lighter or darker
// without drifting in hue.
type lab struct{ l, a, b float64 }

func (c rgb) oklab() lab {
	r, g, b := linear(c.r), linear(c.g), linear(c.b)
	l := math.Cbrt(0.4122214708*r + 0.5363325363*g + 0.0514459929*b)
	m := math.Cbrt(0.2119034982*r + 0.6806995451*g + 0.1073969566*b)
	s := math.Cbrt(0.0883024619*r + 0.2817188376*g + 0.6299787005*b)
	return lab{
		0.2104542553*l + 0.7936177850*m - 0.0040720468*s,
		1.9779984951*l - 2.4285922050*m + 0.4505937099*s,
		0.0259040371*l + 0.7827717662*m - 0.8086757660*s,
	}
}

func (c lab) rgb() (rgb, bool) {
	l := math.Pow(c.l+0.3963377774*c.a+0.2158037573*c.b, 3)
	m := math.Pow(c.l-0.1055613458*c.a-0.0638541728*c.b, 3)
	s := math.Pow(c.l-0.0894841775*c.a-1.2914855480*c.b, 3)
	r := 4.0767416621*l - 3.3077115913*m + 0.2309699292*s
	g := -1.2684380046*l + 2.6097574011*m - 0.3413193965*s
	b := -0.0041960863*l - 0.7034186147*m + 1.7076147010*s
	in := func(x float64) bool { return x >= -1e-4 && x <= 1+1e-4 }
	return rgb{gamma(math.Max(0, r)), gamma(math.Max(0, g)), gamma(math.Max(0, b))}, in(r) && in(g) && in(b)
}

// withLightness keeps c's hue at lightness l, giving up chroma until the
// color exists on screen.
func withLightness(c rgb, l float64) rgb {
	o := c.oklab()
	for k := 1.0; k >= 0; k -= 0.02 {
		if out, ok := (lab{l, o.a * k, o.b * k}).rgb(); ok {
			return out
		}
	}
	out, _ := (lab{l, 0, 0}).rgb()
	return out
}

// rounded is c as it will be written (#rrggbb): contrast is promised for
// that, not for the exact value.
func rounded(c rgb) rgb {
	out, _ := parseHex(c.hex())
	return out
}

// against moves c's lightness, as little as it takes, until it contrasts
// at least min with every one of bgs: lighter in a dark theme, darker in a
// light one.
func against(c rgb, bgs []rgb, min float64, light bool) rgb {
	ok := func(x rgb) bool {
		for _, b := range bgs {
			if ratio(x, rounded(b)) < min {
				return false
			}
		}
		return true
	}
	c = rounded(c)
	if ok(c) {
		return c
	}
	step := 0.01
	if light {
		step = -0.01
	}
	l := c.oklab().l
	for ; l >= 0 && l <= 1; l += step {
		if out := rounded(withLightness(c, l)); ok(out) {
			return out
		}
	}
	return rounded(withLightness(c, math.Max(0, math.Min(1, l))))
}

// AccentTokens derives every accent token from one color, for a theme with
// these colors, so that every pair in Contrast() holds:
//   - accent: fills and borders, at least 3:1 on the background;
//   - accent_fg: text on it, black or white (one of them always has 4.5:1);
//   - accent_deep: large fills, near-black in the accent's hue (a pale tint
//     of it in a light theme);
//   - accent_text: the accent as text, 4.5:1 on the background, on cards
//     and on accent_deep;
//   - selection: the accent over the background, as strong as it can be
//     with text on it still readable.
func AccentTokens(accent string, colors map[string]string, mode string) (map[string]string, error) {
	c, err := parseHex(accent)
	if err != nil {
		return nil, err
	}
	var bg, card, fg rgb
	for name, dst := range map[string]*rgb{"bg": &bg, "surface_raised": &card, "fg": &fg} {
		if *dst, err = parseHex(colors[name]); err != nil {
			return nil, fmt.Errorf("%s: %w", name, err)
		}
	}
	light := mode == "light"
	fill := against(c, []rgb{bg}, 3, light)
	onFill := rgb{1, 1, 1}
	if ratio(rgb{}, fill) > ratio(onFill, fill) {
		onFill = rgb{}
	}
	deep := rounded(mix(rgb{}, fill, 0.27))
	if light {
		deep = rounded(mix(rgb{1, 1, 1}, fill, 0.14))
	}
	text := against(fill, []rgb{bg, card, deep}, 4.5, light)
	selection := rounded(mix(bg, fill, 0.33))
	for t := 0.31; ratio(fg, selection) < 4.5 && t > 0.05; t -= 0.02 {
		selection = rounded(mix(bg, fill, t))
	}
	return map[string]string{
		"accent":      fill.hex(),
		"accent_fg":   onFill.hex(),
		"accent_text": text.hex(),
		"accent_deep": deep.hex(),
		"selection":   selection.hex(),
	}, nil
}

// WithAccent replaces the theme's accent tokens with ones derived from
// accent (see AccentTokens). The theme's own accent keeps the theme's
// hand-picked tokens.
func (t *Theme) WithAccent(accent string) error {
	if strings.EqualFold(accent, t.Colors["accent"]) {
		return nil
	}
	tokens, err := AccentTokens(accent, t.Colors, t.Meta.Mode)
	if err != nil {
		return err
	}
	for k, v := range tokens {
		t.Colors[k] = v
	}
	return nil
}

// Pair is one contrast the theme promises.
type Pair struct {
	Fg, Bg string  // token names
	Min    float64 // WCAG: 4.5 for text, 3 for large text, fills and borders
	Ratio  float64
}

func (p Pair) OK() bool { return p.Ratio >= p.Min }

// Pairs are the combinations plugins use: text colors on the surfaces
// they sit on, and the accent's own.
var pairs = []Pair{
	{"fg", "bg", 4.5, 0}, {"fg", "surface", 4.5, 0}, {"fg", "surface_raised", 4.5, 0},
	{"fg", "selection", 4.5, 0},
	{"fg_muted", "bg", 4.5, 0}, {"fg_muted", "surface_raised", 4.5, 0},
	{"fg_subtle", "bg", 3, 0},
	{"accent_text", "bg", 4.5, 0}, {"accent_text", "surface_raised", 4.5, 0},
	{"accent_text", "accent_deep", 4.5, 0},
	{"accent_fg", "accent", 4.5, 0},
	{"accent", "bg", 3, 0}, {"border", "bg", 1.3, 0},
	{"success", "bg", 4.5, 0}, {"warning", "bg", 4.5, 0},
	{"danger", "bg", 4.5, 0}, {"info", "bg", 4.5, 0},
}

// Contrast checks every pair plugins rely on; the ones below their minimum
// are the theme's problems.
func (t *Theme) Contrast() []Pair {
	var out []Pair
	for _, p := range pairs {
		r, err := Contrast(t.Colors[p.Fg], t.Colors[p.Bg])
		if err != nil {
			continue
		}
		p.Ratio = r // exact: 2.9997 is not 3
		out = append(out, p)
	}
	return out
}
