using System.Runtime.InteropServices;

namespace Mazapan.Cli;

public static partial class Program
{
    static void Header(string title) => Console.WriteLine($"\n{Style.Bold}{title}{Style.Reset}");

    /// <summary>Asks a yes/no question; anything but yes is no.</summary>
    static bool Confirm(string question)
    {
        Console.Write($"\n{question} [y/N] ");
        var a = (Console.In.ReadLine() ?? "").Trim().ToLowerInvariant();
        return a is "y" or "yes" or "s" or "si" or "sí";
    }

    [DllImport("libc", EntryPoint = "isatty")]
    static extern int IsATty(int fd);

    /// <summary>
    /// IsTerminal: a real terminal, not just a character device (/dev/null is
    /// one too): only a terminal can take termios.
    /// </summary>
    static bool IsTerminal(int fd) => IsATty(fd) == 1;

    static string Plural(int n, string one, string many) => n == 1 ? "1 " + one : $"{n} {many}";

    static string Tilde(string p) => Util.Paths.Tilde(p);
}
