using Mazapan.Applying;
using Mazapan.Tests.Install;

namespace Mazapan.Tests.Foundation;

public class IconTests
{
    [Fact]
    public void TheShellsIconIsTheIcon() =>
        Assert.Equal(File.ReadAllText(Path.Join(Repo.Root, "assets", "mazapan.svg")),
            File.ReadAllText(Path.Join(Repo.Root, "plugins", "shell-bar", "mazapan.svg.tmpl")));
}

[Collection("InstallHome")]
public class PruneTests
{
    [Fact]
    public void RemovedFilesTakeTheirEmptyFoldersWithThem()
    {
        using var s = new Sandbox();
        string H(string p) => Path.Join(s.Home, p);
        var widget = H(".config/quickshell/mazapan/widgets/right/X.qml");
        var kept = H(".config/quickshell/mazapan/shell.qml");
        var foot = H(".config/foot/foot.ini");
        foreach (var f in new[] { widget, kept, foot })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            File.WriteAllText(f, "gen");
        }
        var owned = new Owned { [widget] = Apply.Sum("gen"), [foot] = Apply.Sum("gen") };
        var (ch, orphans) = Apply.Plan([], owned);
        Apply.Execute(ch, orphans, owned, false);
        Assert.False(Directory.Exists(H(".config/quickshell/mazapan/widgets")));
        Assert.True(File.Exists(kept));                           // up to a folder that isn't empty
        Assert.True(Directory.Exists(H(".config/foot")));         // an app's: stays, even empty
    }
}
