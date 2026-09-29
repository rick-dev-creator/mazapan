using System.Text;
using System.Text.RegularExpressions;
using MyArch.Util;
using Scriban;
using Scriban.Syntax;

namespace MyArch.Plugins;

public sealed partial class Plugin
{
    /// <summary>
    /// Capabilities are what a plugin can do, worked out from its manifest,
    /// not taken from its word: the code it puts where code runs, the
    /// commands it runs and when, the files it writes, the packages it needs.
    /// This is what a person approves when adding a plugin from git, and what
    /// an update can't grow without asking.
    ///
    /// A command is shown as it will run: calls to the plugin's shared
    /// functions (_*.tmpl) inlined and settings replaced by their defaults,
    /// so that changing a function or a default changes the capability too.
    /// Only the theme (colors, fonts) and the places of each targets are left
    /// as template, and neither comes from the plugin.
    /// </summary>
    public List<string> Capabilities()
    {
        try
        {
            return ComputeCapabilities();
        }
        catch (MyArchException e)
        {
            // Load already refused plugins whose commands don't resolve.
            return ["(unreadable: " + e.Message + ")"];
        }
    }

    List<string> ComputeCapabilities()
    {
        var lib = Library.Load(Dir);
        var set = new HashSet<string>();
        void Command(string kind, string src)
        {
            if (src == "") return;
            string cmd;
            try
            {
                cmd = Commands.Resolve(src, lib, Settings);
            }
            catch (MyArchException e)
            {
                throw new MyArchException($"{kind} \"{src}\": {e.Message}");
            }
            set.Add("runs " + kind + ": " + cmd);
        }
        foreach (var t in Targets)
        {
            if (t.System)
            {
                set.Add("full access, as root: writes " + t.Output + (t.Reboot ? " (after a reboot)" : ""));
                Command("as root after writing", t.Reload);
                continue;
            }
            set.Add(FileCapability(t));
            Command("after writing", t.Reload);
        }
        foreach (var c in Checks) Command("as a health check", c.Run);
        foreach (var a in Actions) Command("when you pick it", a.Run);
        foreach (var pkg in Pacman) set.Add("needs the package " + pkg);
        if (Hardware != null) set.Add("for machines with " + Hardware.Describe());
        // The riskiest first: code, then commands, then files, then packages.
        static int Rank(string s) =>
            s.StartsWith("runs ") ? 1 :
            s.StartsWith("writes ") || s.StartsWith("changes ") ? 2 :
            s.StartsWith("needs ") ? 3 :
            s.StartsWith("for machines") ? 4 : 0;
        return set.OrderBy(Rank).ThenBy(s => s, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// FileCapability says what writing a target amounts to. Nearly any config
    /// can run commands (foot's shell=, hyprlock's cmd[], a .desktop's Exec=,
    /// VS Code's terminal profiles, Firefox's autoconfig), so a file is full
    /// access unless it's a kind known to hold only looks.
    /// </summary>
    static string FileCapability(Target t)
    {
        var where = Paths.Clean(t.Output);
        var path = where;
        if (t.Each.Count > 0)
        {
            where = t.Output + " in each of " + string.Join(", ", t.Each);
            // Classify where it really lands: each's places are the markers' folders.
            path = Paths.Join(Paths.Dir(t.Each[0]), t.Output);
        }
        var ext = Path.GetExtension(path);
        if (ext == ".qml" || path.Contains("/quickshell/")) return "full access, code in the shell (QML): " + where;
        if (ext == ".lua" && path.Contains("/hypr/")) return "full access, code in Hyprland (Lua): " + where;
        if (Paths.Base(path) != "user.js" && // Firefox prefs, JavaScript in name only
            (ext is ".lua" or ".vim" or ".py" or ".sh" || ext.Contains("js") || path.Contains("/bin/")))
            return "full access, code: " + where;
        if (Inert(path)) return (t.Merge != "" ? "changes some settings in " : "writes ") + where;
        return "full access, a config that can run commands: " + where;
    }

    /// <summary>Inert: files that only hold looks: stylesheets and color schemes.</summary>
    static bool Inert(string path) =>
        Path.GetExtension(path) is ".css" or ".qss" or ".theme" or ".colors" or ".svg" or ".png" or ".jpg" ||
        path.Contains("/qt6ct/colors/") || path.Contains("/qt5ct/colors/") ||
        path.EndsWith("/gtk-3.0/settings.ini") || path.EndsWith("/gtk-4.0/settings.ini");

    /// <summary>NewCapabilities: the ones in now that weren't approved.</summary>
    public static List<string> NewCapabilities(IEnumerable<string> approved, IEnumerable<string> now)
    {
        var ok = approved.ToHashSet();
        return now.Where(n => !ok.Contains(n)).ToList();
    }
}

/// <summary>
/// A plugin's shared functions: every _*.tmpl holds only func … end blocks,
/// which templates and commands call by name.
/// </summary>
public sealed class Library
{
    /// <summary>The library files, parsed, in order: rendering runs them first.</summary>
    public List<Template> Templates { get; } = [];
    /// <summary>Each function's body, as written, and its statements (later files win).</summary>
    public Dictionary<string, (string Source, ScriptFunction Function)> Functions { get; } = [];

    public static Library Load(string dir)
    {
        var lib = new Library();
        if (!Directory.Exists(dir)) return lib;
        foreach (var f in Directory.GetFiles(dir, "_*.tmpl").Order(StringComparer.Ordinal))
        {
            string src;
            try
            {
                src = File.ReadAllText(f);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new MyArchException($"{Paths.Base(f)}: {e.Message}");
            }
            var t = Tmpl.Parse(src, f);
            foreach (var s in t.Page!.Body!.Statements)
            {
                switch (s)
                {
                    case ScriptEscapeStatement:
                        break;
                    case ScriptRawStatement r when string.IsNullOrWhiteSpace(r.Text.ToString()):
                        break;
                    case ScriptFunction fn when fn.NameOrDoToken is ScriptVariableGlobal name:
                        lib.Functions[name.Name] = (src, fn);
                        break;
                    default:
                        throw new MyArchException($"{Paths.Base(f)}:{s.Span.Start.Line + 1}: a _*.tmpl file holds only func … end blocks");
                }
            }
            lib.Templates.Add(t);
        }
        return lib;
    }
}

/// <summary>
/// Commands written out as they will run (see Plugin.Capabilities). A
/// command is a small language, on purpose: text, and {{ … }} holding
/// literals, the data (theme.*, settings.*, home, plugin, lang, lang_code,
/// place), $locals, operators and myarch's pure functions (c, hex, quote…);
/// if/else; and a shared function (_*.tmpl) called on its own, shown as its
/// body. Anything else (this, object.*, for, capture, several statements in
/// one {{ }}, translated text…) is refused: what can't be shown can't be
/// approved.
/// </summary>
public static partial class Commands
{
    static readonly HashSet<string> Data = ["theme", "settings", "home", "plugin", "lang", "lang_code", "place", "machine"];

    static readonly HashSet<string> Pure =
        ["c", "hex", "rgb", "rgba", "cssa", "csv", "speed", "spring", "num", "pct", "quote", "lq", "inline", "base", "mix", "solid", "camel", "json"];

    [GeneratedRegex(@"\s+\{\{-")]
    private static partial Regex TrimLeft();

    [GeneratedRegex(@"-\}\}\s+")]
    private static partial Regex TrimRight();

    [GeneratedRegex(@"\bsettings\.([A-Za-z0-9_]+)")]
    private static partial Regex SettingRef();

    public static string Resolve(string src, Library lib, IReadOnlyDictionary<string, object> settings)
    {
        var t = Tmpl.Parse(src, "command");
        var w = new Writer(lib, settings);
        w.Block(src, t.Page!.Body!.Statements, 0);
        // What {{- and -}} take away, as rendering does.
        var out_ = TrimRight().Replace(TrimLeft().Replace(w.ToString(), "{{-"), "-}}");
        // Settings used some other way than {{ settings.x }}: say their defaults.
        var with = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in SettingRef().Matches(out_))
            if (settings.TryGetValue(m.Groups[1].Value, out var v))
                with.Add($"{m.Groups[1].Value} = {Literal(v)}");
        if (with.Count > 0) out_ += "  (with " + string.Join(", ", with) + ")";
        return out_;
    }

    static string Literal(object v) => v switch
    {
        string s => GoFormat.Quote(s),
        _ => Plugin.Show(v),
    };

    static string Text(string src, ScriptNode n) =>
        src.Substring(n.Span.Start.Offset, n.Span.End.Offset - n.Span.Start.Offset + 1);

    static MyArchException Refuse(string what) => new($"a command can't {what}");

    sealed class Writer(Library lib, IReadOnlyDictionary<string, object> settings)
    {
        readonly StringBuilder b = new();
        bool trimNext;

        public override string ToString() => b.ToString();

        void Emit(string s)
        {
            if (trimNext)
            {
                s = s.TrimStart();
                trimNext = s == "";
            }
            b.Append(s);
        }

        void TrimBefore()
        {
            var n = b.Length;
            while (n > 0 && char.IsWhiteSpace(b[n - 1])) n--;
            b.Length = n;
        }

        /// <summary>A list of statements: text, {{ and }}, and what's between them.</summary>
        public void Block(string src, IList<ScriptStatement> st, int depth)
        {
            if (depth > 10) throw Refuse("nest shared functions that deep");
            for (var i = 0; i < st.Count; i++)
            {
                var s = st[i];
                switch (s)
                {
                    case ScriptRawStatement r:
                        Emit(r.Text.ToString());
                        break;
                    case ScriptEscapeStatement e:
                        // {{ x }} alone, when x is a setting or a shared function: shown as its value or body.
                        if (i + 2 < st.Count && st[i + 1] is ScriptExpressionStatement lone && st[i + 2] is ScriptEscapeStatement close
                            && Substitute(lone.Expression!, depth) is { } repl)
                        {
                            if (Text(src, e).EndsWith('-') || Text(src, e) == "{{-") TrimBefore();
                            Emit(repl);
                            trimNext = Text(src, close).StartsWith('-');
                            i += 2;
                            break;
                        }
                        // Spaced as people write it: {{ x }}.
                        var tok = Text(src, e);
                        Emit(tok.StartsWith("{{") ? tok + " " : " " + tok);
                        break;
                    case ScriptExpressionStatement es:
                        Check(es.Expression);
                        Emit(Text(src, es));
                        break;
                    case ScriptIfStatement f:
                        If(src, f, depth);
                        break;
                    case ScriptEndStatement:
                        Emit("end");
                        break;
                    default:
                        throw Refuse($"use {Keyword(s)} (only text, expressions and if/else)");
                }
            }
        }

        void If(string src, ScriptIfStatement f, int depth)
        {
            Check(f.Condition);
            Emit((f.IsElseIf ? "else if " : "if ") + Text(src, f.Condition!));
            Block(src, f.Then!.Statements, depth);
            switch (f.Else)
            {
                case null:
                    break;
                case ScriptIfStatement elseIf:
                    If(src, elseIf, depth);
                    break;
                case ScriptElseStatement e:
                    Emit("else");
                    Block(src, e.Body!.Statements, depth);
                    break;
                default:
                    throw Refuse("use that kind of else");
            }
        }

        static string Keyword(ScriptStatement s) => s.GetType().Name.Replace("Script", "").Replace("Statement", "").ToLowerInvariant();

        /// <summary>What {{ x }} alone shows: a setting's default, a shared function's body; null when it's shown as written.</summary>
        string? Substitute(ScriptExpression x, int depth)
        {
            switch (x)
            {
                case ScriptMemberExpression { Target: ScriptVariableGlobal { Name: "settings" }, Member: ScriptVariable m }
                    when settings.TryGetValue(m.Name, out var v):
                    return Plugin.Show(v);
                case ScriptVariableGlobal g when lib.Functions.TryGetValue(g.Name, out var fn):
                    return Inline(fn.Source, fn.Function, depth);
            }
            return null;
        }

        /// <summary>
        /// A shared function as its body. Only one written as text qualifies:
        /// {{ func name }}…{{ end }}, no parameters; one written as code
        /// ({{ func name; "…"; end }}) can't be shown as it runs.
        /// </summary>
        string Inline(string src, ScriptFunction fn, int depth)
        {
            var name = (fn.NameOrDoToken as ScriptVariable)?.Name ?? "?";
            if (fn.Parameters is { Count: > 0 })
                throw Refuse($"call {name}: a command calls only shared functions without parameters");
            if (fn.Body is not ScriptBlockStatement body) throw Refuse($"call {name}");
            var st = body.Statements;
            if (st.Count < 3 || st[0] is not ScriptEscapeStatement open || st[^2] is not ScriptEscapeStatement close || st[^1] is not ScriptEndStatement)
                throw Refuse($"call {name}: a function a command calls is written as text, {{{{ func {name} }}}}…{{{{ end }}}}");
            var w = new Writer(lib, settings);
            w.Block(src, st.Skip(1).Take(st.Count - 3).ToList(), depth + 1);
            var text = w.ToString();
            // Its own -}} and {{- take the whitespace next to them.
            if (Text(src, open).StartsWith('-')) text = text.TrimStart();
            if (Text(src, close).EndsWith('-')) text = text.TrimEnd();
            return text;
        }

        /// <summary>An expression a command may hold.</summary>
        void Check(ScriptExpression? x)
        {
            switch (x)
            {
                case null:
                case ScriptLiteral:
                case ScriptVariableLocal:
                    return;
                case ScriptVariableGlobal g:
                    if (g.Name is "t" or "tq") throw Refuse($"use translated text ({g.Name}): it isn't shown");
                    if (lib.Functions.ContainsKey(g.Name))
                        throw Refuse($"call {g.Name} but on its own: {{{{ {g.Name} }}}}");
                    if (!Data.Contains(g.Name) && !Pure.Contains(g.Name)) throw Refuse($"use {g.Name}");
                    return;
                case ScriptMemberExpression m:
                    // theme.x.y, settings.x, $local.x: data, never a function.
                    if (m.Target is ScriptVariableGlobal { Name: var root } && !Data.Contains(root)) throw Refuse($"use {root}.{m.Member!.Name}");
                    if (m.Target is not (ScriptVariableGlobal or ScriptVariableLocal or ScriptMemberExpression or ScriptIndexerExpression))
                        throw Refuse("use that member access");
                    Check(m.Target);
                    return;
                case ScriptIndexerExpression ix:
                    if (ix.Target is ScriptVariableGlobal { Name: var r } && !Data.Contains(r)) throw Refuse($"index {r}");
                    Check(ix.Target);
                    Check(ix.Index);
                    return;
                case ScriptFunctionCall call:
                    if (call.Target is not ScriptVariableGlobal { Name: var f } || !Pure.Contains(f))
                        throw Refuse("call that (only c, hex, quote… and shared functions on their own)");
                    foreach (var a in call.Arguments) Check(a);
                    return;
                case ScriptPipeCall pipe:
                    Check(pipe.From);
                    if (pipe.To is ScriptFunctionCall or ScriptVariableGlobal) Check(pipe.To);
                    else throw Refuse("pipe into that");
                    if (pipe.To is ScriptVariableGlobal { Name: var pf } && !Pure.Contains(pf)) throw Refuse($"pipe into {pf}");
                    return;
                case ScriptBinaryExpression bin:
                    Check(bin.Left);
                    Check(bin.Right);
                    return;
                case ScriptUnaryExpression u:
                    Check(u.Right);
                    return;
                case ScriptNestedExpression n:
                    Check(n.Expression);
                    return;
                case ScriptConditionalExpression c:
                    Check(c.Condition);
                    Check(c.ThenValue);
                    Check(c.ElseValue);
                    return;
                case ScriptAssignExpression a:
                    if (a.Target is not ScriptVariableLocal) throw Refuse($"assign to {a.Target} (only to $locals)");
                    Check(a.Value);
                    return;
                default:
                    throw Refuse($"use {x.GetType().Name.Replace("Script", "")}");
            }
        }
    }
}

public static class Tmpl
{
    /// <summary>Parses a template, failing with where the error is.</summary>
    public static Template Parse(string text, string path)
    {
        var t = Template.Parse(text, path);
        if (t.HasErrors) throw new MyArchException(string.Join("\n", t.Messages.Select(m => m.ToString())));
        return t;
    }
}
