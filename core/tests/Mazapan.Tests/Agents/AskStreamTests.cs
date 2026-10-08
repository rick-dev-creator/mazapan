using Mazapan.Cli;

namespace Mazapan.Tests.Agents;

/// <summary>The Ask card's agent, followed as it goes (Claude's stream-json).</summary>
public class AskStreamTests
{
    static (string Answer, string Session, string Problem, string[] Events) Follow(params string[] lines)
    {
        var events = new StringWriter();
        var (a, s, p) = Program.FollowStream(new StringReader(string.Join("\n", lines) + "\n"), events);
        return (a, s, p, events.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    const string Init = """{"type":"system","subtype":"init","session_id":"s-1","tools":["Read"]}""";

    [Fact]
    public void EachToolIsSaidByItsMazapanName()
    {
        var r = Follow(Init,
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"mcp__mazapan__status","input":{}}]}}""",
            """{"type":"result","subtype":"success","is_error":false,"result":"Everforest, oscuro.","session_id":"s-1"}""");
        Assert.Equal(["""{"event":"tool","name":"status"}"""], r.Events);
        Assert.Equal(("Everforest, oscuro.", "s-1", ""), (r.Answer, r.Session, r.Problem));
    }

    [Fact]
    public void AChangeMadeGivesItsIdToUndo()
    {
        var r = Follow(
            """{"type":"assistant","message":{"content":[{"type":"text","text":"Lo cambio."},{"type":"tool_use","id":"t2","name":"mcp__mazapan__apply_change","input":{"theme":"gruvbox"}}]}}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t2","content":[{"type":"text","text":"14 written\nundo with: mazapan undo (id 20261008-034658-884)"}]}]}}""",
            """{"type":"result","is_error":false,"result":"Listo.","session_id":"s-2"}""");
        Assert.Equal(["""{"event":"tool","name":"apply_change"}""", """{"event":"applied","id":"20261008-034658-884"}"""], r.Events);
    }

    [Fact]
    public void AChangeTurnedDownIsSaidSo()
    {
        var r = Follow(
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t3","name":"mcp__mazapan__apply_change","input":{}}]}}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t3","is_error":true,"content":"the person said no: nothing changed"}]}}""");
        Assert.Equal("""{"event":"declined"}""", r.Events[^1]);
    }

    [Fact]
    public void AChangeWithNothingToWriteIsMadeNotTurnedDown()
    {
        var r = Follow(
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t4","name":"mcp__mazapan__apply_change","input":{}}]}}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t4","content":"0 written\n1 system file and 0 packages wait for: mazapan apply --system (with sudo)"}]}}""");
        Assert.Equal("""{"event":"applied","id":""}""", r.Events[^1]);
    }

    [Fact]
    public void OtherToolResultsAndJunkAreLeftAlone()
    {
        var r = Follow("not json", "[]",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"nobody","content":"(id 20261008-034658-884)"}]}}""");
        Assert.Empty(r.Events);
    }

    [Fact]
    public void AnErrorIsAProblemNotAnAnswer()
    {
        var r = Follow("""{"type":"result","is_error":true,"result":"Claude AI usage limit reached","session_id":"s-3"}""");
        Assert.Equal(("", "Claude AI usage limit reached"), (r.Answer, r.Problem));
    }

    [Fact]
    public void ReadingTheWebMarksTheConversation()
    {
        var mark = Path.Combine(Path.GetTempPath(), $"web-test-{Guid.NewGuid():N}");
        try
        {
            var events = new StringWriter();
            Program.FollowStream(new StringReader(
                """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"w1","name":"WebSearch","input":{"query":"x"}}]}}""" + "\n"), events, mark);
            Assert.True(File.Exists(mark));
            Assert.Contains("""{"event":"web"}""", events.ToString());
        }
        finally { File.Delete(mark); }
    }

    [Fact]
    public void AConversationThatReadTheWebChangesNothing()
    {
        var mark = Path.Combine(Path.GetTempPath(), $"web-test-{Guid.NewGuid():N}");
        var old = Environment.GetEnvironmentVariable("MAZAPAN_ASK_WEB");
        try
        {
            Environment.SetEnvironmentVariable("MAZAPAN_ASK_WEB", mark);
            Assert.Null(Program.WebRefusal());
            File.WriteAllText(mark, "");
            Assert.Contains("read the web", Program.WebRefusal());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAZAPAN_ASK_WEB", old);
            File.Delete(mark);
        }
    }

    [Theory]
    [InlineData("mira github.com/rick-dev-creator/mazapan y dime", "github.com")]
    [InlineData("lee https://wiki.archlinux.org/title/Hyprland, por favor", "wiki.archlinux.org")]
    [InlineData("¿qué tema tengo?", "")]
    [InlineData("escríbele a ana@example.com", "")]
    [InlineData("abre printer.local", "")]
    public void TheSitesItMayOpenAreTheOnesNamed(string words, string expected) =>
        Assert.Equal(expected, string.Join(",", Program.WebSites(words)));

    [Fact]
    public void TheCardsAgentIsNamedAsSuch() =>
        Assert.Equal("Mazapan's agent", ApprovalNames.Readable("mazapan-ask"));
}
