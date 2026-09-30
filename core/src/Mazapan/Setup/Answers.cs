using System.Text.Json;
using System.Text.RegularExpressions;
using Mazapan.Util;

namespace Mazapan.Setup;

/// <summary>
/// What the installer asked, as it hands it over (a JSON file): where, who,
/// and how the new system starts. Checked as if a stranger wrote it: every
/// value ends up in a command or a file of the new system.
/// </summary>
public sealed partial class Answers
{
    public string Disk = "";
    /// <summary>The whole disk encrypted, unlocked with the account's password.</summary>
    public bool Encrypt;
    public string User = "", FullName = "", Password = "", Hostname = "";
    public string Timezone = "UTC";
    /// <summary>The desktop's language (mazapan's), and the system's locale.</summary>
    public string Language = "en", Locale = "";
    public string KbLayout = "us", KbVariant = "";
    public string Theme = "";
    /// <summary>Where the time zone is (ISO 3166 code, "MX"): the mirrors nearby. Not asked: Locales.Country.</summary>
    public string Country = "";
    /// <summary>Apps from the catalog, installed on the first login.</summary>
    public List<string> Apps = [];
    /// <summary>
    /// SSH keys the account lets in (with sshd on): not asked, the live
    /// system's own root keys, which are only there when someone put them
    /// there to install remotely (cloud-init's cidata).
    /// </summary>
    public List<string> SshKeys = [];
    /// <summary>
    /// Hardware plugins for this machine (its GPU's drivers…), and the
    /// packages they need: turned on and installed with the system, from the
    /// ISO's repository. Not asked: the installer works them out.
    /// </summary>
    public List<string> Hardware = [], HardwarePackages = [];

    [GeneratedRegex(@"^(ssh-(ed25519|rsa)|ecdsa-sha2-nistp(256|384|521)|sk-(ssh-ed25519|ecdsa-sha2-nistp256)@openssh\.com) [A-Za-z0-9+/]+={0,3}( [^\n\r]{0,200})?\z")]
    private static partial Regex SshKeyRe();

    /// <summary>The keys in an authorized_keys file that are plain keys (no options, nothing else).</summary>
    public static List<string> Keys(string authorizedKeys) =>
        authorizedKeys.Split('\n').Select(l => l.Trim()).Where(l => SshKeyRe().IsMatch(l)).Distinct().ToList();

    [GeneratedRegex(@"^/dev/[A-Za-z0-9/_-]+\z")] private static partial Regex DiskRe();
    [GeneratedRegex(@"^[a-z_][a-z0-9_-]{0,31}\z")] private static partial Regex UserRe();
    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\z")] private static partial Regex HostRe();
    [GeneratedRegex(@"^[A-Za-z0-9_+-]+(/[A-Za-z0-9_+-]+)*\z")] private static partial Regex ZoneRe();
    [GeneratedRegex(@"^[a-z]{2,3}(_[A-Z]{2})?\z")] private static partial Regex LangRe();
    [GeneratedRegex(@"^[a-z]{2,3}_[A-Z]{2}\.UTF-8\z")] private static partial Regex LocaleRe();
    [GeneratedRegex(@"^[a-z]{2,12}\z")] private static partial Regex LayoutRe();
    [GeneratedRegex(@"^[a-z0-9_-]{0,32}\z")] private static partial Regex VariantRe();
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,63}\z")] private static partial Regex ThemeRe();

    /// <summary>Names the new system has already (the base package's), or that mean something else.</summary>
    static readonly HashSet<string> Taken =
    [
        "root", "bin", "daemon", "mail", "ftp", "http", "nobody", "dbus", "polkitd", "git", "live",
        "systemd-journal-remote", "systemd-network", "systemd-oom", "systemd-resolve", "systemd-timesync",
        "systemd-coredump", "uuidd", "avahi", "rtkit", "colord", "flatpak", "geoclue", "alpm", "tss", "admin",
        "greeter", "systemd-imds", "pcscd", "usbmux", "brltty",
    ];

    public static Answers Parse(string json)
    {
        try
        {
            return ParseChecked(json);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            throw new MazapanException("answers: not what the installer writes (" + e.Message + ")");
        }
    }

    static Answers ParseChecked(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.ValueKind != JsonValueKind.Object) throw new MazapanException("answers: not a JSON object");
        string S(string name, string dflt = "") =>
            r.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new MazapanException($"answers: {name} must be a string") : dflt;
        var a = new Answers
        {
            Disk = S("disk"),
            Encrypt = r.TryGetProperty("encrypt", out var e) && e.ValueKind == JsonValueKind.True,
            User = S("user"),
            FullName = S("fullname"),
            Password = S("password"),
            Hostname = S("hostname"),
            Timezone = S("timezone", "UTC"),
            Language = S("language", "en"),
            Locale = S("locale"),
            KbLayout = S("kb_layout", "us"),
            KbVariant = S("kb_variant"),
            Theme = S("theme"),
        };
        if (r.TryGetProperty("apps", out var apps))
        {
            if (apps.ValueKind != JsonValueKind.Array) throw new MazapanException("answers: apps must be a list");
            a.Apps = apps.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : throw new MazapanException("answers: apps must be ids")).ToList();
        }
        if (a.Hostname == "") a.Hostname = UserRe().IsMatch(a.User) ? $"{a.User.Replace('_', '-').Trim('-')}-pc" : "mazapan";
        a.Check();
        return a;
    }

    /// <summary>Every value, or what's wrong with the first that isn't right.</summary>
    public void Check()
    {
        void Need(bool ok, string what) { if (!ok) throw new MazapanException("answers: " + what); }
        Need(DiskRe().IsMatch(Disk) && !Disk.Contains(".."), $"disk {Quote(Disk)} isn't a device");
        Need(UserRe().IsMatch(User), $"user name {Quote(User)}: lowercase letters, digits, - and _, starting with a letter");
        Need(!Taken.Contains(User), $"user name {Quote(User)} is taken by the system");
        Need(FullName.Length <= 100 && !FullName.Any(c => c is ':' or ',' or '\n' or '\r' || char.IsControl(c)), "the full name can't have : , or line breaks");
        Need(Password.Length > 0, "a password is needed");
        Need(!Password.Contains('\n') && !Password.Contains('\0'), "the password can't have line breaks");
        // Encrypted, it's typed at boot before anything but the console's
        // keymap is there: letters with accents or dead keys may not come
        // out the same. What any keyboard types the same way, then.
        Need(!Encrypt || Password.All(c => c >= ' ' && c <= '~'), "with encryption, the password is typed when the computer starts: letters without accents, digits and symbols only");
        Need(HostRe().IsMatch(Hostname), $"computer name {Quote(Hostname)}: lowercase letters, digits and -");
        Need(ZoneRe().IsMatch(Timezone) && !Timezone.Contains(".."), $"time zone {Quote(Timezone)}");
        Need(LangRe().IsMatch(Language), $"language {Quote(Language)}");
        // Empty: from the language and where the time zone is (Locales.For).
        Need(Locale == "" || LocaleRe().IsMatch(Locale), $"locale {Quote(Locale)} (like es_MX.UTF-8)");
        Need(LayoutRe().IsMatch(KbLayout), $"keyboard layout {Quote(KbLayout)}");
        Need(VariantRe().IsMatch(KbVariant), $"keyboard variant {Quote(KbVariant)}");
        Need(Theme == "" || ThemeRe().IsMatch(Theme), $"theme {Quote(Theme)}");
        Need(Apps.All(x => Store.AppCatalog.IsId(x)), "apps: an id that can't be one");
        Need(SshKeys.All(k => SshKeyRe().IsMatch(k)), "an SSH key that isn't one");
        Need(Hardware.All(h => Plugins.Plugin.IdPattern().IsMatch(h)), "a hardware plugin that can't be one");
        Need(HardwarePackages.All(Store.AppCatalog.IsPackage), "a package that can't be one");
    }

    static string Quote(string s) => "\"" + (s.Length > 40 ? s[..40] + "…" : s) + "\"";
}
