using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>One number an agent sent: which metric, its kind (added, removed, user, cli…), for which session, account and repository.</summary>
public sealed record OtelPoint(long TimeMs, string Metric, string Type, string Model, string Session, string Email, string Repo,
    double Value, bool Cumulative, long StartMs);

/// <summary>What OpenTelemetry says a day held: lines written and taken out, commits, pull requests, time, sessions; and lines by repository.</summary>
public sealed record OtelDay(string Day, double LinesAdded, double LinesRemoved, double Commits, double PullRequests,
    double ActiveUserSeconds, double ActiveCliSeconds, double Sessions, Dictionary<string, double> LinesByRepo);

/// <summary>
/// A small OpenTelemetry receiver on this computer: agents that speak it
/// (Claude Code) send their metrics here (OTLP over HTTP, JSON), never
/// anywhere else. Metrics only: no log or event, so never a prompt. What it
/// keeps: the metrics this desktop shows (lines, commits, pull requests,
/// active time, sessions), a line each, in ~/.local/state/mazapan/agents,
/// readable by the person alone. Started by systemd when an agent first
/// connects (a socket), gone after a while idle.
/// </summary>
public static class Otel
{
    public const int DefaultPort = 47318;

    static readonly string[] Kept =
    [
        "claude_code.lines_of_code.count", "claude_code.commit.count", "claude_code.pull_request.count",
        "claude_code.active_time.total", "claude_code.session.count",
    ];

    static string Home => Environment.GetEnvironmentVariable("HOME") ?? "";
    public static string StoreFile => Paths.Join(Environment.GetEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } s ? s : Paths.Join(Home, ".local/state"), "mazapan/agents/otel.jsonl");

    // --- receiving -----------------------------------------------------------------------

    /// <summary>
    /// Serves until idle that long: the socket systemd passes (LISTEN_FDS),
    /// or one of its own on 127.0.0.1:port. One request at a time: an agent
    /// sends a handful a minute.
    /// </summary>
    public static void Serve(int port, TimeSpan idle)
    {
        Socket listener;
        if (Environment.GetEnvironmentVariable("LISTEN_FDS") == "1" && Environment.GetEnvironmentVariable("LISTEN_PID") == Environment.ProcessId.ToString(CultureInfo.InvariantCulture))
            listener = new Socket(new SafeSocketHandle((IntPtr)3, ownsHandle: true));
        else
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listener.Listen(16);
        }
        Prune();
        using (listener)
            while (true)
            {
                using var cts = new CancellationTokenSource(idle);
                Socket client;
                try { client = listener.AcceptAsync(cts.Token).AsTask().GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { return; }
                using (client)
                {
                    client.ReceiveTimeout = 10000;
                    client.SendTimeout = 10000;
                    try { Handle(client); }
                    catch (Exception e) when (e is IOException or SocketException or JsonException or InvalidDataException or FormatException or InvalidOperationException) { }
                }
            }
    }

    const int MaxBody = 8 << 20;

    static void Handle(Socket client)
    {
        using var stream = new NetworkStream(client);
        var (method, path, headers) = ReadHead(stream);
        string Reply(int code, string why) => $"HTTP/1.1 {code} {why}\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{{}}";
        void Say(string s) { var b = Encoding.ASCII.GetBytes(s); stream.Write(b, 0, b.Length); }
        // Traces and logs taken and dropped: a program an agent runs inherits
        // the endpoint, and an error for each export would fill its log.
        if (method == "POST" && path.Split('?')[0] is "/v1/traces" or "/v1/logs") { Say(Reply(200, "OK")); return; }
        if (method != "POST" || path.Split('?')[0] != "/v1/metrics") { Say(Reply(404, "Not Found")); return; }
        if (headers.GetValueOrDefault("content-type", "").Split(';')[0].Trim() != "application/json") { Say(Reply(415, "Unsupported Media Type")); return; }
        byte[] body;
        if (headers.GetValueOrDefault("transfer-encoding", "").Contains("chunked", StringComparison.OrdinalIgnoreCase)) body = ReadChunked(stream);
        else
        {
            if (!int.TryParse(headers.GetValueOrDefault("content-length", "0"), out var len) || len < 0 || len > MaxBody) { Say(Reply(413, "Payload Too Large")); return; }
            body = new byte[len];
            stream.ReadExactly(body);
        }
        if (headers.GetValueOrDefault("content-encoding", "") == "gzip")
        {
            using var gz = new GZipStream(new MemoryStream(body), CompressionMode.Decompress);
            using var ms = new MemoryStream();
            var buf = new byte[81920];
            int n;
            while ((n = gz.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > MaxBody) throw new InvalidDataException("too big");
            }
            body = ms.ToArray();
        }
        using var doc = JsonDocument.Parse(body);
        Append(Parse(doc.RootElement));
        Say(Reply(200, "OK"));
    }

    static (string Method, string Path, Dictionary<string, string> Headers) ReadHead(Stream s)
    {
        var sb = new StringBuilder();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int b;
        while ((b = s.ReadByte()) >= 0)
        {
            sb.Append((char)b);
            if (sb.Length > 16384) throw new InvalidDataException("head too long");
            if (sb.Length >= 4 && sb[^1] == '\n' && sb[^2] == '\r' && sb[^3] == '\n' && sb[^4] == '\r') break;
        }
        var lines = sb.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) throw new InvalidDataException("no request");
        var first = lines[0].Split(' ');
        foreach (var l in lines.Skip(1))
            if (l.IndexOf(':') is var i and > 0) headers[l[..i].Trim()] = l[(i + 1)..].Trim();
        return (first[0], first.Length > 1 ? first[1] : "", headers);
    }

    static byte[] ReadChunked(Stream s)
    {
        using var ms = new MemoryStream();
        while (true)
        {
            var line = new StringBuilder();
            int b;
            while ((b = s.ReadByte()) >= 0 && b != '\n') if (b != '\r') line.Append((char)b);
            var size = int.Parse(line.ToString().Split(';')[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (size == 0) { while ((b = s.ReadByte()) >= 0 && b != '\n') { } return ms.ToArray(); }
            if (ms.Length + size > MaxBody) throw new InvalidDataException("too big");
            var chunk = new byte[size];
            s.ReadExactly(chunk);
            ms.Write(chunk);
            s.ReadByte(); s.ReadByte(); // its \r\n
        }
    }

    // --- reading what was sent -----------------------------------------------------------

    /// <summary>The kept metrics in an OTLP/JSON export: sums (delta or cumulative) and gauges.</summary>
    public static List<OtelPoint> Parse(JsonElement root)
    {
        var out_ = new List<OtelPoint>();
        if (!root.TryGetProperty("resourceMetrics", out var rms) || rms.ValueKind != JsonValueKind.Array) return out_;
        foreach (var rm in rms.EnumerateArray())
        {
            var resource = rm.TryGetProperty("resource", out var res) ? Attributes(res) : [];
            if (!rm.TryGetProperty("scopeMetrics", out var sms) || sms.ValueKind != JsonValueKind.Array) continue;
            foreach (var sm in sms.EnumerateArray())
            {
                if (!sm.TryGetProperty("metrics", out var ms) || ms.ValueKind != JsonValueKind.Array) continue;
                foreach (var m in ms.EnumerateArray())
                {
                    var name = Discovery.Str(m, "name");
                    if (!Kept.Contains(name)) continue;
                    JsonElement data;
                    var cumulative = false;
                    if (m.TryGetProperty("sum", out data))
                        cumulative = data.TryGetProperty("aggregationTemporality", out var t) && t.ValueKind == JsonValueKind.Number && t.GetInt32() == 2;
                    else if (!m.TryGetProperty("gauge", out data)) continue;
                    if (!data.TryGetProperty("dataPoints", out var dps) || dps.ValueKind != JsonValueKind.Array) continue;
                    foreach (var dp in dps.EnumerateArray())
                    {
                        var a = Attributes(dp);
                        foreach (var (k, v) in resource) a.TryAdd(k, v);
                        double value = dp.TryGetProperty("asDouble", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble()
                            : dp.TryGetProperty("asInt", out var n) ? (n.ValueKind == JsonValueKind.Number ? n.GetInt64() : long.Parse(n.GetString() ?? "0", CultureInfo.InvariantCulture))
                            : 0;
                        var repo = a.GetValueOrDefault("vcs.repository.name") ?? a.GetValueOrDefault("vcs.repository.url.full") ?? "";
                        out_.Add(new(Nano(dp, "timeUnixNano"), name, a.GetValueOrDefault("type") ?? "", a.GetValueOrDefault("model") ?? "",
                            a.GetValueOrDefault("session.id") ?? "", a.GetValueOrDefault("user.email") ?? "", Short(repo), value, cumulative, Nano(dp, "startTimeUnixNano")));
                    }
                }
            }
        }
        return out_;
    }

    /// <summary>A repository by its last part ("…/shop-api.git" is shop-api).</summary>
    static string Short(string repo) => repo.TrimEnd('/') is var r && r.LastIndexOfAny(['/', ':']) is var i and >= 0 ? r[(i + 1)..].Replace(".git", "", StringComparison.Ordinal) : repo;

    static long Nano(JsonElement dp, string key) =>
        dp.TryGetProperty(key, out var t) ? (t.ValueKind == JsonValueKind.Number ? t.GetInt64() : long.TryParse(t.GetString(), out var x) ? x : 0) / 1_000_000 : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    static Dictionary<string, string> Attributes(JsonElement e)
    {
        var out_ = new Dictionary<string, string>();
        if (!e.TryGetProperty("attributes", out var attrs) || attrs.ValueKind != JsonValueKind.Array) return out_;
        foreach (var kv in attrs.EnumerateArray())
        {
            var key = Discovery.Str(kv, "key");
            if (key == "" || !kv.TryGetProperty("value", out var v) || v.ValueKind != JsonValueKind.Object) continue;
            foreach (var p in v.EnumerateObject())
                out_[key] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
        }
        return out_;
    }

    // --- keeping it --------------------------------------------------------------------

    static void Append(List<OtelPoint> points)
    {
        if (points.Count == 0) return;
        Directory.CreateDirectory(Paths.Dir(StoreFile));
        var sb = new StringBuilder();
        foreach (var p in points)
            sb.Append(new JsonObject
            {
                ["t"] = p.TimeMs, ["m"] = p.Metric, ["type"] = p.Type, ["model"] = p.Model, ["s"] = p.Session, ["email"] = p.Email,
                ["repo"] = p.Repo, ["v"] = p.Value, ["c"] = p.Cumulative, ["st"] = p.StartMs,
            }.ToJsonString()).Append('\n');
        var fresh = !File.Exists(StoreFile);
        File.AppendAllText(StoreFile, sb.ToString());
        if (fresh) File.SetUnixFileMode(StoreFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>Only the last 400 days kept.</summary>
    static void Prune()
    {
        try
        {
            if (!File.Exists(StoreFile) || new FileInfo(StoreFile).Length < 4 << 20) return;
            var since = DateTimeOffset.UtcNow.AddDays(-400).ToUnixTimeMilliseconds();
            var keep = Load().Where(p => p.TimeMs >= since).ToList();
            File.Delete(StoreFile);
            Append(keep);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static List<OtelPoint> Load()
    {
        var out_ = new List<OtelPoint>();
        if (!File.Exists(StoreFile)) return out_;
        foreach (var line in File.ReadLines(StoreFile))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                out_.Add(new(Usage.Num(r, "t"), Discovery.Str(r, "m"), Discovery.Str(r, "type"), Discovery.Str(r, "model"), Discovery.Str(r, "s"),
                    Discovery.Str(r, "email"), Discovery.Str(r, "repo"), r.GetProperty("v").GetDouble(),
                    r.TryGetProperty("c", out var c) && c.ValueKind == JsonValueKind.True, Usage.Num(r, "st")));
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
        }
        return out_;
    }

    /// <summary>
    /// By local day since then: deltas summed; a cumulative series (one
    /// start, one session, one kind) counted by its last value.
    /// </summary>
    public static List<OtelDay> Days(IEnumerable<OtelPoint> points, DateTimeOffset since)
    {
        var sinceMs = since.ToUnixTimeMilliseconds();
        var deltas = new List<OtelPoint>();
        foreach (var p in points.Where(p => p.TimeMs >= sinceMs && !p.Cumulative)) deltas.Add(p);
        foreach (var g in points.Where(p => p.TimeMs >= sinceMs && p.Cumulative).GroupBy(p => (p.Metric, p.Type, p.Model, p.Session, p.StartMs)))
            deltas.Add(g.MaxBy(p => p.TimeMs)!);
        return deltas.GroupBy(p => DateTimeOffset.FromUnixTimeMilliseconds(p.TimeMs).ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                double Sum(string m, string type = "") => g.Where(p => p.Metric == m && (type == "" || p.Type == type)).Sum(p => p.Value);
                var byRepo = g.Where(p => p.Metric == "claude_code.lines_of_code.count" && p.Repo != "")
                    .GroupBy(p => p.Repo).ToDictionary(r => r.Key, r => r.Sum(p => p.Value));
                return new OtelDay(g.Key, Sum("claude_code.lines_of_code.count", "added"), Sum("claude_code.lines_of_code.count", "removed"),
                    Sum("claude_code.commit.count"), Sum("claude_code.pull_request.count"),
                    Sum("claude_code.active_time.total", "user"), Sum("claude_code.active_time.total", "cli"),
                    Sum("claude_code.session.count"), byRepo);
            }).ToList();
    }

    public static Fields Json(OtelDay d) => new()
    {
        { "day", d.Day }, { "lines_added", d.LinesAdded }, { "lines_removed", d.LinesRemoved }, { "commits", d.Commits },
        { "pull_requests", d.PullRequests }, { "active_user_s", Math.Round(d.ActiveUserSeconds) }, { "active_cli_s", Math.Round(d.ActiveCliSeconds) },
        { "sessions", d.Sessions }, { "lines_by_repo", d.LinesByRepo.OrderByDescending(x => x.Value).Select(x => new Fields { { "repo", x.Key }, { "lines", x.Value } }).ToList() },
    };
}
