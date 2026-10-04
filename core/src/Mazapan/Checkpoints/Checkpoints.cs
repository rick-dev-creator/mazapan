using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mazapan.Pacman;
using Mazapan.Util;

namespace Mazapan.Checkpoints;

/// <summary>
/// A checkpoint: the whole system as it was before a change, a snapper
/// snapshot of / ("pre" or "single"); or the previous system, kept a few
/// days after a checkpoint became the main one ("previous-…").
/// </summary>
public sealed record Checkpoint(string Id, long Number, DateTimeOffset When, string Kind, List<string> Packages, string Command)
{
    /// <summary>What it was before, in words: "before an update (12 packages)".</summary>
    public string Title => Checkpoints.Title(Kind, Packages, Command);
}

/// <summary>
/// Checkpoints anyone understands, on Mazapán's layout: btrfs with the
/// system in the subvolume @, snapper's snapshots nested in it
/// (@/.snapshots/N/snapshot). Each one is in the boot menu (started on an
/// overlay in memory: a snapshot is read-only); started, it says so, and it
/// can become the main system ("keep"), the one it replaces kept a few days
/// as "the previous system". With /boot on the root filesystem a snapshot
/// carries its own kernel; with /boot the EFI partition (encrypted
/// installs), each checkpoint's kernel and initramfs are kept there
/// (/boot/mazapan/k, by content, so the same ones are kept once).
/// </summary>
public static partial class Checkpoints
{
    /// <summary>Where the boot service says which checkpoint this is (for the desktop, which can't read the main system's snapshots).</summary>
    public const string RunState = "/run/mazapan/checkpoint.json";
    /// <summary>A checkpoint that became the main system, said once at the next start.</summary>
    public const string RunRestored = "/run/mazapan/restored.json";
    const string Restored = "/var/lib/mazapan/restored.json";
    /// <summary>What's in the boot menu, readable by the desktop (the EFI partition is root's only).</summary>
    public const string MenuState = "/var/lib/mazapan/checkpoints.json";
    /// <summary>The main system, read only, while on a checkpoint: what changed, for the person and an agent.</summary>
    public const string MainMount = "/run/mazapan/main";
    const string TopMount = "/run/mazapan/top";
    /// <summary>A checkpoint made the main system, from the running one: until the restart, nothing else touches the menu or /boot.</summary>
    public const string KeptPending = "/run/mazapan/checkpoint-kept";
    const string LockFile = "/run/mazapan/checkpoints.lock";
    /// <summary>The first start's checkpoint was taken.</summary>
    const string FirstMark = "/var/lib/mazapan/first-checkpoint";
    const string BootDir = "/boot/mazapan";
    const string Manifest = "/boot/mazapan/checkpoints.json";
    const string MenuFile = "mazapan-checkpoints.cfg";

    // --- what a checkpoint is -------------------------------------------------

    [GeneratedRegex(@"(?:^|\s)mazapan\.checkpoint=(\S+)")]
    private static partial Regex CheckpointArg();

    [GeneratedRegex(@"(?:^|[\s,=""])subvol=""?/?([^\s,""]*?)/?\.snapshots/(\d+)/snapshot")]
    private static partial Regex SnapshotSubvol();

    /// <summary>
    /// The checkpoint this system started from, by the kernel's line: ours
    /// say mazapan.checkpoint=ID; grub-btrfs's mount a snapshot as root.
    /// Null: the main system.
    /// </summary>
    public static string? Booted(string cmdline)
    {
        if (CheckpointArg().Match(cmdline) is { Success: true } m) return m.Groups[1].Value;
        if (SnapshotSubvol().Match(cmdline) is { Success: true } s) return s.Groups[2].Value;
        return null;
    }

    /// <summary>
    /// What a snapshot was before, from snapper's descriptions: snap-pac's
    /// "pre" is pacman's command, its "post" the packages it changed.
    /// </summary>
    public static (string Kind, List<string> Packages) Describe(string pre, string post)
    {
        if (pre.Trim() == "mazapan installed") return ("installed", []);
        var words = pre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var flags = string.Concat(words.Where(w => w.StartsWith('-') && !w.StartsWith("--")).Select(w => w[1..]));
        var longs = words.Where(w => w.StartsWith("--")).ToHashSet();
        var named = words.Skip(1).Where(w => !w.StartsWith('-')).Select(w => Path.GetFileName(w)).ToList();
        var packages = post.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!Path.GetFileName(words.FirstOrDefault() ?? "").StartsWith("pacman")) return ("other", packages);
        if ((flags.Contains('S') || longs.Contains("--sync")) && (flags.Contains('u') || longs.Contains("--sysupgrade")))
            return ("update", packages);
        if (flags.Contains('R') || longs.Contains("--remove")) return ("remove", packages.Count > 0 ? packages : named);
        if (flags.Contains('S') || flags.Contains('U') || longs.Contains("--sync") || longs.Contains("--upgrade"))
            return ("install", packages.Count > 0 ? packages : named);
        return ("other", packages);
    }

    public static string Title(string kind, List<string> packages, string command)
    {
        static string Few(List<string> p) => p.Count <= 3 ? string.Join(", ", p) : $"{string.Join(", ", p.Take(3))} and {p.Count - 3} more";
        return kind switch
        {
            "update" => packages.Count > 0 ? $"before an update ({packages.Count} {(packages.Count == 1 ? "package" : "packages")})" : "before an update",
            "install" => packages.Count > 0 ? $"before installing {Few(packages)}" : "before installing",
            "remove" => packages.Count > 0 ? $"before removing {Few(packages)}" : "before removing",
            "installed" => "the system as installed",
            "previous" => "the previous system",
            _ => command != "" ? $"before {command}" : "a checkpoint",
        };
    }

    /// <summary>
    /// Snapper's snapshots of / (each one's info.xml): the "pre" and "single"
    /// ones, newest first, each told by its "post" too.
    /// </summary>
    public static List<Checkpoint> List(string snapshots = "/.snapshots") => TryList(snapshots) ?? [];

    /// <summary>The same, null when they couldn't be read (nothing is pruned on a listing that failed).</summary>
    static List<Checkpoint>? TryList(string snapshots)
    {
        if (Raw(snapshots) is not { } items) return null;
        var out_ = new List<Checkpoint>();
        foreach (var x in items.Where(i => i.Type is "pre" or "single").OrderByDescending(i => i.N))
        {
            var post = items.FirstOrDefault(y => y.Type == "post" && y.Pre == x.N);
            var (kind, packages) = Describe(x.What, post.What ?? "");
            out_.Add(new Checkpoint(x.N.ToString(CultureInfo.InvariantCulture), x.N, x.Date, kind, packages, x.What));
        }
        return out_;
    }

    /// <summary>One of snapper's snapshots, as its info.xml says.</summary>
    public readonly record struct Snap(long N, string Type, long Pre, DateTimeOffset Date, string What);

    /// <summary>
    /// Every snapshot snapper has (from each info.xml). /.snapshots is root's
    /// alone (0750: old snapshots hold old setuid programs); without the
    /// rights, the list root wrote last (SnapsState) stands in. Null: neither.
    /// </summary>
    public static List<Snap>? Raw(string snapshots = "/.snapshots")
    {
        var items = new List<Snap>();
        try
        {
            if (!Directory.Exists(snapshots)) return snapshots == "/.snapshots" ? FromState() : null;
            foreach (var dir in Directory.GetDirectories(snapshots))
            {
                var info = Path.Join(dir, "info.xml");
                if (!File.Exists(info)) continue;
                try
                {
                    var x = System.Xml.Linq.XDocument.Load(info).Root!;
                    string V(string n) => x.Element(n)?.Value.Trim() ?? "";
                    if (!long.TryParse(V("num"), out var num) || num <= 0) continue;
                    // snapper writes UTC.
                    if (!DateTimeOffset.TryParse(V("date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) continue;
                    items.Add(new Snap(num, V("type"), long.TryParse(V("pre_num"), out var pn) ? pn : 0, date, V("description")));
                }
                catch (Exception e) when (e is System.Xml.XmlException or IOException or UnauthorizedAccessException) { }
            }
        }
        catch (UnauthorizedAccessException) { return snapshots == "/.snapshots" ? FromState() : null; }
        catch (IOException) { return null; }
        return items;
    }

    /// <summary>Snapshots' list for those who can't read /.snapshots (root writes it with the menu).</summary>
    public const string SnapsState = "/var/lib/mazapan/snapshots.json";

    static List<Snap>? FromState()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(SnapsState));
            return doc.RootElement.GetProperty("snapshots").EnumerateArray().Select(e => new Snap(
                e.GetProperty("n").GetInt64(), e.GetProperty("type").GetString() ?? "", e.GetProperty("pre").GetInt64(),
                DateTimeOffset.Parse(e.GetProperty("date").GetString()!, CultureInfo.InvariantCulture), e.GetProperty("what").GetString() ?? "")).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }

    static void SaveSnapsState(List<Snap> items)
    {
        Files.CreateDirectory(Paths.Dir(SnapsState));
        Files.WriteAtomic(SnapsState, GoJson.Marshal(new Fields { { "snapshots", items.OrderBy(i => i.N).Select(i => (object?)new Fields
        {
            { "n", i.N }, { "type", i.Type }, { "pre", i.Pre },
            { "date", i.Date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) }, { "what", i.What },
        }).ToList() } }) + "\n");
        File.SetUnixFileMode(SnapsState, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    // --- the boot menu --------------------------------------------------------

    /// <summary>The main entry of grub.cfg: its lines, the kernel and its arguments, the initrds.</summary>
    public sealed record GrubEntry(List<string> Body, string Kernel, string Args, List<string> Initrds);

    [GeneratedRegex(@"^\s*linux\s+(\S+)\s*(.*)$")]
    private static partial Regex LinuxLine();

    [GeneratedRegex(@"^\s*initrd\s+(.*)$")]
    private static partial Regex InitrdLine();

    /// <summary>grub-mkconfig's first menuentry (the system's own): what a checkpoint's entry is made from.</summary>
    public static GrubEntry? MainEntry(string cfg)
    {
        var lines = cfg.Split('\n');
        var start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("menuentry ") && l.TrimEnd().EndsWith('{'));
        if (start < 0) return null;
        var body = new List<string>();
        string kernel = "", args = "";
        var initrds = new List<string>();
        for (var i = start + 1; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l.Trim() == "}") break;
            if (LinuxLine().Match(l) is { Success: true } m)
            {
                kernel = m.Groups[1].Value;
                args = m.Groups[2].Value.Trim();
            }
            else if (InitrdLine().Match(l) is { Success: true } r)
                initrds = r.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            body.Add(l);
        }
        return kernel == "" ? null : new GrubEntry(body, kernel, args, initrds);
    }

    /// <summary>
    /// The kernel's arguments for a checkpoint: its subvolume as root, on an
    /// overlay in memory, named (mazapan.checkpoint), and never resuming
    /// from hibernation (that image is the main system's).
    /// </summary>
    public static string Args(string main, string subvol, string id)
    {
        var out_ = new List<string>();
        var flagged = false;
        foreach (var a in main.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (a.StartsWith("resume=") || a.StartsWith("resume_offset=") || a.StartsWith("mazapan.checkpoint=") || a.StartsWith("systemd.volatile=") || a == "systemd.mask=systemd-remount-fs.service") continue;
            if (a.StartsWith("rootflags="))
            {
                var opts = a["rootflags=".Length..].Split(',').Where(o => o != "" && !o.StartsWith("subvol=") && !o.StartsWith("subvolid="));
                out_.Add("rootflags=" + string.Join(",", opts.Prepend("subvol=" + subvol)));
                flagged = true;
                continue;
            }
            out_.Add(a);
        }
        if (!flagged) out_.Add("rootflags=subvol=" + subvol);
        out_.Add("systemd.volatile=overlay");
        // fstab's / (subvol=/@) can't be remounted onto the overlay: not tried.
        out_.Add("systemd.mask=systemd-remount-fs.service");
        out_.Add("mazapan.checkpoint=" + id);
        return string.Join(" ", out_);
    }

    static string GrubQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    /// <summary>
    /// One checkpoint's menu entry, from the main one: the same lines, its
    /// kernel and initrds (paths as GRUB sees them), its arguments.
    /// </summary>
    public static string Entry(GrubEntry main, string title, string kernel, List<string> initrds, string args)
    {
        var b = new StringBuilder();
        b.Append("  menuentry ").Append(GrubQuote(title)).Append(" --class arch {\n");
        foreach (var l in main.Body)
        {
            var t = l.TrimStart();
            if (t.StartsWith("echo")) continue;
            if (LinuxLine().IsMatch(l)) b.Append("    linux ").Append(kernel).Append(' ').Append(args).Append('\n');
            else if (InitrdLine().IsMatch(l)) b.Append("    initrd ").Append(string.Join(" ", initrds)).Append('\n');
            else b.Append("    ").Append(t).Append('\n');
        }
        b.Append("  }\n");
        return b.ToString();
    }

    /// <summary>A path in the main entry, moved to another subvolume (/boot on the root filesystem: "/@/boot/x" → "/@/.snapshots/12/snapshot/boot/x").</summary>
    public static string InSubvol(string path, string main, string subvol) =>
        path.StartsWith("/" + main + "/") ? "/" + subvol + path[(main.Length + 1)..] : path;

    // --- the kernels kept for each checkpoint (/boot the EFI partition) ------

    /// <summary>
    /// The manifest of kernels kept: the set /boot has now (with when it was
    /// seen), and each checkpoint's. A set maps the main entry's paths
    /// ("/vmlinuz-linux") to the copies ("k/3fa…").
    /// </summary>
    public sealed class Kept
    {
        public DateTimeOffset CurrentSince;
        public Dictionary<string, string> Current = [];
        public Dictionary<string, Dictionary<string, string>> Sets = [];
        /// <summary>False: the manifest couldn't be read (cut short by a power cut): nothing is pruned on it.</summary>
        public bool Valid = true;

        public static Kept Parse(string json)
        {
            var k = new Kept();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                static Dictionary<string, string> Map(JsonElement e) =>
                    e.ValueKind == JsonValueKind.Object
                        ? e.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(p => p.Name, p => p.Value.GetString()!)
                        : [];
                if (r.TryGetProperty("current_since", out var t) && t.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var since))
                    k.CurrentSince = since;
                if (r.TryGetProperty("current", out var c)) k.Current = Map(c);
                if (r.TryGetProperty("sets", out var s) && s.ValueKind == JsonValueKind.Object)
                    foreach (var p in s.EnumerateObject()) k.Sets[p.Name] = Map(p.Value);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException)
            {
                k.Valid = false;
            }
            return k;
        }

        public string ToJson() => GoJson.Marshal(new Fields
        {
            { "current_since", CurrentSince.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
            { "current", Current.ToDictionary(kv => kv.Key, kv => (object?)kv.Value) },
            { "sets", Sets.ToDictionary(kv => kv.Key, kv => (object?)kv.Value.ToDictionary(x => x.Key, x => (object?)x.Value)) },
        }) + "\n";

        /// <summary>
        /// Checkpoints taken since the current set was seen started with it:
        /// /boot changes only through pacman, whose hook records it.
        /// </summary>
        public void Assign(IEnumerable<Checkpoint> checkpoints)
        {
            if (Current.Count == 0) return;
            foreach (var c in checkpoints)
                if (!Sets.ContainsKey(c.Id) && c.When >= CurrentSince.AddSeconds(-1))
                    Sets[c.Id] = new(Current);
        }
    }

    static Kept LoadKept()
    {
        if (!File.Exists(Manifest)) return new Kept();
        var k = Kept.Parse(File.ReadAllText(Manifest));
        // Kept aside to look at; a new one starts, and nothing goes on this one's word.
        if (!k.Valid) File.Copy(Manifest, Manifest + ".unreadable", overwrite: true);
        return k;
    }

    static void SaveKept(Kept k)
    {
        Files.CreateDirectory(BootDir);
        WriteDurable(Manifest, Files.Utf8.GetBytes(k.ToJson()));
    }

    /// <summary>
    /// Written whole and on the disk before it counts (the EFI partition is
    /// FAT: a power cut can leave a file empty otherwise).
    /// </summary>
    static void WriteDurable(string path, byte[] b)
    {
        var tmp = path + ".mazapan-tmp";
        using (var f = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            f.Write(b);
            f.Flush(flushToDisk: true);
        }
        File.Move(tmp, path, overwrite: true);
        Exec.Run("sync", ["-f", path]);
    }

    static void CopyDurable(string src, string dst)
    {
        var tmp = dst + ".mazapan-tmp";
        using (var i = File.OpenRead(src))
        using (var o = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            i.CopyTo(o);
            o.Flush(flushToDisk: true);
        }
        File.Move(tmp, dst, overwrite: true);
        Exec.Run("sync", ["-f", dst]);
    }

    /// <summary>
    /// One at a time: pacman's hook, mkinitcpio's, snapper's cleanup, the
    /// start and a keep can all come at once, and each reads and writes the
    /// manifest and the menu. Within one process, again: it holds it already.
    /// </summary>
    static int lockDepth;
    static FileStream? lockFile;
    static T Locked<T>(Func<T> f)
    {
        if (lockDepth == 0)
        {
            Files.CreateDirectory(Paths.Dir(LockFile));
            var until = DateTime.UtcNow.AddMinutes(3);
            while (true)
            {
                try
                {
                    lockFile = new FileStream(LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    break;
                }
                catch (IOException) when (DateTime.UtcNow < until)
                {
                    Thread.Sleep(500);
                }
                catch (IOException)
                {
                    throw new MazapanException("checkpoints: another one is busy (" + LockFile + ")");
                }
            }
        }
        lockDepth++;
        try
        {
            return f();
        }
        finally
        {
            if (--lockDepth == 0)
            {
                lockFile?.Dispose();
                lockFile = null;
            }
        }
    }

    /// <summary>Why the menu and /boot are to be left alone now (null: they aren't).</summary>
    static string? HandsOff()
    {
        if (File.Exists(KeptPending)) return "a checkpoint was made the main system: restart first";
        if (Booted(ReadText("/proc/cmdline")) != null) return "on a checkpoint: the main system's /boot and menu are left alone";
        return null;
    }

    /// <summary>
    /// pacman's PreTransaction hook: no package changes on a checkpoint (its
    /// kernels would land on the main system's /boot, its modules in memory)
    /// nor after making one the main system, until the restart.
    /// </summary>
    public static int Guard()
    {
        if (HandsOff() is not { } why) return 0;
        Console.Error.WriteLine($"Not now: {why}. (sudo mazapan checkpoint keep makes this checkpoint the main system; then restart.)");
        return 1;
    }

    /// <summary>
    /// After every pacman transaction (its hook, after mkinitcpio's): the
    /// checkpoints taken since the last one get the kernels /boot had then,
    /// and what /boot has now is kept. Only with /boot apart from the root
    /// filesystem; with it inside, each snapshot carries its own.
    /// </summary>
    public static string Save(string grubCfg = "/boot/grub/grub.cfg") => Locked(() => SaveCore(grubCfg));

    static string SaveCore(string grubCfg)
    {
        if (BootOnRoot()) return "kernels: in each snapshot (/boot is on the root filesystem)";
        if (HandsOff() is { } why) return why;
        if (MainSubvol() is not { } main) return "checkpoints: not on Mazapán's layout (the system in a subvolume of its own)";
        var kept = LoadKept();
        kept.Assign(List(Path.Join("/", ".snapshots")));
        // /boot has changed and these kernels can't be told apart: no
        // checkpoint from now is given any (rather than the wrong ones).
        string Unknown(string why)
        {
            kept.Current = [];
            kept.CurrentSince = DateTimeOffset.UtcNow;
            SaveKept(kept);
            return why;
        }
        if (!File.Exists(grubCfg) || MainEntry(File.ReadAllText(grubCfg)) is not { } entry) return Unknown("no GRUB entry to take the kernel from");
        var set = new Dictionary<string, string>();
        var dir = Path.Join(BootDir, "k");
        Files.CreateDirectory(dir);
        long need = 0;
        var files = entry.Initrds.Prepend(entry.Kernel).Distinct().ToList();
        foreach (var f in files)
        {
            var src = Path.Join("/boot", f);
            if (!File.Exists(src)) return Unknown($"{src}: not there: kernels not kept");
            need += new FileInfo(src).Length;
        }
        // Room on the EFI partition: the oldest checkpoints' kernels go first.
        Prune(kept, keepIds: null);
        if (FreeBytes("/boot") is { } free && free < need + (64L << 20))
            return Unknown("not enough room on /boot to keep the kernels");
        foreach (var f in files)
        {
            var src = Path.Join("/boot", f);
            var hash = Hash(src);
            var copy = Path.Join(dir, hash);
            // A copy cut short (a power cut) is made again.
            if (!File.Exists(copy) || new FileInfo(copy).Length != new FileInfo(src).Length) CopyDurable(src, copy);
            set[f] = "k/" + hash;
        }
        kept.Current = set;
        kept.CurrentSince = DateTimeOffset.UtcNow;
        SaveKept(kept);
        return $"kernels kept: {string.Join(", ", files)}";
    }

    static string Hash(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s))[..32];
    }

    static long? FreeBytes(string path)
    {
        try { return new DriveInfo(path).AvailableFreeSpace; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    /// <summary>Sets of checkpoints that are gone go, then the copies no set names.</summary>
    static void Prune(Kept kept, HashSet<string>? keepIds)
    {
        if (!kept.Valid) return;
        if (keepIds != null)
            foreach (var id in kept.Sets.Keys.Where(id => !keepIds.Contains(id)).ToList()) kept.Sets.Remove(id);
        var used = kept.Sets.Values.SelectMany(s => s.Values).Concat(kept.Current.Values).ToHashSet();
        var dir = Path.Join(BootDir, "k");
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir))
            if (!used.Contains("k/" + Path.GetFileName(f))) File.Delete(f);
    }

    /// <summary>
    /// The boot menu's checkpoints (as root, after every pacman transaction,
    /// snapper's cleanup and every start): the newest `entries` checkpoints
    /// and the previous systems younger than `days`, in
    /// /boot/grub/mazapan-checkpoints.cfg, which grub.cfg reads when it's
    /// there (/etc/grub.d/42_mazapan_checkpoints). Older previous systems go.
    /// mainRoot: where the main system is (/, or the new one right after a
    /// keep, mounted apart).
    /// </summary>
    public static string Menu(int entries, int days) => Locked(() =>
        HandsOff() is { } why ? why
        : MainSubvol() is not { } main ? "checkpoints: not on Mazapán's layout (the system in a subvolume of its own)"
        : MenuCore(entries, days, main, "/"));

    /// <summary>main: the main system's subvolume ("@"), as it's called once a restart is done (a keep renames it first).</summary>
    static string MenuCore(int entries, int days, string main, string mainRoot)
    {
        var onRoot = BootOnRoot();
        var grubDir = onRoot ? Path.Join(mainRoot, "boot/grub") : "/boot/grub";
        var cfgPath = Path.Join(grubDir, "grub.cfg");
        if (!File.Exists(cfgPath) || MainEntry(File.ReadAllText(cfgPath)) is not { } entry) return "no GRUB entry to make the checkpoints' from";
        // A listing that failed prunes nothing.
        var listed0 = TryList(Path.Join(mainRoot, ".snapshots"));
        if (Raw(Path.Join(mainRoot, ".snapshots")) is { } raw) SaveSnapsState(raw);
        var all = listed0 ?? [];
        var kept = onRoot ? null : LoadKept();
        kept?.Assign(all);

        var shown = 0;
        var listed = new List<Fields>();
        var b = new StringBuilder("# Generated by mazapan (mazapan checkpoint menu): the checkpoints. Written again on every change.\n");
        b.Append("submenu 'Checkpoints: start the system as it was before a change' {\n");
        var keepIds = new HashSet<string>(all.Select(c => c.Id));
        WithTop(top =>
        {
            bool Add(string id, string subvol, string title, DateTimeOffset when)
            {
                // A snapshot without its fstab (one taken half way through an
                // install) would start without /home: not offered.
                if (!HasFstab(Path.Join(top, subvol))) return false;
                string kernel;
                List<string> initrds;
                if (onRoot)
                {
                    // Paths as GRUB sees them: from the top of the filesystem.
                    kernel = InSubvol(entry.Kernel, main, subvol);
                    initrds = entry.Initrds.Select(i => InSubvol(i, main, subvol)).ToList();
                    if (kernel == entry.Kernel || initrds.Prepend(kernel).Any(f => !File.Exists(Path.Join(top, f)))) return false;
                }
                else
                {
                    if (!Directory.Exists(Path.Join(top, subvol))) return false;
                    if (!kept!.Sets.TryGetValue(id, out var set)) return false;
                    if (!set.ContainsKey(entry.Kernel) || entry.Initrds.Any(i => !set.ContainsKey(i))) return false;
                    if (entry.Initrds.Prepend(entry.Kernel).Any(f => !File.Exists(Path.Join(BootDir, set[f])))) return false;
                    kernel = "/mazapan/" + set[entry.Kernel];
                    initrds = entry.Initrds.Select(i => "/mazapan/" + set[i]).ToList();
                }
                b.Append(Entry(entry, title, kernel, initrds, Args(entry.Args, subvol, id)));
                listed.Add(new Fields { { "id", id }, { "title", title }, { "when", when.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) } });
                shown++;
                return true;
            }

            var n = 0;
            foreach (var c in all)
            {
                if (n >= entries) break;
                if (Add(c.Id, $"{main}/.snapshots/{c.Number}/snapshot", $"{c.When.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture)}: {c.Title}", c.When)) n++;
            }
            // The previous systems (a checkpoint kept replaced them): a few days, then gone.
            var prefix = main + "-previous-";
            foreach (var d in Directory.GetDirectories(top, prefix + "*").Order(StringComparer.Ordinal).Reverse())
            {
                var name = Path.GetFileName(d);
                var stamp = name[prefix.Length..];
                if (!DateTimeOffset.TryParseExact(stamp, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var when)) continue;
                if (when < DateTimeOffset.Now.AddDays(-days))
                {
                    var r = Exec.Run("btrfs", ["subvolume", "delete", "--recursive", d]);
                    if (r.ExitCode != 0) Console.Error.WriteLine($"{name}: not deleted: {r.Stderr.Trim()}");
                    continue;
                }
                keepIds.Add("previous-" + stamp);
                Add("previous-" + stamp, name, $"The previous system (replaced {when.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture)})", when);
            }
        }, readOnly: false);
        b.Append("}\n");

        if (kept != null)
        {
            if (listed0 != null) Prune(kept, keepIds);
            SaveKept(kept);
        }
        var cfg = Path.Join(grubDir, MenuFile);
        if (shown == 0)
        {
            if (listed0 != null && File.Exists(cfg)) File.Delete(cfg);
        }
        else WriteDurable(cfg, Files.Utf8.GetBytes(b.ToString()));
        Files.CreateDirectory(Paths.Dir(MenuState));
        Files.WriteAtomic(MenuState, GoJson.Marshal(new Fields { { "entries", listed } }) + "\n");
        File.SetUnixFileMode(MenuState, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        return shown == 0 ? "no checkpoints to start from yet" : $"{shown} in the boot menu";
    }

    // --- keeping a checkpoint -------------------------------------------------

    /// <summary>
    /// A checkpoint becomes the main system at the next start: the main
    /// subvolume set aside as "@-previous-DATE" (kept `days`), a writable
    /// copy of the checkpoint in its place, the snapshots moved along; with
    /// /boot apart, the checkpoint's kernels put back as the system's (and
    /// the ones replaced kept, for the previous system's entry).
    /// </summary>
    public static string Keep(string id, int entries, int days) => Locked(() => KeepCore(id, entries, days));

    static string KeepCore(string id, int entries, int days)
    {
        if (File.Exists(KeptPending)) throw new MazapanException("a checkpoint was made the main system already: restart first");
        if (MainSubvol() is not { } main) throw new MazapanException("checkpoints: not on Mazapán's layout (the system in a subvolume of its own)");
        var booted = Booted(ReadText("/proc/cmdline")) != null;
        // pacman halfway through on the main system: its files are half changed.
        var db = booted ? Path.Join(MainMount, "var/lib/pacman/db.lck") : "/var/lib/pacman/db.lck";
        if (File.Exists(db)) throw new MazapanException("pacman is working (" + db + "): wait for it to finish");
        var onRoot = BootOnRoot();
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var prevName = $"{main}-previous-{stamp}";
        var kept = onRoot ? null : LoadKept();
        Dictionary<string, string>? set = null;
        if (kept != null)
        {
            if (!kept.Valid) throw new MazapanException("the kernels' manifest on /boot can't be read: nothing was changed");
            kept.Assign(List(SnapshotsDir(main)));
            if (!kept.Sets.TryGetValue(id, out set)) throw new MazapanException($"checkpoint {id}: its kernel wasn't kept: it can't start");
            // Before anything changes: every file there, whole, and room to put them in place.
            long need = 0;
            foreach (var (path, copy) in set)
            {
                var f = Path.Join(BootDir, copy);
                if (!File.Exists(f) || Hash(f) != Path.GetFileName(copy))
                    throw new MazapanException($"checkpoint {id}: its {path} on /boot is missing or damaged: nothing was changed");
                need += new FileInfo(f).Length;
            }
            if (FreeBytes("/boot") is { } free && free < need + (32L << 20))
                throw new MazapanException("not enough room on /boot: nothing was changed");
        }
        string result = "";
        // On a checkpoint, /boot is kept read only (it's the main system's): for this, writable.
        var remounted = booted && !onRoot && Exec.Run("mount", ["-o", "remount,rw", "/boot"]).ExitCode == 0;
        try
        {
            WithTop(top =>
            {
                var mainDir = Path.Join(top, main);
                var prevDir = Path.Join(top, prevName);
                string source;
                if (id.StartsWith("previous-"))
                {
                    source = Path.Join(top, $"{main}-{id}");
                    if (!Directory.Exists(source)) throw new MazapanException($"{id}: not there anymore");
                }
                else
                {
                    if (!long.TryParse(id, out var n) || !Directory.Exists(Path.Join(mainDir, ".snapshots", id, "snapshot")))
                        throw new MazapanException($"checkpoint {id}: not there");
                    source = Path.Join(prevDir, ".snapshots", n.ToString(CultureInfo.InvariantCulture), "snapshot");
                }
                // What lives in subvolumes of their own inside the system
                // (/var/lib/docker's layers, machines): a snapshot leaves them
                // out, so they move to the new one rather than go with the old.
                var nested = Nested(top, main).Where(p => p != ".snapshots" && !p.StartsWith(".snapshots/")).ToList();

                Directory.Move(mainDir, prevDir);
                var moved = new List<string>();
                var snapsMoved = false;
                var bootChanged = false;
                try
                {
                    var r = Exec.Run("btrfs", ["subvolume", "snapshot", source, mainDir]);
                    if (r.ExitCode != 0) throw new MazapanException("the checkpoint couldn't be copied: " + r.Stderr.Trim());
                    // Taken as pacman began (snap-pac's "pre"), it holds pacman's lock: gone.
                    File.Delete(Path.Join(mainDir, "var/lib/pacman/db.lck"));
                    // The snapshots, history and all, go with the main system.
                    var snaps = Path.Join(mainDir, ".snapshots");
                    var oldSnaps = Path.Join(prevDir, ".snapshots");
                    if (Directory.Exists(oldSnaps))
                    {
                        if (Directory.Exists(snaps)) Directory.Delete(snaps); // empty: a snapshot leaves nested ones out
                        Directory.Move(oldSnaps, snaps);
                        snapsMoved = true;
                    }
                    foreach (var rel in nested)
                    {
                        var to = Path.Join(mainDir, rel);
                        if (!Directory.Exists(Paths.Dir(to))) continue;
                        if (Directory.Exists(to))
                        {
                            if (Directory.EnumerateFileSystemEntries(to).Any()) continue;
                            Directory.Delete(to);
                        }
                        Directory.Move(Path.Join(prevDir, rel), to);
                        moved.Add(rel);
                    }
                    // Said once, at the next start of the new main system.
                    var note = Path.Join(mainDir, Restored.TrimStart('/'));
                    Files.CreateDirectory(Paths.Dir(note));
                    Files.WriteAtomic(note, GoJson.Marshal(new Fields { { "from", id }, { "previous", prevName }, { "until", DateTimeOffset.Now.AddDays(days).ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture) } }) + "\n");
                    if (kept != null)
                    {
                        // The kernels /boot has now are the previous system's.
                        var now = new Dictionary<string, string>(kept.Current);
                        if (now.Count > 0) kept.Sets["previous-" + stamp] = now;
                        bootChanged = true;
                        foreach (var (path, copy) in set!) CopyDurable(Path.Join(BootDir, copy), Path.Join("/boot", path));
                        kept.Current = new(set);
                        kept.CurrentSince = DateTimeOffset.UtcNow;
                        SaveKept(kept);
                    }
                }
                catch (Exception e) when (e is MazapanException or IOException or UnauthorizedAccessException)
                {
                    // Everything back as it was.
                    var undo = new List<string>();
                    foreach (var rel in moved)
                        try { Directory.Move(Path.Join(mainDir, rel), Path.Join(prevDir, rel)); } catch (IOException x) { undo.Add(x.Message); }
                    if (snapsMoved)
                        try { Directory.Move(Path.Join(mainDir, ".snapshots"), Path.Join(prevDir, ".snapshots")); } catch (IOException x) { undo.Add(x.Message); }
                    if (Directory.Exists(mainDir))
                    {
                        var d = Exec.Run("btrfs", ["subvolume", "delete", "--recursive", mainDir]);
                        if (d.ExitCode != 0) undo.Add(d.Stderr.Trim());
                    }
                    if (!Directory.Exists(mainDir))
                        try { Directory.Move(prevDir, mainDir); } catch (IOException x) { undo.Add(x.Message); }
                    if (bootChanged && kept != null)
                        foreach (var (path, copy) in kept.Sets.GetValueOrDefault("previous-" + stamp) ?? [])
                            try { CopyDurable(Path.Join(BootDir, copy), Path.Join("/boot", path)); } catch (IOException x) { undo.Add(x.Message); }
                    throw new MazapanException(undo.Count == 0
                        ? $"{e.Message}: nothing was changed"
                        : $"{e.Message}; putting it back failed too ({string.Join("; ", undo)}): the system is in {prevName}, ask for help before restarting");
                }
                // Until the restart, the running system isn't the main one anymore.
                if (!booted)
                {
                    Files.CreateDirectory(Paths.Dir(KeptPending));
                    File.WriteAllText(KeptPending, prevName + "\n");
                }
                result = MenuCore(entries, days, main, mainDir);
            }, readOnly: false);
        }
        finally
        {
            if (remounted) Exec.Run("mount", ["-o", "remount,ro", "/boot"]);
        }
        return result;
    }

    /// <summary>A system that mounts something besides itself (its fstab has a line).</summary>
    static bool HasFstab(string root)
    {
        try
        {
            return File.ReadLines(Path.Join(root, "etc/fstab")).Any(l => l.Trim() is { Length: > 0 } t && !t.StartsWith('#'));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The subvolumes inside the system's (paths relative to it), deepest last.</summary>
    static List<string> Nested(string top, string main)
    {
        var r = Exec.Run("btrfs", ["subvolume", "list", "-o", Path.Join(top, main)]);
        if (r.ExitCode != 0) return [];
        var out_ = new List<string>();
        foreach (var l in r.Stdout.Split('\n'))
        {
            var i = l.IndexOf(" path ", StringComparison.Ordinal);
            if (i < 0) continue;
            var p = l[(i + 6)..].Trim();
            // "path" is from the top: "@/var/lib/docker".
            if (p.StartsWith(main + "/")) out_.Add(p[(main.Length + 1)..]);
        }
        return out_.OrderBy(p => p.Count(c => c == '/')).ToList();
    }

    static string SnapshotsDir(string main) => Booted(ReadText("/proc/cmdline")) != null ? Path.Join(MainMount, ".snapshots") : "/.snapshots";

    // --- at every start -------------------------------------------------------

    /// <summary>
    /// At every start (as root): on a checkpoint, the main system mounted
    /// read only where the desktop can compare, and which checkpoint this is
    /// written down for it; on the main system, a checkpoint just kept is
    /// said once, and the menu written again.
    /// </summary>
    public static string Boot(int entries, int days) => Locked(() => BootCore(entries, days));

    static string BootCore(int entries, int days)
    {
        Files.CreateDirectory("/run/mazapan");
        var id = Booted(ReadText("/proc/cmdline"));
        if (id == null)
        {
            // The first start: the system as installed, whole (taken while
            // installing it would lack what's written last, its fstab).
            if (!File.Exists(FirstMark) && File.Exists("/etc/snapper/configs/root"))
            {
                var r = Exec.Run("snapper", ["--no-dbus", "-c", "root", "create", "-c", "number", "-d", "mazapan installed", "--userdata", "important=yes"]);
                if (r.ExitCode == 0)
                {
                    Files.CreateDirectory(Paths.Dir(FirstMark));
                    File.WriteAllText(FirstMark, DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\n");
                }
                else Console.Error.WriteLine("the first checkpoint: " + r.Stderr.Trim());
            }
            if (File.Exists(Restored))
            {
                File.Copy(Restored, RunRestored, overwrite: true);
                File.SetUnixFileMode(RunRestored, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                File.Delete(Restored);
                // /boot apart: only the main entry's kernel came back with the
                // checkpoint; every kernel and initramfs made again from this
                // system's own modules (as pacman's hook does).
                if (!BootOnRoot())
                {
                    Refresh();
                    // What /boot has now (mkinitcpio's hook couldn't: this holds the lock).
                    SaveCore("/boot/grub/grub.cfg");
                }
            }
            return MainSubvol() is { } m ? MenuCore(entries, days, m, "/") : "checkpoints: not on Mazapán's layout";
        }
        // The main system's /boot (the EFI partition): not this start's to change.
        if (!BootOnRoot()) Exec.Run("mount", ["-o", "remount,ro", "/boot"]);
        // The snapshot was taken as pacman began: its lock goes (in memory), so
        // pacman says why it won't run here rather than "unable to lock".
        try { File.Delete("/var/lib/pacman/db.lck"); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var main = MainSubvolFromCmdline() ?? "@";
        Files.CreateDirectory(MainMount);
        if (BtrfsDevice() is { } dev && !IsMounted(MainMount))
            Exec.Run("mount", ["-t", "btrfs", "-o", "ro,subvol=" + main, dev, MainMount]);
        Checkpoint? c = id.StartsWith("previous-")
            ? new Checkpoint(id, 0, DateTimeOffset.TryParseExact(id["previous-".Length..], "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var w) ? w : DateTimeOffset.Now, "previous", [], "")
            : List(Path.Join(MainMount, ".snapshots")).FirstOrDefault(x => x.Id == id);
        var f = new Fields
        {
            { "id", id },
            { "number", c?.Number ?? 0 },
            { "when", c?.When.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "" },
            { "kind", c?.Kind ?? "other" },
            { "packages", c?.Packages ?? [] },
            { "title", c?.Title ?? "a checkpoint" },
            { "main", IsMounted(MainMount) ? MainMount : "" },
        };
        Files.WriteAtomic(RunState, GoJson.Marshal(f) + "\n");
        File.SetUnixFileMode(RunState, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        return $"on checkpoint {id}: {c?.Title ?? "?"}";
    }

    static void Refresh()
    {
        foreach (var d in Directory.Exists("/usr/lib/modules") ? Directory.GetDirectories("/usr/lib/modules") : [])
        {
            var vmlinuz = Path.Join(d, "vmlinuz");
            var pkgbase = Path.Join(d, "pkgbase");
            if (!File.Exists(vmlinuz) || !File.Exists(pkgbase)) continue;
            var name = File.ReadAllText(pkgbase).Trim();
            if (!Regex.IsMatch(name, @"^[a-z0-9][a-z0-9._+-]*\z")) continue;
            CopyDurable(vmlinuz, Path.Join("/boot", "vmlinuz-" + name));
        }
        // Its post hook would wait for the lock this holds: told not to.
        var r = Exec.Run("mkinitcpio", ["-P"], ("MAZAPAN_CHECKPOINTS_LOCKED", "1"));
        if (r.ExitCode != 0) Console.Error.WriteLine("mkinitcpio -P: " + (r.Stderr.Trim() is { Length: > 0 } e ? e : r.Stdout.Trim()));
    }

    // --- what changed ---------------------------------------------------------

    /// <summary>Packages by name and version, from pacman's database folder names (name-version-release).</summary>
    public static Dictionary<string, string> Packages(string root)
    {
        var out_ = new Dictionary<string, string>(StringComparer.Ordinal);
        var dir = Path.Join(root, "var/lib/pacman/local");
        string[] dirs;
        try { dirs = Directory.Exists(dir) ? Directory.GetDirectories(dir) : []; }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException) { return out_; }
        foreach (var d in dirs)
        {
            var n = Path.GetFileName(d);
            var rel = n.LastIndexOf('-');
            if (rel <= 0) continue;
            var ver = n.LastIndexOf('-', rel - 1);
            if (ver <= 0) continue;
            out_[n[..ver]] = n[(ver + 1)..];
        }
        return out_;
    }

    /// <summary>What differs between two systems' packages: (name, in the first, in the second), "" where it isn't.</summary>
    public static List<(string Name, string A, string B)> Diff(Dictionary<string, string> a, Dictionary<string, string> b) =>
        a.Keys.Union(b.Keys).Order(StringComparer.Ordinal)
            .Select(n => (n, a.GetValueOrDefault(n, ""), b.GetValueOrDefault(n, "")))
            .Where(x => x.Item2 != x.Item3).ToList();

    // --- the filesystem -------------------------------------------------------

    /// <summary>/proc/self/mountinfo: (root within the filesystem, where, type, source).</summary>
    static List<(string Root, string Where, string Type, string Source)> Mounts()
    {
        static string Unescape(string s) => Regex.Replace(s, @"\\([0-7]{3})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 8)).ToString());
        var out_ = new List<(string, string, string, string)>();
        foreach (var l in ReadText("/proc/self/mountinfo").Split('\n'))
        {
            var dash = l.IndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0) continue;
            var a = l[..dash].Split(' ');
            var b = l[(dash + 3)..].Split(' ');
            if (a.Length < 5 || b.Length < 2) continue;
            out_.Add((Unescape(a[3]), Unescape(a[4]), b[0], Unescape(b[1])));
        }
        return out_;
    }

    static bool IsMounted(string where) => Mounts().Any(m => m.Where == where);

    static bool BootOnRoot() => !Mounts().Any(m => m.Where == "/boot");

    /// <summary>The main system's subvolume ("@"): / itself, or, on a checkpoint, what its snapshot sits in. Null: / is the top of the filesystem.</summary>
    public static string? MainSubvol()
    {
        if (MainSubvolFromCmdline() is { } fromCmdline) return fromCmdline;
        var root = Mounts().LastOrDefault(m => m.Where == "/");
        if (root.Type != "btrfs") return null;
        var s = root.Root.Trim('/');
        // Renamed under the running system by a keep (until the restart): its old name.
        if (Regex.Match(s, @"^(.+)-previous-\d{8}-\d{6}\z") is { Success: true } p) s = p.Groups[1].Value;
        return s == "" || s.Contains('/') ? null : s;
    }

    static string? MainSubvolFromCmdline()
    {
        var cmd = ReadText("/proc/cmdline");
        if (Booted(cmd) == null) return null;
        if (SnapshotSubvol().Match(cmd) is { Success: true } s && s.Groups[1].Value.Trim('/') is { Length: > 0 } m && !m.Contains('/')) return m;
        // A previous system: rootflags=subvol=@-previous-…
        if (Regex.Match(cmd, @"subvol=/?([^\s,/]+)-previous-") is { Success: true } p) return p.Groups[1].Value;
        return null;
    }

    /// <summary>The system's btrfs: /'s, or on a checkpoint (/ an overlay) the kernel line's root=.</summary>
    static string? BtrfsDevice()
    {
        var root = Mounts().LastOrDefault(x => x.Where == "/");
        if (root.Type == "btrfs") return root.Source;
        var cmd = ReadText("/proc/cmdline");
        if (Regex.Match(cmd, @"(?:^|\s)root=UUID=([0-9a-fA-F-]+)") is { Success: true } u) return "/dev/disk/by-uuid/" + u.Groups[1].Value;
        if (Regex.Match(cmd, @"(?:^|\s)root=(/dev/\S+)") is { Success: true } d) return d.Groups[1].Value;
        return null;
    }

    static string? topMounted;

    /// <summary>The top of the btrfs filesystem (every subvolume in it), mounted for the moment.</summary>
    static void WithTop(Action<string> act, bool readOnly)
    {
        if (topMounted != null)
        {
            act(topMounted);
            return;
        }
        if (BtrfsDevice() is not { } dev) throw new MazapanException("no btrfs filesystem mounted");
        // A folder of this process's own: another one's mount never stacks on it.
        var at = TopMount + "-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        Files.CreateDirectory(at);
        var r = Exec.Run("mount", ["-t", "btrfs", "-o", (readOnly ? "ro," : "") + "subvolid=5", dev, at]);
        if (r.ExitCode != 0) throw new MazapanException("the filesystem's top couldn't be mounted: " + r.Stderr.Trim());
        topMounted = at;
        try
        {
            act(at);
        }
        finally
        {
            topMounted = null;
            if (Exec.Run("umount", [at]).ExitCode == 0) Directory.Delete(at);
            else Console.Error.WriteLine($"{at}: still mounted");
        }
    }

    static string ReadText(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }
}
