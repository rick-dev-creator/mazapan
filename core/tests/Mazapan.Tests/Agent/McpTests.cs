using System.Text.Json;
using Mazapan.Cli;

namespace Mazapan.Tests.Agent;

/// <summary>The MCP server's protocol: what any agent's client relies on.</summary>
public class McpTests
{
    static JsonElement Call(string request) => JsonDocument.Parse(Program.Handle(request)!).RootElement;

    [Fact]
    public void InitializeAnswersWithToolsAndTheVersionAsked()
    {
        var r = Call("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{}}}""");
        Assert.Equal(1, r.GetProperty("id").GetInt32());
        var res = r.GetProperty("result");
        Assert.Equal("2025-03-26", res.GetProperty("protocolVersion").GetString());
        Assert.True(res.GetProperty("capabilities").TryGetProperty("tools", out _));
        Assert.Equal("mazapan", res.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public void AnUnknownVersionGetsOurs() =>
        Assert.Equal("2025-06-18", Call("""{"jsonrpc":"2.0","id":"a","method":"initialize","params":{"protocolVersion":"1999-01-01"}}""")
            .GetProperty("result").GetProperty("protocolVersion").GetString());

    [Fact]
    public void ToolsAreListedWithSchemasAndHints()
    {
        var tools = Call("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""").GetProperty("result").GetProperty("tools")
            .EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!);
        foreach (var name in new[] { "status", "doctor", "themes", "plugins", "coverage", "history", "logs", "preview_change", "apply_change", "undo" })
            Assert.Contains(name, tools.Keys);
        Assert.True(tools["preview_change"].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.False(tools["apply_change"].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.Equal("object", tools["apply_change"].GetProperty("inputSchema").GetProperty("type").GetString());
        // Checkpoints: read, never restored by an agent (that's the person's, as root).
        Assert.True(tools["checkpoints"].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.True(tools["checkpoint_diagnose"].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.DoesNotContain(tools.Keys, k => k.Contains("restore") || k.Contains("keep"));
        // Installing plugins or updating the system is the person's call, never an agent's.
        Assert.DoesNotContain(tools.Keys, k => k.Contains("install") || k.Contains("update") || k.Contains("add"));
    }

    [Fact]
    public void ErrorsAreJsonRpcErrors()
    {
        Assert.Equal(-32700, Call("not json").GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(-32601, Call("""{"jsonrpc":"2.0","id":3,"method":"nope"}""").GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(-32602, Call("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"rm"}}""").GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public void NotificationsGetNoAnswer() =>
        Assert.Null(Program.Handle("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));

    [Fact]
    public void AnUnknownArgumentIsAToolError()
    {
        var r = Call("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"preview_change","arguments":{"rm":"-rf"}}}""");
        Assert.True(r.GetProperty("result").GetProperty("isError").GetBoolean());
    }
}
