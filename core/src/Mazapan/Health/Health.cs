using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mazapan.Pacman;
using Mazapan.Rendering;
using Mazapan.Util;

namespace Mazapan.Health;

/// <summary>
/// One check's outcome. The JSON attributes give Go's struct tags
/// ({"plugin","name","ok","skipped,omitempty","output,omitempty","took"}),
/// with took in nanoseconds as Go's time.Duration.
/// </summary>
public sealed record Result
{
    [JsonPropertyName("plugin")] public string Plugin { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("ok")] public bool OK { get; init; }

    /// <summary>Needs the session, which isn't there.</summary>
    [JsonPropertyName("skipped"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Skipped { get; init; }

    /// <summary>Only kept when it failed.</summary>
    [JsonIgnore]
    public string Output { get; init; } = "";

    /// <summary>Output for JSON: null when empty, so it's left out (Go's omitempty).</summary>
    [JsonPropertyName("output"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), EditorBrowsable(EditorBrowsableState.Never)]
    public string? OutputJson
    {
        get => Output == "" ? null : Output;
        init => Output = value ?? "";
    }

    [JsonPropertyName("took"), JsonConverter(typeof(GoDurationConverter))]
    public TimeSpan Took { get; init; }
}

/// <summary>
/// A time.Duration in JSON: an integer count of nanoseconds. TimeSpan holds
/// 100 ns ticks, so what's read loses the last two digits.
/// </summary>
public sealed class GoDurationConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeSpan.FromTicks(reader.GetInt64() / TimeSpan.NanosecondsPerTick);

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Ticks * TimeSpan.NanosecondsPerTick);
}

/// <summary>
/// Runs the checks plugins declare: shell commands that exit 0 when what
/// the plugin is responsible for works.
/// </summary>
public static class Checks
{
    /// <summary>
    /// How long to wait for the output after the check ended (or timed out):
    /// Go's cmd.WaitDelay. Don't wait on children the check left behind (a
    /// shell it restarted).
    /// </summary>
    static readonly TimeSpan WaitDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// InSession tells whether we run inside the graphical session: Hyprland's
    /// instance and a Wayland display are there.
    /// </summary>
    public static bool InSession() =>
        Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE") is { Length: > 0 }
        && Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 };

    /// <summary>
    /// RunOne runs a single check under its timeout. Callers run checks one
    /// after the other: some restart things other checks look at.
    /// </summary>
    public static Result RunOne(RenderedCheck c)
    {
        if (c.Session && !InSession())
            return new Result { Plugin = c.Plugin, Name = c.Name, OK = true, Skipped = true };
        var timeout = TimeSpan.FromSeconds(c.Timeout);
        var clock = Stopwatch.StartNew(); // the context's deadline counts from here
        bool TimedOut() => clock.Elapsed >= timeout;
        var start = clock.Elapsed;
        var (ok, output) = Run(c.Run, timeout, clock);
        var r = new Result { Plugin = c.Plugin, Name = c.Name, OK = ok, Took = clock.Elapsed - start };
        if (!ok)
        {
            var msg = output.Trim();
            // As Go asks ctx.Err(): past the deadline by now, whatever ended the check.
            if (TimedOut()) msg = (msg + "\n(timed out after " + GoDuration(c.Timeout) + ")").Trim();
            r = r with { Output = LastLines(msg, 6) };
        }
        return r;
    }

    /// <summary>
    /// Run is Go's exec.CommandContext(ctx, "sh", "-c", run) with stdout and
    /// stderr in one buffer and WaitDelay: at the deadline the shell is
    /// killed (SIGKILL, not its children); once it has ended (or the deadline
    /// came first) the output is waited for WaitDelay at most, then the pipe
    /// is closed. A check that exited 0 passes even when a child it left
    /// running (a daemon it restarted) holds the output open.
    /// </summary>
    static (bool ok, string output) Run(string run, TimeSpan timeout, Stopwatch clock)
    {
        if (timeout <= TimeSpan.Zero) return (false, ""); // the context is done before it starts
        if (Exec.LookPath("sh") is not { } sh) return (false, "");
        // Go hands the command one pipe for both stdout and stderr, so what it
        // prints keeps its order, and /dev/null as stdin. .NET only gives two
        // pipes: this shell makes stderr the same pipe, points stdin at
        // /dev/null and replaces itself (exec, same process) with the very
        // `sh -c <run>` Go runs, $0 "sh" included.
        var psi = new ProcessStartInfo(sh)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        foreach (var a in new[] { "-c", "exec sh -c \"$1\" </dev/null 2>&1", "sh", run }) psi.ArgumentList.Add(a);
        Process p;
        try
        {
            p = Process.Start(psi)!;
        }
        catch (Win32Exception)
        {
            return (false, "");
        }
        using (p)
        {
            var buf = new MemoryStream();
            using var closePipe = new CancellationTokenSource();
            var copy = Copy(p.StandardOutput.BaseStream, buf, closePipe.Token);

            // WaitForExit can come back a little early: the deadline is the clock's.
            bool exited;
            while (true)
            {
                var remaining = timeout - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    exited = p.HasExited;
                    break;
                }
                if (p.WaitForExit(remaining))
                {
                    exited = true;
                    break;
                }
            }
            if (!exited)
            {
                try
                {
                    p.Kill(); // the shell only, as Go's Cancel
                }
                catch (InvalidOperationException) { } // it had just ended
                p.WaitForExit();
            }
            // WaitDelay's timer starts at whichever came first: the end or the deadline.
            var ioDeadline = (exited ? clock.Elapsed : timeout) + WaitDelay;
            var wait = ioDeadline - clock.Elapsed;
            if (!copy.Wait(wait > TimeSpan.Zero ? wait : TimeSpan.Zero))
            {
                closePipe.Cancel();
                try
                {
                    copy.Wait();
                }
                catch (AggregateException) { }
            }
            // Killed at the deadline is a failure even when it raced a clean exit
            // (Go reports the context's error then).
            var ok = exited && p.ExitCode == 0;
            return (ok, Files.Utf8.GetString(buf.GetBuffer(), 0, (int)buf.Length));
        }
    }

    static async Task Copy(Stream from, MemoryStream to, CancellationToken ct)
    {
        var b = new byte[8192];
        try
        {
            int n;
            while ((n = await from.ReadAsync(b, ct).ConfigureAwait(false)) > 0) to.Write(b, 0, n);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// GoDuration writes a whole number of seconds as time.Duration's String:
    /// "5s", "1m30s", "1h0m0s".
    /// </summary>
    internal static string GoDuration(long seconds)
    {
        if (seconds == 0) return "0s";
        var neg = seconds < 0;
        var s = Math.Abs(seconds);
        string text;
        if (s < 60) text = $"{s}s";
        else if (s < 3600) text = $"{s / 60}m{s % 60}s";
        else text = $"{s / 3600}h{s % 3600 / 60}m{s % 60}s";
        return neg ? "-" + text : text;
    }

    static string LastLines(string s, int n)
    {
        var lines = s.Split('\n');
        if (lines.Length > n) lines = lines[^n..];
        return string.Join('\n', lines);
    }

    /// <summary>Failed returns the results that didn't pass.</summary>
    public static List<Result> Failed(IEnumerable<Result> results) => results.Where(r => !r.OK).ToList();
}
