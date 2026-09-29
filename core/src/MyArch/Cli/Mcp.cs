using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MyArch.Config;
using MyArch.Util;

namespace MyArch.Cli;

/// <summary>
/// `myarch mcp`: the desktop as a Model Context Protocol server (JSON-RPC,
/// one message per line on stdin and stdout), so any agent can read its
/// state and propose changes the way a person would: previewed with the
/// exact diff, applied through myarch apply, undone with undo. It never
/// installs plugins or updates the system: those are the person's calls.
/// Each tool runs the command it's named after, so what an agent does is
/// what `myarch` does, nothing else.
/// </summary>
public static partial class Program
{
    const string McpProtocol = "2025-06-18";

    static readonly string[] McpProtocols = ["2025-06-18", "2025-03-26", "2024-11-05"];

    [GeneratedRegex(@"\u001b\[[0-9;]*[A-Za-z]")]
    private static partial Regex Ansi();

    sealed record Tool(string Name, string Title, string Description, Fields Schema, bool ReadOnly, Func<JsonElement, string[]> Args, Func<string[], int> Run);

    static Fields Obj(Fields properties, params string[] required)
    {
        var f = new Fields { { "type", "object" }, { "properties", properties } };
        if (required.Length > 0) f.Add("required", required.ToList());
        f.Add("additionalProperties", false);
        return f;
    }

    static Fields Prop(string type, string description) => new() { { "type", type }, { "description", description } };

    static readonly Fields ChangeSchema = Obj(new Fields
    {
        { "theme", Prop("string", "switch to this theme (see the themes tool)") },
        { "accent", Prop("string", "accent color #rrggbb, or \"theme\" for the theme's own") },
        { "set", new Fields { { "type", "object" }, { "description", "plugin settings to set: {\"plugin.key\": value}" } } },
        { "reset", new Fields { { "type", "array" }, { "items", Prop("string", "plugin.key") }, { "description", "settings back to the plugin's default" } } },
        { "enable", new Fields { { "type", "array" }, { "items", Prop("string", "plugin id") }, { "description", "plugins to enable" } } },
        { "disable", new Fields { { "type", "array" }, { "items", Prop("string", "plugin id") }, { "description", "plugins to disable" } } },
    });

    static List<Tool> Tools() =>
    [
        new("status", "Desktop status",
            "The whole state as JSON: theme, language, plugins (with origin), generated files that differ from what myarch wrote, updates, what can be undone, problems. Read this first.",
            Obj(new Fields { { "checks", Prop("boolean", "also run every plugin's health checks (they run commands)") } }), true,
            a => a.TryGetProperty("checks", out var c) && c.ValueKind == JsonValueKind.True ? ["--json", "--checks"] : ["--json"], CmdStatus),
        new("doctor", "Health checks", "Run every plugin's health checks; JSON results with each failure's output.",
            Obj([]), true, _ => ["--json"], CmdDoctor),
        new("themes", "Themes", "Every theme with its colors, suggested accents and contrast problems (JSON).",
            Obj([]), true, _ => ["--json"], CmdThemes),
        new("plugins", "Plugins", "Without id: every plugin and its state. With id: what it needs and does (requirements, settings with their values, files, commands).",
            Obj(new Fields { { "id", Prop("string", "a plugin id") } }), true,
            a => a.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } s ? ["show", s] : ["list"], CmdPlugins),
        new("coverage", "Theme coverage", "Installed apps and whether the theme reaches them (JSON).",
            Obj([]), true, _ => ["--json"], CmdCoverage),
        new("history", "History", "Past system updates, and the applies that can be undone.",
            Obj([]), true, _ => [], _ => { CmdHistory(); Console.WriteLine(); return CmdUndo(["--list"]); }),
        new("preview_change", "Preview a change",
            "What a change would do, without doing it: the plan and the exact unified diff of every file it would write. Show it to the person before apply_change.",
            ChangeSchema, true, a => ["--dry-run", "--diff", .. ChangeArgs(a)], CmdApply),
        new("apply_change", "Apply a change",
            "Apply a change (theme, accent, plugin settings that are numbers or switches, enabling or disabling plugins) and write the files. Undoable with undo (the result gives the id). Preview it first. Text settings (commands, keys, formats) are the person's: tell them the command.",
            ChangeSchema, false, a => ["--changes-only", .. ChangeArgs(a)], CmdApply),
        new("undo", "Undo an apply",
            "Put back what the last apply changed (files and config.toml); files changed since are left as they are. Pass the id apply_change gave, so a later change of the person's isn't undone instead.",
            Obj(new Fields { { "id", Prop("string", "the apply to undo (apply_change's result says it); it must be the last one") } }), false,
            a => a.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } s ? ["-y", "--id=" + s] : ["-y"], CmdUndo),
    ];

    /// <summary>A change's JSON as apply's flags.</summary>
    static string[] ChangeArgs(JsonElement a)
    {
        var args = new List<string>();
        foreach (var p in a.EnumerateObject())
        {
            switch (p.Name)
            {
                case "theme" or "accent":
                    args.Add($"--{p.Name}={p.Value.GetString()}");
                    break;
                case "set":
                    foreach (var kv in p.Value.EnumerateObject())
                    {
                        // Text can be a command, a key binding, anything a template puts in
                        // code: an agent sets numbers and switches; text is the person's.
                        if (kv.Value.ValueKind == JsonValueKind.String ||
                            (kv.Value.ValueKind == JsonValueKind.Array && kv.Value.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String)))
                            throw new MyArchException($"{kv.Name}: text settings can hold commands, so they're the person's to set: " +
                                $"myarch apply --set {kv.Name}={TomlValue(kv.Value)}");
                        args.Add($"--set={kv.Name}={TomlValue(kv.Value)}");
                    }
                    break;
                case "reset" or "enable" or "disable":
                    foreach (var v in p.Value.EnumerateArray()) args.Add($"--{p.Name}={v.GetString()}");
                    break;
                default:
                    throw new MyArchException($"unknown argument {p.Name}");
            }
        }
        return [.. args];
    }

    static string TomlValue(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.String => TomlWriter.Str(v.GetString()!),
        JsonValueKind.Array => "[" + string.Join(", ", v.EnumerateArray().Select(TomlValue)) + "]",
        _ => throw new MyArchException($"a setting can't be {v.ValueKind}"),
    };

    static int CmdMcp(string[] args)
    {
        new Flags("mcp").Parse(args);
        Flags.Throw = true;
        var proto = new StreamWriter(Console.OpenStandardOutput(), Files.Utf8) { AutoFlush = true, NewLine = "\n" };
        var input = new StreamReader(Console.OpenStandardInput(), Files.Utf8);
        // Nothing but the protocol reaches stdout.
        Console.SetOut(TextWriter.Null);
        var tools = Tools();
        string? line;
        while ((line = input.ReadLine()) != null)
        {
            if (line.Trim() == "") continue;
            string? reply;
            try
            {
                reply = Handle(line, tools);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // A request shaped wrong (a number where a name goes): an error, not the end.
                reply = RpcError(IdOf(line), -32600, "invalid request: " + e.Message);
            }
            if (reply != null) proto.WriteLine(reply);
        }
        return 0;
    }

    internal static string? Handle(string line)
    {
        try
        {
            return Handle(line, Tools());
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return RpcError(IdOf(line), -32600, "invalid request: " + e.Message);
        }
    }

    static string? Handle(string line, List<Tool> tools)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException e)
        {
            return RpcError(new RawJson("null"), -32700, "parse error: " + e.Message);
        }
        using (doc)
        {
            var req = doc.RootElement;
            if (req.ValueKind != JsonValueKind.Object || !req.TryGetProperty("method", out var m) || m.GetString() is not { } method)
                return RpcError(new RawJson("null"), -32600, "invalid request");
            // A notification (no id) gets no answer.
            if (!req.TryGetProperty("id", out var idEl)) return null;
            var id = new RawJson(idEl.GetRawText());
            var p = req.TryGetProperty("params", out var pe) ? pe : default;
            switch (method)
            {
                case "initialize":
                    var asked = p.ValueKind == JsonValueKind.Object && p.TryGetProperty("protocolVersion", out var pv) ? pv.GetString() : null;
                    return RpcResult(id, new Fields
                    {
                        { "protocolVersion", asked != null && McpProtocols.Contains(asked) ? asked : McpProtocol },
                        { "capabilities", new Fields { { "tools", new Fields { { "listChanged", false } } } } },
                        { "serverInfo", new Fields { { "name", "myarch" }, { "version", "1" } } },
                        {
                            "instructions",
                            "myarch generates this desktop's configuration (Hyprland, Quickshell, app themes) from a theme and plugins. " +
                            "Read status first. To change something, call preview_change and show the person the diff, then apply_change; " +
                            "every apply can be reverted with undo. Never edit generated files by hand: change the theme or plugin settings instead."
                        },
                    });
                case "ping":
                    return RpcResult(id, new Fields());
                case "tools/list":
                    return RpcResult(id, new Fields
                    {
                        {
                            "tools", tools.Select(t => new Fields
                            {
                                { "name", t.Name },
                                { "title", t.Title },
                                { "description", t.Description },
                                { "inputSchema", t.Schema },
                                {
                                    "annotations", new Fields
                                    {
                                        { "readOnlyHint", t.ReadOnly },
                                        { "destructiveHint", !t.ReadOnly },
                                        { "idempotentHint", t.ReadOnly },
                                        { "openWorldHint", false },
                                    }
                                },
                            }).ToList()
                        },
                    });
                case "tools/call":
                    var name = p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var tool = tools.FirstOrDefault(t => t.Name == name);
                    if (tool == null) return RpcError(id, -32602, $"no tool {name}");
                    var arguments = p.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object
                        ? a : JsonDocument.Parse("{}").RootElement;
                    var (text, ok) = CallTool(tool, arguments);
                    return RpcResult(id, new Fields
                    {
                        { "content", new List<Fields> { new() { { "type", "text" }, { "text", text } } } },
                        { "isError", !ok },
                    });
                default:
                    return RpcError(id, -32601, $"no method {method}");
            }
        }
    }

    /// <summary>Runs a tool's command, with what it prints (and its errors) as the result.</summary>
    static (string Text, bool Ok) CallTool(Tool tool, JsonElement arguments)
    {
        var captured = new StringWriter { NewLine = "\n" };
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        Console.SetOut(captured);
        Console.SetError(captured);
        var ok = false;
        try
        {
            ok = tool.Run(tool.Args(arguments)) == 0;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Whatever went wrong is the tool's error, never the server's end.
            captured.WriteLine("myarch: " + e.Message);
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
        return (Ansi().Replace(captured.ToString(), "").TrimEnd(), ok);
    }

    static RawJson IdOf(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("id", out var id) &&
                id.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                return new RawJson(id.GetRawText());
        }
        catch (JsonException) { }
        return new RawJson("null");
    }

    static string RpcResult(RawJson id, Fields result) =>
        GoJson.Marshal(new Fields { { "jsonrpc", "2.0" }, { "id", id }, { "result", result } });

    static string RpcError(RawJson id, int code, string message) =>
        GoJson.Marshal(new Fields { { "jsonrpc", "2.0" }, { "id", id }, { "error", new Fields { { "code", code }, { "message", message } } } });
}
