using System.Globalization;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Serialization;

namespace Mazapan.Util;

// A key given twice is an error, as it was: it must not silently win.
[TomlSourceGenerationOptions(DuplicateKeyHandling = TomlDuplicateKeyHandling.Error)]
[TomlSerializable(typeof(TomlTable))]
internal partial class TomlContext : TomlSerializerContext;

/// <summary>
/// Reads TOML into tables, without reflection (Native AOT), and maps them
/// by hand: every key a file has must be one we read, so a typo is an
/// error instead of a setting silently ignored.
/// </summary>
public static class Toml
{
    static readonly TomlContext Strict = TomlContext.Default;

    public static TomlTable Parse(string text, string path)
    {
        try
        {
            Duplicates(text, path);
            return TomlSerializer.Deserialize(text, Strict.TomlTable) ?? new TomlTable();
        }
        catch (TomlException e)
        {
            // The parser's own words, not the serializer's wrapper around them.
            var inner = e;
            while (inner.InnerException is TomlException deeper) inner = deeper;
            throw new MazapanException($"{path}: {inner.Message}", e);
        }
    }

    /// <summary>
    /// Duplicates refuses a key given twice, or a [table] opened twice, as
    /// the TOML spec says (Tomlyn would let the last one win): a typo in
    /// config.toml must not silently decide.
    /// </summary>
    static void Duplicates(string text, string path)
    {
        var doc = Tomlyn.Parsing.SyntaxParser.ParseStrict(text, path, false);
        if (doc.HasErrors) return; // the parser says what's wrong
        var seen = new HashSet<string>();
        var tables = new HashSet<string>();
        var arrays = new Dictionary<string, int>();
        void Keys(string prefix, IEnumerable<Tomlyn.Syntax.KeyValueSyntax> items)
        {
            foreach (var kv in items)
            {
                var full = prefix + "\0" + Name(kv.Key!);
                if (!seen.Add(full))
                    throw new MazapanException($"{path}: {Name(kv.Key!).Replace('\0', '.')} is set twice");
            }
        }
        Keys("", doc.KeyValues);
        var last = "";
        foreach (var t in doc.Tables)
        {
            var name = Name(t.Name!);
            string prefix;
            if (t is Tomlyn.Syntax.TableArraySyntax)
            {
                // Tomlyn keeps only the tables after the break: say so instead.
                if (arrays.ContainsKey(name) && last != name && !last.StartsWith(name + "\0", StringComparison.Ordinal)
                    && !name.StartsWith(last + "\0", StringComparison.Ordinal))
                    throw new MazapanException($"{path}: the [[{name.Replace('\0', '.')}]] tables must be together, not split by other tables");
                arrays[name] = arrays.GetValueOrDefault(name) + 1;
                prefix = name + "\0#" + arrays[name];
            }
            else
            {
                if (!tables.Add(name)) throw new MazapanException($"{path}: [{name.Replace('\0', '.')}] is there twice");
                prefix = name;
            }
            Keys("\0" + prefix, t.Items.OfType<Tomlyn.Syntax.KeyValueSyntax>());
            last = name;
        }
    }

    static string Name(Tomlyn.Syntax.KeySyntax k)
    {
        static string Part(Tomlyn.Syntax.BareKeyOrStringValueSyntax? p) => p switch
        {
            Tomlyn.Syntax.BareKeySyntax b => b.Key?.Text ?? "",
            Tomlyn.Syntax.StringValueSyntax s => s.Value ?? "",
            _ => "",
        };
        var parts = new List<string> { Part(k.Key) };
        foreach (var d in k.DotKeys) parts.Add(Part(d.Key));
        return string.Join("\0", parts);
    }

    /// <summary>The file's table, or null when there is no file.</summary>
    public static TomlTable? ReadFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MazapanException($"{path}: {e.Message}", e);
        }
        return Parse(text, path);
    }
}

/// <summary>
/// A table being read: takes keys one by one, and tells which ones nobody
/// took.
/// </summary>
public sealed class TomlReader(TomlTable table, string file, string prefix = "")
{
    readonly HashSet<string> taken = [];
    readonly List<TomlReader> children = [];

    public TomlTable Table => table;

    string Where(string key) => prefix == "" ? key : prefix + "." + key;

    MazapanException Wrong(string key, string want, object value) =>
        new($"{file}: {Where(key)}: want {want}, got {Describe(value)}");

    static string Describe(object v) => v switch
    {
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "?",
    };

    public bool Has(string key) => table.ContainsKey(key);

    object? Take(string key)
    {
        taken.Add(key);
        return table.TryGetValue(key, out var v) ? v : null;
    }

    public string String(string key, string def = "") => Take(key) switch
    {
        null => def,
        string s => s,
        var v => throw Wrong(key, "a string", v),
    };

    public long Int(string key, long def = 0) => Take(key) switch
    {
        null => def,
        long n => n,
        var v => throw Wrong(key, "an integer", v),
    };

    public double Float(string key, double def = 0) => Take(key) switch
    {
        null => def,
        double d => d,
        long n => n,
        var v => throw Wrong(key, "a number", v),
    };

    public bool Bool(string key, bool def = false) => Take(key) switch
    {
        null => def,
        bool b => b,
        var v => throw Wrong(key, "true or false", v),
    };

    public List<string> Strings(string key) => Take(key) switch
    {
        null => [],
        TomlArray a => a.Select(x => x as string ?? throw Wrong(key, "a list of strings", a)).ToList(),
        var v => throw Wrong(key, "a list of strings", v),
    };

    public List<double> Floats(string key) => Take(key) switch
    {
        null => [],
        TomlArray a => a.Select(x => x switch
        {
            double d => d,
            long n => (double)n,
            _ => throw Wrong(key, "a list of numbers", a),
        }).ToList(),
        var v => throw Wrong(key, "a list of numbers", v),
    };

    /// <summary>A sub-table, read the same way (missing: an empty one).</summary>
    public TomlReader Sub(string key)
    {
        var child = Take(key) switch
        {
            null => new TomlReader(new TomlTable(), file, Where(key)),
            TomlTable t => new TomlReader(t, file, Where(key)),
            var v => throw Wrong(key, "a table", v),
        };
        children.Add(child);
        return child;
    }

    /// <summary>[[key]] tables, each read the same way.</summary>
    public List<TomlReader> Array(string key)
    {
        var list = Take(key) switch
        {
            null => [],
            TomlTableArray a => a.Select((t, i) => new TomlReader(t, file, $"{Where(key)}[{i}]")).ToList(),
            var v => throw Wrong(key, "[[" + key + "]] tables", v),
        };
        children.AddRange(list);
        return list;
    }

    /// <summary>A table of anything, taken whole (settings).</summary>
    public TomlTable? Raw(string key) => Take(key) switch
    {
        null => null,
        TomlTable t => t,
        var v => throw Wrong(key, "a table", v),
    };

    /// <summary>A table of strings (colors, translations).</summary>
    public Dictionary<string, string> StringMap(string key)
    {
        var t = Raw(key);
        var map = new Dictionary<string, string>();
        if (t == null) return map;
        foreach (var (k, v) in t)
            map[k] = v as string ?? throw Wrong(key + "." + k, "a string", v);
        return map;
    }

    /// <summary>Keys nobody read, here and below.</summary>
    public List<string> Unknown()
    {
        var out_ = table.Select(kv => kv.Key).Where(k => !taken.Contains(k)).Select(Where).ToList();
        foreach (var c in children) out_.AddRange(c.Unknown());
        return out_;
    }

    /// <summary>Throws when a key was left unread: a typo.</summary>
    public void Done()
    {
        var u = Unknown();
        if (u.Count > 0) throw new MazapanException($"{file}: unknown keys: [{string.Join(" ", u)}]");
    }
}
