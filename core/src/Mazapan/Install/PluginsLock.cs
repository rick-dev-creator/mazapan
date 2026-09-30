using System.Text;
using System.Text.RegularExpressions;
using Mazapan.Config;
using Mazapan.Util;
using Tomlyn.Model;

// Package install adds, updates and removes plugins from git, and keeps
// plugins.lock: for each one where it came from, the exact commit, and the
// capabilities the person approved. config.toml and plugins.lock are all it
// takes to put the same desktop on another machine (mazapan plugins sync).
namespace Mazapan.Install;

/// <summary>A plugin installed from git, as plugins.lock has it.</summary>
public sealed class Entry
{
    public string Id { get; set; } = "";
    /// <summary>Source is what git clones: a URL or a path.</summary>
    public string Source { get; set; } = "";
    /// <summary>
    /// Ref is the branch or tag followed by update; empty = the default
    /// branch.
    /// </summary>
    public string Ref { get; set; } = "";
    public string Commit { get; set; } = "";
    /// <summary>
    /// Approved are the capabilities the person said yes to
    /// (Plugin.Capabilities); an update that needs more asks again.
    /// </summary>
    public List<string> Approved { get; set; } = [];
    /// <summary>
    /// Installed from a catalog entry, whose ref it follows: when the catalog
    /// moves it, update goes there. A ref the person picks ends that.
    /// </summary>
    public bool Catalog { get; set; }

    /// <summary>
    /// A copy, as Go's <c>next := *e</c>: changing it leaves the lock's entry
    /// alone.
    /// </summary>
    public Entry Copy() => new() { Id = Id, Source = Source, Ref = Ref, Commit = Commit, Approved = [.. Approved], Catalog = Catalog };

    /// <summary>Takes every field of e, as Go's <c>*old = e</c>.</summary>
    internal void Assign(Entry e)
    {
        Id = e.Id;
        Source = e.Source;
        Ref = e.Ref;
        Commit = e.Commit;
        Approved = e.Approved;
        Catalog = e.Catalog;
    }
}

/// <summary>
/// plugins.lock: the plugins installed from git. Entries are references: an
/// entry from Get is the lock's own, and Put overwrites it in place (as Go's
/// pointer into the slice), so code holding it sees the new values.
/// </summary>
public sealed partial class PluginsLock
{
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*\z")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^([0-9a-f]{40}|[0-9a-f]{64})\z")]
    private static partial Regex FullCommit();

    public List<Entry> Plugins { get; set; } = [];

    public static string Path => Paths.ExpandHome("~/.config/mazapan/plugins.lock");

    static readonly string[] EntryKeys = ["id", "source", "ref", "commit", "approved", "catalog"];

    public static PluginsLock Load()
    {
        var l = new PluginsLock();
        var path = Path;
        var t = Toml.ReadFile(path);
        if (t == null) return l;
        var tables = new List<TomlTable>();
        var unknown = new List<string>();
        foreach (var (k, v) in t)
        {
            if (k != "plugin")
            {
                unknown.Add(TomlWriter.BareOrQuoted(k));
                continue;
            }
            switch (v)
            {
                case TomlTableArray a:
                    tables.AddRange(a);
                    break;
                // plugin = [] (what an emptied lock is written as) or inline tables.
                case TomlArray a when a.All(x => x is TomlTable):
                    tables.AddRange(a.Cast<TomlTable>());
                    break;
                default:
                    throw new MazapanException($"{path}: plugin: want [[plugin]] tables, got {v}");
            }
        }
        foreach (var table in tables)
        {
            var r = new TomlReader(table, path, "plugin");
            l.Plugins.Add(new Entry
            {
                Id = r.String("id"),
                Source = r.String("source"),
                Ref = r.String("ref"),
                Commit = r.String("commit"),
                Approved = r.Strings("approved"),
                Catalog = r.Bool("catalog"),
            });
            foreach (var (k, _) in table)
                if (!EntryKeys.Contains(k)) unknown.Add("plugin." + TomlWriter.BareOrQuoted(k));
        }
        if (unknown.Count > 0)
            throw new MazapanException($"{path}: unknown keys: [{string.Join(" ", unknown)}]");
        // It may come from another machine with the dotfiles: every value ends
        // up in a path or a git command.
        var seen = new HashSet<string>();
        foreach (var e in l.Plugins)
        {
            if (!IdPattern().IsMatch(e.Id))
                throw new MazapanException($"{path}: id {GoFormat.Quote(e.Id)}: lowercase letters, digits and dashes");
            if (seen.Contains(e.Id))
                throw new MazapanException($"{path}: {e.Id} is there twice");
            if (!FullCommit().IsMatch(e.Commit))
                throw new MazapanException($"{path}: {e.Id}: commit {GoFormat.Quote(e.Commit)} isn't a full commit id");
            if (e.Source == "" || e.Source.StartsWith('-'))
                throw new MazapanException($"{path}: {e.Id}: source {GoFormat.Quote(e.Source)}");
            try
            {
                Git.CheckRef(e.Ref);
            }
            catch (MazapanException ex)
            {
                throw new MazapanException($"{path}: {e.Id}: {ex.Message}", ex);
            }
            seen.Add(e.Id);
        }
        return l;
    }

    /// <summary>The lock's own entry for id (changes to it are the lock's), or null.</summary>
    public Entry? Get(string id) => Plugins.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Put adds e, or overwrites the entry with its id in place. The lock
    /// keeps a copy of e, with Approved sorted.
    /// </summary>
    public void Put(Entry e)
    {
        var c = e.Copy();
        c.Approved.Sort(StringComparer.Ordinal);
        if (Get(c.Id) is { } old)
        {
            old.Assign(c);
            return;
        }
        Plugins.Add(c);
        Plugins.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    public void Delete(string id) => Plugins.RemoveAll(e => e.Id == id);

    public const string Header = "# Plugins installed from git: source, commit and what you approved.\n" +
        "# Written by mazapan plugins add/update/remove; with config.toml it\n" +
        "# reproduces this desktop elsewhere (mazapan plugins sync).\n\n";

    /// <summary>
    /// The file as the Go version's TOML encoder wrote it: [[plugin]] tables
    /// with their keys indented by two spaces, a blank line between them,
    /// ref only when set; no plugins at all is "plugin = []".
    /// </summary>
    public string Encode()
    {
        var b = new StringBuilder(Header);
        if (Plugins.Count == 0)
        {
            b.Append("plugin = []\n");
            return b.ToString();
        }
        for (var i = 0; i < Plugins.Count; i++)
        {
            var e = Plugins[i];
            if (i > 0) b.Append('\n');
            b.Append("[[plugin]]\n");
            TomlWriter.Key(b, "id", e.Id, "  ");
            TomlWriter.Key(b, "source", e.Source, "  ");
            if (e.Ref != "") TomlWriter.Key(b, "ref", e.Ref, "  ");
            TomlWriter.Key(b, "commit", e.Commit, "  ");
            TomlWriter.Key(b, "approved", e.Approved, "  ");
            if (e.Catalog) TomlWriter.Key(b, "catalog", true, "  ");
        }
        return b.ToString();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.Dir(Path));
            Files.WriteAtomic(Path, Encode());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MazapanException($"{Path}: {e.Message}", e);
        }
    }
}
