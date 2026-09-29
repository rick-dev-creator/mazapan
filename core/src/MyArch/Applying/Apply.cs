using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MyArch.Rendering;
using MyArch.Util;

namespace MyArch.Applying;

// Applying writes rendered files to disk and remembers which files the core
// owns, so it never silently overwrites something a person wrote and can
// remove what a disabled plugin left behind.

public enum State
{
    Unchanged,
    /// <summary>file doesn't exist yet</summary>
    New,
    /// <summary>ours, and the new content differs</summary>
    Changed,
    /// <summary>exists, but we didn't write it (or it was edited since)</summary>
    Conflict,
    /// <summary>its app is running: left for the next apply</summary>
    Busy,
    /// <summary>shared, but not in a form it can merge into (JSON with comments): left alone</summary>
    Unreadable,
}

public static class StateNames
{
    /// <summary>How a state is printed: "unchanged", "new", "changed", "conflict", "busy", "unreadable".</summary>
    public static string Name(this State s) =>
        new[] { "unchanged", "new", "changed", "conflict", "busy", "unreadable" }[(int)s];
}

/// <summary>Change is a rendered file and what writing it would do.</summary>
public sealed class Change(RenderedFile file, State state = State.Unchanged)
{
    public RenderedFile File { get; } = file;
    public State State { get; set; } = state;

    public string Plugin => File.Plugin;
    public string Path => File.Path;
    public string Content => File.Content;
    public string Reload => File.Reload;
    public string Merge => File.Merge;
    public bool Busy => File.Busy;
}

/// <summary>
/// Owned maps an absolute path to the sha256 of the content we last wrote
/// (or, for a file shared with its app, "&lt;format&gt;:" + the keys: see
/// Apply.IsShared). It is ~/.local/state/myarch/owned.json.
/// </summary>
public sealed class Owned : Dictionary<string, string>
{
    public Owned() : base(StringComparer.Ordinal) { }

    public Owned(IDictionary<string, string> from) : base(from, StringComparer.Ordinal) { }

    /// <summary>The value for path, or "" (a Go map's zero value).</summary>
    public string Get(string path) => TryGetValue(path, out var v) ? v : "";

    /// <summary>As json.MarshalIndent wrote it: 2 spaces, sorted keys, HTML-safe, a final newline.</summary>
    public string ToJson(string indent = "  ") => GoJsonCodec.Marshal(this, true, indent);

    public void Save(string path)
    {
        try
        {
            Files.WriteAtomic(path, ToJson() + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException(e.Message, e);
        }
    }

    /// <summary>
    /// FromTree reads a JSON object of strings, as json.Unmarshal into an
    /// Owned does (a null value is ""); null is a JSON null.
    /// </summary>
    internal static Owned? FromTree(object? node, string goType = "apply.Owned")
    {
        switch (node)
        {
            case null:
                return null;
            case GoJsonCodec.JsonObject o:
                var owned = new Owned();
                GoJsonCodec.JsonError? err = null;
                foreach (var (k, v) in o)
                {
                    switch (v)
                    {
                        case string s: owned[k] = s; break;
                        case null: owned[k] = ""; break;
                        // Go keeps going, and reports the first error.
                        default: err ??= GoJsonCodec.TypeError(v, "string"); break;
                    }
                }
                if (err != null) throw err;
                return owned;
            default:
                throw GoJsonCodec.TypeError(node, goType);
        }
    }
}

/// <summary>Result is what Execute did.</summary>
public sealed class Result
{
    public List<Change> Written { get; } = [];
    /// <summary>path -> backup path</summary>
    public Dictionary<string, string> Backups { get; } = new(StringComparer.Ordinal);
    public List<string> Removed { get; } = [];
    /// <summary>orphans left in place because they were edited</summary>
    public List<string> Kept { get; } = [];
    /// <summary>orphans shared with their app: left in place, released</summary>
    public List<string> Shared { get; } = [];
}

/// <summary>
/// Files that exist and were not written by myarch (or were edited since):
/// Execute writes nothing without adopt.
/// </summary>
public sealed class ConflictException(List<string> paths) : Exception(
    "these files exist and were not written by myarch (or were edited since):\n  " +
    string.Join("\n  ", paths) +
    "\nre-run with --adopt to back them up and take them over")
{
    public List<string> Paths { get; } = paths;
}

public static partial class Apply
{
    public static string StatePath() => Util.Paths.ExpandHome("~/.local/state/myarch/owned.json");

    public static Owned LoadOwned(string path)
    {
        var b = ReadOrNull(path);
        if (b == null) return new Owned();
        try
        {
            // A file holding null leaves Go a nil map; here an empty one.
            return Owned.FromTree(GoJsonCodec.Parse(Files.Utf8.GetString(b))) ?? new Owned();
        }
        catch (GoJsonCodec.JsonError e)
        {
            throw new MyArchException(e.Message, e);
        }
    }

    /// <summary>Sum is how ownership identifies a file's content.</summary>
    public static string Sum(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

    public static string Sum(string s) => Sum(Files.Utf8.GetBytes(s));

    /// <summary>
    /// Plan compares rendered files with what's on disk. Orphans are paths
    /// we own that no enabled plugin generates anymore.
    /// </summary>
    public static (List<Change> Changes, List<string> Orphans) Plan(IEnumerable<RenderedFile> files, Owned owned)
    {
        var changes = new List<Change>();
        var want = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            want.Add(f.Path);
            if (f.Busy)
            {
                // Its app is running and would write its own copy back: later.
                changes.Add(new Change(f, State.Busy));
                continue;
            }
            var disk = Read(f.Path);
            if (f.Merge != "" && disk != null)
            {
                changes.Add(new Change(f, PlanShared(f, disk, owned.Get(f.Path))));
                continue;
            }
            State st;
            if (disk == null) st = State.New;
            else if (disk.AsSpan().SequenceEqual(Files.Utf8.GetBytes(f.Content))) st = State.Unchanged;
            else if (owned.Get(f.Path) == Sum(disk)) st = State.Changed;
            else st = State.Conflict;
            changes.Add(new Change(f, st));
        }
        var orphans = owned.Keys.Where(p => !want.Contains(p)).ToList();
        GoOrder.Sort(orphans);
        return (changes, orphans);
    }

    /// <summary>
    /// PlanShared: a file shared with its app is unchanged when myarch's
    /// keys are as rendered, whatever else is in it; a conflict when one of
    /// them was changed by someone else since myarch wrote it, or, the first
    /// time, when the file has its own value for one of them (a person's
    /// kdeglobals).
    /// </summary>
    static State PlanShared(RenderedFile f, byte[] diskBytes, string owned)
    {
        if (!Files.TryText(diskBytes, out var disk) || !Readable(f.Merge, disk))
            return State.Unreadable; // never rewritten blind, not even with --adopt
        var want = KeysOf(f.Merge, f.Content);
        var have = KeysOf(f.Merge, disk);
        var same = true;
        foreach (var (k, v) in want)
            if (Get(have, k) != v)
                same = false;
        // A key myarch no longer manages, still in a person's file: it goes.
        var drop = NoLonger(owned, want);
        if (same && DropKeys(f.Merge, disk, drop) != disk) same = false;
        if (same) return State.Unchanged;
        if (IsShared(owned)) return Edited(owned, diskBytes) ? State.Conflict : State.Changed;
        if (owned != "" && Sum(diskBytes) == owned)
            return State.Changed; // written whole by an older myarch, untouched since
        foreach (var (k, v) in want)
            if (have.TryGetValue(k, out var hv) && hv != v)
                return State.Conflict; // its own value: theirs until --adopt
        return State.Changed; // only keys it doesn't have yet
    }

    /// <summary>
    /// NoLonger: the keys myarch wrote last (owned) that it doesn't manage
    /// anymore (not in want), with the values it wrote.
    /// </summary>
    static Dictionary<string, string>? NoLonger(string owned, Dictionary<string, string> want)
    {
        if (!FromSharedOwned(owned, out var old, out _)) return null;
        var out_ = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in old)
            if (!want.ContainsKey(k))
                out_[k] = v;
        return out_;
    }

    /// <summary>
    /// WriteShared writes a shared file through a symlink (dotfiles managed
    /// by stow or chezmoi), not over it.
    /// </summary>
    public static void WriteShared(string path, byte[] b)
    {
        if (Util.Paths.Real(path) is { } real) path = real;
        Files.WriteAtomic(path, b);
    }

    /// <summary>
    /// Proposed is what writing c puts on disk, and what ownership records
    /// for it: the rendered file, or for a shared one, the file on disk with
    /// myarch's keys merged in. What --diff shows is exactly this.
    /// </summary>
    public static (byte[] Content, string Sum) Proposed(Change c, Owned owned)
    {
        var content = Files.Utf8.GetBytes(c.Content);
        var sum = Sum(content);
        if (c.Merge != "")
        {
            var keys = KeysOf(c.Merge, c.Content);
            sum = SharedOwned(c.Merge, keys);
            // Not there yet: written as rendered, comments and all.
            if (Read(c.Path) is { } disk)
                content = Files.Utf8.GetBytes(DropKeys(c.Merge,
                    MergeKeys(c.Merge, Files.Utf8.GetString(disk), keys), NoLonger(owned.Get(c.Path), keys)));
        }
        return (content, sum);
    }

    /// <summary>
    /// ProposedOrphan is what removing an orphan does to it: null when it's
    /// deleted, its content when it stays but loses myarch's keys (a shared
    /// file: user.js, kdeglobals), or its content as it is when it's left
    /// alone (edited, or gone).
    /// </summary>
    public static byte[]? ProposedOrphan(string path, Owned owned)
    {
        if (Read(path) is not { } disk) return null;
        if (IsShared(owned.Get(path)))
        {
            FromSharedOwned(owned.Get(path), out var old, out var format);
            return Files.TryText(disk, out var text) ? Files.Utf8.GetBytes(DropKeys(format, text, old)) : disk;
        }
        return Sum(disk) == owned.Get(path) ? null : disk;
    }

    /// <summary>
    /// Execute writes the plan. Conflicts abort (a ConflictException) unless
    /// adopt is set, in which case the existing file is backed up and taken
    /// over. owned is updated as files are written, even when it stops half
    /// way on an error (a MyArchException): save it either way.
    /// </summary>
    public static Result Execute(List<Change> changes, List<string> orphans, Owned owned, bool adopt)
    {
        var conflicts = changes.Where(c => c.State == State.Conflict).Select(c => c.Path).ToList();
        if (conflicts.Count > 0 && !adopt) throw new ConflictException(conflicts);

        var res = new Result();
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            foreach (var c in changes)
            {
                if (c.State is State.Busy or State.Unreadable)
                    continue; // untouched, still ours as it was
                var (content, sum) = Proposed(c, owned);
                switch (c.State)
                {
                    case State.Unchanged:
                        owned[c.Path] = sum;
                        continue;
                    case State.Conflict:
                        // A shared file keeps the app's keys: back it up, merge into it.
                        var bak = c.Path + ".myarch-bak-" + stamp;
                        CopyOrRename(c.Path, bak, c.Merge != "");
                        res.Backups[c.Path] = bak;
                        break;
                }
                if (c.Merge != "") WriteShared(c.Path, content);
                else Files.WriteAtomic(c.Path, content);
                owned[c.Path] = sum;
                res.Written.Add(c);
            }

            foreach (var p in orphans)
            {
                var disk = Read(p);
                if (disk == null)
                {
                }
                else if (IsShared(owned.Get(p)))
                {
                    // The app's (or the person's) file: it stays. In a person's own
                    // (user.js, userChrome.css), myarch's lines go with the plugin.
                    FromSharedOwned(owned.Get(p), out var old, out var format);
                    // Not UTF-8: left as it is rather than rewritten with its bytes changed.
                    if (Files.TryText(disk, out var text))
                    {
                        var kept = DropKeys(format, text, old);
                        if (kept != text) WriteShared(p, Files.Utf8.GetBytes(kept));
                    }
                    res.Shared.Add(p);
                }
                else if (Sum(disk) == owned.Get(p))
                {
                    System.IO.File.Delete(p);
                    res.Removed.Add(p);
                }
                else
                {
                    res.Kept.Add(p);
                }
                owned.Remove(p);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException(e.Message, e);
        }
        return res;
    }

    static void CopyOrRename(string from, string to, bool keep)
    {
        if (!keep)
        {
            System.IO.File.Move(from, to, overwrite: true);
            return;
        }
        // A browser's Preferences hold personal data: backups stay private.
        WriteFile(to, System.IO.File.ReadAllBytes(from), UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>
    /// Reload runs each distinct reload command of plugins whose files
    /// changed, in plugin order. Failures are returned (command -> what went
    /// wrong), not fatal: the files are already written and the next session
    /// picks them up anyway. Each gets 30 s: one that hangs (an app that's
    /// suspended and never answers) mustn't hold up an apply, an update or a
    /// rollback.
    /// </summary>
    public static Dictionary<string, string> Reload(IEnumerable<Change> written)
    {
        var errs = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in written)
        {
            if (c.Reload == "" || !seen.Add(c.Reload)) continue;
            if (RunShell(c.Reload, ReloadTimeout, TimeSpan.FromSeconds(2)) is { } err)
                errs[c.Reload] = err;
        }
        return errs;
    }

    internal static TimeSpan ReloadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// RunShell runs sh -c command as exec.CommandContext and CombinedOutput
    /// do: killed after timeout, its output given up waitDelay after it
    /// ends (its children holding the output open). Null when it went
    /// well, else Go's error text: "exit status 1: output".
    /// </summary>
    internal static string? RunShell(string command, TimeSpan timeout, TimeSpan waitDelay)
    {
        var psi = new ProcessStartInfo("sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);
        Process p;
        try
        {
            p = Process.Start(psi)!;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            return $"exec: \"sh\": {e.Message}: ";
        }
        using (p)
        {
            p.StandardInput.Close();
            // One buffer for both, in the order they come: CombinedOutput.
            var out_ = new MemoryStream();
            // Reads that can be given up: a child left running (a daemon the
            // command started, a hyprctl stuck on a frozen Hyprland) can hold
            // the pipes open for ever; Go stopped waiting after WaitDelay.
            using var closePipe = new CancellationTokenSource();
            async Task Pump(Stream s)
            {
                var buf = new byte[4096];
                try
                {
                    int n;
                    while ((n = await s.ReadAsync(buf, closePipe.Token).ConfigureAwait(false)) > 0)
                        lock (out_) out_.Write(buf, 0, n);
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException) { }
            }
            var pumps = Task.WhenAll(Pump(p.StandardOutput.BaseStream), Pump(p.StandardError.BaseStream));
            var killed = false;
            if (!p.WaitForExit(timeout))
            {
                try
                {
                    p.Kill(entireProcessTree: false);
                }
                catch (InvalidOperationException) { }
                killed = true;
                p.WaitForExit(waitDelay);
            }
            string? err = null;
            if (killed) err = "signal: killed";
            else if (p.ExitCode != 0) err = $"exit status {p.ExitCode}";
            if (!pumps.Wait(waitDelay))
            {
                err ??= "exec: WaitDelay expired before I/O complete";
                closePipe.Cancel();
                try
                {
                    pumps.Wait(TimeSpan.FromSeconds(1));
                }
                catch (AggregateException) { }
            }
            if (err == null) return null;
            string text;
            lock (out_) text = Files.Utf8.GetString(out_.ToArray());
            return err + ": " + text.Trim();
        }
    }

    // ---- files ----

    /// <summary>A file's bytes, or null when it doesn't exist (Go's fs.ErrNotExist).</summary>
    public static byte[]? ReadOrNull(string path)
    {
        try
        {
            return System.IO.File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException(e.Message, e);
        }
    }

    static byte[]? Read(string path) => ReadOrNull(path);

    /// <summary>os.WriteFile: a new file gets mode (less the umask), one that's there keeps its own.</summary>
    internal static void WriteFile(string path, byte[] b, UnixFileMode mode)
    {
        using var f = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = mode,
        });
        f.Write(b);
    }

    static string Get(Dictionary<string, string> m, string k) => m.TryGetValue(k, out var v) ? v : "";
}
