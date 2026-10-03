using Mazapan.Store;
using Mazapan.Util;

namespace Mazapan.Tests.Store;

public class AppCatalogTests
{
    static string Repo => Mazapan.Tests.Foundation.Repo.Root;

    [Fact]
    public void TheCatalogLoads()
    {
        var (profiles, apps) = AppCatalog.Load(Path.Join(Repo, "catalog", "apps.toml"));
        Assert.Contains(profiles, p => p.Id == "basic" && p.Basic);
        Assert.Contains(profiles, p => p.Id == "gaming");
        Assert.All(apps, a => Assert.NotEqual("", a.In("es").Description));
        // Every profile's apps are in it (Parse checks), and every app has one way in.
        Assert.All(apps, a => Assert.Contains(a.Kind, new[] { "pacman", "flatpak", "webapp", "plugin", "vendor" }));
    }

    [Theory]
    [InlineData("pacman = [\"--root=/\"]")]
    [InlineData("pacman = [\"ok\"]\nflatpak = \"org.example.App\"")]
    [InlineData("webapp = \"http://example.com\"")]
    [InlineData("webapp = \"https://a.com/ x\"")]
    [InlineData("flatpak = \"notreverse\"")]
    [InlineData("pacman = [\"ok\"]\ndesktop = \"../x.desktop\"")]
    [InlineData("vendor = \"jetbrains:RD\"\ndescription = \"a\\nExec=sh\"")]
    public void WhatWouldReachACommandIsChecked(string source)
    {
        var text = $"[[app]]\nid = \"x\"\nname = \"X\"\ncategory = \"utilities\"\n{source}\n";
        Assert.Throws<MazapanException>(() => AppCatalog.Parse(text, "t"));
    }

    [Fact]
    public void AProfileNamesOnlyAppsThatAreThere()
    {
        var text = "[[app]]\nid = \"a\"\nname = \"A\"\ncategory = \"utilities\"\npacman = [\"a\"]\n\n[[profile]]\nid = \"p\"\nname = \"P\"\napps = [\"a\", \"b\"]\n";
        Assert.Throws<MazapanException>(() => AppCatalog.Parse(text, "t"));
    }

    [Fact]
    public void OthersCatalogsAddToMazapans()
    {
        using var d = new Mazapan.Tests.Foundation.TempDir();
        var own = d.Write("own.toml", "[[app]]\nid = \"a\"\nname = \"A\"\ncategory = \"utilities\"\npacman = [\"a\"]\n");
        // Its own b, mazapan's a again (the first one's stays), and a profile of both.
        var other = d.Write("other.toml", "[[app]]\nid = \"a\"\nname = \"Not A\"\ncategory = \"games\"\npacman = [\"evil\"]\n\n" +
            "[[app]]\nid = \"b\"\nname = \"B\"\ncategory = \"games\"\nflatpak = \"org.example.B\"\n\n" +
            "[[profile]]\nid = \"fun\"\nname = \"Fun\"\napps = [\"a\", \"b\"]\n");
        var basic = d.Write("basic.toml", "[[profile]]\nid = \"mine\"\nname = \"Mine\"\nbasic = true\napps = [\"a\"]\n");
        var (profiles, apps, problems) = AppCatalog.LoadAll(own, [other, basic, Path.Join(d.Path, "missing.toml"), "http://example.com/apps.toml"]);
        Assert.Equal(["a", "b"], apps.Select(a => a.Id));
        Assert.Equal(["a"], apps[0].Pacman);
        Assert.Equal("", apps[0].From);
        Assert.Equal(other, apps[1].From);
        Assert.Equal(["fun"], profiles.Select(p => p.Id));
        // Only mazapan's has the basic profile; the rest is said, not fatal.
        Assert.Equal(3, problems.Count);
        Assert.Contains(problems, p => p.Contains("basic"));
        Assert.Contains(problems, p => p.Contains("not https"));
    }
}

public class AppsLedgerTests
{
    [Fact]
    public void OnlyWhatTheAppsMenuPutThereIsOurs()
    {
        var txs = new[]
        {
            new AppsTx { Id = "1", Action = "install", Packages = ["lazygit", "zoxide"], Flatpaks = ["com.valvesoftware.Steam"], Webapps = ["https://web.whatsapp.com/"] },
            new AppsTx { Id = "2", Action = "remove", Packages = ["zoxide"] },
            new AppsTx { Id = "3", Action = "install", Packages = ["gimp"] },
        };
        var (pk, fp, wa) = AppsLedger.Owned(txs);
        Assert.Equal(["gimp", "lazygit"], pk.Order());
        Assert.Contains("com.valvesoftware.Steam", fp);
        Assert.Contains("https://web.whatsapp.com", wa); // without the trailing /
        Assert.Contains("git", AppsLedger.Protected);
    }

    [Fact]
    public void ARecordNamingWhatCantBeANameIsLeftOut()
    {
        using var d = new Mazapan.Tests.Applying.TempDir();
        File.WriteAllText(d["a.json"], "{\"id\":\"a\",\"action\":\"install\",\"time\":\"2026-09-29T10:00:00+00:00\",\"packages\":[\"--root=/\"]}");
        File.WriteAllText(d["b.json"], "{\"id\":\"b\",\"action\":\"install\",\"time\":\"2026-09-29T10:00:00+00:00\",\"packages\":[\"gimp\"]}");
        var list = AppsLedger.List(d.Path);
        Assert.Single(list);
        Assert.Equal("b", list[0].Id);
    }

    [Fact]
    public void AnIdIsAnAppsOrAProfiles()
    {
        var text = "[[app]]\nid = \"games\"\nname = \"G\"\ncategory = \"games\"\npacman = [\"g\"]\n\n[[profile]]\nid = \"games\"\nname = \"Games\"\napps = [\"games\"]\n";
        Assert.Throws<MazapanException>(() => AppCatalog.Parse(text, "t"));
    }
}
