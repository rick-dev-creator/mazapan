using System.Security.Cryptography;
using System.Text;
using Mazapan.Plugins;
using Mazapan.Util;

namespace Mazapan.Install;

/// <summary>
/// A catalog lists plugins one can add: where each lives (a git repository)
/// and what it is. It's only pointers: adding one is still `plugins add`,
/// with its capabilities shown and approved. Catalogs are TOML files
/// ([[plugin]] tables), read from a path or a URL (cached for a day); mazapan
/// ships one (catalog/index.toml) and config.toml can add others:
/// catalogs = ["https://…/index.toml"].
/// </summary>
public sealed class CatalogEntry
{
    public string Id = "", Name = "", Description = "", Author = "", Source = "", Ref = "", Homepage = "";
    public List<string> Categories = [];
    /// <summary>The catalog it's from (a path or a URL).</summary>
    public string From = "";
    /// <summary>Name and description in other languages (translations.es.name = …), by language code.</summary>
    public Dictionary<string, (string Name, string Description)> Translations = [];

    /// <summary>The name and description translated to a language (es_MX, then es): "" for what isn't.</summary>
    public (string Name, string Description) TranslatedTo(string lang)
    {
        foreach (var l in new[] { lang, lang.Split('_')[0] })
            if (Translations.TryGetValue(l, out var t)) return t;
        return ("", "");
    }

    /// <summary>The name and description in a language (es_MX, then es), or else as written.</summary>
    public (string Name, string Description) In(string lang)
    {
        foreach (var l in new[] { lang, lang.Split('_')[0] })
            if (Translations.TryGetValue(l, out var t))
                return (t.Name != "" ? t.Name : Name, t.Description != "" ? t.Description : Description);
        return (Name, Description);
    }
}

public static partial class CatalogIndex
{
    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z]{2,3}(_[A-Z]{2})?\z")]
    private static partial System.Text.RegularExpressions.Regex LangPattern();

    public static readonly string[] KnownCategories = ["bar", "panel", "theme", "window", "hardware", "tools", "agent"];

    static string CacheDir() => Paths.ExpandHome("~/.cache/mazapan/catalogs");

    /// <summary>All entries of every catalog, first catalog first; an id seen again is skipped. Problems don't stop the rest.</summary>
    public static (List<CatalogEntry> Entries, List<string> Problems) Load(IEnumerable<string> catalogs, bool refresh)
    {
        var entries = new List<CatalogEntry>();
        var problems = new List<string>();
        var seen = new HashSet<string>();
        foreach (var c in catalogs)
        {
            try
            {
                var text = Fetch(c, refresh, t => Parse(t, c));
                if (text == null) continue;
                foreach (var e in Parse(text, c))
                    if (seen.Add(e.Id)) entries.Add(e);
            }
            catch (Exception ex) when (ex is MazapanException or IOException or HttpRequestException or TaskCanceledException or UnauthorizedAccessException or UriFormatException or InvalidOperationException)
            {
                problems.Add($"catalog {c}: {ex.Message}");
            }
        }
        return (entries, problems);
    }

    /// <summary>
    /// A catalog's text, from a path or an https URL (cached for a day; the
    /// last good one when it can't be reached). Only what passes check is
    /// cached. Shared by the plugin catalogs and the app catalogs.
    /// </summary>
    internal static string? Fetch(string catalog, bool refresh, Action<string> check)
    {
        if (catalog.StartsWith("http://"))
            throw new MazapanException("not https: anyone on the way could point its plugins elsewhere");
        if (catalog.StartsWith("https://") && !(Uri.TryCreate(catalog, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host != ""))
            throw new MazapanException("not an address");
        if (!catalog.StartsWith("https://"))
        {
            var path = Paths.ExpandHome(catalog);
            if (!File.Exists(path)) throw new MazapanException("no such file");
            return File.ReadAllText(path);
        }
        var cache = Paths.Join(CacheDir(), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(catalog)))[..16] + ".toml");
        var failed = cache + ".failed";
        if (!refresh && File.Exists(cache) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cache) < TimeSpan.FromDays(1))
            return File.ReadAllText(cache);
        // Just failed: not tried again for an hour (every plugin page reads
        // the catalogs), unless asked to (--refresh).
        if (!refresh && File.Exists(failed) && DateTime.UtcNow - File.GetLastWriteTimeUtc(failed) < TimeSpan.FromHours(1))
            return File.Exists(cache) ? File.ReadAllText(cache) : throw new MazapanException($"unreachable ({File.ReadAllText(failed).Trim()})");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 1 << 20 };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("mazapan");
            var text = http.GetStringAsync(catalog).GetAwaiter().GetResult();
            check(text); // cache only what reads
            Files.WriteAtomic(cache, text);
            File.Delete(failed);
            return text;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or MazapanException)
        {
            try
            {
                Files.WriteAtomic(failed, e.Message + "\n");
            }
            catch (Exception) { } // only a note
            // Offline, or it serves something broken now: the last good one.
            if (File.Exists(cache)) return File.ReadAllText(cache);
            throw;
        }
    }

    /// <summary>Parse reads a catalog: every entry checked, as each is only a pointer that must lead somewhere sane.</summary>
    public static List<CatalogEntry> Parse(string text, string from)
    {
        var r = new TomlReader(Toml.Parse(text, from), from);
        var out_ = new List<CatalogEntry>();
        foreach (var t in r.Array("plugin"))
        {
            var e = new CatalogEntry
            {
                Id = t.String("id"),
                Name = t.String("name"),
                Description = t.String("description"),
                Author = t.String("author"),
                Source = t.String("source"),
                Ref = t.String("ref"),
                Homepage = t.String("homepage"),
                Categories = t.Strings("categories"),
                From = from,
            };
            var tr = t.Sub("translations");
            foreach (var lang in tr.Table.Keys.ToList())
            {
                if (!LangPattern().IsMatch(lang)) throw new MazapanException($"{from}: {e.Id}: translations.{lang}: a language code (es, pt_BR)");
                var l = tr.Sub(lang);
                e.Translations[lang] = (l.String("name"), l.String("description"));
            }
            if (!Plugin.IdPattern().IsMatch(e.Id)) throw new MazapanException($"{from}: plugin id \"{e.Id}\": lowercase letters, digits and dashes");
            if (e.Source == "" || e.Source.StartsWith('-')) throw new MazapanException($"{from}: {e.Id}: source \"{e.Source}\"");
            try
            {
                if (e.Ref != "") Git.CheckRef(e.Ref);
            }
            catch (MazapanException x)
            {
                throw new MazapanException($"{from}: {e.Id}: ref {x.Message}");
            }
            foreach (var c in e.Categories)
                if (!KnownCategories.Contains(c)) throw new MazapanException($"{from}: {e.Id}: category \"{c}\" (one of {string.Join(", ", KnownCategories)})");
            if (e.Name == "") e.Name = e.Id;
            out_.Add(e);
        }
        r.Done();
        return out_;
    }
}
