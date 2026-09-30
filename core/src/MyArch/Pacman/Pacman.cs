using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using MyArch.Util;

namespace MyArch.Pacman;

/// <summary>
/// Change is one package going from one version to another. From is empty
/// for a newly installed package, To for a removed one.
/// </summary>
/// <remarks>
/// The JSON attributes give Go's struct tags ({"name","from,omitempty",
/// "to,omitempty"}) to a source-generated context: FromJson and ToJson stand
/// in for From and To, null (left out) when empty.
/// </remarks>
public sealed record Change(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonIgnore] string From = "",
    [property: JsonIgnore] string To = "")
{
    /// <summary>From for JSON: null when empty, so it's left out (Go's omitempty).</summary>
    [JsonPropertyName("from"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), EditorBrowsable(EditorBrowsableState.Never)]
    public string? FromJson
    {
        get => From == "" ? null : From;
        init => From = value ?? "";
    }

    /// <summary>To for JSON: null when empty, so it's left out (Go's omitempty).</summary>
    [JsonPropertyName("to"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), EditorBrowsable(EditorBrowsableState.Never)]
    public string? ToJson
    {
        get => To == "" ? null : To;
        init => To = value ?? "";
    }
}

/// <summary>
/// What Revert did: how many packages it put back, what couldn't be found
/// anywhere, and pacman's error when the transaction failed (then Count is 0).
/// </summary>
public sealed record RevertResult(int Count, List<string> Missing, string? Error);

/// <summary>
/// The little of pacman that updates need: what's pending, what's installed,
/// and putting the previous versions back from the cache.
/// </summary>
public static class Packages
{
    static readonly HttpClient Http = new(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    /// <summary>
    /// Pending lists available updates without touching the system's package
    /// database (checkupdates syncs a private copy).
    /// </summary>
    public static List<Change> Pending()
    {
        var r = Exec.Run("checkupdates", []);
        if (r.NotFound) throw new MyArchException("checkupdates not found: install pacman-contrib");
        if (r.ExitCode == 2) return []; // nothing to update
        if (r.Error != null) throw new MyArchException($"checkupdates: {r.Error}: {r.Stderr.Trim()}");
        return ParsePending(r.Stdout);
    }

    /// <summary>ParsePending reads checkupdates' "name old -> new" lines.</summary>
    internal static List<Change> ParsePending(string s)
    {
        var out_ = new List<Change>();
        foreach (var line in Exec.Lines(s))
        {
            var f = Exec.Fields(line);
            if (f.Length >= 4 && f[2] == "->") out_.Add(new Change(f[0], f[1], f[3]));
        }
        return out_;
    }

    /// <summary>Installed maps every installed package to its version.</summary>
    public static Dictionary<string, string> Installed()
    {
        var r = Exec.Run("pacman", ["-Q"]);
        if (r.Error != null) throw new MyArchException($"pacman -Q: {r.Error}");
        var m = new Dictionary<string, string>();
        foreach (var line in Exec.Lines(r.Stdout))
        {
            var f = Exec.Fields(line);
            if (f.Length == 2) m[f[0]] = f[1];
        }
        return m;
    }

    /// <summary>Diff lists what changed between two Installed snapshots, by name.</summary>
    public static List<Change> Diff(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        var out_ = new List<Change>();
        foreach (var (name, v) in after)
        {
            var had = before.TryGetValue(name, out var old);
            if (!had || old != v) out_.Add(new Change(name, old ?? "", v));
        }
        foreach (var (name, v) in before)
            if (!after.ContainsKey(name)) out_.Add(new Change(name, v));
        out_.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return out_;
    }

    /// <summary>CacheDirs are pacman's package caches (pacman-conf), or the default.</summary>
    public static List<string> CacheDirs()
    {
        var r = Exec.Run("pacman-conf", ["CacheDir"]);
        if (r.Error != null || r.Stdout.Trim() == "") return ["/var/cache/pacman/pkg/"];
        return [.. Exec.Fields(r.Stdout)];
    }

    /// <summary>
    /// CachedFile finds the package file for name at version in the caches.
    /// "foot-1.28.0-2-x86_64.pkg.tar.zst" matches foot 1.28.0-2 but not
    /// foot-terminfo, because the version has to follow the name directly.
    /// Null when no cache has it.
    /// </summary>
    public static string? CachedFile(IEnumerable<string> dirs, string name, string version)
    {
        foreach (var d in dirs)
        {
            List<string> matches;
            try
            {
                matches = Glob.Expand(Paths.Join(d, name + "-" + version + "-*.pkg.tar.*"));
            }
            catch (ArgumentException) { continue; } // a bad pattern: Go's Glob errors, ignored
            foreach (var m in matches)
                if (!m.EndsWith(".sig", StringComparison.Ordinal)) return m;
        }
        return null;
    }

    /// <summary>From a panel (no terminal): pacman through pkexec, the password in polkit's dialog.</summary>
    public static bool Gui;

    /// <summary>
    /// run executes a pacman command as root, attached to the terminal so sudo
    /// (or pkexec, Gui) can ask for a password and pacman can show its
    /// progress. Null when it worked, else the error as Go wrote it ("exit status 1").
    /// </summary>
    static string? RunAsRoot(params string[] args)
    {
        var how = Gui ? "pkexec" : "sudo";
        if (Exec.LookPath(how) is not { } sudo)
            return $"exec: \"{how}\": executable file not found in $PATH";
        var psi = new ProcessStartInfo(sudo) { UseShellExecute = false };
        psi.ArgumentList.Add("pacman");
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode == 0 ? null : Exec.ExitText(p.ExitCode);
        }
        catch (Win32Exception e)
        {
            return Exec.StartError(sudo, e);
        }
    }

    /// <summary>
    /// Upgrade runs a full system upgrade without asking: the caller has
    /// already asked. A question that needs a real answer (a conflict) makes
    /// pacman stop without changing anything.
    /// </summary>
    public static void Upgrade()
    {
        if (RunAsRoot("-Syu", "--noconfirm") is { } err) throw new MyArchException(err);
    }

    /// <summary>
    /// ArchiveUrl finds the package file for name at version on the Arch Linux
    /// Archive, which keeps every version ever published (signed, so pacman
    /// verifies it like any other download). The architecture isn't known, so
    /// it tries x86_64 and then any. Null when it isn't there (or the archive
    /// can't be reached).
    /// </summary>
    public static string? ArchiveUrl(string name, string version)
    {
        if (name == "") return null;
        foreach (var arch in new[] { "x86_64", "any" })
        {
            var u = $"https://archive.archlinux.org/packages/{name[0]}/{name}/{name}-{PathEscape(version)}-{arch}.pkg.tar.zst";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, u);
                using var resp = Http.Send(req);
                if (resp.StatusCode == HttpStatusCode.OK) return u;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Go's url.PathEscape: a path segment, with ':' '@' '&amp;' '=' '+' '$' left alone.</summary>
    internal static string PathEscape(string s)
    {
        var b = new StringBuilder();
        foreach (var c in Encoding.UTF8.GetBytes(s))
        {
            var keep = c is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9'
                or (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~'
                or (byte)'$' or (byte)'&' or (byte)'+' or (byte)':' or (byte)'=' or (byte)'@';
            if (keep) b.Append((char)c);
            else b.Append('%').Append(c.ToString("X2"));
        }
        return b.ToString();
    }

    /// <summary>
    /// Revert puts back the previous version of every changed package, in one
    /// transaction so pacman keeps them consistent with each other. Previous
    /// versions come from pacman's cache, or from the Arch Linux Archive when
    /// the cache no longer has them. A package the update newly installed stays,
    /// unless it conflicts with one being put back (a replacement): then it's
    /// removed. Missing lists what couldn't be found anywhere.
    /// </summary>
    public static RevertResult Revert(IEnumerable<Change> changes)
    {
        var dirs = CacheDirs();
        var sources = new List<string>();
        var missing = new List<string>();
        foreach (var c in changes)
        {
            if (c.From == "") continue;
            if (CachedFile(dirs, c.Name, c.From) is { } f) sources.Add(f);
            else if (ArchiveUrl(c.Name, c.From) is { } u) sources.Add(u);
            else missing.Add(c.Name + " " + c.From);
        }
        if (sources.Count == 0) return new RevertResult(0, missing, null);
        // --noconfirm answers "remove the conflicting package?" with no, which
        // would abort the whole transaction when the update replaced a package;
        // --ask=4 (ALPM_QUESTION_CONFLICT_PKG) answers it with yes.
        string[] args = ["-U", "--noconfirm", "--ask=4", .. sources];
        if (RunAsRoot(args) is { } err) return new RevertResult(0, missing, err);
        return new RevertResult(sources.Count, missing, null);
    }
}
