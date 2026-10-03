using System.Text.Json;
using System.Text.Json.Nodes;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>
/// Claude Code's hooks that tell Mazapán what each session is doing: one
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
    public static List<string> Set(IEnumerable<Account> accounts, bool install)
    {
        var changed = new List<string>();
        // Every file read first: one that doesn't read stops it before any is written.
        var updates = new List<(string File, string Updated)>();
        foreach (var file in SettingsFiles(accounts))
        {
            var text = File.Exists(file) ? File.ReadAllText(file) : "{}";
            string? updated;
            try { updated = Apply(text, install); }
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
