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
    public const string Command = "/usr/bin/mazapan agents event claude";
    static readonly string[] Events = ["SessionStart", "UserPromptSubmit", "PreToolUse", "Notification", "Stop", "SessionEnd"];

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
        foreach (var file in SettingsFiles(accounts))
        {
            var text = File.Exists(file) ? File.ReadAllText(file) : "{}";
            var updated = Apply(text, install);
            if (updated == null) continue;
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
        var after = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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
            return JsonNode.Parse(json)?["hooks"] is JsonObject h && Events.All(e => h[e] is JsonArray l && l.Any(Ours));
        }
        catch (JsonException) { return false; }
    }
}
