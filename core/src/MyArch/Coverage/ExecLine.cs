using System.Text;
using MyArch.Util;

namespace MyArch.Coverage;

/// <summary>What a .desktop file's Exec line runs.</summary>
internal static class ExecLine
{
    /// <summary>Fields splits an Exec line the way the spec quotes it: "…" keeps spaces.</summary>
    internal static List<string> Fields(string s)
    {
        var out_ = new List<string>();
        var cur = new StringBuilder();
        bool quoted = false, any = false;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '"')
            {
                quoted = !quoted;
                any = true;
            }
            else if (c == '\\' && quoted && i + 1 < s.Length)
            {
                i++;
                cur.Append(s[i]);
            }
            else if ((c == ' ' || c == '\t') && !quoted)
            {
                if (any || cur.Length > 0) out_.Add(cur.ToString());
                cur.Clear();
                any = false;
            }
            else cur.Append(c);
        }
        if (any || cur.Length > 0) out_.Add(cur.ToString());
        return out_;
    }

    /// <summary>Terminals start what follows -e (or --) in themselves.</summary>
    static readonly HashSet<string> Terminals =
    [
        "xdg-terminal-exec", "foot", "footclient", "kitty", "alacritty",
        "wezterm", "ghostty", "konsole", "gnome-terminal", "xterm",
    ];

    /// <summary>
    /// Program is the executable an Exec line really runs: past `env`, shells
    /// (`sh -c "…"`) and terminals (`xdg-terminal-exec -e top`: a terminal app),
    /// or a Flatpak's id.
    /// </summary>
    internal static (string Prog, bool Terminal, string Flatpak) Program(string execLine)
    {
        var f = Fields(execLine);
        while (f.Count > 0)
        {
            var b = Paths.Base(f[0]);
            if (b == "env")
            {
                f = f[1..];
                while (f.Count > 0 && (f[0].Contains('=') || f[0].StartsWith('-')))
                {
                    if (f[0] == "-u" && f.Count > 1) f = f[1..];
                    f = f[1..];
                }
            }
            else if (b == "exec" && f.Count > 1)
            {
                f = f[1..];
            }
            else if (b == "flatpak" && f.Count > 1 && f[1] == "run")
            {
                foreach (var x in f.Skip(2))
                    if (!x.StartsWith('-')) return (f[0], false, x);
                return (f[0], false, "");
            }
            else if (b is "sh" or "bash" or "zsh" && f.Count > 2 && f[1] == "-c")
            {
                f = Fields(f[2]);
            }
            else if (Terminals.Contains(b) && f.Count > 1)
            {
                var i = f.IndexOf("-e");
                if (i < 0) i = f.IndexOf("--");
                if (i < 0 || i + 1 >= f.Count) return (f[0], false, "");
                var (inner, _, _) = Program(string.Join(" ", QuoteAll(f[(i + 1)..])));
                return (inner, true, "");
            }
            else
            {
                return (f[0], false, "");
            }
        }
        return ("", false, "");
    }

    static IEnumerable<string> QuoteAll(List<string> f) => f.Select(x => "\"" + x.Replace("\"", "\\\"") + "\"");
}
