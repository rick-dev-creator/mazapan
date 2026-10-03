namespace Mazapan.Cli;

static class Style
{
    public const string Bold = "\u001b[1m";
    public const string Dim = "\u001b[2m";
    public const string Red = "\u001b[31m";
    public const string Green = "\u001b[32m";
    public const string Amber = "\u001b[33m";
    public const string Reset = "\u001b[0m";

    /// <summary>The text without its colors (for what isn't a terminal).</summary>
    public static string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(s, "\u001b\\[[0-9;]*m", "");
}
