using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>A model's prices, per token (input, output, cache reads and writes).</summary>
public sealed record Price(double Input, double Output, double CacheRead, double CacheWrite);

/// <summary>
/// What a model costs through its API, from LiteLLM's price table (the one
/// most tools keep current), downloaded at most once a day and kept; the
/// last good one when it can't be reached. A model it doesn't list has no
/// price: its cost is unknown, never guessed.
/// </summary>
public sealed class Prices
{
    public const string Source = "https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json";

    readonly Dictionary<string, Price> table = new(StringComparer.OrdinalIgnoreCase);
    public string Problem { get; private set; } = "";

    static string CachePath => Paths.Join(Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } c ? c : Paths.Join(Environment.GetEnvironmentVariable("HOME") ?? "", ".cache"), "mazapan/agents/prices.json");

    public static Prices Load(bool refresh = false)
    {
        var p = new Prices();
        var path = CachePath;
        var fresh = File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(1);
        if (refresh || !fresh)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 16 << 20 };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("mazapan");
                var text = http.GetStringAsync(Source).GetAwaiter().GetResult();
                using (JsonDocument.Parse(text)) { } // keep only what reads
                Directory.CreateDirectory(Paths.Dir(path));
                Files.WriteAtomic(path, text);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or IOException)
            {
                p.Problem = "prices: " + e.Message + (File.Exists(path) ? " (the last ones kept)" : "");
            }
        }
        if (File.Exists(path))
        {
            try { p.Read(File.ReadAllText(path)); }
            catch (JsonException e) { p.Problem = "prices: " + e.Message; }
        }
        return p;
    }

    /// <summary>LiteLLM's table: model → per-token costs.</summary>
    public void Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var m in doc.RootElement.EnumerateObject())
        {
            if (m.Value.ValueKind != JsonValueKind.Object) continue;
            double D(string k) => m.Value.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
            var input = D("input_cost_per_token");
            var output = D("output_cost_per_token");
            if (input == 0 && output == 0) continue;
            table[m.Name] = new Price(input, output, D("cache_read_input_token_cost"), D("cache_creation_input_token_cost"));
        }
    }

    /// <summary>A model's price: by its name, with its provider before it, or without a date at its end.</summary>
    public Price? For(string provider, string model)
    {
        if (model == "") return null;
        var bare = model.Contains('/') ? model[(model.LastIndexOf('/') + 1)..] : model;
        string[] names = [model, $"{provider}/{model}", bare, $"{provider}/{bare}",
            System.Text.RegularExpressions.Regex.Replace(bare, @"-\d{8}$", ""), $"anthropic/{bare}", $"openai/{bare}"];
        foreach (var n in names)
            if (table.TryGetValue(n, out var p)) return p;
        return null;
    }

    /// <summary>What these tokens cost through the API, or null when the model has no price.</summary>
    public double? Cost(Entry e) =>
        For(e.Provider, e.Model) is { } p ? e.Input * p.Input + e.Output * p.Output + e.CacheRead * p.CacheRead + e.CacheWrite * p.CacheWrite : null;
}
