using System.Globalization;
using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>One window of an account's allowance: how much is used (0–100) and when it starts again.</summary>
public sealed record Window(string Name, double Percent, DateTimeOffset? ResetsAt);

/// <summary>An account's limits, when they were read, and whether that's now or the last known.</summary>
public sealed record Limits(Account Account, List<Window> Windows, DateTimeOffset Read, bool Stale, string Problem);

/// <summary>
/// Each Claude account's allowance (the 5-hour session and the weekly
/// windows, and any for one model), from Anthropic's usage endpoint with
/// that account's own login. Its login is only read: a token that has
/// expired isn't refreshed here (refreshing it changes it under Claude
/// Code's feet); the last values read are shown instead, marked as such,
/// until Claude Code is used with that account again.
/// </summary>
public static class AccountLimits
{
    const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    static string CacheDir => Paths.Join(Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } c ? c : Paths.Join(Environment.GetEnvironmentVariable("HOME") ?? "", ".cache"), "mazapan/agents");

    public static Limits Claude(Account a)
    {
        var cache = Path.Join(CacheDir, "limits-" + a.Id + ".json");
        string token = "";
        long expires = 0;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Join(a.ConfigDir, ".credentials.json")));
            if (doc.RootElement.TryGetProperty("claudeAiOauth", out var o))
            {
                token = Discovery.Str(o, "accessToken");
                expires = Usage.Num(o, "expiresAt");
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        if (token == "" || expires < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000)
            return Cached(a, cache, File.Exists(cache) ? "its login hasn't been used lately: these are the last values read"
                : "not read yet: it is once Claude Code has used this account");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var req = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Headers.Add("anthropic-beta", "oauth-2025-04-20");
            req.Headers.Accept.ParseAdd("application/json");
            using var res = http.Send(req);
            var text = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode) return Cached(a, cache, $"usage: HTTP {(int)res.StatusCode}");
            var windows = Parse(text);
            Directory.CreateDirectory(CacheDir);
            Files.WriteAtomic(cache, text);
            File.SetUnixFileMode(cache, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return new Limits(a, windows, DateTimeOffset.UtcNow, false, "");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return Cached(a, cache, "usage: " + e.Message);
        }
    }

    static Limits Cached(Account a, string cache, string why)
    {
        try
        {
            if (File.Exists(cache))
                return new Limits(a, Parse(File.ReadAllText(cache)), new DateTimeOffset(File.GetLastWriteTimeUtc(cache)), true, why);
        }
        catch (Exception e) when (e is JsonException or IOException) { }
        return new Limits(a, [], DateTimeOffset.MinValue, true, why);
    }

    /// <summary>
    /// The endpoint's answer: five_hour and seven_day (or seven_day_oauth_apps)
    /// windows, and a "limits" list where a window for one model shows up.
    /// Utilization comes as 0–1 or 0–100, depending: read as either.
    /// </summary>
    public static List<Window> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var raw = new List<(string Name, double Value, string Resets)>();
        void Bucket(string key, string name)
        {
            if (r.TryGetProperty(key, out var b) && b.ValueKind == JsonValueKind.Object && b.TryGetProperty("utilization", out var u) && u.ValueKind == JsonValueKind.Number)
                raw.Add((name, u.GetDouble(), Discovery.Str(b, "resets_at")));
        }
        Bucket("five_hour", "session");
        if (r.TryGetProperty("seven_day_oauth_apps", out var oa) && oa.ValueKind == JsonValueKind.Object) Bucket("seven_day_oauth_apps", "week");
        else Bucket("seven_day", "week");
        if (r.TryGetProperty("limits", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var e in list.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.Object
                    || !scope.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.Object) continue;
                var name = Discovery.Str(model, "display_name") is { Length: > 0 } n ? n : Discovery.Str(model, "id");
                if (name == "" || !e.TryGetProperty("percent", out var p) || p.ValueKind != JsonValueKind.Number) continue;
                var kind = Discovery.Str(e, "kind").ToLowerInvariant();
                var window = kind.Contains("week") || kind.Contains("day") ? "week" : kind.Contains("month") ? "month" : "session";
                if (raw.Any(x => x.Name == $"{window}: {name}")) continue;
                raw.Add(($"{window}: {name}", p.GetDouble(), Discovery.Str(e, "resets_at")));
            }
        var percentScale = raw.Any(x => x.Value > 1);
        return raw.Select(x => new Window(x.Name, Math.Clamp(percentScale ? x.Value : x.Value * 100, 0, 100),
            DateTimeOffset.TryParse(x.Resets, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null)).ToList();
    }

    /// <summary>Room in every window (under 95 %, or the window over already).</summary>
    public static bool HasRoom(Limits l) => l.Windows.All(w => w.Percent < 95 || (w.ResetsAt is { } r && r <= DateTimeOffset.UtcNow));

    /// <summary>
    /// The one to use now: the first, in order, known to have room; else the
    /// first whose values are old (unused lately: likely room); null when
    /// every one is known to be full.
    /// </summary>
    public static Limits? Pick(IReadOnlyList<Limits> accounts) =>
        accounts.FirstOrDefault(l => !l.Stale && HasRoom(l)) ?? accounts.FirstOrDefault(l => l.Stale && HasRoom(l));
}
