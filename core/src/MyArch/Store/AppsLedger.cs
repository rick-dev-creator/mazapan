using System.Globalization;
using System.Text.Json;
using MyArch.Util;

namespace MyArch.Store;

/// <summary>An install or removal from the Apps menu, as it happened.</summary>
public sealed class AppsTx
{
    public string Id = "", Action = "";
    public DateTimeOffset Time = DateTimeOffset.Now;
    /// <summary>The apps it did (only those that went through), their names then.</summary>
    public List<string> Apps = [], Names = [];
    /// <summary>What it put there, or took out: packages, Flatpaks (yours), web apps (their addresses), plugins turned on.</summary>
    public List<string> Packages = [], Flatpaks = [], Webapps = [], Plugins = [];

    public bool Empty => Apps.Count == 0 && Packages.Count == 0 && Flatpaks.Count == 0 && Webapps.Count == 0 && Plugins.Count == 0;
}

/// <summary>
/// AppsLedger keeps every install and removal (one JSON file each, in
/// ~/.local/state/myarch/apps): for the timeline, and so only what the
/// Apps menu put there is ever taken out.
/// </summary>
public static class AppsLedger
{
    public static void Save(string dir, AppsTx tx)
    {
        tx.Id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(dir);
        Files.WriteAtomic(Paths.Join(dir, tx.Id + ".json"), GoJson.Marshal(new Fields
        {
            { "id", tx.Id }, { "action", tx.Action }, { "time", tx.Time.ToString("o", CultureInfo.InvariantCulture) },
            { "apps", tx.Apps }, { "names", tx.Names }, { "packages", tx.Packages }, { "flatpaks", tx.Flatpaks },
            { "webapps", tx.Webapps }, { "plugins", tx.Plugins },
        }) + "\n");
    }

    /// <summary>Every one, newest first; a file that doesn't read (or names what can't be a name) is skipped.</summary>
    public static List<AppsTx> List(string dir)
    {
        var out_ = new List<AppsTx>();
        if (!Directory.Exists(dir)) return out_;
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                var r = doc.RootElement;
                List<string> L(string n) => r.TryGetProperty(n, out var a) && a.ValueKind == JsonValueKind.Array
                    ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
                var tx = new AppsTx
                {
                    Id = r.GetProperty("id").GetString() ?? "",
                    Action = r.GetProperty("action").GetString() ?? "",
                    Time = DateTimeOffset.Parse(r.GetProperty("time").GetString() ?? "", CultureInfo.InvariantCulture),
                    Apps = L("apps"), Names = L("names"), Packages = L("packages"), Flatpaks = L("flatpaks"),
                    Webapps = L("webapps"), Plugins = L("plugins"),
                };
                // Yours to edit, so checked as if a stranger wrote it: what it
                // names ends up in commands.
                if (tx.Action is not ("install" or "remove")) continue;
                if (!tx.Packages.All(AppCatalog.IsPackage) || !tx.Flatpaks.All(AppCatalog.IsFlatpak)
                    || !tx.Webapps.All(AppCatalog.IsWebapp) || !tx.Plugins.All(p => MyArch.Plugins.Plugin.IdPattern().IsMatch(p)))
                    continue;
                out_.Add(tx);
            }
            catch (Exception e) when (e is JsonException or IOException or FormatException or KeyNotFoundException or InvalidOperationException) { }
        }
        return out_.OrderByDescending(t => t.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Owned: what the Apps menu put there and hasn't taken out since, going
    /// through the ledger in order.
    /// </summary>
    public static (HashSet<string> Packages, HashSet<string> Flatpaks, HashSet<string> Webapps) Owned(IEnumerable<AppsTx> txs)
    {
        HashSet<string> pk = [], fp = [], wa = [];
        foreach (var t in txs.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            var add = t.Action == "install";
            foreach (var p in t.Packages) { if (add) pk.Add(p); else pk.Remove(p); }
            foreach (var f in t.Flatpaks) { if (add) fp.Add(f); else fp.Remove(f); }
            foreach (var w in t.Webapps) { if (add) wa.Add(w.TrimEnd('/')); else wa.Remove(w.TrimEnd('/')); }
        }
        return (pk, fp, wa);
    }

    /// <summary>
    /// Never removed by the Apps menu, whoever installed them: what the system
    /// stands on, and what myarch itself runs.
    /// </summary>
    public static readonly HashSet<string> Protected =
    [
        "base", "base-devel", "linux", "linux-lts", "linux-zen", "linux-hardened", "linux-firmware", "systemd", "pacman", "sudo",
        "glibc", "bash", "coreutils", "grub", "limine", "efibootmgr", "mkinitcpio", "btrfs-progs", "networkmanager", "iwd",
        "git", "curl", "jq", "hyprland", "quickshell", "pipewire", "wireplumber", "polkit", "flatpak",
    ];
}
