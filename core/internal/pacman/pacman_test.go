package pacman

import (
	"os"
	"path/filepath"
	"reflect"
	"testing"
)

func TestParsePending(t *testing.T) {
	got := parsePending("foot 1.28.0-2 -> 1.28.1-1\nhyprland 0.56.2-3 -> 0.57.0-1\ngarbage line\n")
	want := []Change{
		{Name: "foot", From: "1.28.0-2", To: "1.28.1-1"},
		{Name: "hyprland", From: "0.56.2-3", To: "0.57.0-1"},
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("got %+v", got)
	}
}

func TestDiff(t *testing.T) {
	before := map[string]string{"a": "1", "b": "1", "gone": "1"}
	after := map[string]string{"a": "1", "b": "2", "new": "1"}
	want := []Change{
		{Name: "b", From: "1", To: "2"},
		{Name: "gone", From: "1"},
		{Name: "new", To: "1"},
	}
	if got := Diff(before, after); !reflect.DeepEqual(got, want) {
		t.Fatalf("got %+v", got)
	}
}

func TestCachedFile(t *testing.T) {
	dir := t.TempDir()
	for _, f := range []string{
		"foot-1.28.0-2-x86_64.pkg.tar.zst",
		"foot-1.28.0-2-x86_64.pkg.tar.zst.sig",
		"foot-terminfo-1.28.0-2-x86_64.pkg.tar.zst",
		"go-2:1.27.1-1-x86_64.pkg.tar.zst",
	} {
		os.WriteFile(filepath.Join(dir, f), nil, 0o644)
	}
	if f, ok := CachedFile([]string{dir}, "foot", "1.28.0-2"); !ok || filepath.Base(f) != "foot-1.28.0-2-x86_64.pkg.tar.zst" {
		t.Errorf("foot: %q %v", f, ok)
	}
	if f, ok := CachedFile([]string{dir}, "go", "2:1.27.1-1"); !ok || filepath.Base(f) != "go-2:1.27.1-1-x86_64.pkg.tar.zst" {
		t.Errorf("epoch: %q %v", f, ok)
	}
	if _, ok := CachedFile([]string{dir}, "foot", "1.27.0-1"); ok {
		t.Error("a version not in the cache must not match")
	}
}
