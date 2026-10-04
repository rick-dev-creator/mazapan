using System.Diagnostics;
using Mazapan.Util;

namespace Mazapan.Cli;

/// <summary>
/// An agent's change, the person's to allow: before an apply_change from an
/// agent (the MCP server) writes anything, a card shows who asks, what, and
/// the exact diff, with Allow and Don't allow (the agent plugin's Approve
/// card). No card (a desktop without it): the agent's own client asked
/// already, as before. No answer in two minutes, or no bar to ask on: not
/// allowed. The request and the answer are files in the runtime folder, the
/// person's alone.
/// </summary>
static class Approval
{
    static string Panel => Paths.ExpandHome("~/.config/quickshell/mazapan/panels/approve.qml");

    public static bool Wanted() => File.Exists(Panel);

    // Agents the person always allows ("Always allow" on the card): by name, in
    // the state folder (not config.toml: a list no agent writes through apply).
    static string TrustedFile => Paths.ExpandHome("~/.local/state/mazapan/agents-trusted.json");

    public static List<string> Trusted()
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(TrustedFile));
            return [.. doc.RootElement.EnumerateArray().Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String).Select(e => e.GetString()!)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) { return []; }
    }

    public static void Trust(string by, bool on)
    {
        var list = Trusted();
        list.Remove(by);
        if (on) list.Add(by);
        Files.WriteAtomic(TrustedFile, GoJson.Marshal(list) + "\n");
    }

    public static string Readable(string client) => ApprovalNames.Readable(client);

    public static bool Ask(string by, string what, string diff, TimeSpan wait)
    {
        var dir = Paths.Join(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } r ? r : "/tmp", "mazapan-approve");
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var id = $"r-{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
        var request = Paths.Join(dir, id + ".json");
        var answer = Paths.Join(dir, id + ".answer");
        try
        {
            if (diff.Length > 200_000) diff = diff[..200_000] + "\n…";
            Files.WriteAtomic(request, GoJson.Marshal(new Fields { { "by", by }, { "what", what }, { "diff", diff } }) + "\n");
            File.SetUnixFileMode(request, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var psi = new ProcessStartInfo("qs") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            // Any display: an agent in tmux or over SSH has no WAYLAND_DISPLAY, and would never be asked.
            foreach (var a in new[] { "ipc", "--any-display", "-c", "mazapan", "call", "mazapan", "open", "approve", request }) psi.ArgumentList.Add(a);
            try
            {
                using var p = Process.Start(psi)!;
                if (!p.WaitForExit(10_000) || p.ExitCode != 0 || p.StandardOutput.ReadToEnd().Trim() != "ok")
                    throw new MazapanException("the person couldn't be asked (the bar isn't running): nothing changed");
            }
            catch (System.ComponentModel.Win32Exception) { throw new MazapanException("the person couldn't be asked (no Quickshell): nothing changed"); }
            var until = DateTime.UtcNow + wait;
            while (DateTime.UtcNow < until)
            {
                if (File.Exists(answer) && File.ReadAllText(answer).Trim() is { Length: > 0 } a)
                {
                    // "always": this one, and from now on every one of this agent's.
                    if (a == "always" && by != "an agent") Trust(by, true);
                    return a is "allow" or "always";
                }
                Thread.Sleep(200);
            }
            throw new MazapanException("the person didn't answer in time: nothing changed");
        }
        finally
        {
            File.Delete(request);
            File.Delete(answer);
        }
    }
}

/// <summary>An MCP client's name ("claude-code"), as people know it; anything else, cleaned (it's shown, and kept).</summary>
public static class ApprovalNames
{
    public static List<string> TrustedForTests() => Approval.Trusted();
    public static void TrustForTests(string by, bool on) => Approval.Trust(by, on);

    public static string Readable(string client)
    {
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-code"] = "Claude Code", ["claude-ai"] = "Claude", ["opencode"] = "OpenCode", ["codex-mcp-client"] = "Codex",
            ["gemini-cli-mcp-client"] = "Gemini CLI", ["cursor-vscode"] = "Cursor", ["visual-studio-code"] = "VS Code", ["zed"] = "Zed",
        };
        if (known.TryGetValue(client, out var name)) return name;
        var clean = new string([.. client.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.')]).Trim();
        return clean.Length is > 0 and <= 40 ? clean : "an agent";
    }
}
