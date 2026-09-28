using System.ComponentModel;
using System.Diagnostics;
using MyArch.Util;

namespace MyArch.Pacman;

/// <summary>
/// Running programs the way Go's os/exec did: found on $PATH only (never in
/// the current directory or next to myarch, where .NET would look first),
/// stdin from /dev/null, and errors worded as Go wrote them.
/// </summary>
internal static class Exec
{
    /// <summary>What a finished program left: its output, and Go's error text (null when it exited 0).</summary>
    internal sealed record Result(string Stdout, string Stderr, int ExitCode, string? Error, bool NotFound);

    /// <summary>
    /// Run is exec.Command(file, args...).Output(): stdout captured, stderr
    /// captured apart (Go keeps it for the error, never shows it). env adds
    /// variables to the inherited environment ("LC_ALL=C").
    /// </summary>
    internal static Result Run(string file, IEnumerable<string> args, params (string Key, string Value)[] env)
    {
        var path = LookPath(file);
        if (path == null)
            return new Result("", "", -1, $"exec: {GoFormat.Quote(file)}: executable file not found in $PATH", true);
        var psi = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Files.Utf8,
            StandardErrorEncoding = Files.Utf8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in env) psi.Environment[k] = v;
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Close(); // Go gives the program /dev/null: nothing to read
            var err = p.StandardError.ReadToEndAsync();
            var out_ = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var code = p.ExitCode;
            return new Result(out_, err.GetAwaiter().GetResult(), code, code == 0 ? null : ExitText(code), false);
        }
        catch (Win32Exception e)
        {
            return new Result("", "", -1, StartError(path, e), false);
        }
    }

    /// <summary>
    /// ExitText is how Go words a program that failed: "exit status 1". A
    /// program killed by a signal reads "signal: killed" in Go; .NET only
    /// says 128 + the signal, which reads as "exit status 137" here.
    /// </summary>
    internal static string ExitText(int code) => $"exit status {code}";

    /// <summary>A program that couldn't be started, worded like Go's fork/exec error.</summary>
    internal static string StartError(string path, Win32Exception e) => e.NativeErrorCode switch
    {
        2 => $"exec: {GoFormat.Quote(path)}: executable file not found in $PATH",
        13 => $"fork/exec {path}: permission denied",
        _ => $"fork/exec {path}: {e.Message}",
    };

    /// <summary>
    /// LookPath is Go's exec.LookPath: a name with a slash is used as it is
    /// when it's an executable file; a bare name is searched in $PATH, where
    /// an empty entry means "." and a match found relative to it is refused
    /// (Go's ErrDot). Null when not found.
    /// </summary>
    internal static string? LookPath(string file)
    {
        if (file.Contains('/')) return IsExecutable(file) ? file : null;
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
        {
            var p = Paths.Join(entry == "" ? "." : entry, file);
            if (IsExecutable(p)) return p.StartsWith('/') ? p : null;
        }
        return null;
    }

    /// <summary>
    /// A file (not a folder, symlinks followed) with an execute bit. Go asks
    /// the kernel (access X_OK), which also tells whose execute bit it is;
    /// the bits alone differ only for files executable by others but not us.
    /// </summary>
    static bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            if (OperatingSystem.IsWindows()) return true;
            const UnixFileMode x = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(path) & x) != 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>bufio.Scanner's lines: split at \n, a trailing \r dropped, no empty last line.</summary>
    internal static List<string> Lines(string s)
    {
        var lines = s.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1] == "") lines.RemoveAt(lines.Count - 1);
        return lines.Select(l => l.EndsWith('\r') ? l[..^1] : l).ToList();
    }

    /// <summary>strings.Fields: the words between runs of white space.</summary>
    internal static string[] Fields(string s) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}
