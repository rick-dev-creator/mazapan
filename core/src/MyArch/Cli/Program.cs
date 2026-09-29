using MyArch.Util;

namespace MyArch.Cli;

/// <summary>myarch applies a theme and the enabled plugins to the desktop.</summary>
public static partial class Program
{
    const string Usage = """
        usage: myarch <command> [flags]

        commands:
          apply [--theme ID] [--dry-run] [--adopt]
                         render every enabled plugin with the theme and write the files
          update [-y] [--check] [--no-rollback]
                         update the system: preview, upgrade, re-apply, check, and
                         roll back on its own if a check fails
          doctor [--json]
                 run every plugin's checks now
          history        past updates and how they went
          rollback [ID]  put back the packages and files from before an update
                         (the last one by default)
          plugins [list]  plugins, where they come from, and their state
          plugins show ID
                         what a plugin needs and does: requirements, settings,
                         files, commands, code
          plugins enable|disable ID...
          plugins add URL[#REF] [-y]
                         install a plugin from git, after approving what it can do
          plugins update [ID[#REF]...] [-y]
                         update plugins from git (to another branch or tag with
                         #REF); asks before any new capability
          plugins remove ID...
          plugins sync   install exactly what plugins.lock says (another machine)
          coverage [--json]
                         installed apps, and whether the theme reaches them
          themes [--json]
                         list themes, with their contrast problems

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
                "rollback" => CmdRollback(rest),
                "coverage" => CmdCoverage(rest),
                "-h" or "--help" or "help" => Help(),
                _ => Unknown(),
            };
        }
        catch (Exception e) when (e is MyArchException or Applying.ConflictException or Updates.RestoreException)
        {
            Console.Error.WriteLine("myarch: " + e.Message);
            return 1;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // What the system refused (a file it can't read, a program it can't
            // start): the person's to fix, said as such, not a crash.
            Console.Error.WriteLine("myarch: " + e.Message);
            return 1;
        }
    }

    static int Help()
    {
        Console.Write(Usage);
        return 0;
    }

    static int Unknown()
    {
        Console.Error.Write(Usage);
        return 2;
    }

    /// <summary>
    /// Root is the directory holding the built-in plugins/ and themes/:
    /// $MYARCH_ROOT, or the parent of the directory the binary lives in.
    /// </summary>
    static string Root()
    {
        if (Environment.GetEnvironmentVariable("MYARCH_ROOT") is { Length: > 0 } r) return r;
        var exe = Environment.ProcessPath;
        if (exe == null) return ".";
        exe = Paths.Real(exe) ?? exe;
        return Paths.Dir(Paths.Dir(exe));
    }

    /// <summary>Search paths: the user's directory first, so it can shadow built-ins.</summary>
    static string[] PluginDirs() => [Install.Git.Dir, Paths.Join(Root(), "plugins")];

    static string[] ThemeDirs() => [Paths.ExpandHome("~/.local/share/myarch/themes"), Paths.Join(Root(), "themes")];
}
