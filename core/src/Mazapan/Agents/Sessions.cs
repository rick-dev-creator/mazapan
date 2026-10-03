using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>A session an agent has open: where, what it's doing, since when, and the processes it runs in (to find its window).</summary>
public sealed record Session(string Agent, string Id, string Account, string Project, string State, DateTimeOffset Since, string Message, List<int> Pids);

/// <summary>
/// Agents' live sessions: what each is doing right now (working, waiting
/// for you, done), told by the agents themselves (Claude Code's hooks,
/// opencode's events, through `mazapan agents event`) and kept in the
/// runtime directory (memory, the person's alone, gone at logout). Agents
/// that tell nothing (pi, Codex…) show as running while their process is.
/// </summary>
public static class Sessions
{
    static string Dir => Path.Join(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } r ? r : "/tmp/mazapan-" + Environment.UserName, "mazapan-agents");

    static readonly string[] States = ["idle", "working", "waiting", "done"];

    /// <summary>
    /// What a Claude Code hook says (its JSON on stdin): the session and the
    /// event, as a state. Nothing is ever printed: a hook's output can go
    /// into the conversation.
    /// </summary>
    public static void FromClaudeHook(string json, IReadOnlyList<int> ancestors)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var id = Discovery.Str(r, "session_id");
        if (id == "") return;
        var ev = Discovery.Str(r, "hook_event_name");
        var state = ev switch
        {
            "SessionStart" => "idle",
            "UserPromptSubmit" or "PreToolUse" or "PostToolUse" => "working",
            "Notification" => "waiting",
            "Stop" => "done",
            "SessionEnd" => "end",
            _ => "",
        };
        if (state == "") return;
        // Which account: the configuration directory it runs with.
        var config = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } c ? Path.GetFileName(c.TrimEnd('/')).TrimStart('.') : "claude";
        Record("claude", id, config, Discovery.Str(r, "cwd"), state, Discovery.Str(r, "message"), ancestors);
    }

    /// <summary>A state for a session (end: it's gone). An agent's own message (a permission asked) kept, short.</summary>
    public static void Record(string agent, string id, string account, string project, string state, string message, IReadOnlyList<int> pids)
    {
        if (!SafeName(agent) || !SafeName(id)) return;
        Directory.CreateDirectory(Dir);
        File.SetUnixFileMode(Dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var file = Path.Join(Dir, agent + "-" + id + ".json");
        if (state == "end")
        {
            File.Delete(file);
            return;
        }
        if (!States.Contains(state)) return;
        // The same state again (a tool after a tool): its start kept. What an
        // event doesn't say (a notification has no directory) stays as it was.
        var since = DateTimeOffset.UtcNow;
        if (Read(file) is { } old)
        {
            if (old.State == state) since = old.Since;
            if (project == "") project = old.Project;
            if (account == "") account = old.Account;
        }
        var node = new JsonObject
        {
            ["agent"] = agent, ["id"] = id, ["account"] = account, ["project"] = project, ["state"] = state,
            ["since"] = since.ToUnixTimeMilliseconds(),
            ["message"] = message.Length > 160 ? message[..160] : message,
            ["pids"] = new JsonArray([.. pids.Select(p => (JsonNode)p)]),
        };
        Files.WriteAtomic(file, node.ToJsonString());
    }

    static bool SafeName(string s) => s.Length is > 0 and < 128 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') && s[0] != '.';

    static Session? Read(string file)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var r = doc.RootElement;
            var pids = r.TryGetProperty("pids", out var p) && p.ValueKind == JsonValueKind.Array
                ? p.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList() : [];
            return new Session(Discovery.Str(r, "agent"), Discovery.Str(r, "id"), Discovery.Str(r, "account"), Discovery.Str(r, "project"),
                Discovery.Str(r, "state"), DateTimeOffset.FromUnixTimeMilliseconds(Usage.Num(r, "since")), Discovery.Str(r, "message"), pids);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// The sessions open now: those told (a session whose process has gone
    /// is taken off: an agent killed, a terminal closed), then agents running
    /// that tell nothing.
    /// </summary>
    public static List<Session> List()
    {
        var out_ = new List<Session>();
        if (Directory.Exists(Dir))
            foreach (var f in Directory.EnumerateFiles(Dir, "*.json"))
                if (Read(f) is { } s)
                {
                    // Its agent is its first pid; the rest its window's.
                    if (s.Pids.Count > 0 && !Alive(s.Pids[0])) { File.Delete(f); continue; }
                    out_.Add(s);
                }
        var told = out_.SelectMany(s => s.Pids.Take(1)).ToHashSet();
        foreach (var (pid, agent) in RunningAgents())
            if (!told.Contains(pid) && !out_.Any(s => s.Pids.Contains(pid)))
                out_.Add(new Session(agent, "pid-" + pid.ToString(CultureInfo.InvariantCulture), "", Cwd(pid), "running", Started(pid), "", Ancestors(pid)));
        return out_.OrderBy(s => s.Since).ToList();
    }

    static bool Alive(int pid) => Directory.Exists("/proc/" + pid.ToString(CultureInfo.InvariantCulture));

    /// <summary>A process and those above it (its shell, its terminal): one of them owns a window.</summary>
    public static List<int> Ancestors(int pid)
    {
        var out_ = new List<int>();
        for (var i = 0; i < 12 && pid > 1; i++)
        {
            out_.Add(pid);
            pid = Parent(pid);
        }
        return out_;
    }

    static int Parent(int pid)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            // pid (comm) state ppid …: comm may hold spaces and parentheses.
            var after = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
            return int.Parse(after[1], CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is IOException or FormatException or IndexOutOfRangeException or UnauthorizedAccessException or ArgumentOutOfRangeException) { return 0; }
    }

    static string Cwd(int pid)
    {
        try { return new DirectoryInfo($"/proc/{pid}/cwd").ResolveLinkTarget(false)?.FullName ?? ""; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    static DateTimeOffset Started(int pid)
    {
        try { return new DateTimeOffset(Directory.GetLastWriteTimeUtc($"/proc/{pid}")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return DateTimeOffset.UtcNow; }
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getuid")]
    static extern uint GetUid();

    /// <summary>The person's own processes that are an agent (by the program they run): pid and agent.</summary>
    static IEnumerable<(int Pid, string Agent)> RunningAgents()
    {
        var uid = GetUid().ToString(CultureInfo.InvariantCulture);
        foreach (var d in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(d), out var pid)) continue;
            string[] argv;
            try
            {
                // Uid:	real	effective…: the person's own only.
                var owner = File.ReadLines(Path.Join(d, "status")).FirstOrDefault(l => l.StartsWith("Uid:", StringComparison.Ordinal))?.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (owner is not { Length: > 1 } || owner[1] != uid) continue;
                argv = File.ReadAllText(Path.Join(d, "cmdline")).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            if (argv.Length == 0) continue;
            // node/bun running the agent's script, or the agent itself.
            var names = argv.Take(2).Select(a => Path.GetFileName(a)).ToList();
            var agent = Discovery.Known.FirstOrDefault(k => k.Programs.Any(names.Contains)).Id;
            if (agent != null) yield return (pid, agent);
        }
    }

    static readonly string[] Shells = ["sh", "bash", "dash", "zsh", "fish"];

    static string Comm(int pid)
    {
        try { return File.ReadAllText($"/proc/{pid}/comm").Trim(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>
    /// A hook's agent and the processes above it (its terminal's among
    /// them): the caller's, past the shell the hook itself ran in, so the
    /// first is the agent, alive as long as the session is.
    /// </summary>
    public static List<int> CallerAncestors()
    {
        var pid = Parent(Environment.ProcessId);
        while (pid > 1 && Shells.Contains(Comm(pid))) pid = Parent(pid);
        return Ancestors(pid);
    }
}
