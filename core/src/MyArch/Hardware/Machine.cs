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

    public IEnumerable<PciDevice> Gpus => Pci.Where(p => p.IsGpu);

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
        return m;
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

    public bool Empty => Vendor.Count + Product.Count + Board.Count + Pci.Count + Usb.Count + Gpu.Count +
        Input.Count + Modules.Count == 0 && Gpus == 0;

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
    }.Matches(m).Ok;

    /// <summary>The patterns that don't parse (a bare "[" is a class: write "\\[").</summary>
    public IEnumerable<string> BadPatterns() =>
        Vendor.Concat(Product).Concat(Board).Concat(Pci).Concat(Usb).Concat(Gpu).Concat(Input).Concat(Modules)
            .Where(p => !Glob.IsValid(Flat(p)));

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
