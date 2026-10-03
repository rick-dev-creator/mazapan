using System.Globalization;
using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>What a provider says was spent: by day (UTC, as they report it), a limit and what's left where it has one.</summary>
public sealed record ProviderSpend(string Provider, string Name, List<(string Day, double Usd)> Days, double Today, double Period,
    double? Limit, double? Remaining, string Problem);

/// <summary>
/// The spend the providers themselves report, with the keys in the keyring:
/// OpenRouter's for its key (today, the month, its limit), Anthropic's and
/// OpenAI's for the organization (by day, with an admin key). Kept a quarter
/// of an hour (numbers only) so the bar and the dashboard don't ask each time.
/// </summary>
public static class Spend
{
    static string CacheFile => Paths.Join(Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } c ? c : Paths.Join(Environment.GetEnvironmentVariable("HOME") ?? "", ".cache"), "mazapan/agents/spend.json");

    public static List<ProviderSpend> Read(int days, bool refresh)
    {
        if (!refresh && Cached(days) is { } cached) return cached;
        var out_ = new List<ProviderSpend>();
        foreach (var (id, read) in new (string, Func<string, int, ProviderSpend>)[] { ("openrouter", OpenRouter), ("anthropic-admin", Anthropic), ("openai-admin", OpenAI) })
        {
            string? key;
            try { key = Keys.Get(id); }
            catch (MazapanException) { break; } // no keyring: none
            if (key == null) continue;
            try { out_.Add(read(key, days)); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                var p = Keys.Of(id);
                out_.Add(new(id, p.Name, [], 0, 0, null, null, e is HttpRequestException h && h.StatusCode is { } s ? (int)s is 401 or 403 ? $"key refused ({(int)s})" : $"error {(int)s}" : "can't be reached"));
            }
        }
        Write(days, out_);
        return out_;
    }

    static HttpClient Http()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("mazapan");
        return h;
    }

    static string Day(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    static DateTimeOffset Start(int days) => new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(-(Math.Max(1, days) - 1));

    static ProviderSpend OpenRouter(string key, int days)
    {
        using var http = Http();
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/key");
        req.Headers.Authorization = new("Bearer", key);
        using var res = http.Send(req);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(res.Content.ReadAsStream());
        return ParseOpenRouter(doc.RootElement, days);
    }

    public static ProviderSpend ParseOpenRouter(JsonElement root, int days)
    {
        var d = root.GetProperty("data");
        double? N(string k) => d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        // By day it says only today; the period, the closest it has (the week, the month, all).
        var period = days <= 1 ? N("usage_daily") : days <= 7 ? N("usage_weekly") : days <= 31 ? N("usage_monthly") : N("usage");
        var today = N("usage_daily") ?? 0;
        return new("openrouter", "OpenRouter", [(Day(DateTimeOffset.UtcNow), today)], today, period ?? N("usage") ?? 0, N("limit"), N("limit_remaining"), "");
    }

    static ProviderSpend Anthropic(string key, int days)
    {
        using var http = Http();
        var all = new List<(string, double)>();
        string? page = null;
        do
        {
            var url = $"https://api.anthropic.com/v1/organizations/cost_report?starting_at={Uri.EscapeDataString(Start(days).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}&bucket_width=1d&limit=31"
                + (page != null ? "&page=" + Uri.EscapeDataString(page) : "");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-api-key", key);
            req.Headers.Add("anthropic-version", "2023-06-01");
            using var res = http.Send(req);
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(res.Content.ReadAsStream());
            all.AddRange(ParseAnthropic(doc.RootElement, out page));
        } while (page != null && all.Count < 400);
        return Totals("anthropic-admin", "Anthropic", all);
    }

    /// <summary>Anthropic's cost report: amounts in cents, as decimal strings.</summary>
    public static List<(string Day, double Usd)> ParseAnthropic(JsonElement root, out string? next)
    {
        var out_ = new List<(string, double)>();
        foreach (var b in root.GetProperty("data").EnumerateArray())
        {
            var day = Day(DateTimeOffset.Parse(b.GetProperty("starting_at").GetString()!, CultureInfo.InvariantCulture));
            double sum = 0;
            foreach (var r in b.GetProperty("results").EnumerateArray())
                if (Discovery.Str(r, "currency") is "USD" or "" && double.TryParse(Discovery.Str(r, "amount"), NumberStyles.Float, CultureInfo.InvariantCulture, out var cents))
                    sum += cents / 100;
            out_.Add((day, sum));
        }
        next = root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True ? Discovery.Str(root, "next_page") is { Length: > 0 } p ? p : null : null;
        return out_;
    }

    static ProviderSpend OpenAI(string key, int days)
    {
        using var http = Http();
        var all = new List<(string, double)>();
        string? page = null;
        do
        {
            var url = $"https://api.openai.com/v1/organization/costs?start_time={Start(days).ToUnixTimeSeconds()}&bucket_width=1d&limit={Math.Clamp(days, 1, 180)}"
                + (page != null ? "&page=" + Uri.EscapeDataString(page) : "");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new("Bearer", key);
            using var res = http.Send(req);
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(res.Content.ReadAsStream());
            all.AddRange(ParseOpenAI(doc.RootElement, out page));
        } while (page != null && all.Count < 400);
        return Totals("openai-admin", "OpenAI", all);
    }

    /// <summary>OpenAI's costs: amounts in dollars, start times in Unix seconds.</summary>
    public static List<(string Day, double Usd)> ParseOpenAI(JsonElement root, out string? next)
    {
        var out_ = new List<(string, double)>();
        foreach (var b in root.GetProperty("data").EnumerateArray())
        {
            var day = Day(DateTimeOffset.FromUnixTimeSeconds(b.GetProperty("start_time").GetInt64()));
            double sum = 0;
            foreach (var r in b.GetProperty("results").EnumerateArray())
                if (r.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Object && a.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number
                    && Discovery.Str(a, "currency").ToLowerInvariant() is "usd" or "")
                    sum += v.GetDouble();
            out_.Add((day, sum));
        }
        next = root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True ? Discovery.Str(root, "next_page") is { Length: > 0 } p ? p : null : null;
        return out_;
    }

    static ProviderSpend Totals(string id, string name, List<(string Day, double Usd)> days)
    {
        var merged = days.GroupBy(d => d.Day).Select(g => (g.Key, g.Sum(x => x.Usd))).OrderBy(d => d.Key).ToList();
        var today = Day(DateTimeOffset.UtcNow);
        return new(id, name, merged, merged.Where(d => d.Key == today).Sum(d => d.Item2), merged.Sum(d => d.Item2), null, null, "");
    }

    // --- the cache: {days, at, providers: […]}, numbers and names only ----------------

    static List<ProviderSpend>? Cached(int days)
    {
        try
        {
            if (!File.Exists(CacheFile) || DateTime.UtcNow - File.GetLastWriteTimeUtc(CacheFile) > TimeSpan.FromMinutes(15)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(CacheFile));
            var r = doc.RootElement;
            if (!r.TryGetProperty("days", out var d) || d.GetInt32() != days) return null;
            return r.GetProperty("providers").EnumerateArray().Select(p => new ProviderSpend(
                Discovery.Str(p, "provider"), Discovery.Str(p, "name"),
                p.GetProperty("days").EnumerateArray().Select(x => (Discovery.Str(x, "day"), x.GetProperty("usd").GetDouble())).ToList(),
                p.GetProperty("today").GetDouble(), p.GetProperty("period").GetDouble(),
                p.TryGetProperty("limit", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetDouble() : null,
                p.TryGetProperty("remaining", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetDouble() : null,
                Discovery.Str(p, "problem"))).ToList();
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidOperationException or KeyNotFoundException or FormatException) { return null; }
    }

    static void Write(int days, List<ProviderSpend> list)
    {
        try
        {
            Directory.CreateDirectory(Paths.Dir(CacheFile));
            Files.WriteAtomic(CacheFile, GoJson.Marshal(new Fields { { "days", days }, { "providers", list.Select(Json).ToList() } }) + "\n");
            File.SetUnixFileMode(CacheFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static Fields Json(ProviderSpend s) => new()
    {
        { "provider", s.Provider }, { "name", s.Name },
        { "days", s.Days.Select(d => new Fields { { "day", d.Day }, { "usd", Math.Round(d.Usd, 4) } }).ToList() },
        { "today", Math.Round(s.Today, 4) }, { "period", Math.Round(s.Period, 4) },
        { "limit", s.Limit is { } l ? Math.Round(l, 4) : null }, { "remaining", s.Remaining is { } r ? Math.Round(r, 4) : null },
        { "problem", s.Problem },
    };
}
