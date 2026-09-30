using Mazapan.Config;
using Mazapan.Hardware;
using Mazapan.Plugins;
using Mazapan.Util;

namespace Mazapan.Cli;

public static partial class Program
{
    /// <summary>
    /// `mazapan hardware`: this machine as plugins see it (model, GPUs, USB
    /// devices), and the hardware plugins for it: which are on, and how to
    /// turn on the others. Nothing is turned on by itself, but with --enable
    /// (the installer's, before its apply --system): every one for this
    /// machine (not those for any machine: they're asked for).
    /// </summary>
    static int CmdHardware(string[] args)
    {
        var fs = new Flags("hardware")
            .Bool("json", "as data")
            .Bool("all", "list the hardware plugins for other machines too")
            .Bool("enable", "turn on every hardware plugin for this machine (then: mazapan apply --system)")
            .Parse(args);
        if (fs.IsSet("enable"))
        {
            var cfgOn = Settings.Load();
            var on = HardwareToOffer(cfgOn);
            foreach (var p in on)
            {
                cfgOn.TurnOn(p);
                Console.WriteLine($"on: {p.Id}");
            }
            if (on.Count > 0) cfgOn.Save();
            return 0;
        }
        var m = ThisMachine.Get();
        var cfg = Settings.Load();
        var hw = Plugin.Discover(PluginDirs()).Plugins.Where(p => p.Hardware != null).ToList();
        var rows = hw.Select(p => (Plugin: p, Match: p.Hardware!.Matches(m), On: cfg.IsOn(p))).ToList();

        if (fs.IsSet("json"))
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "version", StatusVersion },
                {
                    "machine", new Fields
                    {
                        { "vendor", m.Vendor }, { "product", m.Product }, { "version", m.Version }, { "board", m.Board },
                        { "gpus", m.Gpus.Select(g => new Fields { { "id", $"{g.Vendor}:{g.Device}" }, { "name", g.Name }, { "driver", g.Driver } }).ToList() },
                        { "usb", m.Usb.Select(u => new Fields { { "id", $"{u.Vendor}:{u.Product}" }, { "name", u.Name } }).ToList() },
                        { "filesystem", m.Filesystem },
                        { "bootloader", m.Bootloader },
                    }
                },
                {
                    "plugins", rows.Select(r => new Fields
                    {
                        { "id", r.Plugin.Id },
                        { "applies", r.Match.Ok },
                        { "on", r.On },
                        { "for", r.Plugin.Hardware!.Describe() },
                        { "description", r.Plugin.Meta.Description },
                    }).ToList()
                },
            }));
            return 0;
        }

        Console.WriteLine($"{Style.Bold}{m.Vendor} {m.Product}{Style.Reset}{(m.Version != "" && m.Version != m.Product ? $" ({m.Version})" : "")}");
        foreach (var g in m.Gpus)
            Console.WriteLine($"  GPU  {g.Name}{(g.Driver != "" ? $" {Style.Dim}[{g.Driver}]{Style.Reset}" : "")}");
        if (m.Filesystem != "" || m.Bootloader != "")
            Console.WriteLine($"  root {(m.Filesystem == "" ? "?" : m.Filesystem)}, boot loader {(m.Bootloader == "" ? "?" : m.Bootloader)}");
        Console.WriteLine();
        var mine = rows.Where(r => r.Match.Ok && !r.Plugin.Hardware!.OnlyAny).ToList();
        if (mine.Count == 0) Console.WriteLine("No hardware plugin is for this machine.");
        foreach (var (p, _, on) in mine)
        {
            Console.WriteLine($"  {(on ? Style.Green + "✓" : Style.Amber + "○")}{Style.Reset} {p.Id,-24} {p.Meta.Description}");
            if (!on) Console.WriteLine($"    {Style.Dim}turn it on: mazapan plugins enable {p.Id} && mazapan apply --system{Style.Reset}");
        }
        if (fs.IsSet("all"))
        {
            Console.WriteLine("\nFor other machines:");
            foreach (var (p, match, _) in rows.Where(r => !r.Match.Ok))
                Console.WriteLine($"  {Style.Dim}{p.Id,-26} {p.Hardware!.Describe()} (here: {match.Why}){Style.Reset}");
            // For every machine, but they change how it starts: only when asked.
            var any = rows.Where(r => r.Plugin.Hardware!.OnlyAny).ToList();
            if (any.Count > 0) Console.WriteLine("\nFor any machine, when you want them:");
            foreach (var (p, _, on) in any)
                Console.WriteLine($"  {(on ? Style.Green + "✓" : Style.Dim + "○")}{Style.Reset} {p.Id,-24} {p.Meta.Description}");
        }
        return 0;
    }

    /// <summary>The hardware plugins for this machine that are off: doctor and status mention them.</summary>
    static List<Plugin> HardwareToOffer(Settings cfg) =>
        Plugin.Discover(PluginDirs()).Plugins
            .Where(p => p.Hardware != null && !cfg.IsOn(p) && p.Hardware.Offered(ThisMachine.Get())).ToList();
}
