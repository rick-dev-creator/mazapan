using System.Text.RegularExpressions;
using Mazapan.Util;

namespace Mazapan.Pacman;

/// <summary>
/// Repository is Mazapán's own package repository: `mazapan` and
/// `mazapan-keyring`, published by pkg/release, in channels: stable, and
/// edge (every release, before it reaches stable). Where it's published
/// is pkg/repository.toml (next to the plugins, in the package too); an
/// installed system reaches it through pacman.conf's [mazapan] section,
/// which includes /etc/pacman.d/mazapan-mirrorlist, written by the
/// installer and by `mazapan channel`. A mazapan run from a checkout (the
/// dev VM's) is the dev channel: the checkout is what it runs.
/// </summary>
public static partial class Repository
{
    public const string Mirrorlist = "/etc/pacman.d/mazapan-mirrorlist";
    public const string Conf = "/etc/pacman.conf";
    public const string Installed = "/usr/lib/mazapan";
    public static readonly string[] Channels = ["stable", "edge"];

    /// <summary>The [mazapan] section, added to pacman.conf when it has none (as root: sh -c).</summary>
    /// <remarks>Its database signed too (repo-add --sign): an old or unsigned one
    /// served by a mirror can't hold a system back.</remarks>
    public const string EnableScript =
        "grep -q '^[[:space:]]*\\[mazapan\\]' /etc/pacman.conf || printf '\\n[mazapan]\\nSigLevel = Required\\nInclude = /etc/pacman.d/mazapan-mirrorlist\\n' >> /etc/pacman.conf";

    /// <summary>Mazapán's keys, as mazapan-keyring installs them: without them its packages can't be trusted.</summary>
    public const string Keyring = "/usr/share/pacman/keyrings/mazapan.gpg";

    /// <summary>
    /// The server pkg/repository.toml names, $channel still in it: "" when
    /// Mazapán isn't published anywhere yet (then nothing adds the
    /// repository: pacman would fail on one it can't reach).
    /// </summary>
    public static string Server(string root)
    {
        var path = Paths.Join(root, "pkg", "repository.toml");
        if (!File.Exists(path)) return "";
        var t = Toml.Parse(File.ReadAllText(path), path);
        foreach (var k in t.Keys)
            if (k != "server") throw new MazapanException($"{path}: unknown key {k}");
        var server = t.TryGetValue("server", out var v) && v is string str ? str : "";
        if (server != "" && !ValidServer().IsMatch(server))
            throw new MazapanException($"{path}: server must be an https:// or file:// URL, with no spaces or quotes");
        return server;
    }

    // Goes into a file pacman reads, and into a shell script while installing.
    [GeneratedRegex(@"^(https|file)://[A-Za-z0-9._~:/?#\[\]@!$&()*+,;=%-]+\z")]
    private static partial Regex ValidServer();

    /// <summary>The mirrorlist for a channel. Its first line says the channel, for ChannelOf.</summary>
    public static string MirrorlistText(string server, string channel)
    {
        if (!Channels.Contains(channel)) throw new MazapanException($"no channel \"{channel}\": {string.Join(" or ", Channels)}");
        return $"# mazapan channel: {channel}\n" +
            "# Mazapán's own packages. `mazapan channel stable|edge` rewrites this file.\n" +
            $"Server = {server.Replace("$channel", channel)}\n";
    }

    /// <summary>The channel a mirrorlist was written for, or null (none, or not one of ours).</summary>
    public static string? ChannelOf(string text)
    {
        var first = text.Split('\n', 2)[0];
        const string prefix = "# mazapan channel: ";
        if (!first.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var c = first[prefix.Length..].Trim();
        return Channels.Contains(c) ? c : null;
    }

    /// <summary>Whether pacman.conf has the [mazapan] section.</summary>
    public static bool Enabled(string confText) =>
        confText.Split('\n').Any(l => l.Trim() == "[mazapan]");

    /// <summary>
    /// Where the changelog of the channel this system follows is: next to
    /// its database (pkg/release puts it there). Null without the repository.
    /// </summary>
    public static string? ChangelogUrl()
    {
        if (!File.Exists(Mirrorlist)) return null;
        foreach (var line in File.ReadAllLines(Mirrorlist))
        {
            var t = line.Trim();
            if (!t.StartsWith("Server", StringComparison.Ordinal) || t.Split('=', 2) is not [_, var url]) continue;
            url = url.Trim().Replace("$repo", "mazapan").Replace("$arch", "x86_64");
            return ValidServer().IsMatch(url) ? url.TrimEnd('/') + "/CHANGELOG.md" : null;
        }
        return null;
    }

    const int MaxChangelog = 256 * 1024;

    /// <summary>The changelog, at most 256 KB of it (it's from the network, unsigned: only words).</summary>
    public static string FetchChangelog(string url, HttpClient http)
    {
        using var stream = url.StartsWith("file://", StringComparison.Ordinal)
            ? File.OpenRead(new Uri(url).LocalPath)
            : http.GetStreamAsync(url).GetAwaiter().GetResult();
        var buf = new byte[MaxChangelog];
        int n = 0, r;
        while (n < buf.Length && (r = stream.Read(buf, n, buf.Length - n)) > 0) n += r;
        return Files.Utf8.GetString(buf, 0, n);
    }

    /// <summary>One release's part of the changelog: its version (or "Unreleased") and its items.</summary>
    public sealed record Release(string Version, List<string> Items);

    /// <summary>
    /// WhatsNew: the changelog's releases newer than the one installed, newest
    /// first: every section above the installed version's own ("0.2.0" for
    /// 0.2.0 and 0.2.0.r3.gabc). Items are its "- " lines, wrapped lines joined.
    /// </summary>
    public static List<Release> WhatsNew(string changelog, string installed, string target)
    {
        var mine = installed.Split(".r", 2)[0];
        var to = target.Split(".r", 2)[0];
        // A release (X.Y.Z) brings nothing of what's still unreleased.
        var edge = target.Contains(".r", StringComparison.Ordinal);
        var out_ = new List<Release>();
        Release? cur = null;
        foreach (var raw in changelog.Split('\n'))
        {
            var line = Plain(raw).TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                // "0.2.0 — date", "v0.2.0", "[0.2.0]": the version.
                var version = line[3..].Trim().Split(' ', 2)[0].Trim('[', ']').TrimStart('v');
                if (version == mine) break;
                // Newer than what the update brings (the changelog is the channel's newest).
                var skip = version == "Unreleased" ? !edge : Newer(version, to);
                cur = skip ? null : new Release(version, []);
                if (cur != null) out_.Add(cur);
                if (skip) cur = new Release("", []); // swallow its items
            }
            else if (cur == null) continue;
            else if (line.StartsWith("- ", StringComparison.Ordinal)) cur.Items.Add(line[2..].Trim());
            else if (line.StartsWith("  ", StringComparison.Ordinal) && line.Trim() != "" && cur.Items.Count > 0)
                cur.Items[^1] += " " + line.Trim();
        }
        out_.RemoveAll(r => r.Items.Count == 0);
        return out_;
    }

    /// <summary>Whether a is a newer X.Y.Z than b (anything not one is not newer).</summary>
    static bool Newer(string a, string b)
    {
        static int[]? Parse(string v) => v.Split('.') is { Length: 3 } p && p.All(x => int.TryParse(x, out _)) ? p.Select(int.Parse).ToArray() : null;
        if (Parse(a) is not { } x || Parse(b) is not { } y) return false;
        for (var i = 0; i < 3; i++) if (x[i] != y[i]) return x[i] > y[i];
        return false;
    }

    /// <summary>Text from the network for a terminal: no control characters (escape sequences).</summary>
    static string Plain(string s) => new(s.Where(c => !char.IsControl(c) || c == '\t').ToArray());
}
