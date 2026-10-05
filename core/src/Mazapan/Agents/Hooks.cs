using System.Text.Json;
using System.Text.Json.Nodes;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>
/// Claude Code's hooks that tell Mazapan what each session is doing: one
/// entry per event in each configuration's settings.json, the person's own
/// hooks left as they are. Recognized by their command, so installing
/// twice adds nothing and removing takes only these. The first change keeps
/// a copy of the file beside it (settings.json.mazapan-backup).
/// </summary>
public static class Hooks
{
    // Never exit 2 (Claude Code takes it as "block this"): a mazapan without
    // `agents` (rolled back, an older one sharing the file) answers usage, 2.
    public const string Command = "/usr/bin/mazapan agents event claude 2>/dev/null || true";
    // PostToolUse: back to work once a permission is given (no other event says it).
    static readonly string[] Events = ["SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "Notification", "Stop", "SessionEnd"];

    /// <summary>Every settings.json the Claude configurations use, once each (two configurations can share one through a symlink).</summary>
    public static List<string> SettingsFiles(IEnumerable<Account> accounts)
    {
        var out_ = new List<string>();
        foreach (var a in accounts)
        {
            var f = Path.Join(a.ConfigDir, "settings.json");
            var real = File.Exists(f) ? Paths.Real(f) ?? f : f;
            if (!out_.Contains(real)) out_.Add(real);
        }
        return out_;
    }

    /// <summary>Puts them in (install) or takes them out; says each file that changed.</summary>
    public static List<string> Set(IEnumerable<Account> accounts, bool install) => Change(accounts, (_, json) => Apply(json, install));

    /// <summary>Each settings.json changed by that (null: as it is); says each file that changed.</summary>
    static List<string> Change(IEnumerable<Account> accounts, Func<string, string, string?> apply)
    {
        var changed = new List<string>();
        // Every file read first: one that doesn't read stops it before any is written.
        var updates = new List<(string File, string Updated)>();
        foreach (var file in SettingsFiles(accounts))
        {
            var text = File.Exists(file) ? File.ReadAllText(file) : "{}";
            string? updated;
            try { updated = apply(file, text); }
            catch (JsonException e) { throw new MazapanException($"{file} isn't valid JSON ({e.Message}): nothing changed"); }
            if (updated != null) updates.Add((file, updated));
        }
        foreach (var (file, updated) in updates)
        {
            var backup = file + ".mazapan-backup";
            if (File.Exists(file) && !File.Exists(backup)) File.Copy(file, backup);
            Directory.CreateDirectory(Paths.Dir(file));
            Files.WriteAtomic(file, updated);
            changed.Add(file);
        }
        return changed;
    }

    /// <summary>The settings with these hooks in or out, or null when nothing changes.</summary>
    public static string? Apply(string json, bool install)
    {
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new MazapanException("settings.json isn't a JSON object");
        var before = root.ToJsonString();
        if (root["hooks"] is not JsonObject hooks)
        {
            if (!install) return null;
            root["hooks"] = hooks = new JsonObject();
        }
        foreach (var ev in Events)
        {
            var list = hooks[ev] as JsonArray;
            if (list != null)
                for (var i = list.Count - 1; i >= 0; i--)
                    if (Ours(list[i])) list.RemoveAt(i);
            if (install)
            {
                if (list == null) hooks[ev] = list = new JsonArray();
                list.Add((JsonNode)new JsonObject { ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = Command }) });
            }
            else if (list != null && list.Count == 0) hooks.Remove(ev);
        }
        if (!install && hooks.Count == 0) root.Remove("hooks");
        // The person's characters as they wrote them (no \u0026 for &).
        var after = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return root.ToJsonString() == before ? null : after + "\n";
    }

    // --- OpenTelemetry: Claude Code's metrics to Mazapan's receiver ---------------------

    // The general variables, not the metrics' own: a program Claude runs
    // inherits them, and one Aspire starts gets its own endpoint, which
    // only a metrics-specific one would override.
    static Dictionary<string, string> TelemetryEnv(int port) => new()
    {
        ["CLAUDE_CODE_ENABLE_TELEMETRY"] = "1",
        ["OTEL_METRICS_EXPORTER"] = "otlp",
        ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/json",
        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = $"http://127.0.0.1:{port}",
        ["OTEL_METRICS_INCLUDE_REPOSITORY"] = "1",
        // The receiver's secret: only these agents may post there.
        ["OTEL_EXPORTER_OTLP_HEADERS"] = Otel.HeaderValue(Otel.Token()),
    };

    public static List<string> SetTelemetry(IEnumerable<Account> accounts, bool on, int port, List<string> theirs) =>
        Change(accounts, (file, json) =>
        {
            var r = ApplyTelemetry(json, on, port, out var own);
            if (own) theirs.Add(file);
            return r;
        });

    // Mazapan's mark beside them: without it, telemetry already there is the person's own.
    const string Mark = "MAZAPAN_OTEL";

    public static string? ApplyTelemetry(string json, bool on, int port, out bool theirOwn)
    {
        theirOwn = false;
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new MazapanException("settings.json isn't a JSON object");
        var before = root.ToJsonString();
        var env = root["env"] as JsonObject;
        string? Get(string k) => env?[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var ours = Get(Mark) == "1";
        string[] watched = ["CLAUDE_CODE_ENABLE_TELEMETRY", "OTEL_METRICS_EXPORTER", "OTEL_EXPORTER_OTLP_PROTOCOL", "OTEL_EXPORTER_OTLP_ENDPOINT",
            "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL", "OTEL_METRICS_INCLUDE_REPOSITORY", "OTEL_EXPORTER_OTLP_HEADERS"];
        // Telemetry set up by the person (their own collector, even on this computer): theirs.
        if (!ours && watched.Any(k => Get(k) != null))
        {
            theirOwn = true;
            return null;
        }
        if (on)
        {
            if (env == null) root["env"] = env = new JsonObject();
            foreach (var (k, v) in TelemetryEnv(port)) env[k] = v;
            env[Mark] = "1";
        }
        else if (ours && env != null)
        {
            // Only what is still as Mazapan wrote it (the endpoint: on this computer).
            foreach (var (k, v) in TelemetryEnv(port))
                if (Get(k) is { } now && (now == v || k == "OTEL_EXPORTER_OTLP_ENDPOINT" && now.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)))
                    env.Remove(k);
            env.Remove(Mark);
            if (env.Count == 0) root.Remove("env");
        }
        if (root.ToJsonString() == before) return null;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    /// <summary>A group of hooks that is this one (by its command).</summary>
    static bool Ours(JsonNode? group) =>
        group?["hooks"] is JsonArray hs && hs.Any(h => h?["command"]?.GetValueKind() == JsonValueKind.String
            && h["command"]!.GetValue<string>().Contains("mazapan agents event", StringComparison.Ordinal));

    /// <summary>Whether a settings.json has them.</summary>
    public static bool Installed(string json)
    {
        try
        {
            // This command exactly: an older one is put in again.
            return JsonNode.Parse(json)?["hooks"] is JsonObject h && Events.All(e => h[e] is JsonArray l && l.Any(g => g?["hooks"] is JsonArray hs
                && hs.Any(x => x?["command"]?.GetValueKind() == JsonValueKind.String && x["command"]!.GetValue<string>() == Command)));
        }
        catch (JsonException) { return false; }
    }
}
