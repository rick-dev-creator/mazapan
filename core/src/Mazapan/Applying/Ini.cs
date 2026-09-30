using System.Text.RegularExpressions;
using Mazapan.Util;

namespace Mazapan.Applying;

// Files shared with the app that uses them (qt6ct.conf, kdeglobals, a
// browser's Preferences) or with the person (Firefox's user.js, their
// userChrome.css): mazapan only manages the keys it renders and leaves the
// rest alone. Formats: "ini"; "prefs" (user_pref("name", value); lines);
// "lines" (lines that must be there); "json" (leaves of an object).
//
// What mazapan wrote last is kept in Owned as "<format>:" + those keys,
// instead of a hash of the whole file: a change is someone else's only
// when one of mazapan's keys differs from what it wrote (or, for the
// person's own files, "prefs" and "lines", when it's gone).

public static partial class Apply
{
    /// <summary>
    /// Readable: can a file in this format be merged into without losing
    /// it? Only JSON can fail: a file that doesn't parse (a crash's
    /// half-write, a comment) is never rewritten: "unreadable", left as it
    /// is.
    /// </summary>
    internal static bool Readable(string format, string text)
    {
        if (format != "json") return true;
        return JsonRoot(text) != null;
    }

    /// <summary>
    /// The object text holds, as json.Unmarshal into a map[string]any: null
    /// when it doesn't parse, isn't an object, or is null.
    /// </summary>
    static Dictionary<string, object?>? JsonRoot(string text)
    {
        if (!GoJsonCodec.TryParse(text, out var tree)) return null;
        if (tree is not GoJsonCodec.JsonObject) return null;
        try
        {
            return (Dictionary<string, object?>)GoJsonCodec.ToAny(tree)!;
        }
        catch (GoJsonCodec.JsonError)
        {
            return null;
        }
    }

    /// <summary>
    /// RemovedIsEdit: in the person's own files, a key of mazapan's that's
    /// gone was taken out on purpose (to opt a profile out): not put back
    /// silently. An app that rewrites its file may just drop it (Chromium
    /// does, while it runs): that's put back.
    /// </summary>
    static bool RemovedIsEdit(string format) => format is "prefs" or "lines";

    /// <summary>
    /// DropKeys takes mazapan's keys that it no longer manages (gone, as it
    /// wrote them) out of the file: a pref of an older version, the @import
    /// of a plugin that's disabled, a settings.json's color customizations
    /// (else an editor stays in a theme nothing manages). INI keeps them: an
    /// app like qt6ct needs its keys set.
    /// </summary>
    internal static string DropKeys(string format, string text, Dictionary<string, string>? drop)
    {
        if (drop == null || drop.Count == 0 || format == "ini") return text;
        if (format == "json") return JsonDrop(text, drop);
        var out_ = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            string key = "", value = "";
            switch (format)
            {
                case "prefs":
                    if (PrefLine.Match(line) is { Success: true } m)
                    {
                        key = "\0" + m.Groups[1].Value;
                        value = m.Groups[2].Value;
                    }
                    break;
                case "lines":
                    key = "\0" + line.Trim();
                    value = "1";
                    break;
            }
            if (drop.TryGetValue(key, out var v) && v == value) continue;
            out_.Add(line);
        }
        return string.Join("\n", out_);
    }

    /// <summary>
    /// KeysOf are a shared file's keys in its format: "section\0key" ->
    /// value ("\0name" for prefs and lines, "\0a\x01b" for the JSON path a.b).
    /// </summary>
    internal static Dictionary<string, string> KeysOf(string format, string text) => format switch
    {
        "prefs" => PrefsKeys(text),
        "lines" => LinesKeys(text),
        "json" => JsonKeys(text),
        _ => IniKeys(text),
    };

    /// <summary>MergeKeys sets want's keys in text, keeping everything else.</summary>
    internal static string MergeKeys(string format, string text, Dictionary<string, string> want) => format switch
    {
        "prefs" => PrefsMerge(text, want),
        "lines" => LinesMerge(text, want),
        "json" => JsonMerge(text, want),
        _ => IniMerge(text, want),
    };

    /// <summary>
    /// "json": mazapan's keys are the leaves of the template's object, by
    /// path, its parts joined by \x01 (keys can hold dots:
    /// "workbench.colorTheme").
    /// </summary>
    internal static Dictionary<string, string> JsonKeys(string text)
    {
        var out_ = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!GoJsonCodec.TryUnmarshalAny(text, out var v)) return out_;
        void Walk(string prefix, object? x)
        {
            if (x is Dictionary<string, object?> { Count: > 0 } m)
            {
                foreach (var (k, y) in m) Walk(prefix + "\x01" + k, y);
                return;
            }
            out_["\0" + (prefix.StartsWith('\x01') ? prefix[1..] : prefix)] = GoJsonCodec.Marshal(x);
        }
        Walk("", v);
        return out_;
    }

    /// <summary>
    /// JsonMerge sets want's paths in the object text holds (a new one when
    /// the file is empty; one that doesn't parse never gets here, see
    /// Readable), creating the objects on the way.
    /// </summary>
    internal static string JsonMerge(string text, Dictionary<string, string> want)
    {
        var root = new Dictionary<string, object?>();
        if (text.Trim() != "")
        {
            root = JsonRoot(text);
            if (root == null) return text; // never rewritten blind: whatever it is stays
        }
        foreach (var (k, raw) in want)
        {
            if (!GoJsonCodec.TryUnmarshalAny(raw, out var val)) continue;
            var path = k.TrimStart0().Split('\x01');
            var m = root;
            foreach (var p in path[..^1])
            {
                if (!(m.TryGetValue(p, out var x) && x is Dictionary<string, object?> next))
                {
                    next = new Dictionary<string, object?>();
                    m[p] = next;
                }
                m = next;
            }
            m[path[^1]] = val;
        }
        return JsonWrite(text, root);
    }

    /// <summary>
    /// JsonWrite: laid out the way the file was, near enough: one line if
    /// it was one line (a browser's Preferences), else indented with tabs (a
    /// settings.json people read). No HTML escaping of &lt;, &gt;, &amp;.
    /// Keys come out sorted, as Go writes a map.
    /// </summary>
    static string JsonWrite(string was, Dictionary<string, object?> root)
    {
        var trimmed = was.Trim();
        var indent = trimmed == "" || trimmed.Contains('\n') ? "\t" : "";
        var s = GoJsonCodec.Marshal(root, escapeHtml: false, indent) + "\n";
        if (!was.EndsWith('\n') && trimmed != "") return s[..^1];
        return s;
    }

    /// <summary>
    /// JsonDrop deletes the paths still holding what mazapan wrote there,
    /// and the objects that leaves empty.
    /// </summary>
    static string JsonDrop(string text, Dictionary<string, string> drop)
    {
        var root = JsonRoot(text);
        if (root == null) return text;
        var changed = false;
        foreach (var (k, v) in drop)
        {
            var path = k.TrimStart0().Split('\x01');
            bool Walk(Dictionary<string, object?> m, int i) // true: m[path[i]] went
            {
                if (i == path.Length - 1)
                {
                    if (m.TryGetValue(path[i], out var cur) && GoJsonCodec.Marshal(cur) == v)
                    {
                        m.Remove(path[i]);
                        return true;
                    }
                    return false;
                }
                if (!(m.TryGetValue(path[i], out var x) && x is Dictionary<string, object?> next) || !Walk(next, i + 1))
                    return false;
                if (next.Count == 0) m.Remove(path[i]);
                return true;
            }
            changed = Walk(root, 0) || changed;
        }
        if (!changed) return text;
        return JsonWrite(text, root);
    }

    /// <summary>
    /// "lines": mazapan's keys are whole lines that must be in the file (an
    /// @import in someone's userChrome.css); a missing one goes at the top,
    /// where CSS wants its imports. Only the head of the file counts (up to
    /// the first line that isn't an @-rule or a comment): an @import after
    /// other rules is ignored by CSS, so it isn't there.
    /// </summary>
    internal static Dictionary<string, string> LinesKeys(string text)
    {
        var out_ = new Dictionary<string, string>(StringComparer.Ordinal);
        var inComment = false;
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (inComment) inComment = !t.Contains("*/");
            else if (t == "") { }
            else if (t.StartsWith("/*", StringComparison.Ordinal)) inComment = !t.Contains("*/");
            else if (t.StartsWith('@')) out_["\0" + t] = "1";
            else return out_;
        }
        return out_;
    }

    static string LinesMerge(string text, Dictionary<string, string> want)
    {
        var have = LinesKeys(text);
        var missing = new List<string>();
        foreach (var k in want.Keys)
            if (Get(have, k) == "")
                missing.Add(k.TrimStart0());
        GoOrder.Sort(missing);
        if (missing.Count == 0) return text;
        return string.Join("\n", missing) + "\n" + text;
    }

    // user_pref("name", value);  // a comment
    // Go's \s is [\t\n\f\r ], and its $ is the end of the text.
    const string S = @"[\t\n\f\r ]";
    static readonly Regex PrefLine = new(
        $@"^{S}*user_pref{S}*\({S}*[""']([^""']+)[""']{S}*,{S}*(.*?){S}*\){S}*;(.*)\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// PrefLines calls f for each user_pref line outside /* … */ comments (a
    /// commented-out pref isn't set).
    /// </summary>
    static void PrefLines(List<string> lines, Action<int, Match> f)
    {
        var inComment = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (inComment)
            {
                inComment = !t.Contains("*/");
                continue;
            }
            if (t.StartsWith("/*", StringComparison.Ordinal))
            {
                inComment = !t.Contains("*/");
                continue;
            }
            if (PrefLine.Match(lines[i]) is { Success: true } m) f(i, m);
        }
    }

    internal static Dictionary<string, string> PrefsKeys(string text)
    {
        var out_ = new Dictionary<string, string>(StringComparer.Ordinal);
        PrefLines([.. text.Split('\n')], (_, m) => out_["\0" + m.Groups[1].Value] = m.Groups[2].Value);
        return out_;
    }

    /// <summary>
    /// PrefsMerge sets want's prefs where they are (keeping a trailing
    /// comment), else adds them at the end.
    /// </summary>
    static string PrefsMerge(string text, Dictionary<string, string> want)
    {
        var lines = text == "" ? [] : text.TrimEnd('\n').Split('\n').ToList();
        var done = new HashSet<string>(StringComparer.Ordinal);
        PrefLines(lines, (i, m) =>
        {
            var name = m.Groups[1].Value;
            if (want.TryGetValue("\0" + name, out var v))
            {
                lines[i] = $"user_pref({GoFormat.Quote(name)}, {v});{m.Groups[3].Value}";
                done.Add("\0" + name);
            }
        });
        var missing = new List<string>();
        foreach (var (k, v) in want)
            if (!done.Contains(k))
                missing.Add($"user_pref({GoFormat.Quote(k.TrimStart0())}, {v});");
        GoOrder.Sort(missing);
        lines.AddRange(missing);
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// IniKeys are an INI file's keys: "section\0key" -> value. Keys before
    /// any section have section "".
    /// </summary>
    internal static Dictionary<string, string> IniKeys(string text)
    {
        var out_ = new Dictionary<string, string>(StringComparer.Ordinal);
        var section = "";
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t == "" || t.StartsWith('#') || t.StartsWith(';')) { }
            else if (t.StartsWith('[') && t.EndsWith(']')) section = t[1..^1];
            else if (t.IndexOf('=') is var eq and >= 0)
                out_[section + "\0" + t[..eq].Trim()] = t[(eq + 1)..].Trim();
        }
        return out_;
    }

    /// <summary>SharedOwned is how Owned keeps a shared file's managed keys.</summary>
    internal static string SharedOwned(string format, Dictionary<string, string> keys)
    {
        var lines = keys.Select(kv => kv.Key + "\0" + kv.Value).ToList();
        GoOrder.Sort(lines);
        return format + ":" + string.Join("\n", lines);
    }

    static readonly string[] Formats = ["ini", "prefs", "lines", "json"];

    /// <summary>
    /// FromSharedOwned: the keys and format of a shared file's Owned entry;
    /// false for a whole file's hash.
    /// </summary>
    internal static bool FromSharedOwned(string s, out Dictionary<string, string> keys, out string format)
    {
        foreach (var f in Formats)
        {
            if (!s.StartsWith(f + ":", StringComparison.Ordinal)) continue;
            keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in s[(f.Length + 1)..].Split('\n'))
            {
                var parts = line.Split('\0', 3);
                if (parts.Length == 3) keys[parts[0] + "\0" + parts[1]] = parts[2];
            }
            format = f;
            return true;
        }
        keys = [];
        format = "";
        return false;
    }

    /// <summary>IsShared tells a shared file's Owned entry from a whole file's.</summary>
    public static bool IsShared(string owned) => FromSharedOwned(owned, out _, out _);

    /// <summary>
    /// Edited: has someone else changed the file since mazapan wrote it
    /// (owned is what Owned has for it)? For a shared file, only its managed
    /// keys count.
    /// </summary>
    public static bool Edited(string owned, byte[] disk)
    {
        if (!FromSharedOwned(owned, out var old, out var format)) return Sum(disk) != owned;
        // Bytes that aren't UTF-8 can't be compared key by key: someone else's.
        if (!Files.TryText(disk, out var diskText)) return true;
        var have = KeysOf(format, diskText);
        foreach (var (k, v) in old)
        {
            var ok = have.TryGetValue(k, out var hv);
            if (ok && hv != v || !ok && RemovedIsEdit(format)) return true;
        }
        return false;
    }

    /// <summary>
    /// RestoreShared puts a shared file's managed keys back as a backup had
    /// them (recorded: what Owned had for it then; current: what it has now;
    /// backup: the file then), keeping whatever the app wrote since. Owned
    /// from before the file was shared is a hash: then every key of the
    /// backup goes back.
    /// </summary>
    public static byte[] RestoreShared(byte[] disk, string recorded, string current, byte[] backup)
    {
        if (!Files.TryText(disk, out var text) ||
            (FromSharedOwned(current, out _, out var cf) && !Readable(cf, text)))
            return disk; // not something it can merge into: left as it is
        if (!FromSharedOwned(recorded, out var keys, out var format))
        {
            FromSharedOwned(current, out _, out format);
            keys = KeysOf(format, Files.Utf8.GetString(backup));
        }
        return Files.Utf8.GetBytes(MergeKeys(format, text, keys));
    }

    /// <summary>
    /// IniMerge sets want's keys in text, in place where they are, else at
    /// the end of their section (created at the end of the file when
    /// missing). Everything else in text stays as it was.
    /// </summary>
    internal static string IniMerge(string text, Dictionary<string, string> want)
    {
        var lines = text == "" ? [] : text.TrimEnd('\n').Split('\n').ToList();
        var done = new HashSet<string>(StringComparer.Ordinal);
        var section = "";
        var lastInSection = new Dictionary<string, int>(StringComparer.Ordinal); // section -> index of its last line
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                section = t[1..^1];
                lastInSection[section] = i;
                continue;
            }
            if (t != "") lastInSection[section] = i;
            var eq = t.IndexOf('=');
            if (eq < 0 || t.StartsWith('#') || t.StartsWith(';')) continue;
            var k = t[..eq].Trim();
            var id = section + "\0" + k;
            if (want.TryGetValue(id, out var v))
            {
                lines[i] = k + "=" + v;
                done.Add(id);
            }
        }
        // Missing keys, grouped by section, in a stable order.
        var missing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var sections = new List<string>();
        foreach (var (id, v) in want)
        {
            if (done.Contains(id)) continue;
            var cut = id.IndexOf('\0');
            string sec = cut < 0 ? id : id[..cut], k = cut < 0 ? "" : id[(cut + 1)..];
            if (!missing.TryGetValue(sec, out var list))
            {
                sections.Add(sec);
                missing[sec] = list = [];
            }
            list.Add(k + "=" + v);
        }
        GoOrder.Sort(sections);
        foreach (var sec in sections)
        {
            var keys = missing[sec];
            GoOrder.Sort(keys);
            if (!lastInSection.ContainsKey(sec) && sec == "")
            {
                // Keys outside any section go before the first one.
                lines.InsertRange(0, keys);
                foreach (var s in lastInSection.Keys.ToList()) lastInSection[s] += keys.Count;
                continue;
            }
            if (lastInSection.TryGetValue(sec, out var at))
            {
                lines.InsertRange(at + 1, keys);
                // Later sections moved down.
                foreach (var s in lastInSection.Keys.ToList())
                    if (lastInSection[s] > at)
                        lastInSection[s] += keys.Count;
                lastInSection[sec] = at + keys.Count;
                continue;
            }
            if (lines.Count > 0) lines.Add("");
            if (sec != "") lines.Add("[" + sec + "]");
            lines.AddRange(keys);
            lastInSection[sec] = lines.Count - 1;
        }
        return string.Join("\n", lines) + "\n";
    }
}

static class KeyText
{
    /// <summary>strings.TrimPrefix(k, "\x00").</summary>
    public static string TrimStart0(this string k) => k.StartsWith('\0') ? k[1..] : k;
}
