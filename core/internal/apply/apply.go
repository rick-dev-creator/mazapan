// Package apply writes rendered files to disk and remembers which files the
// core owns, so it never silently overwrites something a person wrote and
// can remove what a disabled plugin left behind.
package apply

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strings"
	"time"

	"myarch/internal/render"
)

type State int

const (
	Unchanged State = iota
	New             // file doesn't exist yet
	Changed         // ours, and the new content differs
	Conflict        // exists, but we didn't write it (or it was edited since)
)

func (s State) String() string {
	return [...]string{"unchanged", "new", "changed", "conflict"}[s]
}

type Change struct {
	render.File
	State State
}

// Owned maps an absolute path to the sha256 of the content we last wrote.
type Owned map[string]string

func StatePath() string {
	return render.ExpandHome("~/.local/state/myarch/owned.json")
}

func LoadOwned(path string) (Owned, error) {
	b, err := os.ReadFile(path)
	if errors.Is(err, fs.ErrNotExist) {
		return Owned{}, nil
	}
	if err != nil {
		return nil, err
	}
	o := Owned{}
	return o, json.Unmarshal(b, &o)
}

func (o Owned) Save(path string) error {
	b, err := json.MarshalIndent(o, "", "  ")
	if err != nil {
		return err
	}
	return WriteAtomic(path, append(b, '\n'))
}

// Sum is how ownership identifies a file's content.
func Sum(b []byte) string {
	h := sha256.Sum256(b)
	return hex.EncodeToString(h[:])
}

// Plan compares rendered files with what's on disk. Orphans are paths we
// own that no enabled plugin generates anymore.
func Plan(files []render.File, owned Owned) (changes []Change, orphans []string, err error) {
	want := map[string]bool{}
	for _, f := range files {
		want[f.Path] = true
		disk, err := os.ReadFile(f.Path)
		var st State
		switch {
		case errors.Is(err, fs.ErrNotExist):
			st = New
		case err != nil:
			return nil, nil, err
		case bytes.Equal(disk, f.Content):
			st = Unchanged
		case owned[f.Path] == Sum(disk):
			st = Changed
		default:
			st = Conflict
		}
		changes = append(changes, Change{File: f, State: st})
	}
	for p := range owned {
		if !want[p] {
			orphans = append(orphans, p)
		}
	}
	sort.Strings(orphans)
	return changes, orphans, nil
}

type Result struct {
	Written []Change
	Backups map[string]string // path -> backup path
	Removed []string
	Kept    []string // orphans left in place because they were edited
}

// Execute writes the plan. Conflicts abort unless adopt is set, in which case
// the existing file is backed up and taken over.
func Execute(changes []Change, orphans []string, owned Owned, adopt bool) (*Result, error) {
	var conflicts []string
	for _, c := range changes {
		if c.State == Conflict {
			conflicts = append(conflicts, c.Path)
		}
	}
	if len(conflicts) > 0 && !adopt {
		return nil, &ConflictError{Paths: conflicts}
	}

	res := &Result{Backups: map[string]string{}}
	stamp := time.Now().Format("20060102-150405")
	for _, c := range changes {
		switch c.State {
		case Unchanged:
			owned[c.Path] = Sum(c.Content)
			continue
		case Conflict:
			bak := c.Path + ".myarch-bak-" + stamp
			if err := os.Rename(c.Path, bak); err != nil {
				return res, err
			}
			res.Backups[c.Path] = bak
		}
		if err := WriteAtomic(c.Path, c.Content); err != nil {
			return res, err
		}
		owned[c.Path] = Sum(c.Content)
		res.Written = append(res.Written, c)
	}

	for _, p := range orphans {
		disk, err := os.ReadFile(p)
		switch {
		case errors.Is(err, fs.ErrNotExist):
		case err != nil:
			return res, err
		case Sum(disk) == owned[p]:
			if err := os.Remove(p); err != nil {
				return res, err
			}
			res.Removed = append(res.Removed, p)
		default:
			res.Kept = append(res.Kept, p)
		}
		delete(owned, p)
	}
	return res, nil
}

type ConflictError struct{ Paths []string }

func (e *ConflictError) Error() string {
	return "these files exist and were not written by myarch (or were edited since):\n  " +
		strings.Join(e.Paths, "\n  ") +
		"\nre-run with --adopt to back them up and take them over"
}

// Reload runs each distinct reload command of plugins whose files changed,
// in plugin order. Failures are returned, not fatal: the files are already
// written and the next session picks them up anyway.
func Reload(written []Change) map[string]error {
	errs := map[string]error{}
	seen := map[string]bool{}
	for _, c := range written {
		if c.Reload == "" || seen[c.Reload] {
			continue
		}
		seen[c.Reload] = true
		out, err := exec.Command("sh", "-c", c.Reload).CombinedOutput()
		if err != nil {
			errs[c.Reload] = fmt.Errorf("%w: %s", err, strings.TrimSpace(string(out)))
		}
	}
	return errs
}

// WriteAtomic writes through a temporary file, so a reader never sees half
// a file.
func WriteAtomic(path string, b []byte) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	tmp, err := os.CreateTemp(filepath.Dir(path), "."+filepath.Base(path)+".tmp-*")
	if err != nil {
		return err
	}
	defer os.Remove(tmp.Name())
	if _, err := tmp.Write(b); err != nil {
		tmp.Close()
		return err
	}
	if err := tmp.Chmod(0o644); err != nil {
		tmp.Close()
		return err
	}
	if err := tmp.Close(); err != nil {
		return err
	}
	return os.Rename(tmp.Name(), path)
}
