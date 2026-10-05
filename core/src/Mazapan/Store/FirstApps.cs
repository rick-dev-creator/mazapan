using System.Text.RegularExpressions;
using Mazapan.Util;

namespace Mazapan.Store;

/// <summary>
/// The apps chosen in the installer: mazapan install leaves their ids in
/// first-apps, one a line, and mazapan apps first installs them once
/// there's a connection. The file stays until all of them are there: a
/// failed or cancelled try loses nothing, the next one installs what's
/// still missing.
/// </summary>
public static partial class FirstApps
{
    public static string Path => Paths.ExpandHome("~/.local/state/mazapan/first-apps");

    /// <summary>Its ids; other lines (an older "#started" mark) left out.</summary>
    public static List<string> Parse(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => Id().IsMatch(l)).Distinct().ToList();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*\z")] private static partial Regex Id();
}

/// <summary>
/// Why a pacman run as root didn't go through, from what it said: packages
/// the repositories don't have (left out, the rest tried again), the
/// keyring older than the packages' signatures (it first, then again), or
/// pkexec without a polkit agent to ask (the shell still starting).
/// </summary>
public sealed partial record PacmanTrouble(IReadOnlySet<string> Missing, bool Keyring, bool NoAgent)
{
    public static PacmanTrouble Read(string output)
    {
        var missing = TargetNotFound().Matches(output).Select(m => m.Groups[1].Value).ToHashSet();
        var keyring = Signature().IsMatch(output);
        var noAgent = output.Contains("No authentication agent found", StringComparison.Ordinal);
        return new PacmanTrouble(missing, keyring, noAgent);
    }

    [GeneratedRegex(@"error: target not found: (\S+)")] private static partial Regex TargetNotFound();

    [GeneratedRegex(@"\(PGP signature\)|is unknown trust|is marginal trust|is invalid or corrupted|required key missing from keyring|key ""[^""]*"" is unknown")]
    private static partial Regex Signature();
}
