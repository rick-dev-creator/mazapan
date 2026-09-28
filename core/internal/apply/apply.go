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
	Busy            // its app is running: left for the next apply
)

func (s State) String() string {
	return [...]string{"unchanged", "new", "changed", "conflict", "busy"}[s]
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
		if f.Busy {
			// Its app is running and would write its own copy back: later.
			changes = append(changes, Change{File: f, State: Busy})
			continue
		}
		disk, err := os.ReadFile(f.Path)
		if f.Merge != "" && err == nil {
			changes = append(changes, Change{File: f, State: planShared(f, disk, owned[f.Path])})
			continue
		}
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

// planShared: a file shared with its app is unchanged when myarch's keys
// are as rendered, whatever else is in it; a conflict when one of them was
// changed by someone else since myarch wrote it, or, the first time, when
// the file has its own value for one of them (a person's kdeglobals).
func planShared(f render.File, disk []byte, owned string) State {
	if !readable(f.Merge, disk) {
		return Conflict // never rewritten blind: --adopt backs it up first
	}
	want, have := keysOf(f.Merge, string(f.Content)), keysOf(f.Merge, string(disk))
	same := true
	for k, v := range want {
		if have[k] != v {
			same = false
		}
	}
	// A key myarch no longer manages, still in a person's file: it goes.
	if drop := noLonger(owned, want); same && dropKeys(f.Merge, string(disk), drop) != string(disk) {
		same = false
	}
	switch {
	case same:
		return Unchanged
	case IsShared(owned):
		if Edited(owned, disk) {
			return Conflict
		}
		return Changed
	case owned != "" && Sum(disk) == owned:
		return Changed // written whole by an older myarch, untouched since
	}
	for k, v := range want {
		if hv, ok := have[k]; ok && hv != v {
			return Conflict // its own value: theirs until --adopt
		}
	}
	return Changed // only keys it doesn't have yet
}

// noLonger: the keys myarch wrote last (owned) that it doesn't manage
// anymore (not in want), with the values it wrote.
func noLonger(owned string, want map[string]string) map[string]string {
	old, _, ok := fromSharedOwned(owned)
	if !ok {
		return nil
	}
	out := map[string]string{}
	for k, v := range old {
		if _, still := want[k]; !still {
			out[k] = v
		}
	}
	return out
}

type Result struct {
	Written []Change
	Backups map[string]string // path -> backup path
	Removed []string
	Kept    []string // orphans left in place because they were edited
	Shared  []string // orphans shared with their app: left in place, released
}

// WriteShared writes a shared file through a symlink (dotfiles managed by
// stow or chezmoi), not over it.
func WriteShared(path string, b []byte) error {
	if real, err := filepath.EvalSymlinks(path); err == nil {
		path = real
	}
	return WriteAtomic(path, b)
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
		if c.State == Busy {
			continue // untouched, still ours as it was
		}
		sum, content := Sum(c.Content), c.Content
		if c.Merge != "" {
			keys := keysOf(c.Merge, string(c.Content))
			sum = sharedOwned(c.Merge, keys)
			// Not there yet: written as rendered, comments and all.
			if disk, err := os.ReadFile(c.Path); err == nil {
				content = []byte(dropKeys(c.Merge, mergeKeys(c.Merge, string(disk), keys), noLonger(owned[c.Path], keys)))
			} else if !errors.Is(err, fs.ErrNotExist) {
				return res, err
			}
		}
		switch c.State {
		case Unchanged:
			owned[c.Path] = sum
			continue
		case Conflict:
			// A shared file keeps the app's keys: back it up, merge into it.
			bak := c.Path + ".myarch-bak-" + stamp
			if err := copyOrRename(c.Path, bak, c.Merge != ""); err != nil {
				return res, err
			}
			res.Backups[c.Path] = bak
		}
		write := WriteAtomic
		if c.Merge != "" {
			write = WriteShared
		}
		if err := write(c.Path, content); err != nil {
			return res, err
		}
		owned[c.Path] = sum
		res.Written = append(res.Written, c)
	}

	for _, p := range orphans {
		disk, err := os.ReadFile(p)
		switch {
		case errors.Is(err, fs.ErrNotExist):
		case err != nil:
			return res, err
		case IsShared(owned[p]):
			// The app's (or the person's) file: it stays. In a person's own
			// (user.js, userChrome.css), myarch's lines go with the plugin.
			old, format, _ := fromSharedOwned(owned[p])
			if kept := dropKeys(format, string(disk), old); kept != string(disk) {
				if err := WriteShared(p, []byte(kept)); err != nil {
					return res, err
				}
			}
			res.Shared = append(res.Shared, p)
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

func copyOrRename(from, to string, keep bool) error {
	if !keep {
		return os.Rename(from, to)
	}
	b, err := os.ReadFile(from)
	if err != nil {
		return err
	}
	// A browser's Preferences hold personal data: backups stay private.
	return os.WriteFile(to, b, 0o600)
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
// a file. A file that's there keeps its permissions (a browser's 0600
// Preferences); a new one is 0644.
func WriteAtomic(path string, b []byte) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	mode := os.FileMode(0o644)
	if fi, err := os.Stat(path); err == nil {
		mode = fi.Mode().Perm()
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
	if err := tmp.Chmod(mode); err != nil {
		tmp.Close()
		return err
	}
	if err := tmp.Close(); err != nil {
		return err
	}
	return os.Rename(tmp.Name(), path)
}
