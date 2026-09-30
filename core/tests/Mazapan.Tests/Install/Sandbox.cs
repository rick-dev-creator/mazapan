using System.Diagnostics;

namespace Mazapan.Tests.Install;

/// <summary>
/// Tests that change $HOME: they run alone, after the parallel ones, since
/// Paths.Home is the process's.
/// </summary>
[CollectionDefinition("InstallHome", DisableParallelization = true)]
public sealed class InstallHomeCollection;

/// <summary>
/// A temporary $HOME (so Git.Dir and PluginsLock.Path land in it) and a place for
/// source repositories; the old $HOME comes back on Dispose.
/// </summary>
sealed class Sandbox : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("mazapan-install-test-").FullName;
    public string Home => Path.Join(Root, "home");
    readonly string? oldHome = Environment.GetEnvironmentVariable("HOME");

    public Sandbox()
    {
        Directory.CreateDirectory(Home);
        Environment.SetEnvironmentVariable("HOME", Home);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HOME", oldHome);
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException) { }
    }

    /// <summary>git for the test's own repositories: no config but a name.</summary>
    public static string Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in new[] { "-C", dir, "-c", "user.name=test", "-c", "user.email=test@example.com",
                     "-c", "commit.gpgsign=false", "-c", "tag.gpgsign=false", "-c", "protocol.file.allow=always" })
            psi.ArgumentList.Add(a);
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var p = Process.Start(psi)!;
        p.StandardInput.Close();
        var err = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(" ", args)}: {err.Result}");
        return output.Trim();
    }

    public static string Manifest(string id, string version = "1.0.0", string extra = "") =>
        $"[plugin]\nid = \"{id}\"\nname = \"{id}\"\nversion = \"{version}\"\napi = 1\n{extra}";

    /// <summary>A source repository with a plugin manifest committed on main.</summary>
    public string Source(string name, string id)
    {
        var dir = Path.Join(Root, "src", name);
        Directory.CreateDirectory(dir);
        Git(dir, "init", "--quiet", "-b", "main");
        Write(dir, "plugin.toml", Manifest(id));
        Commit(dir, "first");
        return dir;
    }

    public static void Write(string dir, string rel, string content)
    {
        var p = Path.Join(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
    }

    /// <summary>Commits everything; the commit's id.</summary>
    public static string Commit(string dir, string message)
    {
        Git(dir, "add", "--all");
        Git(dir, "commit", "--quiet", "-m", message);
        return Git(dir, "rev-parse", "HEAD");
    }
}
