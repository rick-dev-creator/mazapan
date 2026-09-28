package apply

import (
	"errors"
	"os"
	"path/filepath"
	"testing"

	"myarch/internal/render"
)

func file(path, content string) render.File {
	return render.File{Plugin: "p", Path: path, Content: []byte(content)}
}

func planOne(t *testing.T, f render.File, owned Owned) State {
	t.Helper()
	ch, _, err := Plan([]render.File{f}, owned)
	if err != nil {
		t.Fatal(err)
	}
	return ch[0].State
}

func TestPlanStates(t *testing.T) {
	dir := t.TempDir()
	p := filepath.Join(dir, "a.conf")

	if s := planOne(t, file(p, "v1"), Owned{}); s != New {
		t.Fatalf("missing file: got %v, want new", s)
	}

	os.WriteFile(p, []byte("v1"), 0o644)
	if s := planOne(t, file(p, "v1"), Owned{}); s != Unchanged {
		t.Fatalf("identical content: got %v, want unchanged", s)
	}
	if s := planOne(t, file(p, "v2"), Owned{}); s != Conflict {
		t.Fatalf("foreign file: got %v, want conflict", s)
	}
	if s := planOne(t, file(p, "v2"), Owned{p: sum([]byte("v1"))}); s != Changed {
		t.Fatalf("our file: got %v, want changed", s)
	}

	// We wrote v1, then a person edited it: that's theirs now.
	os.WriteFile(p, []byte("hand edit"), 0o644)
	if s := planOne(t, file(p, "v2"), Owned{p: sum([]byte("v1"))}); s != Conflict {
		t.Fatalf("edited file: got %v, want conflict", s)
	}
}

func TestConflictNeedsAdopt(t *testing.T) {
	dir := t.TempDir()
	p := filepath.Join(dir, "a.conf")
	os.WriteFile(p, []byte("mine"), 0o644)
	owned := Owned{}

	ch, orphans, _ := Plan([]render.File{file(p, "generated")}, owned)
	_, err := Execute(ch, orphans, owned, false)
	var ce *ConflictError
	if !errors.As(err, &ce) {
		t.Fatalf("want ConflictError, got %v", err)
	}
	if b, _ := os.ReadFile(p); string(b) != "mine" {
		t.Fatalf("file was touched without --adopt: %q", b)
	}

	res, err := Execute(ch, orphans, owned, true)
	if err != nil {
		t.Fatal(err)
	}
	if b, _ := os.ReadFile(res.Backups[p]); string(b) != "mine" {
		t.Fatalf("backup content = %q, want the original", b)
	}
	if b, _ := os.ReadFile(p); string(b) != "generated" {
		t.Fatalf("file = %q after adopt", b)
	}
}

func TestOrphans(t *testing.T) {
	dir := t.TempDir()
	clean := filepath.Join(dir, "clean.conf")
	edited := filepath.Join(dir, "edited.conf")
	os.WriteFile(clean, []byte("gen"), 0o644)
	os.WriteFile(edited, []byte("hand edit"), 0o644)
	owned := Owned{clean: sum([]byte("gen")), edited: sum([]byte("gen"))}

	ch, orphans, _ := Plan(nil, owned)
	res, err := Execute(ch, orphans, owned, false)
	if err != nil {
		t.Fatal(err)
	}
	if _, err := os.Stat(clean); !errors.Is(err, os.ErrNotExist) {
		t.Fatal("untouched orphan should be removed")
	}
	if _, err := os.Stat(edited); err != nil {
		t.Fatal("edited orphan must be left in place")
	}
	if len(res.Removed) != 1 || len(res.Kept) != 1 || len(owned) != 0 {
		t.Fatalf("removed=%v kept=%v owned=%v", res.Removed, res.Kept, owned)
	}
}
