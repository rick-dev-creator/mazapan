using System.Globalization;
using System.Text;
using Mazapan.Util;

namespace Mazapan.Applying;

/// <summary>
/// JSON read and written the way Go's encoding/json does, byte for byte:
/// state files (owned.json, record.json) written by the Go version are read
/// as it read them, and files mazapan merges into (a browser's Preferences,
/// a settings.json) come out laid out as the Go version wrote them. No
/// reflection: AOT-safe.
///
/// Parse gives a tree: null, bool, string, <see cref="JsonNumber"/> (the
/// literal, as Go's decoder keeps it until it knows the target type),
/// List&lt;object?&gt; and <see cref="JsonObject"/> (members in file order,
/// duplicates and all). <see cref="ToAny"/> turns it into Go's `any`:
/// Dictionary&lt;string, object?&gt; (last duplicate wins), List&lt;object?&gt;,
/// double, string, bool, null.
/// </summary>
internal static class GoJsonCodec
{
    /// <summary>U+FFFD, Go's stand-in for what isn't valid UTF-8.</summary>
    const char Replacement = (char)0xFFFD;

    /// <summary>A number as written in the file.</summary>
    public sealed record JsonNumber(string Literal);

    /// <summary>An object's members in file order.</summary>
    public sealed class JsonObject : List<KeyValuePair<string, object?>>;

    /// <summary>
    /// Fields in declaration order: how a Go struct is written (a map's keys
    /// are sorted instead).
    /// </summary>
    public sealed class Fields : List<KeyValuePair<string, object?>>
    {
        public void Add(string name, object? value) => Add(new KeyValuePair<string, object?>(name, value));
    }

    /// <summary>A Go syntax or type error, with Go's message.</summary>
    public sealed class JsonError(string message) : Exception(message);

    // ---- Reading ----

    /// <summary>
    /// Parse reads a whole JSON text, as json.Unmarshal checks it first:
    /// any syntax error is Go's message ("unexpected end of JSON input",
    /// "invalid character '}' looking for beginning of object key string").
    /// </summary>
    public static object? Parse(string text) => new Parser(text).ParseAll();

    public static bool TryParse(string text, out object? tree)
    {
        try
        {
            tree = Parse(text);
            return true;
        }
        catch (JsonError)
        {
            tree = null;
            return false;
        }
    }

    /// <summary>
    /// ToAny is json.Unmarshal into an `any`: numbers become float64 (one
    /// beyond its range is Go's type error), objects maps where the last
    /// duplicate wins.
    /// </summary>
    public static object? ToAny(object? node)
    {
        switch (node)
        {
            case JsonNumber n:
                return Float(n.Literal);
            case JsonObject o:
                var m = new Dictionary<string, object?>();
                foreach (var (k, v) in o) m[k] = ToAny(v);
                return m;
            case List<object?> a:
                return a.Select(ToAny).ToList();
            default:
                return node;
        }
    }

    /// <summary>Unmarshal into `any`; false on any error, as Go's err != nil.</summary>
    public static bool TryUnmarshalAny(string text, out object? value)
    {
        value = null;
        try
        {
            value = ToAny(Parse(text));
            return true;
        }
        catch (JsonError)
        {
            return false;
        }
    }

    /// <summary>A number literal as a float64; one out of its range is an error, as in Go.</summary>
    public static double Float(string literal)
    {
        var f = double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (double.IsInfinity(f)) throw new JsonError($"json: cannot unmarshal number {literal} into Go value of type float64");
        return f;
    }

    /// <summary>A number literal as an int64 (a time.Duration); anything else is Go's type error.</summary>
    public static long Int(string literal, string goType)
    {
        if (literal.Length > 0 && literal.All(c => c is >= '0' and <= '9' or '-')
            && long.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
            return n;
        throw new JsonError($"json: cannot unmarshal number {literal} into Go value of type {goType}");
    }

    /// <summary>What Go calls a JSON value's kind in a type error.</summary>
    public static string Kind(object? node) => node switch
    {
        null => "null",
        bool => "bool",
        string => "string",
        JsonNumber => "number",
        JsonObject => "object",
        _ => "array",
    };

    public static JsonError TypeError(object? node, string goType) =>
        new($"json: cannot unmarshal {Kind(node)} into Go value of type {goType}");

    sealed class Parser(string s)
    {
        int i;
        int depth;

        public object? ParseAll()
        {
            SkipSpace();
            var v = Value("looking for beginning of value");
            SkipSpace();
            if (i < s.Length) throw Invalid("after top-level value");
            return v;
        }

        void SkipSpace()
        {
            while (i < s.Length && s[i] is ' ' or '\t' or '\n' or '\r') i++;
        }

        JsonError Eof() => new("unexpected end of JSON input");

        JsonError Invalid(string context) => new($"invalid character {QuoteChar()} {context}");

        /// <summary>Go's quoteChar, of the byte where the scanner stopped.</summary>
        string QuoteChar()
        {
            var c = s[i];
            if (c >= 0x80)
            {
                // Go looks at the first byte of the character's UTF-8 form.
                var n = char.IsHighSurrogate(c) && i + 1 < s.Length ? 2 : 1;
                c = (char)Files.Utf8.GetBytes(s.Substring(i, n))[0];
            }
            if (c == '\'') return "'\\''";
            if (c == '"') return "'\"'";
            var q = GoFormat.Quote(c.ToString());
            return "'" + q[1..^1] + "'";
        }

        object? Value(string context)
        {
            if (i >= s.Length) throw Eof();
            switch (s[i])
            {
                case '{': return Object();
                case '[': return Array();
                case '"': return String();
                case 't': return Literal("true", true);
                case 'f': return Literal("false", false);
                case 'n': return Literal("null", null);
                case '-':
                case >= '0' and <= '9':
                    return Number();
                default:
                    throw Invalid(context);
            }
        }

        void Enter()
        {
            if (++depth > 10000) throw new JsonError("exceeded max depth");
        }

        JsonObject Object()
        {
            Enter();
            i++;
            var o = new JsonObject();
            SkipSpace();
            if (i >= s.Length) throw Eof();
            if (s[i] == '}')
            {
                i++;
                depth--;
                return o;
            }
            while (true)
            {
                if (i >= s.Length) throw Eof();
                if (s[i] != '"') throw Invalid("looking for beginning of object key string");
                var k = String();
                SkipSpace();
                if (i >= s.Length) throw Eof();
                if (s[i] != ':') throw Invalid("after object key");
                i++;
                SkipSpace();
                o.Add(new(k, Value("looking for beginning of value")));
                SkipSpace();
                if (i >= s.Length) throw Eof();
                if (s[i] == '}')
                {
                    i++;
                    depth--;
                    return o;
                }
                if (s[i] != ',') throw Invalid("after object key:value pair");
                i++;
                SkipSpace();
            }
        }

        List<object?> Array()
        {
            Enter();
            i++;
            var a = new List<object?>();
            SkipSpace();
            if (i >= s.Length) throw Eof();
            if (s[i] == ']')
            {
                i++;
                depth--;
                return a;
            }
            while (true)
            {
                a.Add(Value("looking for beginning of value"));
                SkipSpace();
                if (i >= s.Length) throw Eof();
                if (s[i] == ']')
                {
                    i++;
                    depth--;
                    return a;
                }
                if (s[i] != ',') throw Invalid("after array element");
                i++;
                SkipSpace();
            }
        }

        object? Literal(string word, object? value)
        {
            for (var k = 0; k < word.Length; k++, i++)
            {
                if (i >= s.Length) throw Eof();
                if (s[i] != word[k]) throw Invalid($"in literal {word} (expecting '{word[k]}')");
            }
            return value;
        }

        JsonNumber Number()
        {
            var start = i;
            if (s[i] == '-')
            {
                i++;
                if (i >= s.Length) throw Eof();
                if (s[i] is not (>= '0' and <= '9')) throw Invalid("in numeric literal");
            }
            if (s[i] == '0') i++;
            else
                while (i < s.Length && s[i] is >= '0' and <= '9') i++;
            if (i < s.Length && s[i] == '.')
            {
                i++;
                if (i >= s.Length) throw Eof();
                if (s[i] is not (>= '0' and <= '9')) throw Invalid("after decimal point in numeric literal");
                while (i < s.Length && s[i] is >= '0' and <= '9') i++;
            }
            if (i < s.Length && s[i] is 'e' or 'E')
            {
                i++;
                if (i < s.Length && s[i] is '+' or '-') i++;
                if (i >= s.Length) throw Eof();
                if (s[i] is not (>= '0' and <= '9')) throw Invalid("in exponent of numeric literal");
                while (i < s.Length && s[i] is >= '0' and <= '9') i++;
            }
            return new JsonNumber(s[start..i]);
        }

        string String()
        {
            i++; // the opening quote
            var b = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Eof();
                var c = s[i];
                if (c == '"')
                {
                    i++;
                    return b.ToString();
                }
                if (c < 0x20) throw Invalid("in string literal");
                if (c != '\\')
                {
                    // A lone surrogate can't come from UTF-8: it's what Go
                    // makes of bytes that aren't valid UTF-8.
                    if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    {
                        b.Append(c).Append(s[i + 1]);
                        i += 2;
                        continue;
                    }
                    b.Append(char.IsSurrogate(c) ? Replacement : c);
                    i++;
                    continue;
                }
                i++;
                if (i >= s.Length) throw Eof();
                switch (s[i])
                {
                    case '"': b.Append('"'); break;
                    case '\\': b.Append('\\'); break;
                    case '/': b.Append('/'); break;
                    case 'b': b.Append('\b'); break;
                    case 'f': b.Append('\f'); break;
                    case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break;
                    case 't': b.Append('\t'); break;
                    case 'u':
                        i++;
                        var r = Hex4();
                        if (char.IsHighSurrogate((char)r) && i + 5 < s.Length && s[i] == '\\' && s[i + 1] == 'u')
                        {
                            var save = i;
                            i += 2;
                            var r2 = Hex4();
                            if (char.IsLowSurrogate((char)r2))
                            {
                                b.Append((char)r).Append((char)r2);
                                continue;
                            }
                            // Not a pair: the first is invalid, the second
                            // read again on its own.
                            b.Append(Replacement);
                            i = save;
                            continue;
                        }
                        b.Append(char.IsSurrogate((char)r) ? Replacement : (char)r);
                        continue;
                    default:
                        throw Invalid("in string escape code");
                }
                i++;
            }
        }

        int Hex4()
        {
            var r = 0;
            for (var k = 0; k < 4; k++, i++)
            {
                if (i >= s.Length) throw Eof();
                var c = s[i];
                int d = c switch
                {
                    >= '0' and <= '9' => c - '0',
                    >= 'a' and <= 'f' => c - 'a' + 10,
                    >= 'A' and <= 'F' => c - 'A' + 10,
                    _ => -1,
                };
                if (d < 0) throw Invalid("in \\u hexadecimal character escape");
                r = r * 16 + d;
            }
            return r;
        }
    }

    // ---- Writing ----

    /// <summary>
    /// Marshal writes v as Go does: json.Marshal (escapeHtml, no indent),
    /// json.MarshalIndent (indent "  ") or an Encoder with SetEscapeHTML
    /// (false) and SetIndent("", "\t"). Map keys are sorted as Go sorts
    /// them (by UTF-8 bytes); <see cref="Fields"/> keep their order.
    /// Values: null, bool, string, double, long, <see cref="JsonNumber"/>,
    /// IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt; and
    /// &lt;string, object?&gt; (maps), IList&lt;object?&gt;.
    /// </summary>
    public static string Marshal(object? v, bool escapeHtml = true, string indent = "")
    {
        var b = new StringBuilder();
        Write(b, v, escapeHtml, indent, 0);
        return b.ToString();
    }

    static void Newline(StringBuilder b, string indent, int depth)
    {
        b.Append('\n');
        for (var k = 0; k < depth; k++) b.Append(indent);
    }

    static void Write(StringBuilder b, object? v, bool html, string indent, int depth)
    {
        switch (v)
        {
            case null:
                b.Append("null");
                break;
            case bool x:
                b.Append(x ? "true" : "false");
                break;
            case string x:
                String(b, x, html);
                break;
            case double x:
                b.Append(FormatFloat(x));
                break;
            case long x:
                b.Append(x.ToString(CultureInfo.InvariantCulture));
                break;
            case int x:
                b.Append(x.ToString(CultureInfo.InvariantCulture));
                break;
            case JsonNumber x:
                b.Append(x.Literal);
                break;
            case Fields x:
                Members(b, x, html, indent, depth);
                break;
            case IEnumerable<KeyValuePair<string, object?>> x:
                Members(b, x.OrderBy(kv => kv.Key, GoOrder.Instance), html, indent, depth);
                break;
            case IEnumerable<KeyValuePair<string, string>> x:
                Members(b, x.OrderBy(kv => kv.Key, GoOrder.Instance)
                    .Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)), html, indent, depth);
                break;
            case IList<object?> x:
                if (x.Count == 0)
                {
                    b.Append("[]");
                    break;
                }
                b.Append('[');
                for (var k = 0; k < x.Count; k++)
                {
                    if (k > 0) b.Append(',');
                    if (indent != "") Newline(b, indent, depth + 1);
                    Write(b, x[k], html, indent, depth + 1);
                }
                if (indent != "") Newline(b, indent, depth);
                b.Append(']');
                break;
            default:
                throw new ArgumentException($"GoJsonCodec: can't write a {v.GetType().Name}");
        }
    }

    static void Members(StringBuilder b, IEnumerable<KeyValuePair<string, object?>> members, bool html, string indent, int depth)
    {
        var first = true;
        foreach (var (k, x) in members)
        {
            b.Append(first ? "{" : ",");
            first = false;
            if (indent != "") Newline(b, indent, depth + 1);
            String(b, k, html);
            b.Append(indent != "" ? ": " : ":");
            Write(b, x, html, indent, depth + 1);
        }
        if (first)
        {
            b.Append("{}");
            return;
        }
        if (indent != "") Newline(b, indent, depth);
        b.Append('}');
    }

    /// <summary>
    /// A float64 as encoding/json writes it: like %v but never an exponent
    /// between 1e-6 and 1e21, and "e-7" rather than "e-07".
    /// </summary>
    public static string FormatFloat(double f)
    {
        var abs = Math.Abs(f);
        if (abs == 0 || (abs >= 1e-6 && abs < 1e21)) return GoFormat.Num(f);
        // Shortest digits, as .NET's round-trip format has them.
        var r = f.ToString("R", CultureInfo.InvariantCulture);
        var e = r.IndexOfAny(['E', 'e']);
        if (e < 0) return r;
        var exp = int.Parse(r[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var digits = Math.Abs(exp).ToString(CultureInfo.InvariantCulture);
        if (exp > 0 && digits.Length < 2) digits = "0" + digits;
        return r[..e] + "e" + (exp < 0 ? "-" : "+") + digits;
    }

    const string Hex = "0123456789abcdef";


    /// <summary>A string as encoding/json writes it (Go 1.22 and later: \b and \f by name).</summary>
    public static void String(StringBuilder b, string s, bool html)
    {
        b.Append('"');
        for (var k = 0; k < s.Length; k++)
        {
            var c = s[k];
            switch (c)
            {
                case '\\': b.Append("\\\\"); break;
                case '"': b.Append("\\\""); break;
                case '\b': b.Append("\\b"); break;
                case '\f': b.Append("\\f"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                case '<' or '>' or '&' when html:
                    b.Append("\\u00").Append(Hex[c >> 4]).Append(Hex[c & 0xF]);
                    break;
                case < ' ':
                    b.Append("\\u00").Append(Hex[c >> 4]).Append(Hex[c & 0xF]);
                    break;
                case (char)0x2028 or (char)0x2029:
                    b.Append("\\u202").Append(Hex[c & 0xF]);
                    break;
                default:
                    if (char.IsHighSurrogate(c) && k + 1 < s.Length && char.IsLowSurrogate(s[k + 1]))
                    {
                        b.Append(c).Append(s[++k]);
                    }
                    else if (char.IsSurrogate(c))
                    {
                        // Go's stand-in for what isn't valid UTF-8.
                        b.Append("\\ufffd");
                    }
                    else b.Append(c);
                    break;
            }
        }
        b.Append('"');
    }
}

/// <summary>
/// Go's string order (sort.Strings, map keys in encoding/json): by UTF-8
/// bytes, which is by code point. .NET's ordinal order differs only for
/// characters beyond U+FFFF against U+E000–U+FFFF.
/// </summary>
internal sealed class GoOrder : IComparer<string>
{
    public static readonly GoOrder Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x == null || y == null) return string.CompareOrdinal(x, y);
        var n = Math.Min(x.Length, y.Length);
        for (var k = 0; k < n; k++)
        {
            if (x[k] == y[k]) continue;
            return Fix(x[k]) - Fix(y[k]);
        }
        return x.Length - y.Length;
    }

    // Surrogates (U+D800–U+DFFF) stand for code points above U+FFFF: they
    // go after U+E000–U+FFFF.
    static int Fix(char c) => c >= 0xD800 ? (c >= 0xE000 ? c - 0x800 : c + 0x2000) : c;

    public static void Sort(List<string> list) => list.Sort(Instance);
}
