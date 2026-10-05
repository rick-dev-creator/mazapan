namespace Mazapan.Setup;

/// <summary>
/// The Wi-Fi joined on the live system, carried to the installed one: it
/// starts online, and the apps chosen are installed right away. Only Wi-Fi
/// connections with their password in the file (NetworkManager's
/// keyfiles); a "permissions=" line (the live user's alone) is dropped, the
/// account is another one, and so is "interface-name=": the live system
/// calls the card wlan0, the installed one wlp5s0, and a connection tied to
/// a name that isn't there is never tried.
/// </summary>
public static class WifiCarry
{
    public const string Dir = "/etc/NetworkManager/system-connections";

    /// <summary>The keyfile to write in the new system, or null when it isn't one to carry.</summary>
    public static string? Keyfile(string text)
    {
        var lines = text.Replace("\r", "").Split('\n');
        string section = "";
        bool wifi = false, psk = false, keptSecret = false;
        var out_ = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) section = line[1..^1];
            var eq = line.IndexOf('=');
            var key = eq > 0 ? line[..eq].Trim() : "";
            var value = eq > 0 ? line[(eq + 1)..].Trim() : "";
            if (section == "connection" && key == "type" && value is "wifi" or "802-11-wireless") wifi = true;
            if (section == "connection" && key is "permissions" or "interface-name") continue;
            if (section == "wifi-security" && key == "psk" && value != "") psk = true;
            // Kept by an agent (a keyring), not in the file: nothing to carry.
            if (section == "wifi-security" && key == "psk-flags" && value != "0") keptSecret = true;
            if (section == "wifi-security" && key == "key-mgmt" && value == "none") psk = true; // WEP keys or open
            out_.Add(raw);
        }
        if (!wifi || keptSecret) return null;
        // Open networks have no [wifi-security] at all: carried too.
        if (!psk && lines.Any(l => l.Trim() == "[wifi-security]")) return null;
        return string.Join('\n', out_).TrimEnd('\n') + "\n";
    }
}
