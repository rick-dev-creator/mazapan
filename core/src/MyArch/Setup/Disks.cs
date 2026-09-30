using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyArch.Setup;

/// <summary>A disk the system can go on, as the installer shows it.</summary>
public sealed class Disk
{
    public string Path = "", Model = "", Transport = "";
    public long Size;
    public bool Removable;
    /// <summary>What's on it now, as far as its filesystems tell: "windows", "linux", "macos".</summary>
    public List<string> Systems = [];
    /// <summary>Partitions on it now (all erased).</summary>
    public int Partitions;

    public bool TooSmall => Size < Disks.MinSize;
}

/// <summary>
/// Disks reads lsblk: the disks there are, minus what isn't one to install
/// on (the medium this booted from, read-only ones, CD drives, RAM disks).
/// </summary>
public static partial class Disks
{
    /// <summary>Below this there's no room for the system and its snapshots.</summary>
    public const long MinSize = 20L * 1024 * 1024 * 1024;

    /// <summary>The arguments lsblk is run with, for Parse.</summary>
    public static readonly string[] LsblkArgs = ["-J", "-b", "-o", "PATH,TYPE,SIZE,MODEL,TRAN,RM,RO,FSTYPE,LABEL,PARTLABEL,MOUNTPOINTS"];

    /// <summary>The ISO's label (profiledef.sh: MYARCH_YYYYMM): the disk with it is the one this booted from.</summary>
    [GeneratedRegex(@"^MYARCH_\d{6}\z")] private static partial Regex MediumLabel();

    public static List<Disk> Parse(string lsblkJson)
    {
        var out_ = new List<Disk>();
        using var doc = JsonDocument.Parse(lsblkJson);
        if (!doc.RootElement.TryGetProperty("blockdevices", out var devs)) return out_;
        foreach (var d in devs.EnumerateArray())
        {
            if (Str(d, "type") != "disk" || Bool(d, "ro")) continue;
            var path = Str(d, "path");
            // zram swap, and the ISO's own device when it's a disk (a USB stick).
            if (path.StartsWith("/dev/zram") || path.StartsWith("/dev/loop")) continue;
            var parts = d.TryGetProperty("children", out var c) ? c.EnumerateArray().ToList() : [];
            if (MediumLabel().IsMatch(Str(d, "label")) || parts.Any(p => MediumLabel().IsMatch(Str(p, "label")))) continue;
            // In use: the medium this runs from (whatever its label: Rufus,
            // Ventoy), swap, anything mounted. Only a failed install's own
            // mounts (/mnt) don't count: the next one starts by taking them down.
            if (Mounts(d).Any(m => m != "/mnt" && !m.StartsWith("/mnt/"))) continue;
            var disk = new Disk
            {
                Path = path,
                Model = Str(d, "model").Trim(),
                Transport = Str(d, "tran"),
                Size = d.TryGetProperty("size", out var s) && s.TryGetInt64(out var n) ? n : 0,
                Removable = Bool(d, "rm"),
                Partitions = parts.Count,
            };
            foreach (var p in parts)
            {
                var fs = Str(p, "fstype");
                var label = Str(p, "partlabel") + " " + Str(p, "label");
                var sys = fs switch
                {
                    "ntfs" or "BitLocker" => "windows",
                    "apfs" or "hfsplus" => "macos",
                    "ext4" or "ext3" or "btrfs" or "xfs" or "f2fs" or "crypto_LUKS" => "linux",
                    _ => label.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ? "windows" : "",
                };
                if (sys != "" && !disk.Systems.Contains(sys)) disk.Systems.Add(sys);
            }
            out_.Add(disk);
        }
        return out_;
    }

    /// <summary>Where a device and everything on it (partitions, what's opened on them) is mounted; swap as "[SWAP]".</summary>
    static IEnumerable<string> Mounts(JsonElement e)
    {
        if (e.TryGetProperty("mountpoints", out var mp) && mp.ValueKind == JsonValueKind.Array)
            foreach (var m in mp.EnumerateArray())
                if (m.ValueKind == JsonValueKind.String && m.GetString() is { Length: > 0 } s) yield return s;
        if (e.TryGetProperty("children", out var c) && c.ValueKind == JsonValueKind.Array)
            foreach (var child in c.EnumerateArray())
                foreach (var m in Mounts(child)) yield return m;
    }

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    // Older lsblk says "0"/"1", newer true/false.
    static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && v.GetString() == "1"));
}
