using Mazapan.Cli;
using Mazapan.Install;

namespace Mazapan.Tests.Install;

/// <summary>
/// The plugin registry from the command line, against real git: what its
/// catalog lists (a commit, not only a tag) is what gets installed and
/// updated to, the copy shipped with mazapan stands in when it can't be
/// reached, and the plugins Mazapan used to ship come from their own
/// repositories.
/// </summary>
[Collection("InstallHome")]
public sealed class RegistryTests : IDisposable
{
    readonly Sandbox sb = new();
    readonly string root, registry;
    readonly Dictionary<string, string?> oldEnv = [];
    readonly IReadOnlyDictionary<string, Moved.Where> oldMoved = Moved.Plugins;

    public RegistryTests()
    {
        // A mazapan of its own: no built-in plugins, a shipped catalog.
        root = Path.Join(sb.Root, "root");
        Directory.CreateDirectory(Path.Join(root, "plugins"));
        Directory.CreateDirectory(Path.Join(root, "catalog"));
        File.WriteAllText(Path.Join(root, "catalog", "index.toml"), "");
        registry = Path.Join(sb.Root, "registry.toml");
        SetEnv("MAZAPAN_ROOT", root);
        SetEnv("MAZAPAN_PLUGIN_CATALOG", registry);
    }

    void SetEnv(string k, string v)
    {
        oldEnv.TryAdd(k, Environment.GetEnvironmentVariable(k));
        Environment.SetEnvironmentVariable(k, v);
    }

    public void Dispose()
    {
        foreach (var (k, v) in oldEnv) Environment.SetEnvironmentVariable(k, v);
        Moved.Plugins = oldMoved;
        sb.Dispose();
    }

    /// <summary>mazapan ARGS: its exit status and what it said (stdout, then stderr).</summary>
    static (int Exit, string Out) Run(params string[] args)
    {
        var (o, e) = (Console.Out, Console.Error);
        var w = new StringWriter();
        Console.SetOut(w);
        Console.SetError(w);
        try
        {
            return (Program.Main(args), w.ToString());
        }
        finally
        {
            Console.SetOut(o);
            Console.SetError(e);
        }
    }

    static string Entry(string id, string source, string @ref, string commit, string version = "1.0.0") =>
        $"[[plugin]]\nid = \"{id}\"\nname = \"{id}\"\nsource = \"{source}\"\nref = \"{@ref}\"\ncommit = \"{commit}\"\nversion = \"{version}\"\n";

    /// <summary>A plugin that writes one file, and runs a command when it changes when asked to.</summary>
    static string Manifest(string id, string version, string reload = "") =>
        Sandbox.Manifest(id, version) + "[[targets]]\ntemplate = \"a.tmpl\"\noutput = \"~/.config/" + id + "/a\"\n" + (reload != "" ? $"reload = \"{reload}\"\n" : "");

    /// <summary>A source repository with the plugin at 1.0.0, tagged v1.0: its directory and that commit.</summary>
    (string Dir, string Commit) Source(string id)
    {
        var dir = sb.Source(id, id);
        Sandbox.Write(dir, "plugin.toml", Manifest(id, "1.0.0"));
        Sandbox.Write(dir, "a.tmpl", "one\n");
        var c = Sandbox.Commit(dir, "1.0.0");
        Sandbox.Git(dir, "tag", "v1.0");
        return (dir, c);
    }

    static string Lock() => File.ReadAllText(PluginsLock.Path);

    [Fact]
    public void AddInstallsTheCommitTheRegistryListsNotWhereTheTagIsNow()
    {
        var (src, listed) = Source("uptime");
        File.WriteAllText(registry, Entry("uptime", src, "v1.0", listed));
        // The tag moved after the registry looked at it.
        Sandbox.Write(src, "a.tmpl", "two\n");
        var moved = Sandbox.Commit(src, "the tag moved");
        Sandbox.Git(src, "tag", "-f", "v1.0");

        var (exit, said) = Run("plugins", "preview", "uptime", "--json");
        Assert.True(exit == 0, said);
        Assert.Contains($"\"commit\":\"{listed}\"", said.Replace(" ", ""));

        (exit, said) = Run("plugins", "add", "uptime", "-y");
        Assert.True(exit == 0, said);
        var e = PluginsLock.Load().Get("uptime")!;
        Assert.Equal((listed, "v1.0", true), (e.Commit, e.Ref, e.Catalog));
        Assert.NotEqual(moved, e.Commit);
        Assert.Equal("one\n", File.ReadAllText(Path.Join(Git.Dir, "uptime", "a.tmpl")));

        // The panel's install names the commit it showed: another one is refused.
        Run("plugins", "remove", "uptime");
        (exit, said) = Run("plugins", "add", "uptime", "-y", "--commit", moved);
        Assert.Equal(1, exit);
        Assert.Contains("as shown: not installed", said);
        Assert.Null(PluginsLock.Load().Get("uptime"));
    }

    [Fact]
    public void UpdatesGoWhereTheRegistrySays()
    {
        var (src, first) = Source("uptime");
        File.WriteAllText(registry, Entry("uptime", src, "v1.0", first));
        Assert.Equal(0, Run("plugins", "add", "uptime", "-y").Exit);
        // A new tag in the repository is nothing until the registry lists it.
        Sandbox.Write(src, "plugin.toml", Manifest("uptime", "1.1.0"));
        var second = Sandbox.Commit(src, "1.1.0");
        Sandbox.Git(src, "tag", "v1.1");
        Assert.Empty(Program.PendingPluginUpdates());
        Assert.Contains("up to date", Run("plugins", "update", "uptime", "-y").Out);
        Assert.Equal(first, PluginsLock.Load().Get("uptime")!.Commit);

        // Listed: it's an update, and it goes to the commit listed.
        File.WriteAllText(registry, Entry("uptime", src, "v1.1", second, "1.1.0"));
        var pending = Assert.Single(Program.PendingPluginUpdates());
        Assert.Equal(("uptime", first[..10], second[..10]), (pending.Id, pending.From, pending.To));
        var (exit, said) = Run("plugins", "update", "uptime", "-y");
        Assert.True(exit == 0, said);
        var e = PluginsLock.Load().Get("uptime")!;
        Assert.Equal((second, "v1.1", true), (e.Commit, e.Ref, e.Catalog));
        Assert.Empty(Program.PendingPluginUpdates());

        // Another commit under the same tag (the registry looked at a retag): an update too.
        Sandbox.Write(src, "a.tmpl", "fixed\n");
        var third = Sandbox.Commit(src, "1.1.0, fixed");
        File.WriteAllText(registry, Entry("uptime", src, "v1.1", third, "1.1.0"));
        Assert.Single(Program.PendingPluginUpdates());
        Assert.Equal(0, Run("plugins", "update", "uptime", "-y").Exit);
        Assert.Equal(third, PluginsLock.Load().Get("uptime")!.Commit);
    }

    [Fact]
    public void AnUpdateThatWantsMoreStillAsks()
    {
        var (src, first) = Source("uptime");
        File.WriteAllText(registry, Entry("uptime", src, "v1.0", first));
        Assert.Equal(0, Run("plugins", "add", "uptime", "-y").Exit);
        Sandbox.Write(src, "plugin.toml", Manifest("uptime", "2.0.0", "pkill -USR1 uptime"));
        var second = Sandbox.Commit(src, "2.0.0");
        File.WriteAllText(registry, Entry("uptime", src, "v2.0", second, "2.0.0"));

        // Listed by the registry is no approval: no terminal, no -y, no update.
        var (exit, said) = Run("plugins", "update", "uptime");
        Assert.Equal(1, exit);
        Assert.Contains("runs after writing: pkill -USR1 uptime", said);
        Assert.Contains("no terminal to ask on", said);
        Assert.Equal(first, PluginsLock.Load().Get("uptime")!.Commit);

        Assert.Equal(0, Run("plugins", "update", "uptime", "-y").Exit);
        var e = PluginsLock.Load().Get("uptime")!;
        Assert.Equal(second, e.Commit);
        Assert.Contains("runs after writing: pkill -USR1 uptime", e.Approved);
    }

    [Fact]
    public void ARegistryThatGoesBackAVersionIsNotFollowed()
    {
        var (src, first) = Source("uptime");
        Sandbox.Write(src, "plugin.toml", Manifest("uptime", "1.1.0"));
        var second = Sandbox.Commit(src, "1.1.0");
        File.WriteAllText(registry, Entry("uptime", src, "v1.1", second, "1.1.0"));
        Assert.Equal(0, Run("plugins", "add", "uptime", "-y").Exit);
        File.WriteAllText(registry, Entry("uptime", src, "v1.0", first));
        var (exit, said) = Run("plugins", "update", "uptime", "-y");
        Assert.Equal(1, exit);
        Assert.Contains("older than 1.1.0", said);
        Assert.Equal(second, PluginsLock.Load().Get("uptime")!.Commit);
    }

    [Fact]
    public void TheShippedCopyStandsInWhenTheRegistryCantBeReached()
    {
        var (src, listed) = Source("uptime");
        File.WriteAllText(Path.Join(root, "catalog", "index.toml"), Entry("uptime", src, "v1.0", listed));
        // No registry at all (never reached, nothing cached).
        var (exit, said) = Run("plugins", "search", "uptime");
        Assert.True(exit == 0, said);
        Assert.Contains("uptime", said);
        Assert.Contains("registry.toml: no such file", said);
        Assert.Equal(0, Run("plugins", "add", "uptime", "-y").Exit);
        Assert.Equal(listed, PluginsLock.Load().Get("uptime")!.Commit);

        // The registry reached: only what it lists.
        File.WriteAllText(registry, "");
        (exit, said) = Run("plugins", "catalog", "--json");
        Assert.True(exit == 0, said);
        Assert.DoesNotContain("\"available\"", said);
    }

    /// <summary>A plugin Mazapan used to ship, now in its own repository: id "pomo", at the commit that was shipped.</summary>
    (string Dir, string Commit) MovedPlugin()
    {
        var (src, shipped) = Source("pomo");
        Sandbox.Git(src, "tag", "v0.1.1");
        Moved.Plugins = new Dictionary<string, Moved.Where> { ["pomo"] = new(src, "v0.1.1", shipped) };
        // Newer since: the migration still installs what was shipped.
        Sandbox.Write(src, "plugin.toml", Manifest("pomo", "2.0.0", "pkill -USR1 pomo"));
        Sandbox.Commit(src, "2.0.0");
        return (src, shipped);
    }

    static void WriteConfig(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Mazapan.Config.Settings.Path)!);
        File.WriteAllText(Mazapan.Config.Settings.Path, text);
    }

    [Fact]
    public void APluginThatMovedOutIsKeptAndThenInstalledFromItsRepository()
    {
        var (src, shipped) = MovedPlugin();
        WriteConfig("enabled_plugins = [\"pomo\"]\n[plugins.pomo]\n");

        // Applying before it's installed: nothing breaks, its files are kept.
        var w = new StringWriter();
        var err = Console.Error;
        Console.SetError(w);
        try
        {
            var cfg = Mazapan.Config.Settings.Load();
            Assert.Empty(Program.EnabledPlugins(cfg));
            Program.CheckOverrides(cfg);
            Assert.Contains("pomo", Program.BrokenNotOff);
        }
        finally
        {
            Console.SetError(err);
        }
        Assert.Contains("pomo now comes from its own repository", w.ToString());

        // sync (and update) install it: what was shipped, what it could do
        // approved, following the registry from then on.
        var (exit, said) = Run("plugins", "sync");
        Assert.True(exit == 0, said);
        Assert.Contains("pomo", said);
        Assert.Contains("installed from its own repository", said);
        var e = PluginsLock.Load().Get("pomo")!;
        Assert.Equal((src, "v0.1.1", shipped, true), (e.Source, e.Ref, e.Commit, e.Catalog));
        // What the shipped version could do, not what a newer one wants.
        Assert.Equal(Mazapan.Plugins.Plugin.Load(Path.Join(Git.Dir, "pomo")).Capabilities(), e.Approved);
        Assert.NotEmpty(e.Approved);
        Assert.DoesNotContain(e.Approved, a => a.Contains("pkill"));
        var cfg2 = Mazapan.Config.Settings.Load();
        Assert.Equal(["pomo"], Program.EnabledPlugins(cfg2).Select(p => p.Id));
        Assert.DoesNotContain("pomo", Program.BrokenNotOff);
        // Once is enough.
        Assert.DoesNotContain("installed from its own repository", Run("plugins", "sync").Out);
    }

    [Fact]
    public void OnlyThoseThatWereOnMoveIn()
    {
        MovedPlugin();
        // Never turned on (it shipped off), or turned off: nothing to install.
        foreach (var config in new[] { "", "enabled_plugins = [\"pomo\"]\ndisabled_plugins = [\"pomo\"]\n" })
        {
            WriteConfig(config);
            var (exit, said) = Run("plugins", "sync");
            Assert.True(exit == 0, said);
            Assert.Null(PluginsLock.Load().Get("pomo"));
        }
        // A [plugins.pomo] left from when it was here doesn't break apply meanwhile.
        WriteConfig("[plugins.pomo]\n");
        Program.CheckOverrides(Mazapan.Config.Settings.Load());
    }

    [Fact]
    public void AMovedPluginThatCantBeFetchedIsSaidAndLeftForLater()
    {
        var (src, _) = MovedPlugin();
        WriteConfig("enabled_plugins = [\"pomo\"]\n");
        Directory.Move(src, src + "-away"); // offline, as far as git can tell
        var (exit, said) = Run("plugins", "sync");
        Assert.Equal(1, exit);
        Assert.Contains("pomo: not installed from its own repository", said);
        Assert.False(File.Exists(PluginsLock.Path) && PluginsLock.Load().Get("pomo") != null);
        Assert.Empty(Directory.Exists(Git.Dir) ? Directory.GetDirectories(Git.Dir) : []);
        Directory.Move(src + "-away", src);
        Assert.Equal(0, Run("plugins", "sync").Exit);
        Assert.NotNull(PluginsLock.Load().Get("pomo"));
    }
}
