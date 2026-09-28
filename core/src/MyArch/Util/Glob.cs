namespace MyArch.Util;

/// <summary>
/// Go's filepath.Glob and filepath.Match, ported as they are: *, ? and
/// [...] within one path element, by rune; sorted results; a pattern
/// without wildcards matching itself when it exists; a malformed pattern
/// ([]a], [a-], a trailing \) matching nothing.
/// </summary>
public static class Glob
{
    sealed class BadPattern : Exception;

    public static List<string> Expand(string pattern)
    {
        try
        {
            return Expand1(pattern);
        }
        catch (BadPattern)
        {
            return [];
        }
    }

    static List<string> Expand1(string pattern)
    {
        if (!HasMeta(pattern)) return Paths.Exists(pattern) ? [pattern] : [];
        var dir = Paths.Dir(pattern);
        var file = Paths.Base(pattern);
        List<string> dirs = HasMeta(dir) ? Expand1(dir) : [dir];
        var out_ = new List<string>();
        foreach (var d in dirs)
        {
            if (!Directory.Exists(d)) continue;
            string[] names;
            try
            {
                names = Directory.GetFileSystemEntries(d).Select(Path.GetFileName).OfType<string>().ToArray();
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            Array.Sort(names, string.CompareOrdinal);
            foreach (var n in names)
                if (MatchOrThrow(file, n)) out_.Add(Paths.Join(d, n));
        }
        return out_;
    }

    static bool HasMeta(string p) => p.IndexOfAny(['*', '?', '[', '\\']) >= 0;

    /// <summary>Match reports whether name matches the shell pattern (false for a malformed one).</summary>
    public static bool Match(string pattern, string name)
    {
        try
        {
            return MatchOrThrow(pattern, name);
        }
        catch (BadPattern)
        {
            return false;
        }
    }

    static int[] Runes(string s) => s.EnumerateRunes().Select(r => r.Value).ToArray();

    static bool MatchOrThrow(string patternText, string nameText)
    {
        return Match(Runes(patternText), Runes(nameText));
    }

    static bool Match(int[] pattern, int[] name)
    {
    Pattern:
        while (pattern.Length > 0)
        {
            var (star, chunk, rest) = ScanChunk(pattern);
            pattern = rest;
            if (star && chunk.Length == 0)
                // Trailing * matches rest of string unless it has a /.
                return Array.IndexOf(name, '/') < 0;
            // Look for match at current position.
            var (t, ok) = MatchChunk(chunk, name);
            // If we're the last chunk, make sure we've exhausted the name,
            // otherwise we'd give a false result even if we could still match
            // using the star.
            if (ok && (t.Length == 0 || pattern.Length > 0))
            {
                name = t;
                continue;
            }
            if (star)
            {
                // Look for match skipping i+1 runes. Cannot skip /.
                for (var i = 0; i < name.Length && name[i] != '/'; i++)
                {
                    var (t2, ok2) = MatchChunk(chunk, name[(i + 1)..]);
                    if (ok2)
                    {
                        // If we're the last chunk, make sure we exhausted the name.
                        if (pattern.Length == 0 && t2.Length > 0) continue;
                        name = t2;
                        goto Pattern;
                    }
                }
            }
            // Before returning false, check that the remainder of the
            // pattern is valid.
            while (pattern.Length > 0)
            {
                var (_, c, r) = ScanChunk(pattern);
                pattern = r;
                MatchChunk(c, []);
            }
            return false;
        }
        return name.Length == 0;
    }

    static (bool Star, int[] Chunk, int[] Remaining) ScanChunk(int[] pattern)
    {
        var star = false;
        while (pattern.Length > 0 && pattern[0] == '*')
        {
            pattern = pattern[1..];
            star = true;
        }
        var inRange = false;
        var i = 0;
        for (; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\')
            {
                // Error check handled in MatchChunk: bad pattern.
                if (i + 1 < pattern.Length) i++;
            }
            else if (c == '[') inRange = true;
            else if (c == ']') inRange = false;
            else if (c == '*' && !inRange) break;
        }
        return (star, pattern[..i], pattern[i..]);
    }

    static (int[] Remaining, bool Ok) MatchChunk(int[] chunk, int[] s)
    {
        var failed = false;
        while (chunk.Length > 0)
        {
            if (!failed && s.Length == 0) failed = true;
            switch (chunk[0])
            {
                case '[':
                {
                    // A character class.
                    var r = 0;
                    if (!failed)
                    {
                        r = s[0];
                        s = s[1..];
                    }
                    chunk = chunk[1..];
                    var negated = false;
                    if (chunk.Length > 0 && chunk[0] == '^')
                    {
                        negated = true;
                        chunk = chunk[1..];
                    }
                    var match = false;
                    var nrange = 0;
                    while (true)
                    {
                        if (chunk.Length > 0 && chunk[0] == ']' && nrange > 0)
                        {
                            chunk = chunk[1..];
                            break;
                        }
                        var lo = GetEsc(ref chunk);
                        var hi = lo;
                        if (chunk[0] == '-')
                        {
                            chunk = chunk[1..];
                            hi = GetEsc(ref chunk);
                        }
                        if (lo <= r && r <= hi) match = true;
                        nrange++;
                    }
                    if (match == negated) failed = true;
                    break;
                }
                case '?':
                    if (!failed)
                    {
                        if (s[0] == '/') failed = true;
                        s = s[1..];
                    }
                    chunk = chunk[1..];
                    break;
                default:
                    if (chunk[0] == '\\')
                    {
                        chunk = chunk[1..];
                        if (chunk.Length == 0) throw new BadPattern();
                    }
                    if (!failed)
                    {
                        if (chunk[0] != s[0]) failed = true;
                        s = s[1..];
                    }
                    chunk = chunk[1..];
                    break;
            }
        }
        return failed ? ([], false) : (s, true);
    }

    static int GetEsc(ref int[] chunk)
    {
        if (chunk.Length == 0 || chunk[0] == '-' || chunk[0] == ']') throw new BadPattern();
        if (chunk[0] == '\\')
        {
            chunk = chunk[1..];
            if (chunk.Length == 0) throw new BadPattern();
        }
        var r = chunk[0];
        chunk = chunk[1..];
        if (chunk.Length == 0) throw new BadPattern();
        return r;
    }
}
