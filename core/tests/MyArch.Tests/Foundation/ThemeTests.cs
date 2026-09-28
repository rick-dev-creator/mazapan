using MyArch.Themes;

namespace MyArch.Tests.Foundation;

public class ThemeTests
{
    [Fact]
    public void AccentTokensMatchPhosphor()
    {
        var colors = new Dictionary<string, string> { ["bg"] = "#1a1917", ["surface_raised"] = "#2d2b28", ["fg"] = "#dcd3bf" };
        // Phosphor's hand-picked accent tokens, from its accent and colors.
        var got = ColorMath.AccentTokens("#7847eb", colors, "dark");
        Assert.Equal("#7847eb", got["accent"]);
        Assert.Equal("#ffffff", got["accent_fg"]);
        Assert.Equal("#39285d", got["selection"]);
        Assert.True(ColorMath.Contrast(got["accent_deep"], "#21143d") <= 1.1, $"accent_deep {got["accent_deep"]} is far from #21143d");
    }

    static List<Theme> Bundled()
    {
        var out_ = Theme.List([Repo.Themes]).Select(id => Theme.Load([Repo.Themes], id)).ToList();
        Assert.NotEmpty(out_);
        return out_;
    }

    [Fact]
    public void BundledThemesPassContrast()
    {
        foreach (var th in Bundled())
            foreach (var p in th.Contrast())
                Assert.True(p.OK, $"{th.Id}: {p.Fg} on {p.Bg} = {p.Ratio:F3} < {p.Min:F1}");
    }

    /// <summary>
    /// Any accent in any bundled theme keeps every pair the theme promises:
    /// its suggestions, and a grid over the whole color cube.
    /// </summary>
    [Fact]
    public void AnyAccentKeepsEveryPair()
    {
        var grid = new List<string>();
        for (var r = 0; r < 256; r += 51)
            for (var g = 0; g < 256; g += 51)
                for (var b = 0; b < 256; b += 51)
                    grid.Add($"#{r:x2}{g:x2}{b:x2}");
        var bad = new List<string>();
        foreach (var baseTheme in Bundled())
            foreach (var a in baseTheme.Meta.Accents.Concat(grid))
            {
                var th = Theme.Load([Repo.Themes], baseTheme.Id);
                th.WithAccent(a);
                foreach (var p in th.Contrast())
                    if (!p.OK) bad.Add($"{baseTheme.Id} with accent {a}: {p.Fg}/{p.Bg} {p.Ratio:F2}<{p.Min:F1}");
            }
        Assert.Empty(bad);
    }

    [Fact]
    public void OwnAccentKeepsHandPickedTokens()
    {
        var th = Theme.Load([Repo.Themes], "phosphor");
        th.WithAccent("#7847EB");
        Assert.Equal("#a68af9", th.Colors["accent_text"]);
    }
}
