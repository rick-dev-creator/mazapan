using System.Globalization;
using System.Text;

namespace Mazapan.Util;

/// <summary>
/// Formatting that matches what the Go version wrote, byte for byte:
/// generated files must not change with the port.
/// </summary>
public static class GoFormat
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>strconv.FormatFloat(f, 'f', -1, 64): shortest, never an exponent.</summary>
    public static string Num(double f)
    {
        if (double.IsNaN(f)) return "NaN";
        if (double.IsInfinity(f)) return f > 0 ? "+Inf" : "-Inf";
        var s = f.ToString("R", Inv);
        var e = s.IndexOf('E');
        if (e < 0) return s;
        // The shortest digits, with the point moved where the exponent says.
        var neg = s.StartsWith('-');
        var mant = s[(neg ? 1 : 0)..e];
        var exp = int.Parse(s[(e + 1)..], Inv);
        var dot = mant.IndexOf('.');
        var digits = mant.Replace(".", "");
        var point = (dot < 0 ? mant.Length : dot) + exp;
        string out_;
        if (point <= 0) out_ = "0." + new string('0', -point) + digits;
        else if (point >= digits.Length) out_ = digits + new string('0', point - digits.Length);
        else out_ = digits[..point] + "." + digits[point..];
        return (neg ? "-" : "") + out_;
    }

    /// <summary>fmt's %v for a float64: shortest, with an exponent when Go uses one.</summary>
    public static string Float(double f)
    {
        if (f == Math.Floor(f) && Math.Abs(f) < 1e21) return f.ToString("0", Inv);
        var exp = f == 0 ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(f)));
        if (exp < -4 || exp >= 21)
        {
            var s = f.ToString("R", Inv).Replace("E", "e");
            var i = s.IndexOf('e');
            if (i >= 0)
            {
                var sign = s[i + 1] == '-' ? "-" : "+";
                var digits = s[(i + 1)..].TrimStart('+', '-').PadLeft(2, '0');
                return s[..i] + "e" + sign + digits;
            }
            return s;
        }
        return f.ToString("R", Inv);
    }

    /// <summary>
    /// strconv.FormatFloat(f, 'f', prec, 64) and fmt's %.Nf: the exact binary
    /// value rounded half to even (0.625 is "0.62"; .NET would say "0.63").
    /// </summary>
    public static string Fixed(double f, int prec)
    {
        if (double.IsNaN(f) || double.IsInfinity(f)) return f.ToString(Inv);
        var neg = f < 0 || (f == 0 && double.IsNegative(f));
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(f));
        var exp = (int)((bits >> 52) & 0x7ff);
        var mant = bits & 0xfffffffffffffL;
        if (exp == 0) exp = 1; else mant |= 1L << 52;
        exp -= 1075;
        // value = mant * 2^exp = num / den
        System.Numerics.BigInteger num = mant, den = 1;
        if (exp > 0) num <<= exp; else den <<= -exp;
        var scale = System.Numerics.BigInteger.Pow(10, prec);
        var scaled = num * scale;
        var q = System.Numerics.BigInteger.DivRem(scaled, den, out var rem);
        var twice = rem * 2;
        if (twice > den || (twice == den && !q.IsEven)) q += 1;
        var digits = q.ToString(Inv).PadLeft(prec + 1, '0');
        var s = prec == 0 ? digits : digits[..^prec] + "." + digits[^prec..];
        return neg ? "-" + s : s;
    }

    /// <summary>Math.Round as Go's math.Round: halves away from zero.</summary>
    public static double Round(double x) => Math.Round(x, MidpointRounding.AwayFromZero);

    /// <summary>strconv.Quote: a double-quoted Go string literal.</summary>
    public static string Quote(string s)
    {
        var b = new StringBuilder("\"");
        foreach (var r in s.EnumerateRunes())
        {
            switch (r.Value)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\a': b.Append("\\a"); break;
                case '\b': b.Append("\\b"); break;
                case '\f': b.Append("\\f"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                case '\v': b.Append("\\v"); break;
                default:
                    if (IsPrint(r)) b.Append(r.ToString());
                    else if (r.Value < 0x80) b.Append($"\\x{r.Value:x2}");
                    else if (r.Value < 0x10000) b.Append($"\\u{r.Value:x4}");
                    else b.Append($"\\U{r.Value:x8}");
                    break;
            }
        }
        return b.Append('"').ToString();
    }

    static bool IsPrint(Rune r)
    {
        if (r.Value < 0x20 || r.Value == 0x7f) return false;
        if (r.Value == ' ') return true;
        var cat = Rune.GetUnicodeCategory(r);
        return cat switch
        {
            UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.SpaceSeparator => false,
            _ => true,
        };
    }

    /// <summary>A JSON string as Go's encoding/json writes it (HTML-safe).</summary>
    public static void JsonString(StringBuilder b, string s)
    {
        b.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                case '\b': b.Append("\\b"); break;
                case '\f': b.Append("\\f"); break;
                case '<': b.Append("\\u003c"); break;
                case '>': b.Append("\\u003e"); break;
                case '&': b.Append("\\u0026"); break;
                case (char)0x2028: b.Append("\\u2028"); break;
                case (char)0x2029: b.Append("\\u2029"); break;
                default:
                    if (c < 0x20) b.Append($"\\u{(int)c:x4}");
                    else b.Append(c);
                    break;
            }
        }
        b.Append('"');
    }

    public static string JsonString(string s)
    {
        var b = new StringBuilder();
        JsonString(b, s);
        return b.ToString();
    }
}
