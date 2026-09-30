using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mazapan.Util;

namespace Mazapan.News;

/// <summary>One news item: Date keeps the offset the feed wrote.</summary>
public sealed record Item(string Title, string Link, DateTimeOffset Date)
{
    /// <summary>NeedsAction guesses whether an item asks the reader to do something.</summary>
    public bool NeedsAction()
    {
        var t = Title.ToLowerInvariant();
        return t.Contains("manual intervention") || t.Contains("requires") || t.Contains("action required");
    }
}

/// <summary>
/// Reads Arch Linux's news feed: where updates that need manual
/// intervention are announced.
/// </summary>
public static partial class Feed
{
    public const string FeedUrl = "https://archlinux.org/feeds/news/";

    static readonly HttpClient Http = new(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    /// <summary>
    /// Since fetches the feed and returns the items published after t, newest
    /// first.
    /// </summary>
    public static List<Item> Since(DateTimeOffset t)
    {
        HttpResponseMessage resp;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
            resp = Http.Send(req);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // Go: Get "https://…": <what went wrong>
            var why = e is TaskCanceledException ? "context deadline exceeded (Client.Timeout exceeded)" : e.Message;
            throw new MazapanException($"Get {GoFormat.Quote(FeedUrl)}: {why}", e);
        }
        using (resp)
        {
            if (resp.StatusCode != HttpStatusCode.OK)
                throw new MazapanException($"{FeedUrl}: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            List<Item> items;
            try
            {
                using var body = resp.Content.ReadAsStream();
                items = Parse(body);
            }
            catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException)
            {
                throw new MazapanException($"news feed: {e.Message}", e);
            }
            return items.Where(it => it.Date > t).ToList();
        }
    }

    /// <summary>
    /// Parse reads the items of an RSS feed (root>channel>item, as Go's
    /// `xml:"channel>item"` does: the root's name isn't checked). An item
    /// whose date doesn't parse is left out.
    /// </summary>
    internal static List<Item> Parse(Stream r)
    {
        XDocument doc;
        try
        {
            using var xr = XmlReader.Create(r, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            doc = XDocument.Load(xr);
        }
        catch (XmlException e)
        {
            throw new MazapanException($"news feed: {e.Message}", e);
        }
        var out_ = new List<Item>();
        if (doc.Root == null) return out_;
        foreach (var channel in doc.Root.Elements().Where(e => e.Name.LocalName == "channel"))
        {
            foreach (var it in channel.Elements().Where(e => e.Name.LocalName == "item"))
            {
                var d = ParseRfc1123Z(Text(it, "pubDate").Trim());
                if (d == null) continue;
                out_.Add(new Item(Text(it, "title").Trim(), Text(it, "link").Trim(), d.Value));
            }
        }
        // sort.Slice: newest first (a stable sort, where Go leaves ties in any order).
        return out_.OrderByDescending(i => i.Date.UtcTicks).ToList();
    }

    /// <summary>
    /// Text is what Go puts in a string field: the element's own character
    /// data (text and CDATA, not nested elements'); the last such element
    /// wins when there are several.
    /// </summary>
    static string Text(XElement item, string name)
    {
        var e = item.Elements().LastOrDefault(x => x.Name.LocalName == name);
        return e == null ? "" : string.Concat(e.Nodes().OfType<XText>().Select(t => t.Value));
    }

    [GeneratedRegex(@"^([A-Za-z]{3}), ([0-9]{2}) ([A-Za-z]{3}) ([0-9]{4}) ([0-9]{1,2}):([0-9]{2}):([0-9]{2})(?:[.,]([0-9]+))? ([+-])([0-9]{2})([0-9]{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Rfc1123Z();

    static readonly string[] Days = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
    static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>
    /// time.Parse(time.RFC1123Z, s): "Mon, 02 Jan 2006 15:04:05 -0700". As in
    /// Go, names match in any case, the weekday isn't checked against the
    /// date, and fractional seconds are accepted after the seconds.
    /// </summary>
    internal static DateTimeOffset? ParseRfc1123Z(string s)
    {
        var m = Rfc1123Z().Match(s);
        if (!m.Success) return null;
        if (Array.IndexOf(Days, m.Groups[1].Value.ToLowerInvariant()) < 0) return null;
        var month = Array.IndexOf(Months, m.Groups[3].Value.ToLowerInvariant()) + 1;
        if (month == 0) return null;
        int N(int g) => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
        var (day, year, hour, min, sec) = (N(2), N(4), N(5), N(6), N(7));
        if (hour > 23 || min > 59 || sec > 59 || day < 1 || day > DateTime.DaysInMonth(year == 0 ? 2000 : year, month))
            return null;
        var ticks = 0L;
        if (m.Groups[8].Success)
        {
            var frac = (m.Groups[8].Value + "0000000")[..7]; // 100 ns ticks; Go keeps nanoseconds
            ticks = long.Parse(frac, CultureInfo.InvariantCulture);
        }
        var offset = new TimeSpan(N(10), N(11), 0);
        if (m.Groups[9].Value == "-") offset = -offset;
        try
        {
            return new DateTimeOffset(year, month, day, hour, min, sec, offset).AddTicks(ticks);
        }
        catch (ArgumentException) { return null; } // year 0, or an offset .NET can't hold (beyond ±14h)
    }
}
