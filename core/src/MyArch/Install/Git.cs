using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MyArch.Plugins;
using MyArch.Util;

namespace MyArch.Install;

/// <summary>
/// Plugins from git: cloning one into a staging folder to be looked at,
/// updating it through a worktree beside it, and verifying that a checkout
/// is what plugins.lock says.
/// </summary>
public static partial class Git
{
    /// <summary>
    /// Dir is where plugins from git live; it's also the first place plugins
    /// are discovered, so one there shadows a built-in of the same id.
    /// </summary>
    public static string Dir => Paths.ExpandHome("~/.local/share/myarch/plugins");

    // What git gets before the command: none of the repository's hooks or
    // fsmonitor, no symlinks written, no ext:: transport.
    static readonly string[] Hardening =
    [
        "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=false",
        "-c", "core.symlinks=false", "-c", "protocol.ext.allow=never",
    ];

    /// <summary>
    /// Run is git, with none of the person's or the system's git config (no
    /// hooks, filters, fsmonitor or aliases a repository could lean on) and
    /// never stopping to ask for a password. The output is trimmed; a failure
    /// is "git &lt;first argument&gt;: &lt;what git said&gt;".
    /// </summary>
    static readonly HashSet<string> Transport =
        ["GIT_SSH", "GIT_SSH_COMMAND", "GIT_SSH_VARIANT", "GIT_SSL_CAINFO", "GIT_SSL_CAPATH", "GIT_PROXY_COMMAND"];

    /// <summary>The git command args run, past any -c options.</summary>
    static string Subcommand(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-c") { i++; continue; }
            return args[i];
        }
        return "";
    }

    internal static string Run(string dir, params string[] args) => RunEnv(dir, null, args).Trim();

    /// <summary>Run, with more environment (after the rest: it wins), and the output as it came.</summary>
    internal static string RunEnv(string dir, IReadOnlyDictionary<string, string>? env, params string[] args)
    {
        var (output, error) = TryRunEnv(dir, env, args);
        if (error != null) throw new MyArchException(error);
        return output;
    }

    /// <summary>Run that reports a failure instead of throwing: (trimmed output, null) or ("", error).</summary>
    internal static (string Output, string? Error) TryRun(string dir, params string[] args)
    {
        var (output, error) = TryRunEnv(dir, null, args);
        return (output.Trim(), error);
    }

    static (string Output, string? Error) TryRunEnv(string dir, IReadOnlyDictionary<string, string>? env, string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            // No stdin (as Go's exec: /dev/null): git must never wait on the terminal.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Files.Utf8,
            StandardErrorEncoding = Files.Utf8,
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(dir);
        foreach (var a in Hardening) psi.ArgumentList.Add(a);
        foreach (var a in args) psi.ArgumentList.Add(a);
        // None of the caller's git variables: run from a git hook, GIT_DIR
        // and GIT_INDEX_FILE would point git at another repository.
        // How the person reaches their remotes (ssh keys, a proxy, their CA)
        // still goes through: with global config off, it's the only way.
        foreach (var k in psi.Environment.Keys.Where(k => k.StartsWith("GIT_") && !Transport.Contains(k)).ToList())
            psi.Environment.Remove(k);
        // A plugin folder that isn't a repository must not borrow the one
        // it sits in (a home kept in git).
        psi.Environment["GIT_CEILING_DIRECTORIES"] = Paths.Dir(Paths.Clean(dir));
        psi.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GIT_ASKPASS"] = "true";
        psi.Environment["GIT_LFS_SKIP_SMUDGE"] = "1";
        psi.Environment["LC_ALL"] = "C";
        if (env != null)
            foreach (var (k, v) in env) psi.Environment[k] = v;
        using var p = new Process { StartInfo = psi };
        try
        {
            p.Start();
        }
        catch (Win32Exception)
        {
            return ("", $"git {Subcommand(args)}: exec: \"git\": executable file not found in $PATH");
        }
        p.StandardInput.Close();
        var errTask = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEnd();
        var stderr = errTask.GetAwaiter().GetResult();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            var msg = stderr.Trim();
            if (msg == "") msg = $"exit status {p.ExitCode}";
            return ("", $"git {Subcommand(args)}: {msg}");
        }
        return (output, null);
    }

    /// <summary>ParseSource splits "url#ref" (a branch, tag or commit).</summary>
    public static (string Source, string Ref) ParseSource(string s)
    {
        var i = s.IndexOf('#');
        var source = i < 0 ? s : s[..i];
        var @ref = i < 0 ? "" : s[(i + 1)..];
        if (source == "" || source.StartsWith('-'))
            throw new MyArchException($"{GoFormat.Quote(s)} is not a git URL or folder");
        CheckRef(@ref);
        return (source, @ref);
    }

    // \z, not $: .NET's $ also matches before a final newline, Go's doesn't.
    [GeneratedRegex(@"^[A-Za-z0-9._/+-]*\z")]
    private static partial Regex RefPattern();

    /// <summary>
    /// CheckRef refuses what can't be a branch, tag or commit, or could be
    /// taken for a git option.
    /// </summary>
    public static void CheckRef(string @ref)
    {
        if (@ref.StartsWith('-') || @ref.Contains("..") || !RefPattern().IsMatch(@ref))
            throw new MyArchException($"{GoFormat.Quote(@ref)} is not a branch, tag or commit");
    }

    /// <summary>Head is the commit a checkout is at.</summary>
    public static string Head(string dir) => Run(dir, "rev-parse", "HEAD");

    /// <summary>
    /// Safe refuses a commit with symlinks (they'd pull files from outside the
    /// commit into templates) or submodules (unpinned code).
    /// </summary>
    internal static void Safe(string dir, string commit)
    {
        var output = Run(dir, "ls-tree", "-r", "--full-tree", commit);
        foreach (var l in output.Split('\n'))
        {
            var name = l[(l.IndexOf('\t') + 1)..];
            if (l.StartsWith("120000 "))
                throw new MyArchException($"it has a symlink ({name}); plugins from git can't");
            if (l.StartsWith("160000 "))
                throw new MyArchException($"it has a submodule ({name}); plugins from git can't");
        }
    }

    /// <summary>
    /// Changed lists the files that differ from the commit: edited, new, even
    /// ignored ones (every *.tmpl in the folder is parsed). It hashes the
    /// folder into a fresh index and compares trees, so no index flag
    /// (assume-unchanged, skip-worktree) can hide a change.
    /// </summary>
    public static List<string> Changed(string dir)
    {
        var index = Paths.Join(Path.GetTempPath(), "myarch-index-" + Random.Shared.NextInt64(0, 1L << 32));
        try
        {
            // Reserve the name, then leave it to git to make.
            using (new FileStream(index, FileMode.CreateNew, FileAccess.Write)) { }
            File.Delete(index);
            var env = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = index };
            RunEnv(dir, env, "add", "--all", "--force", "--", ".");
            var tree = RunEnv(dir, env, "write-tree").Trim();
            var head = Run(dir, "rev-parse", "HEAD^{tree}");
            if (tree == head) return [];
            var (output, error) = TryRun(dir, "diff-tree", "-r", "--name-only", "--no-renames", head, tree);
            if (error != null) throw new MyArchException(error);
            if (output == "") return ["?"];
            return [.. output.Split('\n')];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException(e.Message, e);
        }
        finally
        {
            try
            {
                File.Delete(index);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    [GeneratedRegex(@"^[0-9a-f]{7,64}\z")]
    private static partial Regex HexCommit();

    /// <summary>
    /// Resolve finds the commit of ref after a fetch: a branch of origin, a
    /// tag, or a commit; no ref is the default branch.
    /// </summary>
    internal static string Resolve(string dir, string @ref)
    {
        CheckRef(@ref);
        List<string> tries;
        if (@ref == "")
            tries = ["refs/remotes/origin/HEAD"];
        else
        {
            tries = ["refs/remotes/origin/" + @ref, "refs/tags/" + @ref];
            if (HexCommit().IsMatch(@ref)) tries.Add(@ref);
        }
        foreach (var t in tries)
        {
            var (c, error) = TryRun(dir, "rev-parse", "--verify", "--quiet", "--end-of-options", t + "^{commit}");
            if (error == null && c != "") return c;
        }
        if (@ref == "") throw new MyArchException("the repository has no default branch");
        throw new MyArchException($"no branch, tag or commit {GoFormat.Quote(@ref)}");
    }

    internal static void Checkout(string dir, string commit) =>
        Run(dir, "-c", "advice.detachedHead=false", "checkout", "--quiet", "--detach", commit);

    /// <summary>Fetch gets what's new from where the plugin came from.</summary>
    internal static void Fetch(string dir) => Run(dir, "fetch", "--quiet", "--tags", "--force", "origin");

    /// <summary>
    /// FetchCommit makes sure a commit is there, asking for it by name when
    /// it's on no branch or tag any more.
    /// </summary>
    internal static void FetchCommit(string dir, string commit)
    {
        if (TryRun(dir, "cat-file", "-e", "--end-of-options", commit + "^{commit}").Error == null) return;
        if (TryRun(dir, "fetch", "--quiet", "origin", "--end-of-options", commit).Error != null)
            throw new MyArchException($"commit {Short(commit)} is gone from where it came from");
    }

    /// <summary>os.RemoveAll: a symlink goes, not what it points to; errors are ignored.</summary>
    internal static void RemoveAll(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.LinkTarget != null || File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Stage makes a staging folder beside the plugin folder, not in it:
    /// discovery must not see it, and the move into place must be a rename on
    /// the same filesystem. Leftovers of interrupted runs go.
    /// </summary>
    internal static string Stage()
    {
        try
        {
            var @base = Paths.Dir(Dir);
            Directory.CreateDirectory(@base);
            foreach (var o in Glob.Expand(Paths.Join(@base, "plugin-add-*")))
            {
                // os.Stat: the time of what a symlink points to.
                var info = Directory.Exists(o) ? (FileSystemInfo)new DirectoryInfo(o) : new FileInfo(o);
                if (info.LinkTarget != null) info = info.ResolveLinkTarget(returnFinalTarget: true) ?? info;
                if (info.Exists && DateTime.UtcNow - info.LastWriteTimeUtc > TimeSpan.FromHours(1)) RemoveAll(o);
            }
            // os.MkdirTemp: a new folder, only the person's, under a random name.
            for (var i = 0; ; i++)
            {
                var tmp = Paths.Join(@base, "plugin-add-" + (uint)Random.Shared.NextInt64(0, 1L << 32));
                if (Paths.Exists(tmp))
                {
                    if (i < 10000) continue;
                    throw new MyArchException($"mkdirtemp {Paths.Join(@base, "plugin-add-*")}: file exists");
                }
                Directory.CreateDirectory(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                return tmp;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException(e.Message, e);
        }
    }

    /// <summary>
    /// Clone fetches source at ref (or at commit, when given: sync) into a
    /// staging folder and reads its manifest.
    /// </summary>
    public static Staged Clone(string source, string @ref, string commit)
    {
        if (Directory.Exists(source))
        {
            // A folder on this machine: git runs elsewhere, so make it absolute.
            source = Abs(source);
        }
        var tmp = Stage();
        var s = new Staged(tmp);
        var repo = Paths.Join(tmp, "repo");
        try
        {
            Run(tmp, "clone", "--quiet", "--no-checkout", "--", source, repo);
            var want = commit;
            if (want == "") want = Resolve(repo, @ref);
            else FetchCommit(repo, want);
            Safe(repo, want);
            Checkout(repo, want);
            s.Commit = Head(repo);
            try
            {
                s.Plugin = Plugin.Load(repo);
            }
            catch (Exception e) when (e is MyArchException or IOException or UnauthorizedAccessException)
            {
                // Where it came from means more than the staging folder.
                throw new MyArchException(e.Message.Replace(repo, source), e);
            }
            return s;
        }
        catch
        {
            s.Discard();
            throw;
        }
    }

    /// <summary>filepath.Abs: joined to the working directory and cleaned.</summary>
    internal static string Abs(string path) =>
        Paths.Clean(path.StartsWith('/') ? path : Paths.Join(Directory.GetCurrentDirectory(), path));

    /// <summary>
    /// Update fetches the latest commit of ref for an installed plugin. It
    /// returns null when it's already there. The checkout must be what the
    /// lock says.
    /// </summary>
    public static Move? Update(Entry e, string @ref)
    {
        var dir = Paths.Join(Dir, e.Id);
        Verify(dir, e, null);
        Fetch(dir);
        var to = Resolve(dir, @ref);
        if (to == e.Commit) return null;
        Safe(dir, to);
        var tmp = Stage();
        var m = new Move(e.Commit, to, dir, tmp);
        m.Log = TryRun(dir, "log", "--oneline", "--no-decorate", "-n", "20", e.Commit + ".." + to).Output;
        var wt = Paths.Join(tmp, e.Id);
        try
        {
            Run(dir, "worktree", "add", "--quiet", "--detach", wt, to);
            m.Plugin = Plugin.Load(wt);
            if (m.Plugin.Id != e.Id)
                throw new MyArchException(
                    $"the new version calls itself {GoFormat.Quote(m.Plugin.Id)}, not {GoFormat.Quote(e.Id)}");
            return m;
        }
        catch
        {
            m.Discard();
            throw;
        }
    }

    /// <summary>Goto moves a clean checkout to the locked commit (sync).</summary>
    public static Plugin Goto(Entry e)
    {
        var dir = Paths.Join(Dir, e.Id);
        var files = Changed(dir);
        if (files.Count > 0) throw ChangedError(e.Id, files);
        try
        {
            FetchCommit(dir, e.Commit);
        }
        catch (MyArchException)
        {
            Fetch(dir);
            FetchCommit(dir, e.Commit);
        }
        Safe(dir, e.Commit);
        Checkout(dir, e.Commit);
        return Plugin.Load(dir);
    }

    static MyArchException ChangedError(string id, List<string> files)
    {
        if (files.Count > 5) files = [.. files.Take(5), "…"];
        return new MyArchException(
            $"{id} was changed outside myarch ({string.Join(", ", files)}); put it back with git -C {Paths.Join(Dir, id)} stash --all, or remove and add it again");
    }

    /// <summary>
    /// Verify says whether an installed plugin is what plugins.lock says: at
    /// its commit, unchanged, and needing nothing that wasn't approved. p is
    /// the loaded plugin (null: don't check capabilities). It throws when it
    /// isn't.
    /// </summary>
    public static void Verify(string dir, Entry e, Plugin? p)
    {
        string head;
        try
        {
            head = Head(dir);
        }
        catch (MyArchException ex)
        {
            throw new MyArchException($"{e.Id}: {ex.Message}", ex);
        }
        if (head != e.Commit)
            throw new MyArchException(
                $"{e.Id} is at {Short(head)}, but plugins.lock says {Short(e.Commit)} (run: myarch plugins sync)");
        List<string> files;
        try
        {
            Safe(dir, head);
            files = Changed(dir);
        }
        catch (MyArchException ex)
        {
            throw new MyArchException($"{e.Id}: {ex.Message}", ex);
        }
        if (files.Count > 0) throw ChangedError(e.Id, files);
        if (p != null)
        {
            var extra = Plugin.NewCapabilities(e.Approved, p.Capabilities());
            if (extra.Count > 0)
                throw new MyArchException($"{e.Id} needs what you didn't approve:\n    {string.Join("\n    ", extra)}");
        }
    }

    internal static string Short(string c) => c.Length > 10 ? c[..10] : c;
}

/// <summary>
/// Staged is a plugin cloned next to the plugin folder, not in it yet: it
/// can be looked at (capabilities, requirements) before it's accepted.
/// Disposing it discards it (Go's defer st.Discard()).
/// </summary>
public sealed class Staged : IDisposable
{
    public Plugin Plugin { get; internal set; } = null!;
    public string Commit { get; internal set; } = "";
    readonly string tmp;

    internal Staged(string tmp) => this.tmp = tmp;

    public void Discard() => Git.RemoveAll(tmp);

    public void Dispose() => Discard();

    /// <summary>Accept moves it into the plugin folder under its id.</summary>
    public void Accept()
    {
        var dest = Paths.Join(Git.Dir, Plugin.Id);
        try
        {
            Directory.CreateDirectory(Git.Dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException(e.Message, e);
        }
        if (Paths.Exists(dest)) throw new MyArchException($"{dest} already exists");
        try
        {
            Directory.Move(Plugin.Dir, dest);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException($"rename {Plugin.Dir} {dest}: {e.Message}", e);
        }
        Plugin.Dir = dest;
        Git.RemoveAll(tmp);
    }
}

/// <summary>
/// Move is a newer commit of an installed plugin, checked out beside it to
/// be looked at; the plugin itself moves only on Keep. Disposing it
/// discards the look (Go's defer m.Discard()).
/// </summary>
public sealed class Move : IDisposable
{
    public Plugin Plugin { get; internal set; } = null!;
    public string From { get; }
    public string To { get; }
    /// <summary>Log: commits between From and To, one per line.</summary>
    public string Log { get; internal set; } = "";
    readonly string dir, tmp;

    internal Move(string from, string to, string dir, string tmp)
    {
        From = from;
        To = to;
        this.dir = dir;
        this.tmp = tmp;
    }

    /// <summary>Discard drops the look at the new version; the plugin stays as it was.</summary>
    public void Discard()
    {
        Git.TryRun(dir, "worktree", "remove", "--force", Paths.Join(tmp, Paths.Base(dir)));
        Git.RemoveAll(tmp);
        Git.TryRun(dir, "worktree", "prune");
    }

    public void Dispose() => Discard();

    /// <summary>Keep moves the plugin to the new commit.</summary>
    public void Keep()
    {
        Discard();
        Git.Checkout(dir, To);
        Plugin.Dir = dir;
    }

    /// <summary>
    /// Back puts the plugin back at From, after a Keep whose lock couldn't be
    /// saved.
    /// </summary>
    public void Back() => Git.Checkout(dir, From);
}
