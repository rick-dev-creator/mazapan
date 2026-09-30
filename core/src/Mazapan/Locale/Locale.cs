using Mazapan.Util;

namespace Mazapan.Locale;

/// <summary>
/// Works out the language to render in and loads plugin translations.
///
/// A plugin keeps its user-facing text in locales/&lt;lang&gt;.toml, flat
/// key = "text" tables. en.toml is required and is the fallback; other
/// languages may leave keys out. The language comes from the OS unless
/// config.toml sets one.
/// </summary>
public static class Languages
{
    /// <summary>Fallback is the language every plugin must provide.</summary>
    public const string Fallback = "en";

    /// <summary>
    /// Detect returns the language as a POSIX locale name without encoding or
    /// modifier ("es_MX", "en"): override if set, else LC_ALL, LC_MESSAGES,
    /// LANG, else LANG from /etc/locale.conf, else Fallback.
    /// </summary>
    public static string Detect(string overrideLang)
    {
        if (overrideLang != "") return Clean(overrideLang);
        foreach (var v in new[] { "LC_ALL", "LC_MESSAGES", "LANG" })
        {
            var l = Clean(Environment.GetEnvironmentVariable(v) ?? "");
            if (l != "") return l;
        }
        var conf = Clean(FromLocaleConf("/etc/locale.conf"));
        return conf != "" ? conf : Fallback;
    }

    /// <summary>Clean turns "es_MX.UTF-8@euro" into "es_MX"; "C" and "POSIX" mean no language at all.</summary>
    public static string Clean(string l)
    {
        l = l.Trim();
        var i = l.IndexOfAny(['.', '@']);
        if (i >= 0) l = l[..i];
        return l is "C" or "POSIX" ? "" : l;
    }

    static string FromLocaleConf(string path)
    {
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                var t = line.Trim();
                if (t.StartsWith("LANG=")) return t[5..].Trim('"', '\'');
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return "";
    }

    /// <summary>Candidates lists the translation files to try, most specific first: "es_MX" -> es_MX, es, en.</summary>
    public static List<string> Candidates(string lang)
    {
        var out_ = new List<string>();
        void Add(string l)
        {
            if (!out_.Contains(l)) out_.Add(l);
        }
        if (lang != "")
        {
            Add(lang);
            var i = lang.IndexOf('_');
            if (i >= 0) Add(lang[..i]);
        }
        Add(Fallback);
        return out_;
    }
}

/// <summary>Catalog is one plugin's translations for one language, with fallbacks.</summary>
public sealed class Catalog
{
    readonly string plugin;
    readonly List<string> names = [];                           // language of each chain entry
    readonly List<Dictionary<string, string>> chain = [];      // most specific first, en last

    Catalog(string plugin) => this.plugin = plugin;

    /// <summary>
    /// Load reads dir/locales for lang. A plugin without a locales directory
    /// gets an empty catalog: any lookup is an error, which is what a plugin
    /// that shows text but ships no translations deserves.
    /// </summary>
    public static Catalog Load(string plugin, string dir, string lang)
    {
        var c = new Catalog(plugin);
        Dictionary<string, string>? en = null;
        foreach (var l in Languages.Candidates(lang))
        {
            Dictionary<string, string>? m;
            try
            {
                m = Read(Paths.Join(dir, "locales", l + ".toml"));
            }
            catch (MazapanException e)
            {
                throw new MazapanException($"plugin {plugin}: {e.Message}");
            }
            if (m == null) continue;
            if (l == Languages.Fallback) en = m;
            c.names.Add(l);
            c.chain.Add(m);
        }
        // A key other languages have but en doesn't is a typo somewhere; but
        // for what plugin.toml says in English already (the name and
        // description, the settings' labels and descriptions).
        if (en != null)
            for (var i = 0; i < c.chain.Count - 1; i++)
                foreach (var k in c.chain[i].Keys.Order(StringComparer.Ordinal))
                    if (!en.ContainsKey(k) && !FromManifest(k))
                        throw new MazapanException($"plugin {plugin}: locales/{c.names[i]}.toml has \"{k}\", which en.toml doesn't");
        return c;
    }

    static bool FromManifest(string key) => key is "plugin.name" or "plugin.description" || key.StartsWith("setting.", StringComparison.Ordinal);

    static Dictionary<string, string>? Read(string path)
    {
        var t = Toml.ReadFile(path);
        if (t == null) return null;
        var m = new Dictionary<string, string>();
        foreach (var (k, v) in t)
            m[k] = v as string ?? throw new MazapanException($"{path}: {k}: want a string");
        return m;
    }

    /// <summary>TryT is T without the error: null when no language has it.</summary>
    public string? TryT(string key)
    {
        foreach (var m in chain)
            if (m.TryGetValue(key, out var v)) return v;
        return null;
    }

    /// <summary>T returns the text for key in the most specific language that has it.</summary>
    public string T(string key)
    {
        foreach (var m in chain)
            if (m.TryGetValue(key, out var v)) return v;
        throw new MazapanException($"plugin {plugin}: no text for \"{key}\" in locales/{Languages.Fallback}.toml");
    }
}
