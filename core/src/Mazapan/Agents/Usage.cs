using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>
/// One answer an agent got from a model: whose, which model, when, for what
/// project, and the tokens it took. Numbers only: no prompt, no answer.
/// Cost is what the agent itself reported (opencode, pi), null when it
/// doesn't say (Claude Code, Codex): the price table works one out.
/// </summary>
public sealed record Entry(string Agent, string Source, string Provider, string Model, DateTimeOffset Time, string Project, string Session,
    long Input, long Output, long CacheRead, long CacheWrite, long Reasoning, double? Cost, string Key);

/// <summary>
/// What every agent here has used, read from what each keeps: Claude Code's
/// projects/*.jsonl (each configuration's; a directory two configurations
/// share is read once), pi's and Codex's sessions, opencode's database
/// (read only, through sqlite3). Files already read are kept in a cache by
/// size and time, so only what changed is read again.
/// </summary>
public static class Usage
{
    static string Home => Environment.GetEnvironmentVariable("HOME") ?? "";
    static string CacheDir => Paths.Join(Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } c ? c : Paths.Join(Home, ".cache"), "mazapan/agents");

    public static (List<Entry> Entries, List<string> Problems) Collect(List<Agent> agents, DateTimeOffset since)
    {
        var out_ = new List<Entry>();
        var problems = new List<string>();
        void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                problems.Add($"{what}: {e.Message}");
            }
        }
        foreach (var agent in agents)
            switch (agent.Id)
            {
                case "claude": Try("claude", () => out_.AddRange(Claude(agent.Accounts, since))); break;
                case "pi": Try("pi", () => out_.AddRange(Jsonl("pi", Paths.Join(Home, ".pi/agent/sessions"), since, Pi))); break;
                case "codex": Try("codex", () => out_.AddRange(Jsonl("codex", Paths.Join(Home, ".codex/sessions"), since, Codex))); break;
                case "opencode": Try("opencode", () => out_.AddRange(Opencode(since))); break;
            }
        // The same answer seen twice (a session resumed into a new file): once.
        var seen = new HashSet<string>();
        return (out_.Where(e => e.Key == "" || seen.Add(e.Agent + "|" + e.Key)).ToList(), problems);
    }

    // --- Claude Code ------------------------------------------------------------------------

    /// <summary>
    /// Each configuration's projects directory, once per real directory:
    /// configurations sharing one (a symlink) can't be told apart in it, so
    /// their use is theirs together ("claude+claude-work").
    /// </summary>
    static IEnumerable<Entry> Claude(List<Account> accounts, DateTimeOffset since)
    {
        var byReal = new Dictionary<string, List<string>>();
        foreach (var a in accounts)
        {
            var projects = Path.Join(a.ConfigDir, "projects");
            if (!Directory.Exists(projects)) continue;
            var real = Paths.Real(projects) ?? projects;
            if (!byReal.TryGetValue(real, out var ids)) byReal[real] = ids = [];
            ids.Add(a.Id);
        }
        // A projects directory without a login (~/.claude only): still read.
        var plain = Paths.Join(Home, ".claude/projects");
        if (Directory.Exists(plain) && !byReal.ContainsKey(Paths.Real(plain) ?? plain)) byReal[Paths.Real(plain) ?? plain] = ["claude"];
        foreach (var (dir, ids) in byReal)
            foreach (var e in Jsonl("claude", dir, since, ClaudeLine, string.Join("+", ids)))
                yield return e;
    }

    static Entry? ClaudeLine(string line, string source, Context ctx)
    {
        if (!line.Contains("\"usage\"", StringComparison.Ordinal) || !line.Contains("\"assistant\"", StringComparison.Ordinal)) return null;
        using var doc = JsonDocument.Parse(line);
        var r = doc.RootElement;
        if (Discovery.Str(r, "type") != "assistant" || !r.TryGetProperty("message", out var m) || !m.TryGetProperty("usage", out var u)) return null;
        var model = Discovery.Str(m, "model");
        if (model is "" or "<synthetic>") return null;
        var reasoning = u.TryGetProperty("output_tokens_details", out var d) ? Num(d, "thinking_tokens") : 0;
        return new Entry("claude", source, "anthropic", model, Time(r, "timestamp"), Discovery.Str(r, "cwd"), Discovery.Str(r, "sessionId"),
            Num(u, "input_tokens"), Num(u, "output_tokens"), Num(u, "cache_read_input_tokens"), Num(u, "cache_creation_input_tokens"), reasoning, null,
            Discovery.Str(m, "id") + ":" + Discovery.Str(r, "requestId"));
    }

    // --- pi ---------------------------------------------------------------------------------

    /// <summary>pi: a session's first line says its directory; each answer its provider, model, use and cost.</summary>
    static Entry? Pi(string line, string source, Context ctx)
    {
        using var doc = JsonDocument.Parse(line);
        var r = doc.RootElement;
        if (Discovery.Str(r, "type") == "session") { ctx.Project = Discovery.Str(r, "cwd"); ctx.Session = Discovery.Str(r, "id"); return null; }
        if (!r.TryGetProperty("message", out var m) || !m.TryGetProperty("usage", out var u) || Discovery.Str(m, "role") != "assistant") return null;
        double? cost = u.TryGetProperty("cost", out var c) && c.ValueKind == JsonValueKind.Object && c.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDouble() : null;
        var provider = Discovery.Str(m, "provider");
        return new Entry("pi", provider, provider, Discovery.Str(m, "model"), Time(r, "timestamp"), ctx.Project, ctx.Session,
            Num(u, "input"), Num(u, "output"), Num(u, "cacheRead"), Num(u, "cacheWrite"), 0, cost, Discovery.Str(r, "id"));
    }

    // --- Codex ------------------------------------------------------------------------------

    /// <summary>
    /// Codex: the model from its turn's context, each answer's use from a
    /// token_count event (its last_token_usage; input includes what was
    /// cached).
    /// </summary>
    static Entry? Codex(string line, string source, Context ctx)
    {
        using var doc = JsonDocument.Parse(line);
        var r = doc.RootElement;
        if (!r.TryGetProperty("payload", out var p) || p.ValueKind != JsonValueKind.Object) return null;
        switch (Discovery.Str(r, "type"))
        {
            case "session_meta": ctx.Project = Discovery.Str(p, "cwd"); ctx.Session = Discovery.Str(p, "id"); return null;
            case "turn_context": ctx.Model = Discovery.Str(p, "model"); return null;
        }
        if (Discovery.Str(p, "type") != "token_count" || !p.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object
            || !info.TryGetProperty("last_token_usage", out var u)) return null;
        var cached = Num(u, "cached_input_tokens");
        var ts = Time(r, "timestamp");
        return new Entry("codex", "codex", "openai", ctx.Model, ts, ctx.Project, ctx.Session,
            Math.Max(0, Num(u, "input_tokens") - cached), Num(u, "output_tokens"), cached, 0, Num(u, "reasoning_output_tokens"), null,
            ctx.Session + ":" + ts.ToUnixTimeMilliseconds());
    }

    // --- opencode ---------------------------------------------------------------------------

    /// <summary>
    /// opencode's database, read only, through sqlite3 (one query; its
    /// messages carry provider, model, tokens and the cost opencode worked
    /// out).
    /// </summary>
    static IEnumerable<Entry> Opencode(DateTimeOffset since)
    {
        var db = Paths.Join(Home, ".local/share/opencode/opencode.db");
        if (!File.Exists(db)) return [];
        if (Discovery.Which("sqlite3") is not { } sqlite) throw new InvalidOperationException("sqlite3 isn't installed (package sqlite)");
        var sql = "select m.id, m.time_created, s.directory, m.session_id,"
            + " json_extract(m.data,'$.providerID'), json_extract(m.data,'$.modelID'),"
            + " json_extract(m.data,'$.tokens.input'), json_extract(m.data,'$.tokens.output'),"
            + " json_extract(m.data,'$.tokens.cache.read'), json_extract(m.data,'$.tokens.cache.write'),"
            + " json_extract(m.data,'$.tokens.reasoning'), json_extract(m.data,'$.cost')"
            + " from message m join session s on s.id = m.session_id"
            + $" where json_extract(m.data,'$.role') = 'assistant' and m.time_created >= {since.ToUnixTimeMilliseconds()}";
        var psi = new ProcessStartInfo(sqlite) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-readonly", "-separator", "\t", "-newline", "\n", $"file:{db}?mode=ro", sql }) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var text = proc.StandardOutput.ReadToEnd();
        var err = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0) throw new InvalidOperationException("sqlite3: " + err.Trim());
        var out_ = new List<Entry>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.Split('\t');
            if (f.Length < 12) continue;
            long L(string s) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
            double? cost = double.TryParse(f[11], NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : null;
            out_.Add(new Entry("opencode", f[4], f[4], f[5], DateTimeOffset.FromUnixTimeMilliseconds(L(f[1])), f[2], f[3],
                L(f[6]), L(f[7]), L(f[8]), L(f[9]), L(f[10]), cost, f[0]));
        }
        return out_;
    }

    // --- JSONL files, with their cache -------------------------------------------------------

    sealed class Context
    {
        public string Project = "", Session = "", Model = "";
    }

    /// <summary>
    /// Every .jsonl under a directory: each file read once while it doesn't
    /// change (size and time), its answers kept in a cache (TSV, numbers and
    /// names only, readable by its owner alone).
    /// </summary>
    static IEnumerable<Entry> Jsonl(string agent, string dir, DateTimeOffset since, Func<string, string, Context, Entry?> parse, string source = "")
    {
        if (!Directory.Exists(dir)) return [];
        var cacheFile = Path.Join(CacheDir, agent + "-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(dir)))[..12] + ".tsv");
        var cache = ReadCache(cacheFile);
        var fresh = new Dictionary<string, (long Size, long Mtime, List<Entry> Entries)>();
        var changed = false;
        foreach (var file in Directory.EnumerateFiles(dir, "*.jsonl", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
        {
            var info = new FileInfo(file);
            var rel = Path.GetRelativePath(dir, file);
            var mtime = info.LastWriteTimeUtc.Ticks;
            if (cache.TryGetValue(rel, out var c) && c.Size == info.Length && c.Mtime == mtime)
            {
                fresh[rel] = c;
                continue;
            }
            var ctx = new Context();
            var entries = new List<Entry>();
            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (line.Length == 0) continue;
                    try
                    {
                        if (parse(line, source, ctx) is { } e) entries.Add(e);
                    }
                    // A line half written (the next read has it), or not of the shape known.
                    catch (Exception x) when (x is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { }
                }
            }
            catch (Exception x) when (x is IOException or UnauthorizedAccessException) { continue; } // gone meanwhile
            // One answer, one entry: Claude Code writes a line per part of it
            // (the fullest one's counts kept).
            entries = entries.Where(e => e.Key == "")
                .Concat(entries.Where(e => e.Key != "").GroupBy(e => e.Key).Select(g => g.MaxBy(e => e.Output + e.Input + e.CacheRead + e.CacheWrite)!))
                .OrderBy(e => e.Time).ToList();
            fresh[rel] = (info.Length, mtime, entries);
            changed = true;
        }
        if (changed || fresh.Count != cache.Count) WriteCache(cacheFile, fresh);
        return fresh.Values.SelectMany(v => v.Entries).Where(e => e.Time >= since).Select(e => e with { Source = source != "" ? source : e.Source });
    }

    static string Clean(string s) => s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

    static void WriteCache(string path, Dictionary<string, (long Size, long Mtime, List<Entry> Entries)> files)
    {
        var b = new StringBuilder("mazapan-agents-cache 1\n");
        foreach (var (rel, (size, mtime, entries)) in files)
        {
            b.Append("F\t").Append(Clean(rel)).Append('\t').Append(size).Append('\t').Append(mtime).Append('\n');
            foreach (var e in entries)
                b.Append("E\t").Append(Clean(e.Agent)).Append('\t').Append(Clean(e.Source)).Append('\t').Append(Clean(e.Provider)).Append('\t')
                    .Append(Clean(e.Model)).Append('\t').Append(e.Time.ToUnixTimeMilliseconds()).Append('\t').Append(Clean(e.Project)).Append('\t')
                    .Append(Clean(e.Session)).Append('\t').Append(e.Input).Append('\t').Append(e.Output).Append('\t').Append(e.CacheRead).Append('\t')
                    .Append(e.CacheWrite).Append('\t').Append(e.Reasoning).Append('\t')
                    .Append(e.Cost is { } c ? c.ToString("R", CultureInfo.InvariantCulture) : "").Append('\t').Append(Clean(e.Key)).Append('\n');
        }
        Directory.CreateDirectory(Paths.Dir(path));
        File.SetUnixFileMode(Paths.Dir(path), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Files.WriteAtomic(path, b.ToString());
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    static Dictionary<string, (long Size, long Mtime, List<Entry> Entries)> ReadCache(string path)
    {
        var out_ = new Dictionary<string, (long, long, List<Entry>)>();
        if (!File.Exists(path)) return out_;
        try
        {
            List<Entry>? current = null;
            var first = true;
            foreach (var line in File.ReadLines(path))
            {
                if (first) { first = false; if (line != "mazapan-agents-cache 1") return []; continue; }
                var f = line.Split('\t');
                if (f[0] == "F" && f.Length == 4)
                {
                    current = [];
                    out_[f[1]] = (long.Parse(f[2], CultureInfo.InvariantCulture), long.Parse(f[3], CultureInfo.InvariantCulture), current);
                }
                else if (f[0] == "E" && f.Length == 15 && current != null)
                {
                    long L(int i) => long.Parse(f[i], CultureInfo.InvariantCulture);
                    double? cost = f[13] == "" ? null : double.Parse(f[13], CultureInfo.InvariantCulture);
                    current.Add(new Entry(f[1], f[2], f[3], f[4], DateTimeOffset.FromUnixTimeMilliseconds(L(5)), f[6], f[7], L(8), L(9), L(10), L(11), L(12), cost, f[14]));
                }
            }
        }
        catch (Exception e) when (e is FormatException or OverflowException or IOException)
        {
            return []; // a cache that doesn't read is read again from the files
        }
        return out_;
    }

    internal static long Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    static DateTimeOffset Time(JsonElement e, string name) =>
        DateTimeOffset.TryParse(Discovery.Str(e, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : DateTimeOffset.MinValue;

    // --- for tests --------------------------------------------------------------------------

    internal static Entry? ParseClaude(string line, string source) => ClaudeLine(line, source, new Context());
    internal static List<Entry> ParsePi(IEnumerable<string> lines)
    {
        var ctx = new Context();
        return lines.Select(l => Pi(l, "", ctx)).OfType<Entry>().ToList();
    }
    internal static List<Entry> ParseCodex(IEnumerable<string> lines)
    {
        var ctx = new Context();
        return lines.Select(l => Codex(l, "", ctx)).OfType<Entry>().ToList();
    }
}
