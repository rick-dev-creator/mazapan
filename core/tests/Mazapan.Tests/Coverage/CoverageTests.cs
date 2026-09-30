using System.Text;
using Mazapan.Coverage;

namespace Mazapan.Tests.Coverage;

public sealed class CoverageTests : IDisposable
{
    readonly List<string> temps = [];

    string TempDir()
    {
        var d = Directory.CreateTempSubdirectory("mazapan-test-").FullName;
        temps.Add(d);
        return d;
    }

    public void Dispose()
    {
        foreach (var d in temps) Directory.Delete(d, true);
    }

    static void Write(string dir, string name, string body)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Join(dir, name), body);
    }

    static void WriteExecutable(string path, string body)
    {
        File.WriteAllText(path, body);
        File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
    }

    [Fact]
    public void Scan()
    {
        string user = TempDir(), system = TempDir();
        Write(system, "term.desktop", "[Desktop Entry]\nType=Application\nName=Term\nName[es]=Terminal\nExec=env FOO=1 \"myterm\" %U\n");
        Write(system, "hidden.desktop", "[Desktop Entry]\nType=Application\nName=Hidden\nExec=x\nNoDisplay=true\n");
        Write(system, "link.desktop", "[Desktop Entry]\nType=Link\nName=Link\nURL=https://x\n");
        Write(system, "tui.desktop", "[Desktop Entry]\nType=Application\nName=Top\nExec=top\nTerminal=true\n[Desktop Action new]\nExec=other\n");
        // The person's own copy wins over the system's.
        Write(user, "tui.desktop", "[Desktop Entry]\nType=Application\nName=My top\nExec=htop\nTerminal=true\n");
        // Not installed anymore, or for another desktop: not shown.
        Write(system, "gone.desktop", "[Desktop Entry]\nType=Application\nName=Gone\nExec=gone\nTryExec=/nonexistent/gone\n");
        Write(system, "gnome.desktop", "[Desktop Entry]\nType=Application\nName=G\nExec=g\nOnlyShowIn=GNOME;\n");
        // In a subdirectory: its id carries the directory.
        Write(Path.Join(system, "vendor"), "app.desktop", "[Desktop Entry]\nType=Application\nName=V\nExec=v\n");
        var saved = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        List<App> apps;
        try
        {
            Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", "Hyprland");
            apps = Apps.Scan([user, system], "es_MX");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", saved);
        }
        Assert.Equal(3, apps.Count);
        var byId = apps.ToDictionary(a => a.Id);
        Assert.True(byId["term"] is { Name: "Terminal", Exec: "myterm" });
        Assert.True(byId["tui"] is { Name: "My top", Exec: "htop", Terminal: true });
        Assert.True(byId.ContainsKey("vendor-app"), "vendor/app.desktop should be vendor-app");
    }

    [Fact]
    public void SymlinkedFolderIsNotWalked()
    {
        // filepath.WalkDir doesn't follow symlinks to folders, not even the root.
        string real = TempDir(), links = TempDir();
        Write(real, "a.desktop", "[Desktop Entry]\nType=Application\nName=A\nExec=a\n");
        Directory.CreateSymbolicLink(Path.Join(links, "sub"), real);
        Assert.Empty(Apps.Scan([Path.Join(links, "sub")], "en"));
        Assert.Empty(Apps.Scan([links], "en"));
        Assert.Single(Apps.Scan([real], "en"));
    }

    [Theory]
    [InlineData("firefox %u", "firefox", false, "")]
    [InlineData("env -u FOO BAR=1 \"/opt/My App/app\" --x", "/opt/My App/app", false, "")]
    [InlineData("xdg-terminal-exec --app-id=TUI.float -e bash -c \"dua i /\"", "dua", true, "")]
    [InlineData("foot -e htop", "htop", true, "")]
    [InlineData("sh -c \"exec obsidian --foo\"", "obsidian", false, "")]
    [InlineData("/usr/bin/flatpak run --branch=stable org.gimp.GIMP @@u %U @@", "/usr/bin/flatpak", false, "org.gimp.GIMP")]
    public void Program(string line, string prog, bool terminal, string flatpak) =>
        Assert.Equal((prog, terminal, flatpak), ExecLine.Program(line));

    [Theory]
    [InlineData("electron", "gtk3", "electron32", "nss")]
    [InlineData("chromium", "gtk3", "nss", "alsa-lib")]
    [InlineData("other", "gtk3", "webkit2gtk-4.1")]
    [InlineData("gtk4", "gtk4>=4.14", "glib2")]
    [InlineData("gtk3", "gtk3", "gtk4-layer-shell")]
    [InlineData("", "gtk4-layer-shell", "wayland")]
    public void FromDeps(string want, params string[] deps) => Assert.Equal(want, Detector.FromDeps(deps));

    [Fact]
    public void Report()
    {
        var dir = TempDir();
        Write(dir, "yt.desktop", "[Desktop Entry]\nType=Application\nName=YouTube\nExec=omarchy-launch-webapp https://youtube.com/\n");
        Write(dir, "a.desktop", "[Desktop Entry]\nType=Application\nName=A tui\nExec=top\nTerminal=true\n");
        Write(dir, "kitty.desktop", "[Desktop Entry]\nType=Application\nName=kitty\nExec=kitty\n");
        Write(dir, "foot.desktop", "[Desktop Entry]\nType=Application\nName=Foot\nExec=foot\n");
        var apps = Apps.Report([dir], "en", [new Covers("theme-foot", ["foot"], [Toolkit.Terminal])]);
        Assert.Equal(4, apps.Count);
        Assert.True(apps[0] is { Name: "kitty", Plugin: "" } && apps[1].Toolkit == Toolkit.Web, "uncovered first");
        foreach (var a in apps[2..]) Assert.Equal("theme-foot", a.Plugin);
    }

    [Fact]
    public void JsonEscapesAsGo()
    {
        const string want = "[{\"id\":\"a\\u0026b\",\"name\":\"\\u003cx\\u003e \\\"q\\\" é\",\"exec\":\"e\",\"toolkit\":\"t\"}]";
        Assert.Equal(want, Apps.Json([new App { Id = "a&b", Name = "<x> \"q\" é", Exec = "e", Toolkit = "t" }]));
    }

    [Fact]
    public void JsonOfNoneIsAnEmptyList() => Assert.Equal("[]", Apps.Json([]));

    [Fact]
    public void ToolkitFromScript()
    {
        var dir = TempDir();
        WriteExecutable(Path.Join(dir, "target"), "#!/usr/bin/python\nfrom gi.repository import Gtk\n");
        var cases = new Dictionary<string, (string Body, string Want)>
        {
            ["py-gtk4"] = ("#!/usr/bin/python\ngi.require_version('Gtk', '4.0')\n", Toolkit.GTK4),
            ["py-gtk3"] = ("#!/usr/bin/python\nfrom gi.repository import Gtk\n", Toolkit.GTK3),
            ["py-qt6"] = ("#!/usr/bin/python\nfrom PyQt6 import QtWidgets\n", Toolkit.Qt6),
            ["py-qt5"] = ("#!/usr/bin/python\nimport PySide2\n", Toolkit.Qt5),
            ["el"] = ("#!/bin/sh\nexec electron32 /usr/lib/app.asar \"$@\"\n", Toolkit.Electron),
            ["prose"] = ("#!/bin/sh\n# STMicroelectronics chips; Adwaita; Gtk immodule\necho hi\n", ""),
            ["gtkish"] = ("#!/usr/bin/python\nfrom gi.repository import GtkSource\n", ""), // \b: GtkSource isn't Gtk
            ["wrapper"] = ("#!/bin/bash\nexport X=1\nexec " + Path.Join(dir, "target") + " \"$@\"\n", Toolkit.GTK3),
            ["web"] = ("#!/bin/sh\nexec chromium --app=https://x\n", Toolkit.Web),
            ["plain"] = ("not a script", ""),
        };
        foreach (var (name, (body, want)) in cases)
        {
            var p = Path.Join(dir, name);
            WriteExecutable(p, body);
            Assert.True(want == Detector.FromBinary(p, 0), $"{name}: got {Detector.FromBinary(p, 0)}, want {want}");
        }
    }

    [Fact]
    public void ScanLinesStopAtATooLongLine()
    {
        var data = Encoding.UTF8.GetBytes("a\r\nb\n" + new string('x', 65535) + "\nc\n" + new string('y', 65536) + "\nd");
        Assert.Equal(["a", "b", new string('x', 65535), "c"], GoStrings.ScanLines(data));
        Assert.Equal(["a", "b"], GoStrings.ScanLines(Encoding.UTF8.GetBytes("a\nb")));
    }

    [Fact]
    public void ElfNeededLibraries()
    {
        // Any dynamically linked system binary: sh links libc, a GTK app libgtk.
        var elf = Elf.Open("/usr/bin/bash");
        Assert.NotNull(elf);
        var libs = elf.ImportedLibraries();
        elf.Close();
        Assert.NotNull(libs);
        Assert.Contains(libs, l => l.StartsWith("libc.so"));
        Assert.Null(Elf.Open("/etc/hostname"));
    }
}
