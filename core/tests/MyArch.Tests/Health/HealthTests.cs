using System.Text.Json;
using System.Text.Json.Serialization;
using MyArch.Health;
using MyArch.Rendering;

namespace MyArch.Tests.Health;

[JsonSerializable(typeof(Result))]
internal partial class HealthJson : JsonSerializerContext;

public class HealthTests
{
    [Fact]
    public void Run()
    {
        var results = new[]
        {
            new RenderedCheck("a", "passes", "true", 5, false),
            new RenderedCheck("b", "fails", "echo broken; exit 3", 5, false),
            new RenderedCheck("c", "hangs", "sleep 10", 1, false),
        }.Select(Checks.RunOne).ToList();

        Assert.True(results[0].OK && results[0].Output == "", $"passing check: {results[0]}");
        Assert.True(!results[1].OK && results[1].Output == "broken", $"failing check should keep its output: {results[1]}");
        Assert.True(!results[2].OK && results[2].Output.Contains("timed out") && results[2].Took.TotalSeconds <= 3,
            $"hanging check should time out: {results[2]}");
        Assert.Equal(2, Checks.Failed(results).Count);
    }

    [Fact]
    public void SessionChecksAreSkippedOutsideIt()
    {
        var saved = Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE");
        try
        {
            Environment.SetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE", "");
            var r = Checks.RunOne(new RenderedCheck("", "bar", "exit 1", 5, true));
            Assert.True(r.OK && r.Skipped, $"outside the session a session check must be skipped, not failed: {r}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE", saved);
        }
    }

    [Fact]
    public void ChildKeepingOutputOpenIsNotAFailure()
    {
        // Like a check that restarts a daemon: it exits 0, the child lives on.
        var r = Checks.RunOne(new RenderedCheck("", "restarts", "sleep 5 & exit 0", 10, false));
        Assert.True(r.OK, $"a passing check with a child left running must pass: {r}");
        Assert.True(r.Took.TotalSeconds < 3, $"WaitDelay is a second: {r.Took}");
    }

    [Fact]
    public void OutputIsCombinedInOrderAndKeepsTheLastLines()
    {
        var r = Checks.RunOne(new RenderedCheck("", "noisy", "for i in 1 2 3 4; do echo out$i; echo err$i >&2; done; exit 1", 5, false));
        Assert.Equal("err2\nout3\nerr3\nout4\nerr4", string.Join("\n", r.Output.Split('\n')[1..]));
        Assert.Equal(6, r.Output.Split('\n').Length);
    }

    [Fact]
    public void StdinIsEmpty()
    {
        var r = Checks.RunOne(new RenderedCheck("", "reads", "cat; echo \"$0\"; exit 1", 5, false));
        Assert.Equal("sh", r.Output); // nothing to read, and $0 is sh as in Go
    }

    [Fact]
    public void NonzeroExitWithAChildHoldingTheOutputFails()
    {
        var r = Checks.RunOne(new RenderedCheck("", "x", "echo bye; sleep 5 & exit 2", 10, false));
        Assert.False(r.OK);
        Assert.Equal("bye", r.Output);
    }

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(5, "5s")]
    [InlineData(60, "1m0s")]
    [InlineData(90, "1m30s")]
    [InlineData(3600, "1h0m0s")]
    [InlineData(3725, "1h2m5s")]
    [InlineData(-5, "-5s")]
    public void GoDuration(long s, string want) => Assert.Equal(want, Checks.GoDuration(s));

    [Fact]
    public void JsonAsGoWroteIt()
    {
        var r = new Result { Plugin = "p", Name = "n", OK = true, Took = TimeSpan.FromMilliseconds(1.5) };
        Assert.Equal("""{"plugin":"p","name":"n","ok":true,"took":1500000}""", JsonSerializer.Serialize(r, HealthJson.Default.Result));
        var f = r with { OK = false, Output = "x", Skipped = true };
        Assert.Equal("""{"plugin":"p","name":"n","ok":false,"skipped":true,"output":"x","took":1500000}""", JsonSerializer.Serialize(f, HealthJson.Default.Result));
        Assert.Equal(f, JsonSerializer.Deserialize(JsonSerializer.Serialize(f, HealthJson.Default.Result), HealthJson.Default.Result));
    }
}
