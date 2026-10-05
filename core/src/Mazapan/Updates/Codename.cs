namespace Mazapan.Updates;

/// <summary>
/// Each release's name, easier to remember than its number: the first is
/// Mazapan; each one after it is added here before its version is tagged.
/// One per minor version: 0.1.0 and 0.1.3 are both Mazapan.
/// Builds between releases (0.1.0.r5.gabc1234) are the release they follow;
/// before the first (0.0.0.rN), none. See docs/versioning.md.
/// </summary>
public static class Codename
{
    public static readonly (string Version, string Name)[] Names =
    [
        ("0.1", "Mazapan"),
    ];

    /// <summary>The name of the release a version belongs to, or "" (none given yet).</summary>
    public static string For(string version)
    {
        var parts = version.Split('.');
        if (parts.Length < 2) return "";
        var minor = parts[0] + "." + parts[1];
        return Names.FirstOrDefault(n => n.Version == minor).Name ?? "";
    }
}
