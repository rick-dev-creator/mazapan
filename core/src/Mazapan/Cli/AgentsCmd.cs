using System.Diagnostics;
using System.Globalization;
using Mazapan.Agents;
using Mazapan.Util;

namespace Mazapan.Cli;

/// <summary>
/// mazapan agents: the coding agents here, every account they use, what
/// each has spent (tokens, and cost: as the agent reported it, or worked
/// out from the model's API prices) and each account's limits; and
/// launching an agent with the account that has room. Numbers only, read
/// locally: no prompt or answer is ever read or kept.
/// </summary>
public static partial class Program
{
    const int AgentsVersion = 1;

    static int CmdAgents(string[] args)
    {
        var sub = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : "list";
        var rest = args.Length > 0 && !args[0].StartsWith('-') ? args[1..] : args;
        if (sub == "run") return AgentsRun(rest);
        if (sub == "event") return AgentsEvent(rest);
        if (sub == "keys") return AgentsKeys(rest);
        if (sub == "otel") return AgentsOtel(rest);
        var fs = new Flags("agents " + sub)
            .Bool("json", "as JSON, for the bar, the dashboard and agents")
            .String("days", "how far back, in days (usage; 30)")
            .Bool("refresh", "fetch the price table again now (usage)")
            .Parse(rest);
        var json = fs.IsSet("json");
        var days = fs.Get("days") is { Length: > 0 } d ? (int.TryParse(d, out var n) ? n : 0) : 30;
        if (sub == "spend") return AgentsSpend(days, fs.IsSet("refresh"), json);
        var agents = Discovery.Find();
        return sub switch
        {
            "list" => AgentsList(agents, json),
            "usage" => AgentsUsage(agents, days, fs.IsSet("refresh"), json),
            "limits" => AgentsLimits(agents, json),
            "sessions" => AgentsSessions(json),
            "hooks" => AgentsHooks(agents, fs.Rest, json),
            "telemetry" => AgentsTelemetry(agents, fs.Rest),
            _ => throw new MazapanException("usage: mazapan agents [list|usage|limits|spend|sessions|hooks install|remove|telemetry on|off [PORT]|keys [set|remove PROVIDER]|run AGENT [ARGS…]] [--json] [--days N]"),
        };
    }

    static Fields AccountJson(Account a) => new()
    {
        { "agent", a.Agent }, { "id", a.Id }, { "label", a.Label }, { "kind", a.Kind }, { "plan", a.Plan },
    };

    static int AgentsList(List<Agent> agents, bool json)
    {
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "version", AgentsVersion },
                {
                    "agents", agents.Select(a => new Fields
                    {
                        { "id", a.Id }, { "name", a.Name }, { "installed", a.Binary != "" },
                        { "accounts", a.Accounts.Select(AccountJson).ToList() },
                    }).ToList()
                },
            }));
            return 0;
        }
        if (agents.Count == 0) Console.WriteLine("No coding agents found.");
        foreach (var a in agents)
        {
            Console.WriteLine($"{Style.Bold}{a.Name}{Style.Reset}{(a.Binary == "" ? $" {Style.Dim}(not installed){Style.Reset}" : "")}");
            foreach (var acc in a.Accounts)
                Console.WriteLine($"  {acc.Label,-36} {Style.Dim}{acc.Kind}{(acc.Plan != "" ? ", " + acc.Plan : "")}{Style.Reset}");
        }
        return 0;
    }

    /// <summary>A day's use, by agent, account, provider, model and project.</summary>
    sealed record UsageRow(string Day, string Agent, string Source, string Provider, string Model, string Project)
    {
        public long Requests, Input, Output, CacheRead, CacheWrite, Reasoning;
        public double Reported, Estimated;
        public long Unpriced; // answers whose model has no price
    }

    static int AgentsUsage(List<Agent> agents, int days, bool refresh, bool json)
    {
        if (days is < 1 or > 3650) throw new MazapanException("--days: 1 to 3650");
        var since = new DateTimeOffset(DateTime.Today.AddDays(-(days - 1)));
        var (entries, problems) = Mazapan.Agents.Usage.Collect(agents, since);
        var prices = Prices.Load(refresh);
        if (prices.Problem != "") problems.Add(prices.Problem);
        var rows = new Dictionary<(string, string, string, string, string, string), UsageRow>();
        foreach (var e in entries)
        {
            var day = e.Time.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var k = (day, e.Agent, e.Source, e.Provider, e.Model, e.Project);
            if (!rows.TryGetValue(k, out var r)) rows[k] = r = new UsageRow(day, e.Agent, e.Source, e.Provider, e.Model, e.Project);
            r.Requests++;
            r.Input += e.Input;
            r.Output += e.Output;
            r.CacheRead += e.CacheRead;
            r.CacheWrite += e.CacheWrite;
            r.Reasoning += e.Reasoning;
            r.Reported += e.Cost ?? 0;
            if (prices.Cost(e) is { } c) r.Estimated += c;
            else r.Unpriced++;
        }
        var list = rows.Values.OrderBy(r => r.Day, StringComparer.Ordinal).ThenBy(r => r.Agent, StringComparer.Ordinal).ToList();
        // For the dashboard: sessions a day (distinct), and when answers come
        // (weekday × hour, local time; Monday first).
        var sessions = entries.Where(e => e.Session != "")
            .GroupBy(e => (Day: e.Time.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.Agent))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Session).Distinct().Count());
        var hours = new long[7][];
        for (var i = 0; i < 7; i++) hours[i] = new long[24];
        foreach (var e in entries)
        {
            var t = e.Time.ToLocalTime();
            hours[((int)t.DayOfWeek + 6) % 7][t.Hour]++;
        }
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "version", AgentsVersion },
                { "since", since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                { "problems", problems },
                {
                    "rows", list.Select(r => new Fields
                    {
                        { "day", r.Day }, { "agent", r.Agent }, { "source", r.Source }, { "provider", r.Provider }, { "model", r.Model },
                        { "project", r.Project }, { "requests", r.Requests }, { "input", r.Input }, { "output", r.Output },
                        { "cache_read", r.CacheRead }, { "cache_write", r.CacheWrite }, { "reasoning", r.Reasoning },
                        // What the agent said it cost (an API's spend; 0 on a plan), and what
                        // the same tokens cost through the model's API.
                        { "cost_reported", Math.Round(r.Reported, 6) }, { "cost_api", Math.Round(r.Estimated, 6) }, { "unpriced", r.Unpriced },
                    }).ToList()
                },
                {
                    "sessions", sessions.OrderBy(kv => kv.Key.Day, StringComparer.Ordinal)
                        .Select(kv => new Fields { { "day", kv.Key.Day }, { "agent", kv.Key.Agent }, { "count", kv.Value } }).ToList()
                },
                { "hours", hours.Select(h => h.ToList()).ToList() },
                // What OpenTelemetry brought (Claude Code): lines, commits, active time, by day.
                { "otel", Otel.Days(Otel.Load(), since).Select(Otel.Json).ToList() },
            }));
            return 0;
        }
        foreach (var p in problems) Console.Error.WriteLine("warning: " + p);
        Console.WriteLine($"Since {since:yyyy-MM-dd} ({days} days):\n");
        foreach (var g in list.GroupBy(r => (r.Agent, r.Source, r.Model)).OrderByDescending(g => g.Sum(r => r.Estimated)))
        {
            var tokens = g.Sum(r => r.Input + r.Output + r.CacheRead + r.CacheWrite);
            var unpriced = g.Sum(r => r.Unpriced) > 0 ? " (some unpriced)" : "";
            Console.WriteLine($"  {g.Key.Agent,-9} {g.Key.Source,-24} {g.Key.Model,-26} {g.Sum(r => r.Requests),7} answers  {Short(tokens),7} tokens  " +
                $"API ${g.Sum(r => r.Estimated):0.00}{unpriced}" + (g.Sum(r => r.Reported) > 0 ? $"  reported ${g.Sum(r => r.Reported):0.00}" : ""));
        }
        return 0;
    }

    static string Short(long n) => n >= 1_000_000_000 ? $"{n / 1e9:0.0}B" : n >= 1_000_000 ? $"{n / 1e6:0.0}M" : n >= 1000 ? $"{n / 1e3:0.0}k" : n.ToString(CultureInfo.InvariantCulture);

    static List<Limits> ClaudeLimits(List<Agent> agents) =>
        agents.FirstOrDefault(a => a.Id == "claude")?.Accounts.Select(AccountLimits.Claude).ToList() ?? [];

    static int AgentsLimits(List<Agent> agents, bool json)
    {
        var all = ClaudeLimits(agents);
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "version", AgentsVersion },
                {
                    "accounts", all.Select(l => new Fields
                    {
                        { "account", AccountJson(l.Account) },
                        { "read", l.Read == DateTimeOffset.MinValue ? "" : l.Read.ToString("o", CultureInfo.InvariantCulture) },
                        { "stale", l.Stale }, { "problem", l.Problem },
                        {
                            "windows", l.Windows.Select(w => new Fields
                            {
                                { "name", w.Name }, { "percent", Math.Round(w.Percent, 1) },
                                { "resets_at", w.ResetsAt?.ToString("o", CultureInfo.InvariantCulture) ?? "" },
                            }).ToList()
                        },
                    }).ToList()
                },
            }));
            return 0;
        }
        if (all.Count == 0) Console.WriteLine("No accounts with limits to show (Claude's are read; others' come later).");
        foreach (var l in all)
        {
            Console.WriteLine($"{Style.Bold}{l.Account.Label}{Style.Reset} {Style.Dim}{l.Account.Plan}{(l.Stale ? ", as last read" + (l.Read != DateTimeOffset.MinValue ? $" {l.Read.ToLocalTime():MMM d HH:mm}" : "") : "")}{Style.Reset}");
            foreach (var w in l.Windows)
                Console.WriteLine($"  {w.Name,-28} {w.Percent,5:0}%" + (w.ResetsAt is { } r ? $"  {Style.Dim}resets {r.ToLocalTime():ddd HH:mm}{Style.Reset}" : ""));
            if (l.Windows.Count == 0 && l.Problem != "") Console.WriteLine($"  {Style.Dim}{l.Problem}{Style.Reset}");
        }
        return 0;
    }

    /// <summary>
    /// mazapan agents event claude (a Claude Code hook: its JSON on stdin), or
    /// event AGENT STATE SESSION [DIR] (opencode's plugin): a session's state.
    /// Silent and never failing: it runs inside the agent.
    /// </summary>
    static int AgentsEvent(string[] args)
    {
        try
        {
            var who = Sessions.CallerAncestors();
            if (args is ["claude"]) Sessions.FromClaudeHook(Console.In.ReadToEnd(), who);
            else if (args.Length >= 3) Sessions.Record(args[0], args[2], args[0], args.Length > 3 ? args[3] : "", args[1], "", who);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
        return 0;
    }

    static int AgentsSessions(bool json)
    {
        var all = Sessions.List();
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "version", AgentsVersion },
                {
                    "sessions", all.Select(s => new Fields
                    {
                        { "agent", s.Agent }, { "id", s.Id }, { "account", s.Account }, { "project", s.Project }, { "state", s.State },
                        { "since", s.Since.ToUnixTimeMilliseconds() }, { "message", s.Message }, { "pids", s.Pids },
                    }).ToList()
                },
            }));
            return 0;
        }
        if (all.Count == 0) Console.WriteLine("No agent sessions open.");
        foreach (var s in all)
            Console.WriteLine($"  {s.Agent,-9} {s.State,-8} {Tilde(s.Project),-40} {Style.Dim}{(DateTimeOffset.UtcNow - s.Since).TotalMinutes:0} min{Style.Reset}");
        return 0;
    }

    static int AgentsHooks(List<Agent> agents, List<string> rest, bool json)
    {
        var accounts = agents.FirstOrDefault(a => a.Id == "claude")?.Accounts ?? [];
        if (rest is not ["install" or "remove"]) throw new MazapanException("usage: mazapan agents hooks install|remove");
        var changed = Hooks.Set(accounts, rest[0] == "install");
        if (json) Console.WriteLine(GoJson.Marshal(new Fields { { "changed", changed } }));
        else Console.WriteLine(changed.Count == 0 ? "Nothing to change." : string.Join("\n", changed.Select(c => (rest[0] == "install" ? "hooks in " : "hooks out of ") + Tilde(c))));
        return 0;
    }

    /// <summary>mazapan agents telemetry on|off [PORT]: Claude Code's metrics to the receiver here, in each configuration's settings.json.</summary>
    static int AgentsTelemetry(List<Agent> agents, List<string> rest)
    {
        if (rest is not (["on" or "off"] or ["on" or "off", _])) throw new MazapanException("usage: mazapan agents telemetry on|off [PORT]");
        var port = rest.Count > 1 && int.TryParse(rest[1], out var p) && p is > 1024 and < 65536 ? p : Otel.DefaultPort;
        var theirs = new List<string>();
        var changed = Hooks.SetTelemetry(agents.FirstOrDefault(a => a.Id == "claude")?.Accounts ?? [], rest[0] == "on", port, theirs);
        foreach (var c in changed) Console.WriteLine($"telemetry {rest[0]} in {Tilde(c)}");
        foreach (var t in theirs) Console.WriteLine($"{Tilde(t)}: telemetry of your own there, left as it is");
        if (changed.Count == 0 && theirs.Count == 0) Console.WriteLine("Nothing to change.");
        return 0;
    }

    /// <summary>mazapan agents otel [--port N] [--idle MINUTES]: the receiver (systemd starts it, through its socket).</summary>
    static int AgentsOtel(string[] args)
    {
        var fs = new Flags("agents otel")
            .String("port", $"the port on 127.0.0.1 without systemd's socket ({Otel.DefaultPort})")
            .String("idle", "minutes without a request before it ends (10)")
            .Parse(args);
        var port = int.TryParse(fs.Get("port"), out var p) ? p : Otel.DefaultPort;
        var idle = int.TryParse(fs.Get("idle"), out var m) && m > 0 ? m : 10;
        Otel.Serve(port, TimeSpan.FromMinutes(idle));
        return 0;
    }

    /// <summary>
    /// mazapan agents run AGENT [ARGS…]: the agent, in this terminal. Claude
    /// with the first account that has room (its own configuration directory);
    /// with none, said, and when the first comes back.
    /// </summary>
    static int AgentsRun(string[] args)
    {
        if (args.Length == 0) throw new MazapanException("usage: mazapan agents run AGENT [ARGS…]");
        var agents = Discovery.Find();
        var agent = agents.FirstOrDefault(a => a.Id == args[0]) ?? throw new MazapanException($"no agent \"{args[0]}\" here (mazapan agents)");
        if (agent.Binary == "") throw new MazapanException($"{agent.Name} isn't installed");
        var psi = new ProcessStartInfo(agent.Binary) { UseShellExecute = false };
        foreach (var a in args[1..]) psi.ArgumentList.Add(a);
        if (agent.Id == "claude" && agent.Accounts.Count > 1)
        {
            var limits = ClaudeLimits(agents);
            var pick = AccountLimits.Pick(limits);
            if (pick == null)
            {
                var next = limits.SelectMany(l => l.Windows.Where(w => w.Percent >= 95 && w.ResetsAt != null).Select(w => (l, w.ResetsAt!.Value)))
                    .OrderBy(x => x.Value).FirstOrDefault();
                throw new MazapanException("every Claude account is at its limit" + (next.l != null ? $"; {next.l.Account.Label} is back {next.Value.ToLocalTime():ddd HH:mm}" : ""));
            }
            var home = Environment.GetEnvironmentVariable("HOME") ?? "";
            // ~/.claude is Claude's own default: its profile is ~/.claude.json, so
            // it's left unset for it.
            if (pick.Account.ConfigDir.TrimEnd('/') != Paths.Join(home, ".claude")) psi.Environment["CLAUDE_CONFIG_DIR"] = pick.Account.ConfigDir;
            else psi.Environment.Remove("CLAUDE_CONFIG_DIR");
            var skipped = limits.TakeWhile(l => l != pick).Where(l => !AccountLimits.HasRoom(l)).ToList();
            Console.Error.WriteLine($"mazapan: {agent.Name} as {pick.Account.Label}" +
                (skipped.Count > 0 ? $" ({string.Join(", ", skipped.Select(s => s.Account.Label))} at its limit)" : ""));
        }
        // The API keys in the keyring it can take (not for Claude Code and Codex: they sign in).
        foreach (var (k, v) in Keys.Environment(agent.Id)) psi.Environment[k] = v;
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return p.ExitCode;
    }

    /// <summary>
    /// mazapan agents keys [set|remove PROVIDER]: API keys in the keyring.
    /// set reads the key from stdin (typed hidden in a terminal), so it never
    /// shows in a command line or the shell's history. Listed by provider only.
    /// </summary>
    static int AgentsKeys(string[] args)
    {
        var json = args.Contains("--json");
        args = [.. args.Where(a => a != "--json")];
        switch (args)
        {
            case []:
                var kept = Keys.Kept();
                if (json)
                {
                    Console.WriteLine(GoJson.Marshal(new Fields
                    {
                        { "version", AgentsVersion },
                        { "providers", Keys.Providers.Select(p => new Fields { { "id", p.Id }, { "name", p.Name }, { "env", p.Env }, { "admin", p.Admin }, { "kept", kept.Contains(p) } }).ToList() },
                    }));
                    return 0;
                }
                foreach (var p in Keys.Providers)
                    Console.WriteLine($"  {(kept.Contains(p) ? Style.Green + "✓" : " ")}{Style.Reset} {p.Id,-16} {Style.Dim}{p.Name}{(p.Env != "" ? " · " + p.Env : " · its spend")}{Style.Reset}");
                Console.WriteLine($"{Style.Dim}mazapan agents keys set PROVIDER (the key on stdin) · remove PROVIDER{Style.Reset}");
                return 0;
            case ["set", var id]:
                var provider = Keys.Of(id);
                var key = Console.IsInputRedirected ? Console.In.ReadToEnd() : ReadSecret($"{provider.Name} API key: ");
                Keys.Set(id, key);
                Console.WriteLine($"{provider.Name}: kept in the keyring.");
                return 0;
            case ["remove", var id]:
                Console.WriteLine(Keys.Remove(id) ? $"{Keys.Of(id).Name}: taken out of the keyring." : $"{Keys.Of(id).Name}: there was none.");
                return 0;
            default:
                throw new MazapanException("usage: mazapan agents keys [set|remove PROVIDER] [--json]");
        }
    }

    /// <summary>mazapan agents spend: what the providers say was spent, with the keys in the keyring.</summary>
    static int AgentsSpend(int days, bool refresh, bool json)
    {
        var list = Spend.Read(days, refresh);
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields { { "version", AgentsVersion }, { "days", days }, { "providers", list.Select(Spend.Json).ToList() } }));
            return 0;
        }
        if (list.Count == 0)
        {
            Console.WriteLine("No provider to ask: an OpenRouter key, or an admin key for Anthropic or OpenAI (mazapan agents keys).");
            return 0;
        }
        foreach (var s in list)
        {
            if (s.Problem != "") { Console.WriteLine($"{s.Name,-20} {Style.Dim}{s.Problem}{Style.Reset}"); continue; }
            Console.WriteLine($"{s.Name,-20} today ${s.Today:0.00} · {days} days ${s.Period:0.00}"
                + (s.Limit is { } l ? $" · limit ${l:0.00}" + (s.Remaining is { } r ? $", ${r:0.00} left" : "") : ""));
        }
        return 0;
    }
}
