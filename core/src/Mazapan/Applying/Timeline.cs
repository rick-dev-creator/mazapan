using Mazapan.Config;

namespace Mazapan.Applying;

/// <summary>
/// Timeline says what an apply changed, in the terms a person set it in:
/// the theme, the accent, the language, a plugin turned on or off, a
/// setting from one value to another. It compares config.toml before and
/// after (the snapshot keeps both); files that changed without config.toml
/// changing (a plugin's new version, a hand-edited file taken over) are only
/// counted.
/// </summary>
public static class Timeline
{
    /// <summary>
    /// One change. Kind: theme, accent, language, font (Key: ui, mono, size), on, off, setting, catalog
    /// (added), catalog-removed. From/To: TOML values as read (null: not set,
    /// the default).
    /// </summary>
    public sealed record Change(string Kind, string Plugin = "", string Key = "", object? From = null, object? To = null);

    public static List<Change> Diff(Settings a, Settings b)
    {
        var out_ = new List<Change>();
        if (a.Theme != b.Theme) out_.Add(new Change("theme", From: a.Theme, To: b.Theme));
        if (a.Accent != b.Accent) out_.Add(new Change("accent", From: Blank(a.Accent), To: Blank(b.Accent)));
        if (a.Language != b.Language) out_.Add(new Change("language", From: Blank(a.Language), To: Blank(b.Language)));
        if (a.FontUI != b.FontUI) out_.Add(new Change("font", Key: "ui", From: Blank(a.FontUI), To: Blank(b.FontUI)));
        if (a.FontMono != b.FontMono) out_.Add(new Change("font", Key: "mono", From: Blank(a.FontMono), To: Blank(b.FontMono)));
        if (a.FontSize != b.FontSize) out_.Add(new Change("font", Key: "size", From: a.FontSize > 0 ? a.FontSize : null, To: b.FontSize > 0 ? b.FontSize : null));
        // Off: disabled (or no longer enabled, a hardware plugin); on: the other way.
        foreach (var id in b.Disabled.Except(a.Disabled).Concat(a.Enabled.Except(b.Enabled)).Distinct())
            out_.Add(new Change("off", id));
        foreach (var id in a.Disabled.Except(b.Disabled).Concat(b.Enabled.Except(a.Enabled)).Distinct())
            out_.Add(new Change("on", id));
        foreach (var id in a.Plugins.Keys.Union(b.Plugins.Keys).Order(StringComparer.Ordinal))
        {
            var x = a.Plugins.GetValueOrDefault(id);
            var y = b.Plugins.GetValueOrDefault(id);
            var keys = (x?.Keys ?? Enumerable.Empty<string>()).Union(y?.Keys ?? Enumerable.Empty<string>()).Order(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                var from = x?.GetValueOrDefault(k);
                var to = y?.GetValueOrDefault(k);
                if (Same(from, to)) continue;
                out_.Add(new Change("setting", id, k, from, to));
            }
        }
        foreach (var c in b.Catalogs.Except(a.Catalogs)) out_.Add(new Change("catalog", To: c));
        foreach (var c in a.Catalogs.Except(b.Catalogs)) out_.Add(new Change("catalog-removed", From: c));
        return out_;
    }

    static object? Blank(string s) => s == "" ? null : s;

    /// <summary>Same: the same TOML value (or both unset).</summary>
    public static bool Same(object? a, object? b) =>
        a == null || b == null ? a == null && b == null : TomlWriter.Value(a) == TomlWriter.Value(b);

    /// <summary>
    /// The config.toml an apply started from and the one it left, for each
    /// snapshot (newest first): its own copies, or for a snapshot from
    /// before they were kept, the next one's "before" (or today's
    /// config.toml, for the newest) when it's the same file. Null when
    /// unknown.
    /// </summary>
    public static List<(Snapshots.Snapshot Snap, Settings? Before, Settings? After)> Configs(List<Snapshots.Snapshot> snaps, string configPath)
    {
        var out_ = new List<(Snapshots.Snapshot, Settings?, Settings?)>();
        for (var i = 0; i < snaps.Count; i++)
        {
            var s = snaps[i];
            Settings? before = null, after = null;
            try
            {
                if (s.ConfigBefore == "") before = new Settings();
                else if (Snapshots.ConfigFile(s, false) is { } b) before = Settings.LoadFrom(b);
                if (s.ConfigAfter == s.ConfigBefore) after = before;
                else if (Snapshots.ConfigFile(s, true) is { } a) after = Settings.LoadFrom(a);
                else if (i > 0 && snaps[i - 1].ConfigBefore == s.ConfigAfter && Snapshots.ConfigFile(snaps[i - 1], false) is { } n)
                    after = Settings.LoadFrom(n);
                else if (i == 0 && Snapshots.SumOf(configPath) == s.ConfigAfter && File.Exists(configPath))
                    after = Settings.LoadFrom(configPath);
            }
            catch (Mazapan.Util.MazapanException)
            {
                // A copy that doesn't read (edited by hand into nonsense): unknown.
            }
            out_.Add((s, before, after));
        }
        return out_;
    }
}
