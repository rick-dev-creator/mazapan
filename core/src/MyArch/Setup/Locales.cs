namespace MyArch.Setup;

/// <summary>
/// Locales picks the system's locale: the language, as it's written where
/// the time zone is (Spanish in America/Mexico_City: es_MX), when the system
/// has that one; else the language's usual one.
/// </summary>
public static class Locales
{
    /// <summary>
    /// The country a time zone is in: the first code on its zone1970.tab
    /// line (a zone shared by several countries, America/Puerto_Rico, lists
    /// the one it's named after first); "" when it's none's (UTC).
    /// </summary>
    public static string Country(string timezone, string zoneTab)
    {
        foreach (var line in zoneTab.Split('\n'))
        {
            var f = line.Split('\t');
            if (f.Length >= 3 && !line.StartsWith('#') && f[2].Trim() == timezone) return f[0].Split(',')[0];
        }
        return "";
    }

    static readonly Dictionary<string, string> Usual = new() { ["en"] = "en_US", ["es"] = "es_ES" };

    /// <param name="zoneTab">tzdata's zone1970.tab: codes, coordinates, zone.</param>
    /// <param name="supported">glibc's SUPPORTED: "es_MX.UTF-8 UTF-8" a line.</param>
    public static string For(string language, string timezone, string zoneTab, string supported)
    {
        var lang = language.Split('_')[0];
        var have = supported.Split('\n').Select(l => l.Split(' ')[0]).Where(l => l.EndsWith(".UTF-8")).ToHashSet();
        // Already a region ("es_MX").
        if (language.Contains('_') && have.Contains(language + ".UTF-8")) return language + ".UTF-8";
        if (Country(timezone, zoneTab) is { Length: > 0 } cc && have.Contains($"{lang}_{cc}.UTF-8")) return $"{lang}_{cc}.UTF-8";
        var usual = (Usual.GetValueOrDefault(lang) ?? lang + "_" + lang.ToUpperInvariant()) + ".UTF-8";
        return have.Contains(usual) || have.Count == 0 ? usual : "en_US.UTF-8";
    }
}
