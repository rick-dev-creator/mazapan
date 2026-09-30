using System.Globalization;
using MyArch.Util;

namespace MyArch.Hardware;

/// <summary>A PCI device: ids as lowercase hex ("10de", "2684"), its class ("0300"), driver and name.</summary>
public sealed record PciDevice(string Vendor, string Device, string Class, string Driver, string Name)
{
    /// <summary>A display controller: a GPU (class 03xx).</summary>
    public bool IsGpu => Class.StartsWith("03");
}

/// <summary>A USB device: ids as lowercase hex, and its product name.</summary>
public sealed record UsbDevice(string Vendor, string Product, string Name);

/// <summary>
/// Machine is what hardware plugins match against, read from /sys and /proc
/// without root: the model (DMI), PCI and USB devices, input devices and
/// loaded kernel modules. Root is "/" but for tests.
/// </summary>
public sealed class Machine
{
    public string Vendor = "", Product = "", Version = "", BoardVendor = "", Board = "";
    public List<PciDevice> Pci = [];
    public List<UsbDevice> Usb = [];
    public List<string> Inputs = [];
    public HashSet<string> Modules = [];
    /// <summary>The root filesystem's type ("btrfs", "ext4").</summary>
    public string Filesystem = "";
    /// <summary>The boot loader: "grub", "limine", "systemd-boot"; "" unknown.</summary>
    public string Bootloader = "";
    /// <summary>
    /// The kernels are on the root filesystem (/boot isn't a partition of
    /// its own): a snapshot of / has the kernel that goes with its modules.
    /// </summary>
    public bool BootOnRoot;
    /// <summary>The ISO's live system (archiso's): nothing installed yet.</summary>
    public bool Live;

    public IEnumerable<PciDevice> Gpus => Pci.Where(p => p.IsGpu);

    /// <summary>
    /// As installed: this machine's hardware (seen from the ISO), the way
    /// the installer puts the system on it. What hardware plugins to turn on
    /// while installing is worked out against it.
    /// </summary>
    public Machine Installed(string filesystem, string bootloader, bool bootOnRoot)
    {
        var m = (Machine)MemberwiseClone();
        m.Filesystem = filesystem;
        m.Bootloader = bootloader;
        m.BootOnRoot = bootOnRoot;
        m.Live = false;
        return m;
    }

    public static Machine Read(string root = "/")
    {
        string R(string p) => Paths.Join(root, p);
        static string Text(string path)
        {
            try
            {
                return File.ReadAllText(path).Trim();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return "";
            }
        }
        static string Hex(string s) => s.StartsWith("0x") ? s[2..].ToLowerInvariant() : s.ToLowerInvariant();

        var m = new Machine
        {
            Vendor = Text(R("sys/class/dmi/id/sys_vendor")),
            Product = Text(R("sys/class/dmi/id/product_name")),
            Version = Text(R("sys/class/dmi/id/product_version")),
            BoardVendor = Text(R("sys/class/dmi/id/board_vendor")),
            Board = Text(R("sys/class/dmi/id/board_name")),
        };
        var names = PciNames.Load(R("usr/share/hwdata/pci.ids"));
        foreach (var dir in Dirs(R("sys/bus/pci/devices")))
        {
            var vendor = Hex(Text(Paths.Join(dir, "vendor")));
            var device = Hex(Text(Paths.Join(dir, "device")));
            var cls = Hex(Text(Paths.Join(dir, "class")));
            if (vendor == "") continue;
            var driver = new FileInfo(Paths.Join(dir, "driver")).LinkTarget is { } t ? Paths.Base(t) : "";
            m.Pci.Add(new PciDevice(vendor, device, cls.Length >= 4 ? cls[..4] : cls, driver, names.Name(vendor, device)));
        }
        foreach (var dir in Dirs(R("sys/bus/usb/devices")))
        {
            var vendor = Text(Paths.Join(dir, "idVendor")).ToLowerInvariant();
            if (vendor == "") continue;
            m.Usb.Add(new UsbDevice(vendor, Text(Paths.Join(dir, "idProduct")).ToLowerInvariant(), Text(Paths.Join(dir, "product"))));
        }
        foreach (var line in Lines(R("proc/bus/input/devices")))
            if (line.StartsWith("N: Name=")) m.Inputs.Add(line[8..].Trim('"'));
        foreach (var line in Lines(R("proc/modules")))
            if (line.Split(' ') is [var mod, ..] && mod != "") m.Modules.Add(mod);
        var mounts = Lines(R("proc/mounts")).Select(l => l.Split(' ')).Where(f => f.Length > 2).ToList();
        foreach (var f in mounts)
            if (f[1] == "/") m.Filesystem = f[2];
        // A snapshot started from the menu runs on an overlay: what's under it is btrfs.
        if (m.Filesystem == "overlay" && Text(R("proc/cmdline")).Contains("rootflags=subvol=")) m.Filesystem = "btrfs";
        m.BootOnRoot = !mounts.Any(f => f[1] == "/boot");
        m.Live = Directory.Exists(R("run/archiso")) && (root != "/" || !InChroot());
        m.Bootloader = BootloaderOf(R, efi: root != "/" || !InChroot());
        return m;
    }

    /// <summary>
    /// In a chroot (an install, from the ISO): the firmware's variables then
    /// say how the ISO started, not the system being installed.
    /// </summary>
    static bool? inChroot;
    static bool InChroot() => inChroot ??= DetectChroot();
    static bool DetectChroot()
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("systemd-detect-virt", "--chroot")
                { RedirectStandardOutput = true, RedirectStandardError = true })!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The boot loader: what the firmware says started (LoaderInfo, set by
    /// systemd-boot and Limine), else its configuration's files (an EFI
    /// partition at /boot is often readable by root only), else its package.
    /// </summary>
    static string BootloaderOf(Func<string, string> R, bool efi = true)
    {
        if (efi) try
        {
            foreach (var v in Directory.Exists(R("sys/firmware/efi/efivars")) ? Directory.GetFiles(R("sys/firmware/efi/efivars"), "LoaderInfo-*") : [])
            {
                var b = File.ReadAllBytes(v);
                var s = b.Length > 4 ? System.Text.Encoding.Unicode.GetString(b, 4, b.Length - 4).ToLowerInvariant() : "";
                if (s.Contains("systemd-boot")) return "systemd-boot";
                if (s.Contains("limine")) return "limine";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        bool Any(params string[] ps) => ps.Any(p =>
        {
            try { return File.Exists(R(p)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
        });
        if (Any("boot/grub/grub.cfg", "boot/grub2/grub.cfg")) return "grub";
        if (Any("boot/limine.conf", "boot/limine/limine.conf", "boot/EFI/limine/limine.conf", "efi/limine.conf", "boot/EFI/BOOT/limine.conf")) return "limine";
        if (Any("boot/loader/loader.conf", "efi/loader/loader.conf")) return "systemd-boot";
        // Only its package is there to say (/boot unreadable): GRUB with its settings.
        bool Installed(string pkg) => Dirs(R("var/lib/pacman/local")).Any(d => Paths.Base(d).StartsWith(pkg + "-") && char.IsDigit(Paths.Base(d)[pkg.Length + 1]));
        if (Any("etc/default/grub") && Installed("grub")) return "grub";
        if (Installed("limine")) return "limine";
        return "";
    }

    static IEnumerable<string> Dirs(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.GetFileSystemEntries(path).Order(StringComparer.Ordinal).ToList() : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    static IEnumerable<string> Lines(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllLines(path) : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>Device names from the PCI id database (hwdata's pci.ids): "NVIDIA Corporation AD102 [GeForce RTX 4090]".</summary>
sealed class PciNames
{
    readonly Dictionary<string, string> vendors = [];
    readonly Dictionary<string, string> devices = [];

    public static PciNames Load(string path)
    {
        var n = new PciNames();
        if (!File.Exists(path)) return n;
        var vendor = "";
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.Length < 6) continue;
            if (line.StartsWith('C')) break; // device classes: past the devices
            if (line[0] != '\t')
            {
                vendor = line[..4].ToLowerInvariant();
                n.vendors[vendor] = line[4..].Trim();
            }
            else if (line[1] != '\t' && vendor != "")
                n.devices[vendor + ":" + line[1..5].ToLowerInvariant()] = line[5..].Trim();
        }
        return n;
    }

    public string Name(string vendor, string device)
    {
        var v = vendors.GetValueOrDefault(vendor, vendor);
        return devices.TryGetValue(vendor + ":" + device, out var d) ? $"{v} {d}" : v;
    }
}

/// <summary>
/// Rules say which machines a hardware plugin is for ([hardware] in its
/// plugin.toml). Every rule given must hold; a rule with several patterns
/// holds when any of them matches (shell patterns, case-insensitive).
/// </summary>
public sealed class Rules
{
    /// <summary>DMI: the maker, the model, the board ("LENOVO", "*Yoga Pro 7*").</summary>
    public List<string> Vendor = [], Product = [], Board = [];
    /// <summary>PCI and USB ids, vendor:device in hex ("10de:*", "05ac:0250").</summary>
    public List<string> Pci = [], Usb = [];
    /// <summary>A GPU's name from the PCI database ("*GeForce RTX 40*", "Intel*Iris*").</summary>
    public List<string> Gpu = [];
    /// <summary>An input device's name ("*Synaptics*").</summary>
    public List<string> Input = [];
    /// <summary>A loaded kernel module ("hid_apple").</summary>
    public List<string> Modules = [];
    /// <summary>At least this many GPUs (2: one renders, another drives screens).</summary>
    public long Gpus;
    /// <summary>The root filesystem ("btrfs"), and the boot loader ("grub", "limine", "systemd-boot").</summary>
    public List<string> Filesystem = [], Bootloader = [];
    /// <summary>The kernels on the root filesystem (no /boot partition of its own).</summary>
    public bool BootOnRoot;
    /// <summary>The ISO's live system (the installer is for it).</summary>
    public bool Live;
    /// <summary>Any machine: a plugin that's for every one, but changes the system, so only on when asked.</summary>
    public bool Any_;

    /// <summary>Only "any": for every machine, so never offered as this one's (it's asked for).</summary>
    public bool OnlyAny => Any_ && new Rules { Vendor = Vendor, Product = Product, Board = Board, Pci = Pci, Usb = Usb, Gpu = Gpu,
        Input = Input, Modules = Modules, Gpus = Gpus, Filesystem = Filesystem, Bootloader = Bootloader, BootOnRoot = BootOnRoot, Live = Live }.Empty;

    /// <summary>Offered: this machine's, to suggest turning on (doctor, status, the Plugins panel).</summary>
    public bool Offered(Machine m) => !OnlyAny && Matches(m).Ok;

    public bool Empty => Vendor.Count + Product.Count + Board.Count + Pci.Count + Usb.Count + Gpu.Count +
        Input.Count + Modules.Count + Filesystem.Count + Bootloader.Count == 0 && Gpus == 0 && !BootOnRoot && !Live && !Any_;

    // Names aren't paths: a "/" in "RX 7900 XT/7900 XTX" is text, which * crosses.
    static string Flat(string s) => s.ToLowerInvariant().Replace('/', '\u2215');

    static bool Any(List<string> patterns, IEnumerable<string> values) =>
        patterns.Any(p => values.Any(v => Glob.Match(Flat(p), Flat(v))));

    /// <summary>Matches says whether m is a machine these rules are for, and why not when it isn't.</summary>
    public (bool Ok, string Why) Matches(Machine m)
    {
        (bool, string) No(string why) => (false, why);
        if (Vendor.Count > 0 && !Any(Vendor, [m.Vendor])) return No($"made by {m.Vendor}");
        if (Product.Count > 0 && !Any(Product, [m.Product, m.Version])) return No($"the model is {m.Product}");
        if (Board.Count > 0 && !Any(Board, [m.Board])) return No($"the board is {m.Board}");
        if (Pci.Count > 0 && !Any(Pci, m.Pci.Select(p => $"{p.Vendor}:{p.Device}"))) return No("no such PCI device");
        if (Usb.Count > 0 && !Any(Usb, m.Usb.Select(u => $"{u.Vendor}:{u.Product}"))) return No("no such USB device");
        if (Gpu.Count > 0 && !Any(Gpu, m.Gpus.Select(g => g.Name))) return No("no such GPU");
        if (Input.Count > 0 && !Any(Input, m.Inputs)) return No("no such input device");
        if (Modules.Count > 0 && !Any(Modules, m.Modules)) return No("no such kernel module loaded");
        if (Filesystem.Count > 0 && !Any(Filesystem, [m.Filesystem])) return No($"the root filesystem is {(m.Filesystem == "" ? "unknown" : m.Filesystem)}");
        if (Bootloader.Count > 0 && !Any(Bootloader, [m.Bootloader])) return No($"the boot loader is {(m.Bootloader == "" ? "unknown" : m.Bootloader)}");
        if (BootOnRoot && !m.BootOnRoot) return No("/boot is a partition of its own (snapshots of / don't have the kernel)");
        if (Live && !m.Live) return No("this isn't the ISO's live system");
        if (Gpus > 0 && m.Gpus.Count() < Gpus)
            return No($"{m.Gpus.Count()} GPU{(m.Gpus.Count() == 1 ? "" : "s")}, not {Gpus}");
        return (true, "");
    }

    /// <summary>
    /// MatchesIdentity: what says which machine this is (maker, model, board,
    /// PCI devices, GPUs), leaving out what comes and goes (a keyboard plugged
    /// in, a module loaded, a dock's GPU). A plugin that's on stays on while
    /// the machine is the same one; the rest only decides whether to offer it.
    /// </summary>
    public bool MatchesIdentity(Machine m) => new Rules
    {
        Vendor = Vendor, Product = Product, Board = Board, Pci = Pci, Gpu = Gpu,
        // How the system is installed says which machine too: a config.toml
        // shared with a laptop on ext4, or on another boot loader, does nothing there.
        Filesystem = Filesystem, Bootloader = Bootloader, BootOnRoot = BootOnRoot, Live = Live,
    }.Matches(m).Ok;

    /// <summary>The patterns that don't parse (a bare "[" is a class: write "\\[").</summary>
    public IEnumerable<string> BadPatterns() =>
        Vendor.Concat(Product).Concat(Board).Concat(Pci).Concat(Usb).Concat(Gpu).Concat(Input).Concat(Modules)
            .Concat(Filesystem).Concat(Bootloader).Where(p => !Glob.IsValid(Flat(p)));

    /// <summary>Describe is the rules in words, for listing: "GPUs: 2 or more; PCI 10de:*".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        void Add(string what, List<string> l)
        {
            if (l.Count > 0) parts.Add($"{what} {string.Join(" or ", l)}");
        }
        Add("maker", Vendor);
        Add("model", Product);
        Add("board", Board);
        Add("PCI", Pci);
        Add("USB", Usb);
        Add("GPU", Gpu);
        Add("input", Input);
        Add("module", Modules);
        Add("root filesystem", Filesystem);
        Add("boot loader", Bootloader);
        if (BootOnRoot) parts.Add("/boot on the root filesystem");
        if (Live) parts.Add("the ISO's live system");
        if (Any_ && parts.Count == 0) parts.Add("any hardware");
        if (Gpus > 0) parts.Add($"{Gpus} or more GPUs");
        return string.Join("; ", parts);
    }
}

/// <summary>What was read once per run: the machine doesn't change while myarch runs.</summary>
public static class ThisMachine
{
    static Machine? m;

    public static Machine Get() => m ??= Machine.Read(Environment.GetEnvironmentVariable("MYARCH_SYSROOT") is { Length: > 0 } r ? r : "/");
}
