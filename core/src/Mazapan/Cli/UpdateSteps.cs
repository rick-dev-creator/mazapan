using System.Diagnostics;
using Mazapan.Pacman;
using Mazapan.Util;

namespace Mazapan.Cli;

public static partial class Program
{
    /// <summary>
    /// Steps: an update in a few lines, each with its ✓ (pacman's own output
    /// in between). From the panel (--gui) each also goes out as a line the
    /// panel reads: "::step ID run|ok|fail|skip DETAIL".
    /// </summary>
    sealed class Steps(bool gui)
    {
        // The panel's run id (MAZAPAN_STEP_TAG): only lines with it are ours,
        // not a check's or pacman's output that happens to look the same.
        readonly string tag = Environment.GetEnvironmentVariable("MAZAPAN_STEP_TAG") is { Length: > 0 } t
            && t.All(char.IsAsciiLetterOrDigit) ? t + ":" : "";

        public void Start(string id, string title)
        {
            Console.WriteLine($"\n{Style.Bold}› {title}{Style.Reset}");
            if (gui) Console.WriteLine($"::{tag}step {id} run");
        }

        public void Ok(string id, string detail) => End(id, "ok", Style.Green + "✓" + Style.Reset, detail);

        public void Fail(string id, string detail) => End(id, "fail", Style.Red + "✗" + Style.Reset, detail);

        public void Skip(string id, string detail) => End(id, "skip", Style.Dim + "–" + Style.Reset, detail);

        void End(string id, string state, string mark, string detail)
        {
            Console.WriteLine($"  {mark} {detail}");
            if (gui) Console.WriteLine($"::{tag}step {id} {state} {detail.ReplaceLineEndings(" ")}");
        }

        /// <summary>What needs a restart, for the panel's button.</summary>
        public void Reboot(string why)
        {
            if (gui) Console.WriteLine($"::{tag}reboot {why}");
        }
    }

    const long MinFree = 2L << 30;
    const int MinBattery = 20;

    /// <summary>
    /// Ready: what has to hold before anything changes (Omarchy's checks):
    /// room for the packages, and power. An error says what to do; else what
    /// was found, in a few words.
    /// </summary>
    static (string? Error, string Detail) Ready()
    {
        var parts = new List<string>();
        foreach (var dir in new[] { "/", "/var/cache/pacman/pkg" }.Distinct())
        {
            if (FreeBytes(dir) is not { } free) continue;
            if (free < MinFree)
                return ($"only {Gib(free)} free on {dir}: an update needs {Gib(MinFree)} (sudo paccache -rk1 frees old packages)", "");
            if (dir == "/") parts.Add($"{Gib(free)} free");
        }
        var (onBattery, percent) = Battery();
        if (onBattery && percent < MinBattery)
            return ($"the battery is at {percent} %: plug it in first (an update stopped half way can leave the system unable to start)", "");
        parts.Add(onBattery ? $"on battery, {percent} %" : "on power");
        return (null, string.Join(", ", parts));
    }

    static string Gib(long bytes) => $"{bytes / (double)(1L << 30):0.#} GB";

    static long? FreeBytes(string path)
    {
        var r = Exec.Run("df", ["-B1", "--output=avail", path]);
        return r.ExitCode == 0 && Exec.Lines(r.Stdout).LastOrDefault() is { } l && long.TryParse(l.Trim(), out var n) ? n : null;
    }

    /// <summary>Running on a battery (one discharging), and its charge.</summary>
    static (bool OnBattery, int Percent) Battery()
    {
        const string sys = "/sys/class/power_supply";
        if (!Directory.Exists(sys)) return (false, 100);
        foreach (var d in Directory.GetDirectories(sys))
        {
            string Read(string f)
            {
                try { return File.ReadAllText(Path.Join(d, f)).Trim(); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
            }
            if (Read("type") != "Battery" || Read("scope") == "Device") continue;
            if (Read("status") == "Discharging" && int.TryParse(Read("capacity"), out var c)) return (true, c);
        }
        return (false, 100);
    }

    /// <summary>
    /// KeepAwake holds a systemd inhibitor while the update runs: no sleep,
    /// no idle lock, the lid ignored (an update stopped half way is the one
    /// that breaks a system). Nothing when systemd-inhibit isn't there. It
    /// holds it with `cat` on a pipe from here: however mazapan ends (killed
    /// too), the pipe closes and the inhibitor goes with it.
    /// </summary>
    sealed class KeepAwake : IDisposable
    {
        readonly Process? p;

        public KeepAwake()
        {
            if (Exec.LookPath("systemd-inhibit") is not { } path) return;
            var psi = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in new[] { "--what=sleep:idle:handle-lid-switch", "--who=Mazapán", "--why=Updating the system", "--mode=block", "cat" })
                psi.ArgumentList.Add(a);
            try
            {
                p = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception) { }
        }

        public bool Held => p is { HasExited: false };

        public void Dispose()
        {
            try
            {
                if (p is { HasExited: false })
                {
                    p.StandardInput.Close();
                    if (!p.WaitForExit(2000)) p.Kill(true);
                }
            }
            catch (InvalidOperationException) { }
            p?.Dispose();
        }
    }

    /// <summary>
    /// What needs a restart after an update: a new kernel (the running one's
    /// modules are gone), or Hyprland replaced under the running session.
    /// </summary>
    static List<string> RebootReasons(IEnumerable<Change> changes)
    {
        var out_ = new List<string>();
        if (changes.Any(c => IsKernel(c.Name) && c.To != "")) out_.Add("kernel");
        if (changes.Any(c => c.Name == "hyprland") && HyprlandReplaced()) out_.Add("hyprland");
        return out_;
    }

    static bool HyprlandReplaced() => Replaced(exe => exe.StartsWith("/usr/bin/Hyprland", StringComparison.Ordinal));

    /// <summary>
    /// A running program whose binary an update replaced (its exe now
    /// "… (deleted)"), picked by that path; only the processes this account
    /// can see.
    /// </summary>
    static bool Replaced(Func<string, bool> which)
    {
        try
        {
            foreach (var d in Directory.GetDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(d), out _)) continue;
                try
                {
                    if (new FileInfo(Path.Join(d, "exe")).LinkTarget is { } t && t.EndsWith(" (deleted)", StringComparison.Ordinal) && which(t)) return true;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return false;
    }

    /// <summary>
    /// Hyprland knows the desktop's own screen readers by their binary's path
    /// (privacy's screen permission): one left running on a replaced binary
    /// has none, and its next look at the screen would ask as "an unknown
    /// app". The screen-sharing portal, replaced, starts again on the new one
    /// (the bar's own check restarts the bar).
    /// </summary>
    static void RestartReplaced(IEnumerable<Change> changes)
    {
        if (Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE") is null or "") return;
        if (changes.Any(c => c.Name == "xdg-desktop-portal-hyprland") && Replaced(exe => exe.StartsWith("/usr/lib/xdg-desktop-portal-hyprland", StringComparison.Ordinal)))
            Exec.Run("systemctl", ["--user", "try-restart", "xdg-desktop-portal-hyprland.service"]);
    }

    static readonly HttpClient ChangelogHttp = new() { Timeout = TimeSpan.FromSeconds(8) };

    /// <summary>
    /// What's new in Mazapán, when the update brings it: the changelog of the
    /// channel this system follows, from the version installed on. Empty
    /// (never an error) when it can't be read: it's only words.
    /// </summary>
    static List<Repository.Release> WhatsNew(IEnumerable<Change> pending)
    {
        if (pending.FirstOrDefault(p => p.Name == "mazapan") is not { } m || Repository.ChangelogUrl() is not { } url) return [];
        try
        {
            static string V(string v) => v.Split(':')[^1].Split('-')[0]; // no epoch, no release
            return Repository.WhatsNew(Repository.FetchChangelog(url, ChangelogHttp), V(m.From), V(m.To));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or UriFormatException or MazapanException)
        {
            return [];
        }
    }

    /// <summary>A device's firmware with a newer version (fwupd).</summary>
    internal sealed record Firmware(string Device, string From, string To, string Summary);

    /// <summary>
    /// The firmware with updates, as fwupd knows them (fwupdmgr get-updates):
    /// none without fwupd, or with nothing to update (it exits non-zero then).
    /// Updated with `fwupdmgr update`, which may finish at the next start.
    /// </summary>
    internal static List<Firmware> PendingFirmware()
    {
        if (Exec.LookPath("fwupdmgr") == null) return [];
        var r = Exec.Run("fwupdmgr", ["get-updates", "--json"]);
        if (r.ExitCode != 0 || r.Stdout.Trim() == "") return [];
        return ParseFirmware(r.Stdout);
    }

    internal static List<Firmware> ParseFirmware(string json)
    {
        var out_ = new List<Firmware>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Devices", out var devices)) return out_;
            foreach (var d in devices.EnumerateArray())
            {
                string S(System.Text.Json.JsonElement e, string k) =>
                    e.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString()! : "";
                if (!d.TryGetProperty("Releases", out var rel) || rel.GetArrayLength() == 0) continue;
                var newest = rel[0];
                out_.Add(new Firmware(S(d, "Name"), S(d, "Version"), S(newest, "Version"), S(newest, "Summary")));
            }
        }
        catch (System.Text.Json.JsonException) { }
        return out_;
    }

    /// <summary>A plugin from git with a newer commit where it follows.</summary>
    internal sealed record PluginUpdate(string Id, string From, string To);

    /// <summary>
    /// The plugins from git whose branch or tag has moved (git ls-remote: the
    /// checkout isn't touched). Said only: `mazapan plugins update` brings
    /// them, asking again for anything new they'd be able to do.
    /// </summary>
    internal static List<PluginUpdate> PendingPluginUpdates()
    {
        var out_ = new List<PluginUpdate>();
        Install.PluginsLock lk;
        try { lk = Install.PluginsLock.Load(); }
        catch (MazapanException) { return out_; }
        foreach (var e in lk.Plugins)
        {
            var dir = Paths.Join(Install.Git.Dir, e.Id);
            if (!Directory.Exists(dir)) continue;
            // Exactly the ref followed: ls-remote matches a pattern's tail, so
            // "main" alone would also find refs/heads/release/main.
            string[] wanted = e.Ref == "" ? ["HEAD"] : [$"refs/tags/{e.Ref}^{{}}", $"refs/tags/{e.Ref}", $"refs/heads/{e.Ref}"];
            var (text, err) = e.Ref == ""
                ? Install.Git.TryRun(dir, "ls-remote", "origin", "HEAD")
                : Install.Git.TryRun(dir, "ls-remote", "origin", $"refs/heads/{e.Ref}", $"refs/tags/{e.Ref}");
            if (err != null) continue;
            // A tag's own commit is its peeled line (^{}), when there is one.
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('\t')).Where(f => f.Length == 2).ToList();
            var head = wanted.Select(w => lines.FirstOrDefault(f => f[1] == w)).FirstOrDefault(f => f != null)?[0];
            if (head != null && head != e.Commit) out_.Add(new PluginUpdate(e.Id, Install.Git.Short(e.Commit), Install.Git.Short(head)));
        }
        return out_;
    }
}
