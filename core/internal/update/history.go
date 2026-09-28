// Package update keeps the record of each system update: what changed, how
// the checks went, whether it was rolled back, and a copy of the generated
// files from before, so a rollback puts back exactly what was there.
package update

import (
	"encoding/json"
	"errors"
	"io/fs"
	"os"
	"path/filepath"
	"sort"
	"time"

	"myarch/internal/apply"
	"myarch/internal/health"
	"myarch/internal/pacman"
	"myarch/internal/render"
)

const (
	InProgress         = "in-progress"         // started; still running, or interrupted
	OK                 = "ok"                  // updated, every check passed
	RolledBack         = "rolled-back"         // a check failed, everything was put back
	RollbackIncomplete = "rollback-incomplete" // a check failed, not everything could be put back
	Failed             = "failed"              // the upgrade itself failed
)

type Record struct {
	ID       string          `json:"id"`
	Started  time.Time       `json:"started"`
	Finished time.Time       `json:"finished"`
	Changes  []pacman.Change `json:"changes,omitempty"`
	Checks   []health.Result `json:"checks,omitempty"`
	Outcome  string          `json:"outcome"`
	Note     string          `json:"note,omitempty"`
	// Generated files before the update. No omitempty: an empty backup
	// ({}) and no backup at all (null) mean different things.
	Owned apply.Owned `json:"owned"`
	// Generated files right after the update's apply. A rollback leaves
	// alone what changed after that (a later `myarch apply`), instead of
	// undoing it behind config.toml's back.
	AfterOwned apply.Owned `json:"after_owned,omitempty"`
}

func Root() string { return render.ExpandHome("~/.local/state/myarch/updates") }

func New() *Record {
	now := time.Now()
	return &Record{ID: now.Format("20060102-150405"), Started: now}
}

func (r *Record) Dir() string { return filepath.Join(Root(), r.ID) }

func (r *Record) Save() error {
	b, err := json.MarshalIndent(r, "", "  ")
	if err != nil {
		return err
	}
	// Atomic: the record is rewritten many times during an update, and one
	// cut off half way would be unreadable and vanish from the history.
	return apply.WriteAtomic(filepath.Join(r.Dir(), "record.json"), append(b, '\n'))
}

// List returns every record, newest first.
func List() ([]*Record, error) {
	entries, err := os.ReadDir(Root())
	if errors.Is(err, fs.ErrNotExist) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	var out []*Record
	for _, e := range entries {
		b, err := os.ReadFile(filepath.Join(Root(), e.Name(), "record.json"))
		if err != nil {
			continue
		}
		r := &Record{}
		if json.Unmarshal(b, r) == nil {
			out = append(out, r)
		}
	}
	sort.Slice(out, func(i, j int) bool { return out[i].ID > out[j].ID })
	return out, nil
}

// LastSuccess is when the system was last updated successfully, or the zero
// time.
func LastSuccess() time.Time {
	records, _ := List()
	for _, r := range records {
		if r.Outcome == OK {
			return r.Finished
		}
	}
	return time.Time{}
}

// BackupFiles copies every generated file as it is now.
func (r *Record) BackupFiles(owned apply.Owned) error {
	r.Owned = apply.Owned{}
	for path, sum := range owned {
		b, err := os.ReadFile(path)
		if errors.Is(err, fs.ErrNotExist) {
			continue
		}
		if err != nil {
			return err
		}
		dst := filepath.Join(r.Dir(), "files", path)
		if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
			return err
		}
		if err := os.WriteFile(dst, b, 0o644); err != nil {
			return err
		}
		r.Owned[path] = sum
	}
	return nil
}

// SaveBefore keeps the full package list from before the update. If the
// update is interrupted before its changes are recorded, a rollback works
// them out by comparing this with what's installed then.
func (r *Record) SaveBefore(installed map[string]string) error {
	b, err := json.Marshal(installed)
	if err != nil {
		return err
	}
	return apply.WriteAtomic(filepath.Join(r.Dir(), "before.json"), b)
}

// WorkOutChanges fills in the changes of an update interrupted before it
// could record them, by comparing the packages from before it with now.
func (r *Record) WorkOutChanges(installed func() (map[string]string, error)) {
	// Only an interrupted update: one that finished with no package
	// changes (config only) changed none, and diffing its old package list
	// with today's would "revert" every upgrade made since.
	if r.Outcome != InProgress || len(r.Changes) > 0 {
		return
	}
	before, ok := r.LoadBefore()
	if !ok {
		return
	}
	if now, err := installed(); err == nil {
		r.Changes = pacman.Diff(before, now)
	}
}

// Live reports whether the update's changes are still in place: not rolled
// back, and not an upgrade that failed without changing anything.
func (r *Record) Live() bool {
	return r.Outcome != RolledBack && !(r.Outcome == Failed && len(r.Changes) == 0)
}

func (r *Record) LoadBefore() (map[string]string, bool) {
	b, err := os.ReadFile(filepath.Join(r.Dir(), "before.json"))
	if err != nil {
		return nil, false
	}
	m := map[string]string{}
	return m, json.Unmarshal(b, &m) == nil
}

// Restored says what RestoreFiles did.
type Restored struct {
	Owned      apply.Owned       // ownership matching the disk, even after an error
	Files      int               // files rewritten as they were
	Backups    map[string]string // edited by hand since: path -> where the edit was kept
	LeftBehind []string          // added since and edited by hand: left in place
	Later      []string          // rewritten by a later `myarch apply`: left as they are
	NoBackup   bool              // the record has no copy of the files: nothing touched
}

// RestoreFiles puts the generated files back as they were before the
// update and removes the ones it added. It never loses work:
//   - a file edited by hand since is backed up before being restored;
//   - a file a later `myarch apply` rewrote is left alone (it follows the
//     config as it is now);
//   - an added file that was edited stays where it is.
//
// Owned in the result always matches the disk, even when it stops half way.
func (r *Record) RestoreFiles(current apply.Owned) (*Restored, error) {
	res := &Restored{Owned: apply.Owned{}, Backups: map[string]string{}}
	for k, v := range current {
		res.Owned[k] = v
	}
	// No copy of the files (a damaged or foreign record): "nothing was
	// there before" would mean deleting every generated file. Touch nothing.
	if r.Owned == nil {
		res.NoBackup = true
		return res, nil
	}
	changedLater := func(path string) bool {
		return r.AfterOwned != nil && current[path] != r.AfterOwned[path]
	}
	stamp := time.Now().Format("20060102-150405")
	for path, sum := range r.Owned {
		if changedLater(path) {
			res.Later = append(res.Later, path)
			continue
		}
		want, err := os.ReadFile(filepath.Join(r.Dir(), "files", path))
		if err != nil {
			return res, err
		}
		disk, readErr := os.ReadFile(path)
		switch {
		case readErr == nil && string(disk) == string(want):
			// Already as it was: nothing to write, nothing to back up.
		case readErr == nil && apply.Sum(disk) != current[path]:
			bak := path + ".myarch-bak-" + stamp
			if err := os.Rename(path, bak); err != nil {
				return res, err
			}
			res.Backups[path] = bak
			fallthrough
		default:
			if err := apply.WriteAtomic(path, want); err != nil {
				return res, err
			}
			res.Files++
		}
		res.Owned[path] = sum
	}
	for path := range current {
		if _, before := r.Owned[path]; before {
			continue
		}
		if changedLater(path) {
			res.Later = append(res.Later, path)
			continue
		}
		if b, err := os.ReadFile(path); err == nil && apply.Sum(b) != current[path] {
			res.LeftBehind = append(res.LeftBehind, path)
			continue
		}
		if err := os.Remove(path); err != nil && !errors.Is(err, fs.ErrNotExist) {
			return res, err
		}
		delete(res.Owned, path)
	}
	return res, nil
}
