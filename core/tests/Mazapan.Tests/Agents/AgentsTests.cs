using Mazapan.Agents;

namespace Mazapan.Tests.Agents;

[Collection("HOME")]
public class AgentsTests
{
    const string ClaudeLine = """
        {"parentUuid":"a","message":{"model":"claude-opus-5-5","id":"msg_1","type":"message","role":"assistant","usage":{"input_tokens":2,"cache_creation_input_tokens":14034,"cache_read_input_tokens":25147,"output_tokens":164,"output_tokens_details":{"thinking_tokens":29}}},"requestId":"req_1","type":"assistant","uuid":"u","timestamp":"2026-10-03T02:13:56.674Z","cwd":"/home/x/proj","sessionId":"s1"}
        """;

    [Fact]
    public void AClaudeAnswerIsItsTokensNotItsWords()
    {
        var e = Usage.ParseClaude(ClaudeLine, "claude")!;
        Assert.Equal(("claude", "anthropic", "claude-opus-5-5"), (e.Agent, e.Provider, e.Model));
        Assert.Equal((2L, 164L, 25147L, 14034L, 29L), (e.Input, e.Output, e.CacheRead, e.CacheWrite, e.Reasoning));
        Assert.Equal("/home/x/proj", e.Project);
        Assert.Equal("msg_1:req_1", e.Key);
        Assert.Null(e.Cost);
        Assert.Null(Usage.ParseClaude("""{"type":"user","message":{"role":"user","content":"hi"}}""", "claude"));
        Assert.Null(Usage.ParseClaude(ClaudeLine.Replace("claude-opus-5-5", "<synthetic>"), "claude"));
    }

    [Fact]
    public void PiSaysItsProviderModelAndCost()
    {
        var lines = new[]
        {
            """{"type":"session","version":3,"id":"sess","timestamp":"2026-10-02T14:15:00.222Z","cwd":"/home/x/kasumi"}""",
            """{"type":"message","id":"m1","timestamp":"2026-10-02T14:15:29.324Z","message":{"role":"assistant","provider":"zai","model":"glm-5.3-flash","usage":{"input":120,"output":30,"cacheRead":10,"cacheWrite":0,"totalTokens":160,"cost":{"total":0.0042}}}}""",
            """{"type":"message","id":"m2","message":{"role":"user","content":"x"}}""",
        };
        var e = Assert.Single(Usage.ParsePi(lines));
        Assert.Equal(("pi", "zai", "glm-5.3-flash", "/home/x/kasumi"), (e.Agent, e.Provider, e.Model, e.Project));
        Assert.Equal(0.0042, e.Cost);
        Assert.Equal((120L, 30L, 10L), (e.Input, e.Output, e.CacheRead));
    }

    [Fact]
    public void CodexCachedInputIsntCountedTwice()
    {
        var lines = new[]
        {
            """{"timestamp":"2026-10-01T10:00:00Z","type":"session_meta","payload":{"id":"c1","cwd":"/home/x/p"}}""",
            """{"timestamp":"2026-10-01T10:00:01Z","type":"turn_context","payload":{"model":"gpt-5.5-codex"}}""",
            """{"timestamp":"2026-10-01T10:00:09Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":1000,"cached_input_tokens":800,"output_tokens":50,"reasoning_output_tokens":20}}}}""",
        };
        var e = Assert.Single(Usage.ParseCodex(lines));
        Assert.Equal(("gpt-5.5-codex", "/home/x/p"), (e.Model, e.Project));
        Assert.Equal((200L, 800L, 50L, 20L), (e.Input, e.CacheRead, e.Output, e.Reasoning));
    }

    [Fact]
    public void PricesFindAModelUnderItsProviderOrWithoutItsDate()
    {
        var p = new Prices();
        p.Read("""{"sample_spec":{"input_cost_per_token":0},"claude-opus-5-5":{"input_cost_per_token":0.000015,"output_cost_per_token":0.000075,"cache_read_input_token_cost":0.0000015,"cache_creation_input_token_cost":0.00001875},"openai/gpt-5.5":{"input_cost_per_token":0.00001,"output_cost_per_token":0.00004}}""");
        var e = Usage.ParseClaude(ClaudeLine, "claude")!;
        var cost = p.Cost(e)!.Value;
        Assert.Equal(2 * 0.000015 + 164 * 0.000075 + 25147 * 0.0000015 + 14034 * 0.00001875, cost, 9);
        Assert.NotNull(p.For("openai", "gpt-5.5"));
        Assert.NotNull(p.For("anthropic", "claude-opus-5-5-20261001"));
        Assert.Null(p.For("zai", "glm-5.3")); // not listed: unknown, never guessed
    }

    [Fact]
    public void LimitsAreInPercentAndAModelsOwnWindow()
    {
        var w = AccountLimits.Parse("""
            {"five_hour":{"utilization":42,"resets_at":"2026-10-03T18:00:00Z"},"seven_day":{"utilization":90,"resets_at":"2026-10-07T00:00:00Z"},
             "limits":[{"kind":"weekly_scoped","percent":50,"scope":{"model":{"display_name":"Opus 5"}},"resets_at":"2026-10-07T00:00:00Z"}]}
            """);
        Assert.Equal(["session", "week", "week: Opus 5"], w.Select(x => x.Name));
        Assert.Equal(42, w[0].Percent, 6);
        Assert.Equal(90, w[1].Percent, 6);
        // Just after a reset: 1 % is 1 %, not full.
        var low = AccountLimits.Parse("""{"five_hour":{"utilization":1.0,"resets_at":null},"seven_day":{"utilization":0}}""");
        Assert.Equal(1, low[0].Percent, 6);
        Assert.Null(low[0].ResetsAt);
    }

    [Fact]
    public void TheAccountWithRoomIsTaken()
    {
        var a = new Account("claude", "claude", "a@x", "subscription", "max 20x", "/h/.claude");
        var b = a with { Id = "claude-cuenta2", Label = "b@x" };
        var c = a with { Id = "claude-old", Label = "c@x" };
        var full = new Limits(a, [new Window("session", 99, DateTimeOffset.UtcNow.AddHours(2))], DateTimeOffset.UtcNow, false, "");
        var room = new Limits(b, [new Window("session", 40, DateTimeOffset.UtcNow.AddHours(2))], DateTimeOffset.UtcNow, false, "");
        var old = new Limits(c, [], DateTimeOffset.MinValue, true, "unused lately");
        Assert.Equal(b, AccountLimits.Pick([full, room, old])!.Account);
        Assert.Equal(c, AccountLimits.Pick([full, old])!.Account);       // known full: an unknown one before it
        Assert.Null(AccountLimits.Pick([full]));
        var over = full with { Windows = [new Window("session", 99, DateTimeOffset.UtcNow.AddMinutes(-1))] };
        Assert.Equal(a, AccountLimits.Pick([over])!.Account);            // its window is over already
    }

    [Fact]
    public void ClaudeConfigurationsAreFoundWithTheirPlans()
    {
        using var d = new Mazapan.Tests.Foundation.TempDir();
        var home = d.Path;
        d.Write(".claude/.credentials.json", """{"claudeAiOauth":{"accessToken":"secret","subscriptionType":"max","rateLimitTier":"default_claude_max_20x"}}""");
        d.Write(".claude.json", """{"oauthAccount":{"emailAddress":"one@example.com"}}""");
        d.Write(".claude-work/.credentials.json", """{"claudeAiOauth":{"accessToken":"secret2","subscriptionType":"pro"}}""");
        d.Write(".claude-work/.claude.json", """{"oauthAccount":{"emailAddress":"two@example.com"}}""");
        Directory.CreateDirectory(Path.Join(home, ".claude-empty")); // no login: not an account
        var old = Environment.GetEnvironmentVariable("HOME");
        var oldCfg = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        try
        {
            Environment.SetEnvironmentVariable("HOME", home);
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
            var accounts = Discovery.ClaudeAccounts();
            Assert.Equal(["one@example.com", "two@example.com"], accounts.Select(a => a.Label));
            Assert.Equal(["max 20x", "pro"], accounts.Select(a => a.Plan));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", old);
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", oldCfg);
        }
    }

    [Fact]
    public void OpencodesDatabaseIsReadOnlyItsAnswers()
    {
        if (Discovery.Which("sqlite3") == null) return; // nothing to read it with here
        using var d = new Mazapan.Tests.Foundation.TempDir();
        var dir = Path.Join(d.Path, ".local/share/opencode");
        Directory.CreateDirectory(dir);
        var db = Path.Join(dir, "opencode.db");
        var sql = "create table session (id text primary key, directory text not null);"
            + "create table message (id text primary key, session_id text not null, time_created integer not null, data text not null);"
            + "insert into session values ('s1', '/home/x/app');"
            + "insert into message values ('m1', 's1', 1790951746626, '{\"role\":\"assistant\",\"providerID\":\"zai-coding-plan\",\"modelID\":\"glm-5.3\",\"cost\":0,\"tokens\":{\"input\":2008,\"output\":342,\"reasoning\":502,\"cache\":{\"write\":0,\"read\":284416}}}');"
            + "insert into message values ('m2', 's1', 1790951746700, '{\"role\":\"user\"}');";
        var p = System.Diagnostics.Process.Start("sqlite3", [db, sql])!;
        p.WaitForExit();
        Assert.Equal(0, p.ExitCode);
        var old = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", d.Path);
            var (entries, problems) = Usage.Collect([new Mazapan.Agents.Agent("opencode", "OpenCode", "", [])], DateTimeOffset.FromUnixTimeMilliseconds(0));
            Assert.Empty(problems);
            var e = Assert.Single(entries);
            Assert.Equal(("zai-coding-plan", "glm-5.3", "/home/x/app"), (e.Provider, e.Model, e.Project));
            Assert.Equal((2008L, 342L, 284416L, 502L), (e.Input, e.Output, e.CacheRead, e.Reasoning));
            Assert.Equal(0.0, e.Cost);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", old);
        }
    }

    [Fact]
    public void HooksGoInBesideThePersonsOwnAndComeOutAlone()
    {
        const string mine = """{"model":"opus","hooks":{"Stop":[{"hooks":[{"type":"command","command":"notify-send done"}]}]}}""";
        var with = Hooks.Apply(mine, install: true)!;
        Assert.True(Hooks.Installed(with));
        Assert.Contains("notify-send done", with);
        Assert.Contains("\"model\": \"opus\"", with);
        Assert.Null(Hooks.Apply(with, install: true));        // twice: nothing more
        var without = Hooks.Apply(with, install: false)!;
        Assert.False(Hooks.Installed(without));
        Assert.Contains("notify-send done", without);           // the person's own stay
        Assert.DoesNotContain("SessionStart", without);
        Assert.Null(Hooks.Apply("{}", install: false));
        // An older command of ours is replaced, not kept beside; & stays &.
        var older = with.Replace(Hooks.Command, "/usr/bin/mazapan agents event claude");
        Assert.False(Hooks.Installed(older));
        var again = Hooks.Apply(older, install: true)!;
        Assert.True(Hooks.Installed(again));
        Assert.Equal(7, again.Split("mazapan agents event").Length - 1);
        Assert.Contains("|| true", again);
        Assert.DoesNotContain("\\u0026", Hooks.Apply("""{"x":"a && b"}""", install: true)!);
    }

    [Fact]
    public void AHookTellsTheSessionsStateAndItsEndTakesItOff()
    {
        using var d = new Mazapan.Tests.Foundation.TempDir();
        var old = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", d.Path);
            var me = Environment.ProcessId;
            Sessions.FromClaudeHook("""{"session_id":"abc-1","hook_event_name":"UserPromptSubmit","cwd":"/home/x/p"}""", [me]);
            var s = Assert.Single(Sessions.List(), x => x.Id == "abc-1");
            Assert.Equal(("claude", "working", "/home/x/p"), (s.Agent, s.State, s.Project));
            Sessions.FromClaudeHook("""{"session_id":"abc-1","hook_event_name":"Notification","message":"Claude needs your permission to use Bash"}""", [me]);
            s = Assert.Single(Sessions.List(), x => x.Id == "abc-1");
            Assert.Equal("waiting", s.State);
            Assert.StartsWith("Claude needs", s.Message);
            Sessions.FromClaudeHook("""{"session_id":"abc-1","hook_event_name":"PostToolUse"}""", [me]);
            Assert.Equal("working", Assert.Single(Sessions.List(), x => x.Id == "abc-1").State);
            Sessions.FromClaudeHook("""{"session_id":"abc-1","hook_event_name":"Stop"}""", [me]);
            // Idle a minute after its answer: still done.
            Sessions.FromClaudeHook("""{"session_id":"abc-1","hook_event_name":"Notification","notification_type":"idle_prompt","message":"Claude is waiting for your input"}""", [me]);
            Assert.Equal("done", Assert.Single(Sessions.List(), x => x.Id == "abc-1").State);
            Sessions.FromClaudeHook("""{"session_id":"abc-1","hook_event_name":"SessionEnd"}""", [me]);
            Assert.DoesNotContain(Sessions.List(), x => x.Id == "abc-1");
            // A session whose agent has gone is taken off; a name that could be a path, refused.
            Sessions.Record("claude", "gone", "claude", "", "working", "", [int.MaxValue - 7]);
            Assert.DoesNotContain(Sessions.List(), x => x.Id == "gone");
            Sessions.Record("claude", "../x", "claude", "", "working", "", [me]);
            Assert.False(File.Exists(Path.Join(d.Path, "x.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", old);
        }
    }

    [Fact]
    public void ProvidersSpendIsReadInTheirOwnUnits()
    {
        // Anthropic: cents, as decimal strings ("123.45" is $1.23), a bucket per UTC day.
        using (var a = System.Text.Json.JsonDocument.Parse("""
            {"data":[{"starting_at":"2026-10-03T00:00:00Z","ending_at":"2026-10-04T00:00:00Z","results":[{"amount":"123.45","currency":"USD"},{"amount":"76.55","currency":"USD"}]},
                     {"starting_at":"2026-10-04T00:00:00Z","ending_at":"2026-10-05T00:00:00Z","results":[]}],"has_more":true,"next_page":"page_x"}
            """))
        {
            var days = Spend.ParseAnthropic(a.RootElement, out var next);
            Assert.Equal([("2026-10-03", 2.0), ("2026-10-04", 0.0)], days.Select(d => (d.Day, Math.Round(d.Usd, 6))));
            Assert.Equal("page_x", next);
        }
        // OpenAI: dollars, start times in Unix seconds.
        using (var o = System.Text.Json.JsonDocument.Parse("""
            {"data":[{"start_time":1759449600,"end_time":1759536000,"results":[{"amount":{"value":0.06,"currency":"usd"}},{"amount":{"value":1.5,"currency":"usd"}}]}],"has_more":false,"next_page":null}
            """))
        {
            var days = Spend.ParseOpenAI(o.RootElement, out var next);
            Assert.Equal([("2025-10-03", 1.56)], days.Select(d => (d.Day, Math.Round(d.Usd, 6))));
            Assert.Null(next);
        }
        // OpenRouter: its key's own numbers, in dollars; the period the closest it has.
        using var r = System.Text.Json.JsonDocument.Parse("""
            {"data":{"label":"sk-or-v1-...","limit":100,"limit_remaining":74.5,"usage":25.5,"usage_daily":1.25,"usage_weekly":6,"usage_monthly":20}}
            """);
        var or = Spend.ParseOpenRouter(r.RootElement, 30);
        Assert.Equal((1.25, 20.0, 100.0, 74.5), (or.Today, or.Period, or.Limit!.Value, or.Remaining!.Value));
        Assert.Equal(6, Spend.ParseOpenRouter(r.RootElement, 7).Period);
    }

    [Fact]
    public void AgentsThatSignInAreGivenNoKey()
    {
        Assert.False(Keys.Takes("claude"));   // ANTHROPIC_API_KEY would win over its subscription
        Assert.False(Keys.Takes("codex"));
        Assert.True(Keys.Takes("opencode"));
        Assert.True(Keys.Takes("pi"));
        Assert.Empty(Keys.Environment("claude"));
        Assert.Throws<Mazapan.Util.MazapanException>(() => Keys.Of("nope"));
        Assert.All(Keys.Providers.Where(p => !p.Admin), p => Assert.EndsWith("_API_KEY", p.Env));
    }
}
