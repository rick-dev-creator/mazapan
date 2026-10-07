namespace Mazapan.Install;

/// <summary>
/// Plugins Mazapan used to ship (community/) that now live in their own
/// repositories, listed in the plugin registry like anyone's. A system that
/// had one on gets it from there, installed from git at the commit that is
/// exactly what it had: same files, same capabilities, so nothing new to
/// approve. From then on it follows the registry, as any plugin from it.
/// </summary>
public static class Moved
{
    public sealed record Where(string Source, string Ref, string Commit);

    /// <summary>By id; tests put their own repositories here.</summary>
    public static IReadOnlyDictionary<string, Where> Plugins { get; internal set; } = new Dictionary<string, Where>
    {
        ["markets"] = new("https://github.com/rick-dev-creator/mazapan-markets", "v0.1.0", "fc7cf623163a235fbd211971a5aa38e3e0e12ad1"),
        ["pomodoro"] = new("https://github.com/rick-dev-creator/mazapan-pomodoro", "v0.1.1", "2ee37a4ec2d0cd4cb659021f1d4463e1ae1ff086"),
    };
}
