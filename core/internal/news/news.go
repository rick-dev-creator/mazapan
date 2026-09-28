// Package news reads Arch Linux's news feed: where updates that need manual
// intervention are announced.
package news

import (
	"encoding/xml"
	"fmt"
	"io"
	"net/http"
	"sort"
	"strings"
	"time"
)

const FeedURL = "https://archlinux.org/feeds/news/"

type Item struct {
	Title string
	Link  string
	Date  time.Time
}

// Since fetches the feed and returns the items published after t, newest
// first.
func Since(t time.Time) ([]Item, error) {
	client := &http.Client{Timeout: 8 * time.Second}
	resp, err := client.Get(FeedURL)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("%s: %s", FeedURL, resp.Status)
	}
	items, err := parse(resp.Body)
	if err != nil {
		return nil, err
	}
	var out []Item
	for _, it := range items {
		if it.Date.After(t) {
			out = append(out, it)
		}
	}
	return out, nil
}

func parse(r io.Reader) ([]Item, error) {
	var feed struct {
		Items []struct {
			Title   string `xml:"title"`
			Link    string `xml:"link"`
			PubDate string `xml:"pubDate"`
		} `xml:"channel>item"`
	}
	if err := xml.NewDecoder(r).Decode(&feed); err != nil {
		return nil, fmt.Errorf("news feed: %w", err)
	}
	var out []Item
	for _, it := range feed.Items {
		d, err := time.Parse(time.RFC1123Z, strings.TrimSpace(it.PubDate))
		if err != nil {
			continue
		}
		out = append(out, Item{Title: strings.TrimSpace(it.Title), Link: strings.TrimSpace(it.Link), Date: d})
	}
	sort.Slice(out, func(i, j int) bool { return out[i].Date.After(out[j].Date) })
	return out, nil
}

// NeedsAction guesses whether an item asks the reader to do something.
func (it Item) NeedsAction() bool {
	t := strings.ToLower(it.Title)
	return strings.Contains(t, "manual intervention") || strings.Contains(t, "requires") ||
		strings.Contains(t, "action required")
}
