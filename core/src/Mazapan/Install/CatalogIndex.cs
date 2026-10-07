using System.Security.Cryptography;
using System.Text;
using Mazapan.Plugins;
using Mazapan.Util;

namespace Mazapan.Install;

/// <summary>
/// A catalog lists plugins one can add: where each lives (a git repository)
/// and what it is. It's only pointers: adding one is still `plugins add`,
/// with its capabilities shown and approved. Catalogs are TOML files
/// ([[plugin]] tables), read from a path or a URL (cached for a day).
/// Mazapan's own is the plugin registry's (mazapan.dev/plugins/index.toml,
/// made from each plugin's repository), with a copy shipped for when it
/// can't be reached; config.toml can add others: catalogs = ["https://…/index.toml"].
/// </summary>
public sealed class CatalogEntry
{
    public string Id = "", Name = "", Description = "", Author = "", Source = "", Ref = "", Homepage = "";
    /// <summary>
    /// Commit: the exact commit the catalog lists (the one its maintainers
    /// looked at); "" follows whatever ref is. When set, that's what gets
    /// installed and updated to, whatever the ref points to by then.
    /// </summary>
    public string Commit = "";
    /// <summary>Its version and license at that commit, as the catalog read them: words for the list.</summary>
    public string Version = "", License = "";
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

    /// <summary>
    /// Mazapan's own catalog: the plugin registry's, as mazapan.dev publishes
    /// it. MAZAPAN_PLUGIN_CATALOG puts another in its place (a path or an
    /// https URL; tests, a mirror).
    /// </summary>
    public static string Official =>
        Environment.GetEnvironmentVariable("MAZAPAN_PLUGIN_CATALOG") is { Length: > 0 } c ? c : "https://mazapan.dev/plugins/index.toml";

    [System.Text.RegularExpressions.GeneratedRegex(@"^([0-9a-f]{40}|[0-9a-f]{64})\z")]
    private static partial System.Text.RegularExpressions.Regex FullCommit();

    /// <summary>
    /// All entries of every catalog, first catalog first; an id seen again is
    /// skipped. Problems don't stop the rest. fallback: read in the first
    /// catalog's place when it can't be read at all (never reached, nothing
    /// cached): the copy of the registry shipped with mazapan.
    /// </summary>
    public static (List<CatalogEntry> Entries, List<string> Problems) Load(IEnumerable<string> catalogs, bool refresh, string? fallback = null)
    {
        var entries = new List<CatalogEntry>();
        var problems = new List<string>();
        var seen = new HashSet<string>();
        var first = true;
        foreach (var c in catalogs)
        {
            List<CatalogEntry> read;
            try
            {
                var text = Fetch(c, refresh, t => Parse(t, c));
                if (text == null) continue;
                read = Parse(text, c);
            }
            catch (Exception ex) when (ex is MazapanException or IOException or HttpRequestException or TaskCanceledException or UnauthorizedAccessException or UriFormatException or InvalidOperationException)
            {
                problems.Add($"catalog {c}: {ex.Message}");
                if (!first || fallback == null || !File.Exists(fallback)) continue;
                try
                {
                    read = Parse(File.ReadAllText(fallback), fallback);
                }
                catch (Exception fx) when (fx is MazapanException or IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    problems.Add($"catalog {fallback}: {fx.Message}");
                    continue;
                }
            }
            finally
            {
                first = false;
            }
            foreach (var e in read)
                if (seen.Add(e.Id)) entries.Add(e);
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

    /// <summary>
    /// Parse reads a catalog: every entry checked, as each is only a pointer
    /// that must lead somewhere sane. Keys it doesn't know are left alone: a
    /// catalog is read by every Mazapan out there, older ones too, and one
    /// that says more than they know must still list its plugins.
    /// </summary>
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
                Commit = t.String("commit"),
                Version = t.String("version"),
                License = t.String("license"),
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
            if (e.Commit != "" && !FullCommit().IsMatch(e.Commit))
                throw new MazapanException($"{from}: {e.Id}: commit \"{e.Commit}\" isn't a full commit id");
            // A category this Mazapan doesn't know (a newer one's) is left out.
            e.Categories = [.. e.Categories.Where(KnownCategories.Contains)];
            if (e.Name == "") e.Name = e.Id;
            out_.Add(e);
        }
        return out_;
    }
}
