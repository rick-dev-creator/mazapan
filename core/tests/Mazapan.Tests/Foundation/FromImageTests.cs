using System.Text;
using Mazapan.Themes;
using Mazapan.Util;

namespace Mazapan.Tests.Foundation;

public class FromImageTests
{
    /// <summary>A picture made of colors, each in its share of pixels.</summary>
    static List<Rgb> Picture(params (string Hex, int Count)[] parts)
    {
        var out_ = new List<Rgb>();
        foreach (var (hex, n) in parts)
            for (var i = 0; i < n; i++) out_.Add(Rgb.Parse(hex));
        return out_;
    }

    /// <summary>The theme a palette makes, written and loaded as any theme.</summary>
    static Theme Made(TempDir d, List<Rgb> pixels, string mode = "")
    {
        var palette = FromImage.Palette(pixels);
        if (mode == "") mode = FromImage.ModeOf(palette);
        var (colors, ansi, accents) = FromImage.Build(palette, mode);
        var like = Theme.Load([Path.Join(Repo.Root, "themes")], "gruvbox");
        d.Write("themes/made/theme.toml", FromImage.Toml("Made", mode, "test", "plain", colors, ansi, accents, like));
        return Theme.Load([Path.Join(d.Path, "themes")], "made");
    }

    [Fact]
    public void ADarkPictureMakesADarkThemeWithItsBrightColorAsTheAccent()
    {
        using var d = new TempDir();
        // A sunset: mostly night blue, some orange, a little pink.
        var t = Made(d, Picture(("#141a33", 600), ("#1f2a4d", 200), ("#f28a30", 150), ("#e0608f", 50)));
        Assert.Equal("dark", t.Meta.Mode);
        Assert.DoesNotContain(t.Contrast(), p => !p.OK);
        // The accent is the orange's hue (OKLab a > 0, b > 0: warm).
        var accent = Rgb.Parse(t.Colors["accent"]).OkLab();
        Assert.True(accent.A > 0.02 && accent.B > 0.05, t.Colors["accent"]);
        // The surfaces barely tinted, in the picture's blue.
        var bg = Rgb.Parse(t.Colors["bg"]).OkLab();
        Assert.True(bg.B < 0 && Math.Sqrt(bg.A * bg.A + bg.B * bg.B) <= 0.031, t.Colors["bg"]);
        Assert.NotEmpty(t.Meta.Accents); // the pink, as a suggestion
    }

    [Fact]
    public void TheSubjectIsTheAccentNotTheBackdrop()
    {
        using var d = new TempDir();
        // A jellyfish: three shades of orange (the subject) on a lot of blue.
        var t = Made(d, Picture(("#065cb7", 450), ("#063e91", 300), ("#ee6607", 71), ("#c3510f", 53), ("#f5910e", 51), ("#473e5f", 29)));
        var accent = Rgb.Parse(t.Colors["accent"]).OkLab();
        Assert.True(accent.A > 0.03 && accent.B > 0.05, "orange, not blue: " + t.Colors["accent"]);
    }

    [Fact]
    public void ALightPictureMakesALightThemeThatHoldsEveryContrast()
    {
        using var d = new TempDir();
        var t = Made(d, Picture(("#f4efe6", 700), ("#dcd3c3", 150), ("#3a6fb0", 100), ("#2f4a36", 50)));
        Assert.Equal("light", t.Meta.Mode);
        Assert.DoesNotContain(t.Contrast(), p => !p.OK);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void AGreyPictureStillGetsAnAccentAndEitherMode(string mode)
    {
        using var d = new TempDir();
        var t = Made(d, Picture(("#808080", 500), ("#606060", 300), ("#a0a0a0", 200)), mode);
        Assert.Equal(mode, t.Meta.Mode);
        Assert.DoesNotContain(t.Contrast(), p => !p.OK);
        Assert.True(Math.Sqrt(Math.Pow(Rgb.Parse(t.Colors["accent"]).OkLab().A, 2) + Math.Pow(Rgb.Parse(t.Colors["accent"]).OkLab().B, 2)) > 0.05);
    }

    [Fact]
    public void OneColorIsEnough()
    {
        using var d = new TempDir();
        var t = Made(d, Picture(("#c0392b", 10)));
        Assert.DoesNotContain(t.Contrast(), p => !p.OK);
    }

    [Fact]
    public void TheSamePictureMakesTheSamePalette()
    {
        var pic = Picture(("#141a33", 600), ("#f28a30", 150), ("#e0608f", 50), ("#88c0d0", 40));
        Assert.Equal(FromImage.Palette(pic), FromImage.Palette(pic));
        var shares = FromImage.Palette(pic).Select(s => s.Share).ToList();
        Assert.Equal(1.0, shares.Sum(), 6);
        Assert.Equal(shares.OrderByDescending(x => x), shares);
    }

    [Fact]
    public void PpmIsRead()
    {
        var head = Encoding.ASCII.GetBytes("P6\n# made by a test\n2 1\n255\n");
        var ppm = head.Concat(new byte[] { 255, 0, 0, 0, 0, 255 }).ToArray();
        Assert.Equal([Rgb.Parse("#ff0000"), Rgb.Parse("#0000ff")], FromImage.Ppm(ppm));
        Assert.Throws<MazapanException>(() => FromImage.Ppm(Encoding.ASCII.GetBytes("P3\n1 1\n255\n0 0 0")));
        Assert.Throws<MazapanException>(() => FromImage.Ppm(head.Concat(new byte[] { 1, 2 }).ToArray()));
    }
}
