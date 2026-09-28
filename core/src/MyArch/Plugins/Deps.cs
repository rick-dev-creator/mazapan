using System.Text.RegularExpressions;
using MyArch.Util;

namespace MyArch.Plugins;

public static partial class Versions
{
    [GeneratedRegex(@"^v?[0-9]+(\.[0-9]+)*\z")]
    private static partial Regex Pattern();

    public static List<long> Parse(string v)
    {
        if (!Pattern().IsMatch(v)) throw new MyArchException($"version \"{v}\" is not numbers and dots");
        return v.TrimStart('v').Split('.').Select(p => long.TryParse(p, out var n) ? n : long.MaxValue).ToList();
    }

    /// <summary>Compare: -1, 0 or 1; missing parts count as 0 (1.2 = 1.2.0).</summary>
    public static int Compare(string a, string b)
    {
        List<long> x, y;
        try
        {
            x = Parse(a);
            y = Parse(b);
        }
        catch (MyArchException) { return 0; }
        for (var i = 0; i < x.Count || i < y.Count; i++)
        {
            var p = i < x.Count ? x[i] : 0;
            var q = i < y.Count ? y[i] : 0;
            if (p != q) return p < q ? -1 : 1;
        }
        return 0;
    }
}

/// <summary>A requirement: "shell-bar", "shell-bar >= 0.2", "theme-gtk = 1.0.0".</summary>
public sealed partial record Requirement(string Id, string Op, string Version)
{
    [GeneratedRegex(@"^\s*([a-z0-9][a-z0-9-]*)\s*(?:(>=|<=|>|<|=)\s*(\S+))?\s*\z")]
    private static partial Regex Pattern();

    public override string ToString() => Op == "" ? Id : $"{Id} {Op} {Version}";

    public static Requirement Parse(string s)
    {
        var m = Pattern().Match(s);
        if (!m.Success) throw new MyArchException($"requires \"{s}\": use \"id\" or \"id >= 1.2\"");
        if (m.Groups[2].Success)
        {
            try
            {
                Versions.Parse(m.Groups[3].Value);
            }
            catch (MyArchException e)
            {
                throw new MyArchException($"requires \"{s}\": {e.Message}");
            }
        }
        return new(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
    }

    /// <summary>Satisfied by a plugin at version v?</summary>
    public bool Satisfied(string v)
    {
        if (Op == "") return true;
        var c = Versions.Compare(v, Version);
        return Op switch
        {
            ">=" => c >= 0,
            ">" => c > 0,
            "=" => c == 0,
            "<=" => c <= 0,
            _ => c < 0,
        };
    }
}

/// <summary>A Problem is a requirement that isn't met.</summary>
public sealed record Problem(string Plugin, string Requires, string Text)
{
    public override string ToString() => Text;
}

public static class Dependencies
{
    /// <summary>
    /// Unmet lists, for the enabled plugins, what they require that isn't
    /// there: missing, disabled, or at a version that doesn't do. all is every
    /// plugin found, enabled or not; broken are those that don't load.
    /// </summary>
    public static List<Problem> Unmet(IEnumerable<Plugin> enabled, IEnumerable<Plugin> all, IReadOnlyDictionary<string, string>? broken)
    {
        var on = new Dictionary<string, Plugin>();
        foreach (var p in enabled) on[p.Id] = p;
        var found = all.Select(p => p.Id).ToHashSet();
        var out_ = new List<Problem>();
        foreach (var p in on.Values.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            foreach (var s in p.Meta.Requires)
            {
                var r = Requirement.Parse(s); // checked when loaded
                on.TryGetValue(r.Id, out var dep);
                string text;
                if (dep == null && broken != null && broken.ContainsKey(r.Id))
                    text = $"{p.Id} requires {r}, which doesn't load";
                else if (dep == null && found.Contains(r.Id))
                    text = $"{p.Id} requires {r}, which is disabled";
                else if (dep == null)
                    text = $"{p.Id} requires {r}, which isn't installed";
                else if (!r.Satisfied(dep.Meta.Version))
                    text = $"{p.Id} requires {r}; {r.Id} is {dep.Meta.Version}";
                else
                    continue;
                out_.Add(new(p.Id, r.ToString(), text));
            }
        }
        return out_;
    }

    /// <summary>Dependents are the plugins in enabled that require id.</summary>
    public static List<string> Dependents(string id, IEnumerable<Plugin> enabled)
    {
        var out_ = new List<string>();
        foreach (var p in enabled)
            foreach (var s in p.Meta.Requires)
            {
                Requirement r;
                try
                {
                    r = Requirement.Parse(s);
                }
                catch (MyArchException) { continue; }
                if (r.Id == id && p.Id != id)
                {
                    out_.Add(p.Id);
                    break;
                }
            }
        return out_;
    }
}
