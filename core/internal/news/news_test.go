package news

import (
	"strings"
	"testing"
	"time"
)

const feed = `<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0"><channel><title>Arch Linux: Recent news updates</title>
<item><title>linux-firmware &gt;= 20250613 upgrade requires manual intervention</title>
<link>https://archlinux.org/news/a/</link><pubDate>Sat, 21 Jun 2025 10:00:00 +0000</pubDate></item>
<item><title>Valkey to replace Redis</title>
<link>https://archlinux.org/news/b/</link><pubDate>Thu, 17 Apr 2025 12:00:00 +0000</pubDate></item>
</channel></rss>`

func TestParse(t *testing.T) {
	items, err := parse(strings.NewReader(feed))
	if err != nil {
		t.Fatal(err)
	}
	if len(items) != 2 || items[0].Title != "linux-firmware >= 20250613 upgrade requires manual intervention" {
		t.Fatalf("items = %+v", items)
	}
	if !items[0].NeedsAction() || items[1].NeedsAction() {
		t.Error("only the first item asks for manual intervention")
	}
	if !items[0].Date.Equal(time.Date(2025, 6, 21, 10, 0, 0, 0, time.UTC)) {
		t.Errorf("date = %v", items[0].Date)
	}
}
