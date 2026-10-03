using Mazapan.Util;

namespace Mazapan.Cli;

/// <summary>mazapan applies a theme and the enabled plugins to the desktop.</summary>
public static partial class Program
{
    const string Usage = """
        usage: mazapan <command> [flags]

        commands:
          apply [--theme ID] [--dry-run] [--adopt]
                         render every enabled plugin with the theme and write the files
          update [-y] [--check] [--no-rollback]
                         update the system: preview, upgrade, re-apply, check, and
                         roll back on its own if a check fails
          doctor [--json]
                 run every plugin's checks now
          history        past updates and how they went
          channel [stable|edge]
                         where Mazapán's own updates come from: stable, or every
                         release first (edge); without one, which it is
          version        this Mazapán's version
          password       change your password everywhere it is: the disk's (when
                         it's encrypted), the account's, the keyring's
          timeline [--json]
                         what changed on the desktop, in words: every apply and
                         update, newest first
          install disks [--json] | plan ANSWERS.json | run ANSWERS.json
                         this system onto a disk, from the ISO (its installer runs it)
          apps [list|plan|plan-remove|install|remove|undo] [ID...] [--json] [--gui] [-y]
                         the apps the catalog knows how to install, by profile
                         (Gaming, Development…), and installing or removing them
          apps permissions ID [--json] | permit ID KEY on|off | permit ID reset
                         what a Flatpak app may reach (network, sound, devices,
                         home, files, downloads, bluetooth), changed for you
          timeline undo ID [-y]
                         put back what one apply changed (a theme, a setting, a
                         plugin on or off), even if others came after it
          rollback [ID]  put back the packages and files from before an update
                         (the last one by default)
          plugins [list]  plugins, where they come from, and their state
          plugins show ID
                         what a plugin needs and does: requirements, settings,
                         files, commands, code
          plugins catalog [--json] [--refresh]
                         every plugin: built in, installed, and in the catalogs
          plugins search TERM...
          plugins preview ID|URL[#REF] [--json]
                         a plugin's page (README, what it can do, settings)
                         without installing it
          plugins enable|disable ID...
          plugins add ID|URL[#REF] [-y]
                         install a plugin from git, after approving what it can do
          plugins update [ID[#REF]...] [-y]
                         update plugins from git (to another branch or tag with
                         #REF); asks before any new capability
          plugins remove ID...
          plugins sync   install exactly what plugins.lock says (another machine)
          plugins new ID [--kind bar|panel|window|theme|tools] [--dir DIR]
                         a working plugin to start from, in the plugin folder (or DIR)
          plugins check [ID|DIR]
                         before sharing a plugin: rendered with every theme, in every
                         language it has; what's missing to translate or describe it
          plugins dev [ID|DIR]
                         apply a plugin on every save (a folder elsewhere is linked in)
          plugins fork ID [NEW]
                         copy a built-in plugin to change it (NEW: a copy beside it)
          plugins diff ID
                         what your fork changed from the built-in
          coverage [--json]
                         installed apps, and whether the theme reaches them
          themes [--json]
                         list themes, with their contrast problems
          themes from-image PICTURE [--name NAME] [--mode dark|light] [--apply] [--json]
                         a theme from a picture's colors, every contrast checked
          themes remove ID
                         a theme of your own (made from a picture) goes

        """;

    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.Write(Usage);
            return 2;
        }
        try
        {
            var rest = args[1..];
            return args[0] switch
            {
                "themes" => CmdThemes(rest),
                "plugins" => CmdPlugins(rest),
                "apply" => CmdApply(rest),
                "update" => CmdUpdate(rest),
                "doctor" => CmdDoctor(rest),
                "status" => CmdStatus(rest),
                "undo" => CmdUndo(rest),
                "mcp" => CmdMcp(rest),
                "report" => CmdReport(rest),
                "hardware" => CmdHardware(rest),
                "history" => CmdHistory(),
                "timeline" => CmdTimeline(rest),
                "apps" => CmdApps(rest),
                "install" => CmdInstall(rest),
                "rollback" => CmdRollback(rest),
                "coverage" => CmdCoverage(rest),
                "channel" => CmdChannel(rest),
                "password" => CmdPassword(rest),
                "version" or "--version" => PrintVersion(),
                "-h" or "--help" or "help" => Help(),
                _ => Unknown(),
            };
        }
        catch (Exception e) when (e is MazapanException or Applying.ConflictException or Updates.RestoreException)
        {
            Console.Error.WriteLine("mazapan: " + e.Message);
            return 1;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // What the system refused (a file it can't read, a program it can't
            // start): the person's to fix, said as such, not a crash.
            Console.Error.WriteLine("mazapan: " + e.Message);
            return 1;
        }
    }

    static int Help()
    {
        Console.Write(Usage);
        return 0;
    }

    /// <summary>The version core/build gave the binary (pkg/version): the package's.</summary>
    internal static string Version =>
        typeof(Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            is [System.Reflection.AssemblyInformationalVersionAttribute a, ..] ? a.InformationalVersion : "0.0.0-dev";

    static int PrintVersion()
    {
        Console.WriteLine("mazapan " + Version);
        return 0;
    }

    static int Unknown()
    {
        Console.Error.Write(Usage);
        return 2;
    }

    /// <summary>
    /// Root is the directory holding the built-in plugins/ and themes/:
    /// $MAZAPAN_ROOT, or the parent of the directory the binary lives in.
    /// </summary>
    static string Root()
    {
        if (Environment.GetEnvironmentVariable("MAZAPAN_ROOT") is { Length: > 0 } r) return r;
        var exe = Environment.ProcessPath;
        if (exe == null) return ".";
        exe = Paths.Real(exe) ?? exe;
        return Paths.Dir(Paths.Dir(exe));
    }

    /// <summary>Search paths: the user's directory first, so it can shadow built-ins.</summary>
    static string[] PluginDirs() => [Install.Git.Dir, Paths.Join(Root(), "plugins")];

    static string[] ThemeDirs() => [Paths.ExpandHome("~/.local/share/mazapan/themes"), Paths.Join(Root(), "themes")];
}
