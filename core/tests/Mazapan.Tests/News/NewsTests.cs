using System.Text;
using Mazapan.News;
using Mazapan.Util;

namespace Mazapan.Tests.News;

public class NewsTests
{
    const string FeedXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0"><channel><title>Arch Linux: Recent news updates</title>
        <item><title>linux-firmware &gt;= 20250613 upgrade requires manual intervention</title>
        <link>https://archlinux.org/news/a/</link><pubDate>Sat, 21 Jun 2025 10:00:00 +0000</pubDate></item>
        <item><title>Valkey to replace Redis</title>
        <link>https://archlinux.org/news/b/</link><pubDate>Thu, 17 Apr 2025 12:00:00 +0000</pubDate></item>
        </channel></rss>
        """;

    static List<Item> Parse(string xml) => Feed.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void ParseFeed()
    {
        var items = Parse(FeedXml);
        Assert.Equal(2, items.Count);
        Assert.Equal("linux-firmware >= 20250613 upgrade requires manual intervention", items[0].Title);
        Assert.True(items[0].NeedsAction());
        Assert.False(items[1].NeedsAction()); // only the first item asks for manual intervention
        Assert.Equal(new DateTimeOffset(2025, 6, 21, 10, 0, 0, TimeSpan.Zero), items[0].Date);
    }

    [Fact]
    public void NewestFirstAndBadDatesSkipped()
    {
        var items = Parse("""
            <rss><channel>
            <item><title> old </title><pubDate>Mon, 01 Jan 2024 00:00:00 +0000</pubDate></item>
            <item><title>bad</title><pubDate>2025-01-01</pubDate></item>
            <item><title>new</title><link> l </link><pubDate> Tue, 02 Jan 2024 09:00:00 +0200 </pubDate></item>
            </channel></rss>
            """);
        Assert.Equal(["new", "old"], items.Select(i => i.Title));
        Assert.Equal("l", items[0].Link);
        Assert.Equal(TimeSpan.FromHours(2), items[0].Date.Offset);
    }

    [Fact]
    public void BrokenXmlIsAnError()
    {
        var e = Assert.Throws<MazapanException>(() => Parse("<rss><channel><item>"));
        Assert.StartsWith("news feed: ", e.Message);
    }

    [Theory]
    [InlineData("Sat, 21 Jun 2025 10:00:00 +0000", true)]
    [InlineData("sat, 21 jun 2025 9:00:00 -0500", true)] // names in any case, a one-digit hour
    [InlineData("Sat, 21 Jun 2025 10:00:00.5 +0000", true)] // fractional seconds
    [InlineData("Sat, 1 Jun 2025 10:00:00 +0000", false)] // the day takes two digits
    [InlineData("Sat, 31 Jun 2025 10:00:00 +0000", false)]
    [InlineData("Sat, 21 Jun 2025 10:00:00 GMT", false)] // RFC1123, not RFC1123Z
    [InlineData("Xyz, 21 Jun 2025 10:00:00 +0000", false)]
    public void Rfc1123Z(string s, bool ok) => Assert.Equal(ok, Feed.ParseRfc1123Z(s) != null);
}
