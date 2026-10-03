using Mazapan.Themes;

namespace Mazapan.Tests.Foundation;

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

    // Two of Omarchy's palettes as its colors.toml has them: a dark one and a light one.
    static readonly Dictionary<string, string> Catppuccin = new()
    {
        ["mode"] = "dark", ["accent"] = "#89b4fa", ["selection"] = "#45475a", ["muted"] = "#585b70",
        ["background"] = "#1e1e2e", ["dark_background"] = "#161622", ["darker_background"] = "#101019", ["lighter_background"] = "#313244",
        ["foreground"] = "#cdd6f4", ["dark_foreground"] = "#6c7086", ["light_foreground"] = "#bac2de", ["bright_foreground"] = "#cdd6f4",
        ["red"] = "#f38ba8", ["yellow"] = "#f9e2af", ["orange"] = "#f6b6ab", ["green"] = "#a6e3a1", ["cyan"] = "#94e2d5", ["blue"] = "#89b4fa", ["magenta"] = "#f5c2e7",
        ["bright_red"] = "#f38ba8", ["bright_yellow"] = "#f9e2af", ["bright_green"] = "#a6e3a1", ["bright_cyan"] = "#94e2d5", ["bright_blue"] = "#89b4fa", ["bright_magenta"] = "#f5c2e7",
    };
    static readonly Dictionary<string, string> Flexoki = new()
    {
        ["mode"] = "light", ["accent"] = "#205EA6", ["selection"] = "#CECDC3", ["muted"] = "#B7B5AC",
        ["background"] = "#FFFCF0", ["dark_background"] = "#f2efe4", ["darker_background"] = "#e5e2d8", ["lighter_background"] = "#E6E4D9",
        ["foreground"] = "#100F0F", ["dark_foreground"] = "#878580", ["light_foreground"] = "#403E3C", ["bright_foreground"] = "#100F0F",
        ["red"] = "#D14D41", ["yellow"] = "#D0A215", ["orange"] = "#d0772b", ["green"] = "#879A39", ["cyan"] = "#3AA99F", ["blue"] = "#205EA6", ["magenta"] = "#CE5D97",
        ["bright_red"] = "#D14D41", ["bright_yellow"] = "#D0A215", ["bright_green"] = "#879A39", ["bright_cyan"] = "#3AA99F", ["bright_blue"] = "#4385BE", ["bright_magenta"] = "#CE5D97",
    };

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void AnOmarchyThemeKeepsItsColorsAndEveryContrast(string which)
    {
        var palette = which == "dark" ? Catppuccin : Flexoki;
        var (colors, ansi, accents, mode) = Omarchy.Convert(palette);
        Assert.Equal(which, mode);
        Assert.Equal(palette["background"].ToLowerInvariant(), colors["bg"]);
        Assert.Equal(palette["foreground"].ToLowerInvariant(), colors["fg"]);
        // The terminal as Omarchy draws it.
        Assert.Equal(palette["background"].ToLowerInvariant(), ansi["black"]);
        Assert.Equal(palette["muted"].ToLowerInvariant(), ansi["bright_black"]);
        Assert.Equal(palette["red"].ToLowerInvariant(), ansi["red"]);
        Assert.Equal(colors["accent"], accents[0]);
        // As a theme: loads, and every pair the desktop promises holds.
        using var d = new TempDir();
        var like = Theme.Load([Repo.Themes], Theme.List([Repo.Themes])[0]);
        Directory.CreateDirectory(Path.Join(d.Path, "omarchy-x"));
        File.WriteAllText(Path.Join(d.Path, "omarchy-x", "theme.toml"), FromImage.Toml("X", mode, "Omarchy's X", "grid", colors, ansi, accents, like));
        var t = Theme.Load([d.Path], "omarchy-x");
        foreach (var p in t.Contrast()) Assert.True(p.OK, $"{which}: {p.Fg} on {p.Bg} = {p.Ratio:F3} < {p.Min:F1}");
    }

    [Fact]
    public void OmarchyThemesGetAnIdOfTheirOwn()
    {
        Assert.Equal("omarchy-tokyo-night", Omarchy.Id("tokyo-night"));
        Assert.Equal("omarchy-dune", Omarchy.Id("omarchy-dune-theme"));
        Assert.Equal("Tokyo Night", Omarchy.Readable("tokyo-night"));
    }

    [Fact]
    public void AnOlderOmarchyThemeIsReadFromItsAlacrittyColors()
    {
        using var d = new TempDir();
        File.WriteAllText(Path.Join(d.Path, "alacritty.toml"), """
            [colors.primary]
            background = "0x1a1b26"
            foreground = "#a9b1d6"
            [colors.normal]
            red = "#f7768e"
            blue = "#7aa2f7"
            [colors.bright]
            black = "#444b6a"
            """);
        var p = Omarchy.Palette(d.Path)!;
        Assert.Equal("#1a1b26", p["background"]);
        Assert.Equal("#444b6a", p["muted"]);
        var (colors, _, _, mode) = Omarchy.Convert(p);
        Assert.Equal("dark", mode);
        Assert.Equal("#1a1b26", colors["bg"]);
    }
}
