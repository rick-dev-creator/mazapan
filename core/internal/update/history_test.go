package update

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"myarch/internal/apply"
	"myarch/internal/render"
)

// sharedOwned writes content as a shared file (merge = "ini") through apply,
// and returns what Owned then has for it.
func sharedOwned(t *testing.T, path, content string) string {
	t.Helper()
	owned := apply.Owned{}
	f := render.File{Plugin: "p", Path: path, Content: []byte(content), Merge: "ini"}
	ch, orphans, err := apply.Plan([]render.File{f}, owned)
	if err != nil {
		t.Fatal(err)
	}
	if _, err := apply.Execute(ch, orphans, owned, true); err != nil {
		t.Fatal(err)
	}
	return owned[path]
}

// A person's kdeglobals, taken over (shared) by an update: rolling back
// must not delete it.
func TestRestoreNeverRemovesASharedFile(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	p := filepath.Join(t.TempDir(), "kdeglobals")
	os.WriteFile(p, []byte("[KFileDialog Settings]\nx=1\n"), 0o644)
	r := New()
	r.BackupFiles(apply.Owned{}) // it wasn't myarch's before the update
	current := apply.Owned{p: sharedOwned(t, p, "[KDE]\nwidgetStyle=Fusion\n")}
	res, err := r.RestoreFiles(current)
	if err != nil {
		t.Fatal(err)
	}
	if _, err := os.Stat(p); err != nil || len(res.Shared) != 1 || res.Owned[p] != "" {
		t.Fatalf("kept and released: %v %+v", err, res)
	}
}

// Rolling back a shared file puts myarch's keys back and keeps what the
// app wrote since.
func TestRestoreSharedKeepsTheAppsKeys(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	p := filepath.Join(t.TempDir(), "qt6ct.conf")
	before := sharedOwned(t, p, "[Appearance]\nstyle=Fusion\n")
	r := New()
	r.BackupFiles(apply.Owned{p: before})
	after := sharedOwned(t, p, "[Appearance]\nstyle=Windows\n")
	// The app saves its window since.
	b, _ := os.ReadFile(p)
	os.WriteFile(p, append(b, []byte("[SettingsWindow]\ngeometry=x\n")...), 0o644)
	res, err := r.RestoreFiles(apply.Owned{p: after})
	if err != nil {
		t.Fatal(err)
	}
	got, _ := os.ReadFile(p)
	if !strings.Contains(string(got), "style=Fusion") || !strings.Contains(string(got), "geometry=x") || len(res.Backups) != 0 {
		t.Fatalf("got %q, %+v", got, res)
	}
}

func TestBackupAndRestore(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	dir := t.TempDir()
	kept := filepath.Join(dir, "kept.conf")
	added := filepath.Join(dir, "added.conf")
	os.WriteFile(kept, []byte("before"), 0o644)

	r := New()
	if err := r.BackupFiles(apply.Owned{kept: apply.Sum([]byte("before"))}); err != nil {
		t.Fatal(err)
	}

	// The update rewrites one file and adds another.
	os.WriteFile(kept, []byte("after"), 0o644)
	os.WriteFile(added, []byte("new"), 0o644)
	res, err := r.RestoreFiles(apply.Owned{kept: apply.Sum([]byte("after")), added: apply.Sum([]byte("new"))})
	if err != nil {
		t.Fatal(err)
	}
	if b, _ := os.ReadFile(kept); string(b) != "before" || res.Files != 1 || len(res.Backups) != 0 {
		t.Errorf("kept.conf = %q, %+v", b, res)
	}
	if _, err := os.Stat(added); !os.IsNotExist(err) {
		t.Error("a file the update added must be removed")
	}
	if len(res.Owned) != 1 || res.Owned[kept] != apply.Sum([]byte("before")) || res.Owned[added] != "" {
		t.Errorf("owned = %v", res.Owned)
	}
}

func TestRestoreKeepsHandEdits(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	dir := t.TempDir()
	kept := filepath.Join(dir, "kept.conf")
	added := filepath.Join(dir, "added.conf")
	os.WriteFile(kept, []byte("before"), 0o644)
	r := New()
	r.BackupFiles(apply.Owned{kept: apply.Sum([]byte("before"))})

	// After the update, the person edited both files by hand.
	os.WriteFile(kept, []byte("my edit"), 0o644)
	os.WriteFile(added, []byte("my other edit"), 0o644)
	res, err := r.RestoreFiles(apply.Owned{kept: apply.Sum([]byte("after")), added: apply.Sum([]byte("new"))})
	if err != nil {
		t.Fatal(err)
	}
	if b, _ := os.ReadFile(res.Backups[kept]); string(b) != "my edit" {
		t.Errorf("the hand edit must be kept in a backup, got %q", b)
	}
	if b, _ := os.ReadFile(added); string(b) != "my other edit" || len(res.LeftBehind) != 1 {
		t.Errorf("an edited added file must stay: %q %+v", b, res)
	}
}

func TestBefore(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	r := New()
	if err := r.SaveBefore(map[string]string{"foot": "1"}); err != nil {
		t.Fatal(err)
	}
	if m, ok := r.LoadBefore(); !ok || m["foot"] != "1" {
		t.Fatalf("LoadBefore = %v %v", m, ok)
	}
}

func TestHistory(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	old := &Record{ID: "20260101-000000", Outcome: OK, Finished: time.Date(2026, 1, 1, 0, 0, 0, 0, time.UTC)}
	bad := &Record{ID: "20260201-000000", Outcome: RolledBack}
	for _, r := range []*Record{old, bad} {
		if err := r.Save(); err != nil {
			t.Fatal(err)
		}
	}
	list, _ := List()
	if len(list) != 2 || list[0].ID != bad.ID {
		t.Fatalf("list = %+v", list)
	}
	if got := LastSuccess(); !got.Equal(old.Finished) {
		t.Errorf("LastSuccess = %v, want the last update that went through", got)
	}
}

func TestRestoreWithoutBackupTouchesNothing(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	f := filepath.Join(t.TempDir(), "a.conf")
	os.WriteFile(f, []byte("current"), 0o644)
	r := New() // never backed up
	res, err := r.RestoreFiles(apply.Owned{f: apply.Sum([]byte("current"))})
	if err != nil {
		t.Fatal(err)
	}
	if b, _ := os.ReadFile(f); string(b) != "current" || !res.NoBackup || len(res.Owned) != 1 {
		t.Fatalf("a record without a backup must not touch files: %q %+v", b, res)
	}
}

func TestEmptyBackupSurvivesSaving(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	r := New()
	r.BackupFiles(apply.Owned{}) // nothing generated yet: an empty backup, not none
	r.Outcome = OK
	r.Save()
	list, _ := List()
	if list[0].Owned == nil {
		t.Fatal("an empty backup must load back as empty, not as no backup")
	}
}

func TestRestoreLeavesLaterAppliesAlone(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	f := filepath.Join(t.TempDir(), "theme.conf")
	os.WriteFile(f, []byte("old theme"), 0o644)
	r := New()
	r.BackupFiles(apply.Owned{f: apply.Sum([]byte("old theme"))})
	r.AfterOwned = apply.Owned{f: apply.Sum([]byte("updated"))}
	// After the update, `myarch apply --theme other` rewrote it.
	os.WriteFile(f, []byte("other theme"), 0o644)
	res, err := r.RestoreFiles(apply.Owned{f: apply.Sum([]byte("other theme"))})
	if err != nil {
		t.Fatal(err)
	}
	if b, _ := os.ReadFile(f); string(b) != "other theme" || len(res.Later) != 1 {
		t.Fatalf("a later apply must be left alone: %q %+v", b, res)
	}
}

func TestRestoreSameContentIsNotAnEdit(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	f := filepath.Join(t.TempDir(), "a.conf")
	os.WriteFile(f, []byte("hand edited before the update"), 0o644)
	r := New()
	r.BackupFiles(apply.Owned{f: "sum the update never matched"})
	res, err := r.RestoreFiles(apply.Owned{f: "sum the update never matched"})
	if err != nil {
		t.Fatal(err)
	}
	if len(res.Backups) != 0 || res.Files != 0 {
		t.Fatalf("content already as it was must be neither backed up nor rewritten: %+v", res)
	}
}

func TestLiveAndWorkOutChanges(t *testing.T) {
	t.Setenv("HOME", t.TempDir())
	failed := &Record{Outcome: Failed, Owned: apply.Owned{}}
	if failed.Live() {
		t.Error("a failed upgrade that changed nothing is not live")
	}
	done := New()
	done.Outcome = OK // config-only update: no package changes
	done.SaveBefore(map[string]string{"glibc": "1"})
	done.WorkOutChanges(func() (map[string]string, error) { return map[string]string{"glibc": "2"}, nil })
	if len(done.Changes) != 0 {
		t.Error("a finished update must never get changes worked out from today's packages")
	}
	cut := New()
	cut.Outcome = InProgress
	cut.SaveBefore(map[string]string{"foot": "1"})
	cut.WorkOutChanges(func() (map[string]string, error) { return map[string]string{"foot": "2"}, nil })
	if len(cut.Changes) != 1 {
		t.Error("an interrupted update gets its changes worked out")
	}
}
