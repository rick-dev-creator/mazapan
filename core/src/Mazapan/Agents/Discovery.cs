using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>
/// One account an agent works with: a Claude configuration (its own
/// directory, its own subscription), a provider opencode or pi is set up
/// with (a subscription or an API key), Codex's login.
/// </summary>
public sealed record Account(string Agent, string Id, string Label, string Kind, string Plan, string ConfigDir);

/// <summary>An agent installed here, and the accounts it has.</summary>
public sealed record Agent(string Id, string Name, string Binary, List<Account> Accounts);

/// <summary>
/// The coding agents on this computer, found by themselves: by their
/// program on the PATH (mise's and npm's places too) and by what they keep
/// in the home directory. Only names and kinds are read from their
/// configuration: never a key, never a token's value.
/// </summary>
public static class Discovery
{
    static string Home => Environment.GetEnvironmentVariable("HOME") ?? "";

    /// <summary>The agents known, by id: what they're called and the program that runs them.</summary>
    public static readonly (string Id, string Name, string[] Programs)[] Known =
    [
        ("claude", "Claude Code", ["claude"]),
        ("opencode", "OpenCode", ["opencode"]),
        ("pi", "Pi", ["pi"]),
        ("codex", "Codex", ["codex"]),
        ("gemini", "Gemini CLI", ["gemini"]),
        ("crush", "Crush", ["crush"]),
        ("aider", "Aider", ["aider"]),
    ];

    public static List<Agent> Find()
    {
        var out_ = new List<Agent>();
        foreach (var (id, name, programs) in Known)
        {
            var bin = programs.Select(Which).FirstOrDefault(b => b != null);
            var accounts = id switch
            {
                "claude" => ClaudeAccounts(),
                "opencode" => ProviderAccounts("opencode", Paths.Join(Home, ".local/share/opencode/auth.json")),
                "pi" => ProviderAccounts("pi", Paths.Join(Home, ".pi/agent/auth.json")),
                "codex" => CodexAccounts(),
                _ => [],
            };
            if (bin != null || accounts.Count > 0) out_.Add(new Agent(id, name, bin ?? "", accounts));
        }
        return out_;
    }

    /// <summary>A program on the PATH, or in the places tools install themselves (mise, npm, ~/.local/bin).</summary>
    public static string? Which(string program)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries).ToList();
        dirs.AddRange([Paths.Join(Home, ".local/bin"), Paths.Join(Home, ".local/share/mise/shims"), Paths.Join(Home, ".npm-global/bin"), Paths.Join(Home, ".bun/bin")]);
        foreach (var d in dirs.Distinct())
        {
            var p = Path.Join(d, program);
            if (File.Exists(p)) return p;
        }
        // mise installs without shims on the PATH: its own installs.
        var mise = Paths.Join(Home, ".local/share/mise/installs", program);
        if (Directory.Exists(mise))
            foreach (var p in Directory.EnumerateFiles(mise, program, new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4 }))
                return p;
        return null;
    }

    /// <summary>
    /// Every Claude configuration: ~/.claude, any ~/.claude-* beside it and
    /// CLAUDE_CONFIG_DIR's, each with its own login (.credentials.json).
    /// </summary>
    public static List<Account> ClaudeAccounts()
    {
        var dirs = new List<string>();
        if (Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } env) dirs.Add(env);
        dirs.Add(Paths.Join(Home, ".claude"));
        if (Directory.Exists(Home))
            dirs.AddRange(Directory.EnumerateDirectories(Home, ".claude-*").Order(StringComparer.Ordinal));
        var out_ = new List<Account>();
        foreach (var dir in dirs.Select(d => d.TrimEnd('/')).Distinct())
        {
            var creds = Path.Join(dir, ".credentials.json");
            if (!File.Exists(creds)) continue;
            var plan = "";
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(creds));
                if (doc.RootElement.TryGetProperty("claudeAiOauth", out var o))
                {
                    plan = Str(o, "subscriptionType");
                    var tier = Str(o, "rateLimitTier");
                    // default_claude_max_20x: Max 20x.
                    if (tier.LastIndexOf('_') is var i and > 0 && tier[(i + 1)..] is var x && x.EndsWith('x')) plan += " " + x;
                }
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
            // Who it is: the e-mail the configuration says (~/.claude's is in ~/.claude.json).
            var profile = dir == Paths.Join(Home, ".claude") ? Paths.Join(Home, ".claude.json") : Path.Join(dir, ".claude.json");
            var label = "";
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(profile));
                if (doc.RootElement.TryGetProperty("oauthAccount", out var a)) label = Str(a, "emailAddress");
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
            var id = Path.GetFileName(dir).TrimStart('.');
            out_.Add(new Account("claude", id, label != "" ? label : id, "subscription", plan, dir));
        }
        return out_;
    }

    /// <summary>The providers an agent is logged into (opencode's and pi's auth.json): their names and kinds only.</summary>
    static List<Account> ProviderAccounts(string agent, string auth)
    {
        var out_ = new List<Account>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(auth));
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var type = p.Value.ValueKind == JsonValueKind.Object ? Str(p.Value, "type") : "";
                // A coding plan or an OAuth login is a subscription; a key is paid by use.
                var kind = type is "oauth" || p.Name.Contains("plan", StringComparison.Ordinal) || p.Name.Contains("coding", StringComparison.Ordinal) ? "subscription" : "api";
                out_.Add(new Account(agent, p.Name, p.Name, kind, type, Paths.Dir(auth)));
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        return out_;
    }

    static List<Account> CodexAccounts()
    {
        var auth = Paths.Join(Home, ".codex/auth.json");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(auth));
            var mode = Str(doc.RootElement, "auth_mode");
            return [new Account("codex", "codex", "Codex", mode == "apikey" ? "api" : "subscription", mode, Paths.Dir(auth))];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return []; }
    }

    internal static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
