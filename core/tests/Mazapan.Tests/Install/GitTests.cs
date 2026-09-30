using Mazapan.Install;
using Mazapan.Util;

namespace Mazapan.Tests.Install;

/// <summary>Against real git, with repositories made in a temporary folder.</summary>
[Collection("InstallHome")]
public class GitTests
{
    /// <summary>Clones source into the plugin folder and locks it, as plugins add does.</summary>
    static Entry Add(string source, string @ref = "")
    {
        using var st = Git.Clone(source, @ref, "");
        var e = new Entry { Id = st.Plugin.Id, Source = source, Ref = @ref, Commit = st.Commit, Approved = st.Plugin.Capabilities() };
        var l = PluginsLock.Load();
        l.Put(e);
        l.Save();
        st.Accept();
        return PluginsLock.Load().Get(e.Id)!;
    }

    static string[] StagingLeft() =>
        Directory.GetDirectories(Paths.Dir(Git.Dir), "plugin-add-*");

    [Fact]
    public void CloneAtTag()
    {
        using var sb = new Sandbox();
        var src = sb.Source("tagged", "tagged");
        var v1 = Sandbox.Git(src, "rev-parse", "HEAD");
        Sandbox.Git(src, "tag", "v1");
        Sandbox.Write(src, "plugin.toml", Sandbox.Manifest("tagged", "2.0.0"));
        Sandbox.Commit(src, "second");

        using (var st = Git.Clone(src, "v1", ""))
        {
            Assert.Equal(v1, st.Commit);
            Assert.Equal("1.0.0", st.Plugin.Meta.Version);
            Assert.StartsWith(Paths.Dir(Git.Dir) + "/plugin-add-", st.Plugin.Dir);
            st.Accept();
            Assert.Equal(Path.Join(Git.Dir, "tagged"), st.Plugin.Dir);
            Assert.Equal(v1, Git.Head(st.Plugin.Dir));
        }
        Assert.Empty(StagingLeft());

        // No ref: the default branch.
        Directory.Delete(Path.Join(Git.Dir, "tagged"), true);
        using (var st = Git.Clone(src, "", ""))
            Assert.Equal("2.0.0", st.Plugin.Meta.Version);
        Assert.Empty(StagingLeft());

        // A commit (sync), even from a relative folder.
        var cwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.Join(sb.Root, "src"));
        try
        {
            using var st = Git.Clone("tagged", "", v1);
            Assert.Equal(v1, st.Commit);
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
        }

        var err = Assert.Throws<MazapanException>(() => Git.Clone(src, "nope", ""));
        Assert.Equal("no branch, tag or commit \"nope\"", err.Message);
        err = Assert.Throws<MazapanException>(() => Git.Clone(src, "", new string('a', 40)));
        Assert.Equal("commit aaaaaaaaaa is gone from where it came from", err.Message);
        Assert.Empty(StagingLeft());
    }

    [Fact]
    public void CloneReportsSourceNotStaging()
    {
        using var sb = new Sandbox();
        var src = sb.Source("broken", "broken");
        Sandbox.Write(src, "plugin.toml", "[plugin]\nid = \"broken\"\n");
        Sandbox.Commit(src, "break it");
        var err = Assert.Throws<MazapanException>(() => Git.Clone(src, "", ""));
        Assert.StartsWith(src + "/plugin.toml: ", err.Message);
        Assert.DoesNotContain("plugin-add-", err.Message);
        Assert.Empty(StagingLeft());
    }

    [Fact]
    public void RefusesSymlinksAndSubmodules()
    {
        using var sb = new Sandbox();
        var src = sb.Source("linked", "linked");
        File.CreateSymbolicLink(Path.Join(src, "secrets"), "/etc/passwd");
        Sandbox.Commit(src, "link");
        var err = Assert.Throws<MazapanException>(() => Git.Clone(src, "", ""));
        Assert.Equal("it has a symlink (secrets); plugins from git can't", err.Message);

        var sub = sb.Source("sub", "sub");
        var commit = Sandbox.Git(sub, "rev-parse", "HEAD");
        Sandbox.Git(sub, "update-index", "--add", "--cacheinfo", "160000," + commit + ",vendor/lib");
        Sandbox.Git(sub, "commit", "--quiet", "-m", "submodule");
        err = Assert.Throws<MazapanException>(() => Git.Clone(sub, "", ""));
        Assert.Equal("it has a submodule (vendor/lib); plugins from git can't", err.Message);
        Assert.Empty(StagingLeft());
        Assert.False(Directory.Exists(Git.Dir) && Directory.EnumerateFileSystemEntries(Git.Dir).Any());
    }

    [Fact]
    public void ChangedSeesEverything()
    {
        using var sb = new Sandbox();
        var src = sb.Source("watched", "watched");
        Sandbox.Write(src, ".gitignore", "*.log\n");
        Sandbox.Write(src, "a.tmpl", "a");
        Sandbox.Commit(src, "more");
        var e = Add(src);
        var dir = Path.Join(Git.Dir, "watched");
        Assert.Empty(Git.Changed(dir));
        Git.Verify(dir, e, Mazapan.Plugins.Plugin.Load(dir));

        // Edited.
        File.WriteAllText(Path.Join(dir, "a.tmpl"), "b");
        Assert.Equal(["a.tmpl"], Git.Changed(dir));
        Sandbox.Git(dir, "checkout", "--", "a.tmpl");
        Assert.Empty(Git.Changed(dir));

        // New, not tracked.
        File.WriteAllText(Path.Join(dir, "new.tmpl"), "x");
        Assert.Equal(["new.tmpl"], Git.Changed(dir));
        File.Delete(Path.Join(dir, "new.tmpl"));

        // Ignored.
        File.WriteAllText(Path.Join(dir, "debug.log"), "x");
        Assert.Equal(["debug.log"], Git.Changed(dir));
        File.Delete(Path.Join(dir, "debug.log"));

        // Hidden from git status by assume-unchanged, not from Changed.
        Sandbox.Git(dir, "update-index", "--assume-unchanged", "a.tmpl");
        File.WriteAllText(Path.Join(dir, "a.tmpl"), "sneaky");
        Assert.Equal("", Sandbox.Git(dir, "status", "--porcelain"));
        Assert.Equal(["a.tmpl"], Git.Changed(dir));
        var err = Assert.Throws<MazapanException>(() => Git.Verify(dir, e, null));
        Assert.Equal($"watched was changed outside mazapan (a.tmpl); put it back with git -C {dir} stash --all, or remove and add it again",
            err.Message);
        File.WriteAllText(Path.Join(dir, "a.tmpl"), "a");
        Assert.Empty(Git.Changed(dir));

        // A symlink put in afterwards.
        File.CreateSymbolicLink(Path.Join(dir, "evil.tmpl"), "/etc/passwd");
        Assert.Equal(["evil.tmpl"], Git.Changed(dir));
        File.Delete(Path.Join(dir, "evil.tmpl"));

        // More than five: the first five and an ellipsis.
        for (var i = 0; i < 7; i++) File.WriteAllText(Path.Join(dir, $"f{i}"), "x");
        err = Assert.Throws<MazapanException>(() => Git.Verify(dir, e, null));
        Assert.Contains("(f0, f1, f2, f3, f4, …)", err.Message);
    }

    [Fact]
    public void VerifyChecksCommitAndApprovals()
    {
        using var sb = new Sandbox();
        var src = sb.Source("verified", "verified");
        var e = Add(src);
        var dir = Path.Join(Git.Dir, "verified");
        var p = Mazapan.Plugins.Plugin.Load(dir);
        Git.Verify(dir, e, p);

        var other = e.Copy();
        other.Commit = new string('b', 40);
        var err = Assert.Throws<MazapanException>(() => Git.Verify(dir, other, null));
        Assert.Equal($"verified is at {e.Commit[..10]}, but plugins.lock says bbbbbbbbbb (run: mazapan plugins sync)", err.Message);

        p.Pacman = ["foo"];
        err = Assert.Throws<MazapanException>(() => Git.Verify(dir, e, p));
        Assert.Equal("verified needs what you didn't approve:\n    needs the package foo", err.Message);

        err = Assert.Throws<MazapanException>(() => Git.Verify(Path.Join(sb.Root, "nowhere"), e, null));
        Assert.StartsWith("verified: git rev-parse: ", err.Message);
    }

    [Fact]
    public void UpdateThroughWorktree()
    {
        using var sb = new Sandbox();
        var src = sb.Source("moving", "moving");
        var e = Add(src);
        var dir = Path.Join(Git.Dir, "moving");
        var from = e.Commit;

        Assert.Null(Git.Update(e, ""));

        Sandbox.Write(src, "plugin.toml", Sandbox.Manifest("moving", "1.1.0", "[packages]\npacman = [\"foo\"]\n"));
        var to = Sandbox.Commit(src, "newer");

        // Looked at, then dropped: the plugin stays where it was.
        using (var m = Git.Update(e, "")!)
        {
            Assert.Equal(from, m.From);
            Assert.Equal(to, m.To);
            Assert.Contains("newer", m.Log);
            Assert.Equal("1.1.0", m.Plugin.Meta.Version);
            Assert.Equal(["needs the package foo"], Mazapan.Plugins.Plugin.NewCapabilities(e.Approved, m.Plugin.Capabilities()));
            Assert.StartsWith(Paths.Dir(Git.Dir) + "/plugin-add-", m.Plugin.Dir);
            Assert.Equal(from, Git.Head(dir));
            m.Discard();
            Assert.False(Directory.Exists(m.Plugin.Dir));
        }
        Assert.Equal(from, Git.Head(dir));
        Assert.Empty(Git.Changed(dir));
        Assert.Single(Sandbox.Git(dir, "worktree", "list").Split('\n'));
        Assert.Empty(StagingLeft());

        // Kept; then back, as when the lock can't be saved.
        using (var m = Git.Update(e, "")!)
        {
            m.Keep();
            Assert.Equal(to, Git.Head(dir));
            Assert.Equal(dir, m.Plugin.Dir);
            Assert.Single(Sandbox.Git(dir, "worktree", "list").Split('\n'));
            m.Back();
            Assert.Equal(from, Git.Head(dir));
            m.Keep();
        }
        Assert.Equal(to, Git.Head(dir));
        Assert.Empty(StagingLeft());

        // The lock still says from: Update refuses until it's saved.
        var err = Assert.Throws<MazapanException>(() => Git.Update(e, ""));
        Assert.StartsWith("moving is at ", err.Message);
        var l = PluginsLock.Load();
        var next = e.Copy();
        next.Commit = to;
        l.Put(next);
        l.Save();
        e = PluginsLock.Load().Get("moving")!;
        Assert.Null(Git.Update(e, ""));
    }

    [Fact]
    public void UpdateRefusesAnotherId()
    {
        using var sb = new Sandbox();
        var src = sb.Source("renamed", "renamed");
        var e = Add(src);
        Sandbox.Write(src, "plugin.toml", Sandbox.Manifest("other"));
        Sandbox.Commit(src, "rename");
        var err = Assert.Throws<MazapanException>(() => Git.Update(e, ""));
        Assert.Equal("the new version calls itself \"other\", not \"renamed\"", err.Message);
        Assert.Empty(StagingLeft());
        Assert.Single(Sandbox.Git(Path.Join(Git.Dir, "renamed"), "worktree", "list").Split('\n'));

        // Nor a symlink in the new commit.
        Sandbox.Write(src, "plugin.toml", Sandbox.Manifest("renamed"));
        File.CreateSymbolicLink(Path.Join(src, "l"), "/etc/passwd");
        Sandbox.Commit(src, "link");
        err = Assert.Throws<MazapanException>(() => Git.Update(e, ""));
        Assert.Equal("it has a symlink (l); plugins from git can't", err.Message);
    }

    [Fact]
    public void UpdateFollowsARef()
    {
        using var sb = new Sandbox();
        var src = sb.Source("branchy", "branchy");
        var e = Add(src);
        Sandbox.Git(src, "checkout", "--quiet", "-b", "next");
        Sandbox.Write(src, "x.tmpl", "x");
        var next = Sandbox.Commit(src, "on next");
        using var m = Git.Update(e, "next")!;
        Assert.Equal(next, m.To);
    }

    [Fact]
    public void GotoMovesToTheLockedCommit()
    {
        using var sb = new Sandbox();
        var src = sb.Source("synced", "synced");
        var first = Sandbox.Git(src, "rev-parse", "HEAD");
        Sandbox.Write(src, "plugin.toml", Sandbox.Manifest("synced", "2.0.0"));
        Sandbox.Commit(src, "second");
        var e = Add(src);
        var dir = Path.Join(Git.Dir, "synced");
        var locked = e.Copy();
        locked.Commit = first;
        var p = Git.Goto(locked);
        Assert.Equal(first, Git.Head(dir));
        Assert.Equal("1.0.0", p.Meta.Version);

        File.WriteAllText(Path.Join(dir, "plugin.toml"), "edited");
        var err = Assert.Throws<MazapanException>(() => Git.Goto(e));
        Assert.StartsWith("synced was changed outside mazapan (plugin.toml)", err.Message);
    }

    [Fact]
    public void StageClearsOldLeftovers()
    {
        using var sb = new Sandbox();
        var @base = Paths.Dir(Git.Dir);
        var old = Path.Join(@base, "plugin-add-1");
        var recent = Path.Join(@base, "plugin-add-2");
        Directory.CreateDirectory(Path.Join(old, "repo"));
        Directory.CreateDirectory(recent);
        Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-2));
        var tmp = Git.Stage();
        Assert.False(Directory.Exists(old));
        Assert.True(Directory.Exists(recent));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(tmp));
        Assert.Equal(@base, Paths.Dir(tmp));
    }

    [Fact]
    public void AcceptRefusesWhatIsThere()
    {
        using var sb = new Sandbox();
        var src = sb.Source("taken", "taken");
        Directory.CreateDirectory(Path.Join(Git.Dir, "taken"));
        using var st = Git.Clone(src, "", "");
        var err = Assert.Throws<MazapanException>(st.Accept);
        Assert.Equal(Path.Join(Git.Dir, "taken") + " already exists", err.Message);
    }

    [Fact]
    public void GitErrorsNameTheCommand()
    {
        using var sb = new Sandbox();
        var err = Assert.Throws<MazapanException>(() => Git.Clone(Path.Join(sb.Root, "missing"), "", ""));
        Assert.StartsWith("git clone: ", err.Message);
        Assert.Empty(StagingLeft());
    }
}
