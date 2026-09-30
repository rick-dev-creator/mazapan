using System.Collections;
using System.Text;

namespace Mazapan.Util;

/// <summary>
/// A struct as Go's encoding/json writes it: its fields in the order
/// they're declared (a field left out is omitempty's doing).
/// </summary>
public sealed class Fields : IEnumerable<KeyValuePair<string, object?>>
{
    readonly List<KeyValuePair<string, object?>> items = [];

    public void Add(string name, object? value) => items.Add(new(name, value));

    /// <summary>A field's value (null when there's none).</summary>
    public object? this[string name] => items.FirstOrDefault(kv => kv.Key == name).Value;

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>JSON written as it is (a JSON-RPC id echoed back, whatever it was).</summary>
public sealed record RawJson(string Text);

/// <summary>
/// JSON byte for byte as Go's json.Marshal wrote it: maps with their keys
/// sorted, structs (Fields) in declaration order, HTML-safe strings, floats
/// in Go's shortest form. The theme picker and other panels read it.
/// </summary>
public static class GoJson
{
    public static string Marshal(object? v)
    {
        var b = new StringBuilder();
        Write(b, v);
        return b.ToString();
    }

    static void Write(StringBuilder b, object? v)
    {
        switch (v)
        {
            case null:
                b.Append("null");
                break;
            case string s:
                GoFormat.JsonString(b, s);
                break;
            case RawJson r:
                b.Append(r.Text);
                break;
            case bool x:
                b.Append(x ? "true" : "false");
                break;
            case int or long:
                b.Append(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case double d:
                b.Append(Float(d));
                break;
            case Fields f:
                b.Append('{');
                var first = true;
                foreach (var (k, x) in f)
                {
                    if (!first) b.Append(',');
                    first = false;
                    GoFormat.JsonString(b, k);
                    b.Append(':');
                    Write(b, x);
                }
                b.Append('}');
                break;
            case IDictionary m:
                b.Append('{');
                var keys = m.Keys.Cast<string>().Order(StringComparer.Ordinal).ToList();
                for (var i = 0; i < keys.Count; i++)
                {
                    if (i > 0) b.Append(',');
                    GoFormat.JsonString(b, keys[i]);
                    b.Append(':');
                    Write(b, m[keys[i]]);
                }
                b.Append('}');
                break;
            case IEnumerable list:
                b.Append('[');
                var n = 0;
                foreach (var x in list)
                {
                    if (n++ > 0) b.Append(',');
                    Write(b, x);
                }
                b.Append(']');
                break;
            default:
                throw new ArgumentException($"GoJson: can't encode {v.GetType().Name}");
        }
    }

    /// <summary>encoding/json's float: 'f' form, 'e' outside 1e-6..1e21, "e-07" as "e-7".</summary>
    public static string Float(double f)
    {
        var abs = Math.Abs(f);
        if (abs == 0 || (abs >= 1e-6 && abs < 1e21)) return GoFormat.Num(f);
        var s = f.ToString("E16", System.Globalization.CultureInfo.InvariantCulture);
        // Shortest mantissa that round-trips.
        var r = f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (r.Contains('E')) s = r;
        var i = s.IndexOf('E');
        var mant = s[..i];
        var exp = int.Parse(s[(i + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        return mant + "e" + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("00", System.Globalization.CultureInfo.InvariantCulture).TrimStart('0').PadLeft(exp < 0 ? 1 : 2, '0');
    }
}
