using System.Text;

namespace MyArch.Util;

/// <summary>
/// Unified diffs (diff -u), for showing what an apply would change before
/// it does: a person or an agent reviews exactly what gets written.
/// </summary>
public static class Diff
{
    enum Op { Same, Del, Add }

    /// <summary>The unified diff from a to b ("" when they're the same).</summary>
    public static string Unified(string a, string b, string fromName, string toName, int context = 3)
    {
        if (a == b) return "";
        var x = Lines(a);
        var y = Lines(b);
        var ops = Script(x, y);
        var out_ = new StringBuilder();
        out_.Append("--- ").Append(fromName).Append('\n').Append("+++ ").Append(toName).Append('\n');
        // Hunks: runs of changes with up to `context` same lines around them.
        var i = 0;
        while (i < ops.Count)
        {
            if (ops[i].Op == Op.Same)
            {
                i++;
                continue;
            }
            var start = Math.Max(0, i - context);
            var end = i;
            var sameRun = 0;
            while (end < ops.Count)
            {
                if (ops[end].Op == Op.Same)
                {
                    if (++sameRun > 2 * context) break;
                }
                else sameRun = 0;
                end++;
            }
            // Keep `context` of the same lines that ended the hunk.
            var trailing = end < ops.Count ? sameRun - 1 : sameRun;
            end -= Math.Max(0, trailing - context);
            int aStart = ops[start].A, bStart = ops[start].B;
            int aLen = 0, bLen = 0;
            var body = new StringBuilder();
            for (var k = start; k < end; k++)
            {
                var (op, ai, bi) = ops[k];
                switch (op)
                {
                    case Op.Same:
                        body.Append(Show(' ', x[ai]));
                        aLen++;
                        bLen++;
                        break;
                    case Op.Del:
                        body.Append(Show('-', x[ai]));
                        aLen++;
                        break;
                    case Op.Add:
                        body.Append(Show('+', y[bi]));
                        bLen++;
                        break;
                }
            }
            out_.Append($"@@ -{Range(aStart, aLen)} +{Range(bStart, bLen)} @@\n").Append(body);
            i = end;
        }
        return out_.ToString();
    }

    static string Range(int start, int len) => len == 1 ? $"{start + 1}" : $"{(len == 0 ? start : start + 1)},{len}";

    /// <summary>Marks a last line without its newline, so it differs from one with it.</summary>
    const string NoEol = "\u0000noeol";

    static List<string> Lines(string s)
    {
        var l = s.Split('\n').ToList();
        if (l.Count > 0 && l[^1] == "") l.RemoveAt(l.Count - 1);
        else if (l.Count > 0) l[^1] += NoEol;
        return l;
    }

    /// <summary>A line as diff -u prints it, with its "no newline" note.</summary>
    static string Show(char mark, string line) =>
        line.EndsWith(NoEol) ? $"{mark}{line[..^NoEol.Length]}\n\\ No newline at end of file\n" : $"{mark}{line}\n";

    /// <summary>The edit script, by longest common subsequence: each step with its line in a and in b.</summary>
    static List<(Op Op, int A, int B)> Script(List<string> x, List<string> y)
    {
        int n = x.Count, m = y.Count;
        // Common head and tail first: most changes are small.
        var head = 0;
        while (head < n && head < m && x[head] == y[head]) head++;
        var tail = 0;
        while (tail < n - head && tail < m - head && x[n - 1 - tail] == y[m - 1 - tail]) tail++;
        int xn = n - head - tail, yn = m - head - tail;
        // A huge change (a big file taken over whole): all of a out, all of b
        // in, rather than a table the size of both.
        if ((long)xn * yn > 4_000_000)
        {
            var whole = new List<(Op, int, int)>();
            for (var k = 0; k < head; k++) whole.Add((Op.Same, k, k));
            for (var k = 0; k < xn; k++) whole.Add((Op.Del, head + k, head));
            for (var k = 0; k < yn; k++) whole.Add((Op.Add, head + xn, head + k));
            for (var k = 0; k < tail; k++) whole.Add((Op.Same, n - tail + k, m - tail + k));
            return whole;
        }
        var lcs = new int[xn + 1, yn + 1];
        for (var i = xn - 1; i >= 0; i--)
            for (var j = yn - 1; j >= 0; j--)
                lcs[i, j] = x[head + i] == y[head + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
        var ops = new List<(Op, int, int)>();
        for (var k = 0; k < head; k++) ops.Add((Op.Same, k, k));
        int a = 0, c = 0;
        while (a < xn || c < yn)
        {
            if (a < xn && c < yn && x[head + a] == y[head + c])
            {
                ops.Add((Op.Same, head + a, head + c));
                a++;
                c++;
            }
            else if (c < yn && (a == xn || lcs[a, c + 1] >= lcs[a + 1, c]))
            {
                ops.Add((Op.Add, head + a, head + c));
                c++;
            }
            else
            {
                ops.Add((Op.Del, head + a, head + c));
                a++;
            }
        }
        for (var k = 0; k < tail; k++) ops.Add((Op.Same, n - tail + k, m - tail + k));
        // Deletions before additions within a change, as diff -u shows them.
        for (var k = 1; k < ops.Count; k++)
            for (var j = k; j > 0 && ops[j].Item1 == Op.Del && ops[j - 1].Item1 == Op.Add; j--)
                (ops[j], ops[j - 1]) = (ops[j - 1], ops[j]);
        // Each step's place in a and in b, counted again in the new order.
        int pa = 0, pb = 0;
        for (var k = 0; k < ops.Count; k++)
        {
            var op = ops[k].Item1;
            ops[k] = (op, pa, pb);
            if (op != Op.Add) pa++;
            if (op != Op.Del) pb++;
        }
        return ops;
    }
}
