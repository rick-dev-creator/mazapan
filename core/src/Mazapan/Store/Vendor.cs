using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Store;

/// <summary>A maker's latest release: its version, where it is, its checksum, how big.</summary>
public sealed record VendorRelease(string Version, string Url, string Sha256, long Size);

/// <summary>
/// Apps from their makers, for what neither Arch's repositories nor Flathub
/// can give as it should be (Rider: its Flatpak can't see the .NET SDK; VS
/// Code: Microsoft's own build, which the C# Dev Kit needs). The maker's
/// latest release, over https, checked against the SHA-256 the maker
/// publishes, unpacked in the person's home (~/.local/share/mazapan/vendor/ID,
/// a version beside the other, "current" pointing at one), with its
/// launcher. Updated by `mazapan update`; removed whole.
///
/// Claude Code is one binary, not a tarball, and installs itself: checked
/// the same way, then its own `claude install` puts it in
/// ~/.local/share/claude and ~/.local/bin/claude, and it keeps itself up to
/// date (mazapan update leaves it be). One installed by hand counts as there.
///
/// From GitHub ("github:OWNER/REPO"): the latest release's asset for this
/// machine, checked against the SHA-256 GitHub keeps for it (its "digest"):
/// a tarball, a single program (Herdr), or an AppImage, unpacked here so
/// it needs no FUSE (T3 Code).
/// </summary>
public static class Vendor
{
    /// <summary>
    /// A maker this knows: how to find its latest release, and what's in it.
    /// Layout: "tar" (a tarball, one folder), "self" (a single binary that
    /// installs and updates itself, no launcher of mazapan's), "binary" (a
    /// single program, run in a terminal), "appimage" (unpacked). Asset: on
    /// GitHub, the release file for x86_64 and for arm64 ({v}: the version).
    /// </summary>
    public sealed record Maker(string Spec, string Executable, string Icon, string WmClass, string Command,
        string Layout = "tar", string AssetX64 = "", string AssetArm64 = "")
    {
        public bool SelfInstalls => Layout == "self";
        /// <summary>A window of its own: a launcher in the menu and the palette.</summary>
        public bool HasLauncher => Layout is "tar" or "appimage";
    }

    public static readonly Maker[] Makers =
    [
        new("jetbrains:RD", "bin/rider", "bin/rider.svg", "jetbrains-rider", "rider"),
        new("vscode:stable", "code", "resources/app/resources/linux/code.png", "Code", "code"),
        new("anthropic:claude-code", "claude", "", "", "claude", Layout: "self"),
        new("github:pingdotgg/t3code", "AppRun", "t3code.png", "com.t3tools.T3Code", "t3code", Layout: "appimage",
            AssetX64: "T3-Code-{v}-x86_64.AppImage", AssetArm64: "T3-Code-{v}-arm64.AppImage"),
        new("github:herdrdev/herdr", "herdr", "", "", "herdr", Layout: "binary",
            AssetX64: "herdr-linux-x86_64", AssetArm64: "herdr-linux-aarch64"),
    ];

    const string ClaudeReleases = "https://downloads.claude.ai/claude-code-releases";

    public static bool IsSpec(string s) => Makers.Any(m => m.Spec == s);

    static string Home => Environment.GetEnvironmentVariable("HOME") ?? "";
    public static string Root(string id) => Paths.Join(Home, ".local/share/mazapan/vendor", id);
    static string DesktopFile(string id) => Paths.Join(Home, ".local/share/applications", $"mazapan-vendor-{id}.desktop");
    public static string DesktopName(string id) => $"mazapan-vendor-{id}.desktop";

    /// <summary>The version installed, or "" (none).</summary>
    public static string Installed(string id, string spec = "")
    {
        if (spec == "anthropic:claude-code") return ClaudeInstalled(Home);
        var f = Path.Join(Root(id), "version");
        return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
    }

    /// <summary>
    /// Claude Code where its own installer puts it, by mazapan or by hand:
    /// the newest of ~/.local/share/claude/versions, or "installed" when only
    /// its command is there.
    /// </summary>
    public static string ClaudeInstalled(string home)
    {
        var dir = Path.Join(home, ".local/share/claude/versions");
        if (Directory.Exists(dir))
        {
            var newest = Directory.EnumerateFileSystemEntries(dir).Select(Path.GetFileName)
                .Select(n => (Name: n ?? "", V: System.Version.TryParse((n ?? "").Split('-')[0], out var v) ? v : null))
                .Where(x => x.V != null).OrderByDescending(x => x.V).Select(x => x.Name).FirstOrDefault();
            if (newest != null) return newest;
        }
        return File.Exists(Path.Join(home, ".local/bin/claude")) ? "installed" : "";
    }

    static HttpClient Http(int seconds = 30)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(seconds) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("mazapan");
        return http;
    }

    /// <summary>The maker's latest release, from its own API.</summary>
    public static VendorRelease Latest(string spec)
    {
        using var http = Http();
        if (spec.StartsWith("jetbrains:", StringComparison.Ordinal))
        {
            var code = spec["jetbrains:".Length..];
            var json = http.GetStringAsync($"https://data.services.jetbrains.com/products/releases?code={code}&latest=true&type=release").GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var rel = doc.RootElement.GetProperty(code)[0];
            var dl = rel.GetProperty("downloads").GetProperty(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "linuxARM64" : "linux");
            var link = dl.GetProperty("link").GetString() ?? "";
            var sum = http.GetStringAsync(dl.GetProperty("checksumLink").GetString() ?? "").GetAwaiter().GetResult();
            return Checked(new VendorRelease(rel.GetProperty("version").GetString() ?? "", link, sum.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0],
                dl.TryGetProperty("size", out var size) ? size.GetInt64() : 0), "https://download.jetbrains.com/");
        }
        if (spec == "vscode:stable")
        {
            var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            var json = http.GetStringAsync($"https://update.code.visualstudio.com/api/update/{arch}/stable/latest").GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var rel = Checked(new VendorRelease(r.GetProperty("productVersion").GetString() ?? "", r.GetProperty("url").GetString() ?? "",
                r.GetProperty("sha256hash").GetString() ?? "", 0), "https://");
            // Microsoft's own download servers only.
            var host = new Uri(rel.Url).Host;
            if (!(host.EndsWith(".microsoft.com", StringComparison.Ordinal) || host.EndsWith(".visualstudio.com", StringComparison.Ordinal)))
                throw new MazapanException($"the maker's address isn't one to trust: {rel.Url}");
            return rel;
        }
        if (spec == "anthropic:claude-code")
        {
            var version = http.GetStringAsync($"{ClaudeReleases}/latest").GetAwaiter().GetResult().Trim();
            if (version.Length > 40) throw new MazapanException("the maker's version doesn't read");
            var manifest = http.GetStringAsync($"{ClaudeReleases}/{version}/manifest.json").GetAwaiter().GetResult();
            return ClaudeRelease(version, manifest, ClaudePlatform());
        }
        if (spec.StartsWith("github:", StringComparison.Ordinal))
        {
            var repo = spec["github:".Length..];
            var json = http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest").GetAwaiter().GetResult();
            return GitHubRelease(MakerOf(spec), json, System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64);
        }
        throw new MazapanException($"no maker \"{spec}\"");
    }

    /// <summary>
    /// A GitHub release (the API's JSON for it): the asset for this machine,
    /// its SHA-256 as GitHub keeps it. Only from that repository's releases.
    /// </summary>
    public static VendorRelease GitHubRelease(Maker maker, string json, bool arm64)
    {
        var repo = maker.Spec["github:".Length..];
        using var doc = JsonDocument.Parse(json);
        var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        var version = tag.StartsWith('v') ? tag[1..] : tag;
        var want = (arm64 ? maker.AssetArm64 : maker.AssetX64).Replace("{v}", version);
        if (want == "") throw new MazapanException($"{repo} has no build for this machine");
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (a.GetProperty("name").GetString() != want) continue;
            var digest = a.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
            if (!digest.StartsWith("sha256:", StringComparison.Ordinal)) throw new MazapanException($"{repo}: GitHub keeps no checksum for {want}");
            return Checked(new VendorRelease(version, a.GetProperty("browser_download_url").GetString() ?? "", digest["sha256:".Length..],
                a.TryGetProperty("size", out var size) ? size.GetInt64() : 0), $"https://github.com/{repo}/releases/download/");
        }
        throw new MazapanException($"{repo}: its latest release has no {want}");
    }

    /// <summary>linux-x64 or linux-arm64 (-musl where the C library is musl), as Claude's manifest names them.</summary>
    static string ClaudePlatform()
    {
        var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
        var musl = Directory.Exists("/lib") && Directory.EnumerateFiles("/lib", "ld-musl-*").Any();
        return $"linux-{arch}" + (musl ? "-musl" : "");
    }

    /// <summary>Claude Code's release for a platform, from its manifest.json: only from downloads.claude.ai.</summary>
    public static VendorRelease ClaudeRelease(string version, string manifest, string platform)
    {
        using var doc = JsonDocument.Parse(manifest);
        if (!doc.RootElement.TryGetProperty("platforms", out var platforms) || !platforms.TryGetProperty(platform, out var p))
            throw new MazapanException($"Claude Code has no build for {platform}");
        if (doc.RootElement.TryGetProperty("version", out var mv) && mv.GetString() is { } said && said != version)
            throw new MazapanException($"the maker's manifest is for another version ({said})");
        return Checked(new VendorRelease(version, $"{ClaudeReleases}/{version}/{platform}/claude",
            p.GetProperty("checksum").GetString() ?? "", p.TryGetProperty("size", out var size) ? size.GetInt64() : 0), ClaudeReleases + "/");
    }

    /// <summary>What a maker's API says, checked before anything is done with it: https, a hash, a plain version.</summary>
    static VendorRelease Checked(VendorRelease r, string prefix)
    {
        if (!r.Url.StartsWith(prefix, StringComparison.Ordinal) || !r.Url.StartsWith("https://", StringComparison.Ordinal))
            throw new MazapanException($"the maker's address isn't one to trust: {r.Url}");
        if (r.Sha256.Length != 64 || !r.Sha256.All(Uri.IsHexDigit)) throw new MazapanException("the maker's checksum doesn't read");
        if (r.Version == "" || !char.IsAsciiLetterOrDigit(r.Version[0]) || r.Version is "current" or "version"
            || !r.Version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
            throw new MazapanException($"the maker's version doesn't read: {r.Version}");
        return r;
    }

    /// <summary>
    /// The release, installed: downloaded beside its folder, its checksum
    /// checked, unpacked, made current (the one before goes once the new one is
    /// in place), its launcher written. Says its progress as lines ("progress
    /// DONE TOTAL") for the panel.
    /// </summary>
    public static void Install(string id, Maker maker, VendorRelease r, string name)
    {
        if (maker.SelfInstalls)
        {
            SelfInstall(id, r, name);
            return;
        }
        var root = Root(id);
        Directory.CreateDirectory(root);
        var tarball = Path.Join(root, ".download.tar.gz");
        try
        {
            Download(r, tarball, name);
            // Unpacked apart, then put in place: a half-unpacked one is never current.
            var unpack = Path.Join(root, ".unpack");
            if (Directory.Exists(unpack)) Directory.Delete(unpack, true);
            Directory.CreateDirectory(unpack);
            Unpack(maker, tarball, unpack, name);
            var top = Directory.GetDirectories(unpack);
            if (top.Length != 1 || !File.Exists(Path.Join(top[0], maker.Executable)))
                throw new MazapanException($"{name}: the download isn't laid out as expected ({maker.Executable})");
            var target = Path.Join(root, r.Version);
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(top[0], target);
            Directory.Delete(unpack, true);
            // The link made beside, then renamed over the old one (rename(2):
            // never without a current). .NET's own Move follows a link to a
            // folder, hence libc.
            var current = Path.Join(root, "current");
            var tmpLink = Path.Join(root, ".current");
            // The one before is kept (it may be open now: its files are read as it runs).
            var before = new FileInfo(current).LinkTarget is { } lt ? Path.GetFileName(lt.TrimEnd('/')) : "";
            Unlink(tmpLink);
            File.CreateSymbolicLink(tmpLink, r.Version);
            if (Rename(tmpLink, current) != 0) throw new MazapanException($"{name}: couldn't make the new version current");
            Files.WriteAtomic(Path.Join(root, "version"), r.Version + "\n");
            // The versions older than that go ("current" is a link to one: left).
            foreach (var old in Directory.GetDirectories(root).Where(d => Path.GetFileName(d) is var n && !n.StartsWith('.') && n != r.Version && n != before && n != "current"
                && new DirectoryInfo(d).LinkTarget == null))
                Directory.Delete(old, true);
        }
        finally
        {
            File.Delete(tarball);
        }
        Launcher(id, maker, name);
    }

    /// <summary>
    /// What was downloaded, into one folder in `unpack`: a tarball's own; a
    /// single program put in one; an AppImage's insides (its own
    /// --appimage-extract, so no FUSE is needed to run it).
    /// </summary>
    static void Unpack(Maker maker, string file, string unpack, string name)
    {
        switch (maker.Layout)
        {
            case "binary":
            {
                var dir = Path.Join(unpack, "app");
                Directory.CreateDirectory(dir);
                var exe = Path.Join(dir, maker.Executable);
                File.Move(file, exe);
                File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                return;
            }
            case "appimage":
            {
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var x = Process.Start(new ProcessStartInfo(file, ["--appimage-extract"])
                    { UseShellExecute = false, WorkingDirectory = unpack, RedirectStandardOutput = true, RedirectStandardError = true })!;
                x.StandardOutput.ReadToEnd();
                x.StandardError.ReadToEnd();
                x.WaitForExit();
                if (x.ExitCode != 0) throw new MazapanException($"{name}: its AppImage couldn't be unpacked");
                return;
            }
            default:
            {
                var tar = Process.Start(new ProcessStartInfo("tar", ["-xzf", file, "-C", unpack, "--no-same-owner"]) { UseShellExecute = false })!;
                tar.WaitForExit();
                if (tar.ExitCode != 0) throw new MazapanException($"{name}: tar couldn't unpack it");
                return;
            }
        }
    }

    /// <summary>The release, to a file, its checksum checked as it comes. Says its progress ("progress DONE TOTAL").</summary>
    static void Download(VendorRelease r, string to, string name)
    {
        using var http = Http(3600);
        using var res = http.GetAsync(r.Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        res.EnsureSuccessStatusCode();
        var total = res.Content.Headers.ContentLength ?? r.Size;
        using var src = res.Content.ReadAsStream();
        using var dst = File.Create(to);
        using var sha = SHA256.Create();
        var buf = new byte[1 << 20];
        long done = 0, said = 0;
        // Each app from nothing (the panel shows the last of these).
        Console.WriteLine($"progress 0 {total}");
        int n;
        // A connection that stalls: given up after a minute without a byte.
        while ((n = ReadSome(src, buf)) > 0)
        {
            dst.Write(buf, 0, n);
            sha.TransformBlock(buf, 0, n, null, 0);
            done += n;
            if (done - said > 32 << 20) { Console.WriteLine($"progress {done} {total}"); said = done; }
        }
        sha.TransformFinalBlock([], 0, 0);
        Console.WriteLine($"progress {done} {Math.Max(done, total)}");
        if (!Convert.ToHexStringLower(sha.Hash!).Equals(r.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new MazapanException($"{name}: the download isn't what its maker published (checksum): not installed");
    }

    /// <summary>
    /// A binary that installs itself (Claude Code): downloaded and checked,
    /// then its own install run, as its maker's install script does. It
    /// leaves nothing of mazapan's but this, gone once it's done.
    /// </summary>
    static void SelfInstall(string id, VendorRelease r, string name)
    {
        var root = Root(id);
        Directory.CreateDirectory(root);
        var bin = Path.Join(root, ".download");
        try
        {
            Download(r, bin, name);
            File.SetUnixFileMode(bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var psi = new ProcessStartInfo(bin, ["install"]) { UseShellExecute = false, RedirectStandardInput = true };
            var p = Process.Start(psi)!;
            p.StandardInput.Close();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new MazapanException($"{name}: its installer stopped ({p.ExitCode})");
        }
        finally
        {
            File.Delete(bin);
            try { Directory.Delete(root); } catch (IOException) { }
        }
    }

    static int ReadSome(Stream src, byte[] buf)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try { return src.ReadAsync(buf, cts.Token).AsTask().GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { throw new MazapanException("the download stalled: try again"); }
    }

    /// <summary>Its launcher (the menu, the palette) and its command in ~/.local/bin (when nothing else has the name).</summary>
    static void Launcher(string id, Maker maker, string name)
    {
        var current = Path.Join(Root(id), "current");
        var desktop = DesktopFile(id);
        // A program for the terminal: its command only (the palette opens it in one).
        if (maker.HasLauncher)
        {
            Directory.CreateDirectory(Paths.Dir(desktop));
            Files.WriteAtomic(desktop, string.Join("\n",
                "[Desktop Entry]",
                "Type=Application",
                $"Name={new string([.. name.Where(c => !char.IsControl(c))])}",
                $"Exec=\"{Path.Join(current, maker.Executable)}\" %F",
                $"Icon={Path.Join(current, maker.Icon)}",
                $"StartupWMClass={maker.WmClass}",
                "Categories=Development;IDE;",
                "Terminal=false",
                $"X-Mazapan-Vendor={id}",
                "") );
        }
        var bin = Paths.Join(Home, ".local/bin", maker.Command);
        var mine = File.Exists(bin) && new FileInfo(bin).LinkTarget is { } t && t.StartsWith(Root(id), StringComparison.Ordinal);
        var taken = Discovery(maker.Command);
        if (mine || (!File.Exists(bin) && !taken))
        {
            Directory.CreateDirectory(Paths.Dir(bin));
            if (File.Exists(bin)) File.Delete(bin);
            File.CreateSymbolicLink(bin, Path.Join(current, maker.Command == "code" ? "bin/code" : maker.Executable));
        }
    }

    /// <summary>Whether a command of that name is already there (Code - OSS's code, for one).</summary>
    static bool Discovery(string command) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin").Split(':').Any(d => File.Exists(Path.Join(d, command)) && !d.EndsWith("/.local/bin", StringComparison.Ordinal));

    /// <summary>Taken out whole: its folder, its launcher, its command.</summary>
    public static void Remove(string id, Maker maker)
    {
        if (maker.SelfInstalls)
        {
            // Its program and its versions; never ~/.claude (the person's settings and history).
            var cmd = Paths.Join(Home, ".local/bin", maker.Command);
            if (File.Exists(cmd) || new FileInfo(cmd).LinkTarget != null) File.Delete(cmd);
            var own = Paths.Join(Home, ".local/share/claude");
            if (Directory.Exists(own)) Directory.Delete(own, true);
            return;
        }
        var root = Root(id);
        var bin = Paths.Join(Home, ".local/bin", maker.Command);
        if (new FileInfo(bin).LinkTarget is { } t && t.StartsWith(root, StringComparison.Ordinal)) File.Delete(bin);
        File.Delete(DesktopFile(id));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    static extern int Rename([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string from,
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string to);

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "unlink", SetLastError = true)]
    static extern int Unlink([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path);

    public static Maker MakerOf(string spec) => Makers.FirstOrDefault(m => m.Spec == spec) ?? throw new MazapanException($"no maker \"{spec}\"");

    /// <summary>"2.4 GB".</summary>
    public static string Size(long b) => b <= 0 ? "" : b >= 1L << 30 ? (b / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : (b >> 20) + " MB";
}
