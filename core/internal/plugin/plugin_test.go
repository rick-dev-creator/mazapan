package plugin

import (
	"strings"
	"testing"
)

func withSettings(s map[string]any) *Plugin {
	p := &Plugin{Settings: s}
	p.Meta.ID = "columns"
	return p
}

func TestResolve(t *testing.T) {
	p := withSettings(map[string]any{"key": "SUPER + equal", "auto": false, "min_width": int64(480), "ratio": 0.5})

	got, err := p.Resolve(map[string]any{"auto": true, "ratio": int64(1)})
	if err != nil {
		t.Fatal(err)
	}
	if got["auto"] != true || got["key"] != "SUPER + equal" || got["ratio"] != 1.0 {
		t.Fatalf("merge: %v", got)
	}
	if p.Settings["auto"] != false {
		t.Fatal("Resolve must not modify the defaults")
	}

	for name, bad := range map[string]map[string]any{
		"unknown key":   {"atuo": true},
		"wrong type":    {"auto": "yes"},
		"float for int": {"min_width": 480.5},
	} {
		if _, err := p.Resolve(bad); err == nil || !strings.Contains(err.Error(), "[plugins.columns]") {
			t.Errorf("%s: want an error naming the section, got %v", name, err)
		}
	}
}
