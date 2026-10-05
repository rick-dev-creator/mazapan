using Mazapan.Pacman;
using Mazapan.Util;

namespace Mazapan.Cli;

public static partial class Program
{
    /// <summary>
    /// `mazapan channel [stable|edge]`: where Mazapan's own updates come
    /// from. Without one, which it is; with one, the mirrorlist rewritten
    /// (with sudo), and the next update takes it.
    /// </summary>
    static int CmdChannel(string[] args)
    {
        var root = Root();
        if (args.Length > 1 || args is [{ } a] && a.StartsWith('-'))
        {
            Console.Error.WriteLine("usage: mazapan channel [stable|edge]");
            return 2;
        }
        // A checkout (the dev VM's): what it runs is the checkout itself.
        var dev = Paths.Clean(root) != Repository.Installed;
        var server = Repository.Server(root);
        var current = File.Exists(Repository.Mirrorlist) && File.Exists(Repository.Conf) &&
            Repository.Enabled(File.ReadAllText(Repository.Conf))
            ? Repository.ChannelOf(File.ReadAllText(Repository.Mirrorlist)) : null;

        if (args.Length == 0)
        {
            if (dev) Console.WriteLine($"dev: this mazapan runs from a checkout ({root}), and updates with it");
            else if (current != null) Console.WriteLine(current);
            else if (server == "") Console.WriteLine("none: Mazapan isn't published yet, so it updates with nothing but a new install");
            else Console.WriteLine("none: this system doesn't have Mazapan's repository yet (`mazapan channel stable` adds it)");
            return 0;
        }

        var want = args[0];
        if (!Repository.Channels.Contains(want))
            throw new MazapanException($"no channel \"{want}\": {string.Join(" or ", Repository.Channels)}");
        if (dev) throw new MazapanException($"this mazapan runs from a checkout ({root}): its channel is the checkout");
        if (server == "") throw new MazapanException("Mazapan isn't published yet: there's no channel to switch to");
        if (want == current)
        {
            Console.WriteLine($"Already {want}.");
            return 0;
        }
        Applying.AsRoot.UseRepository(Repository.MirrorlistText(server, want));
        Console.WriteLine($"Mazapan now updates from {want}: `mazapan update` takes it there.");
        if (want == "stable" && current == "edge")
            Console.WriteLine($"{Style.Dim}What edge brought stays until stable reaches it (nothing goes back).{Style.Reset}");
        return 0;
    }
}
