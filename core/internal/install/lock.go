// Package install adds, updates and removes plugins from git, and keeps
// plugins.lock: for each one where it came from, the exact commit, and the
// capabilities the person approved. config.toml and plugins.lock are all it
// takes to put the same desktop on another machine (myarch plugins sync).
package install

import (
	"bytes"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"

	"github.com/BurntSushi/toml"

	"myarch/internal/apply"
	"myarch/internal/render"
)

var (
	idPattern  = regexp.MustCompile(`^[a-z0-9][a-z0-9-]*$`)
	fullCommit = regexp.MustCompile(`^([0-9a-f]{40}|[0-9a-f]{64})$`)
)

type Entry struct {
	ID string `toml:"id"`
	// Source is what git clones: a URL or a path.
	Source string `toml:"source"`
	// Ref is the branch or tag followed by update; empty = the default
	// branch.
	Ref    string `toml:"ref,omitempty"`
	Commit string `toml:"commit"`
	// Approved are the capabilities the person said yes to
	// (plugin.Capabilities); an update that needs more asks again.
	Approved []string `toml:"approved"`
}

type Lock struct {
	Plugins []Entry `toml:"plugin"`
}

func LockPath() string { return render.ExpandHome("~/.config/myarch/plugins.lock") }

// Dir is where plugins from git live; it's also the first place plugins
// are discovered, so one there shadows a built-in of the same id.
func Dir() string { return render.ExpandHome("~/.local/share/myarch/plugins") }

func LoadLock() (*Lock, error) {
	l := &Lock{}
	md, err := toml.DecodeFile(LockPath(), l)
	if errors.Is(err, fs.ErrNotExist) {
		return l, nil
	}
	if err != nil {
		return nil, err
	}
	if u := md.Undecoded(); len(u) > 0 {
		return nil, fmt.Errorf("%s: unknown keys: %v", LockPath(), u)
	}
	// It may come from another machine with the dotfiles: every value ends
	// up in a path or a git command.
	seen := map[string]bool{}
	for _, e := range l.Plugins {
		switch {
		case !idPattern.MatchString(e.ID):
			return nil, fmt.Errorf("%s: id %q: lowercase letters, digits and dashes", LockPath(), e.ID)
		case seen[e.ID]:
			return nil, fmt.Errorf("%s: %s is there twice", LockPath(), e.ID)
		case !fullCommit.MatchString(e.Commit):
			return nil, fmt.Errorf("%s: %s: commit %q isn't a full commit id", LockPath(), e.ID, e.Commit)
		case e.Source == "" || strings.HasPrefix(e.Source, "-"):
			return nil, fmt.Errorf("%s: %s: source %q", LockPath(), e.ID, e.Source)
		}
		if err := CheckRef(e.Ref); err != nil {
			return nil, fmt.Errorf("%s: %s: %w", LockPath(), e.ID, err)
		}
		seen[e.ID] = true
	}
	return l, nil
}

func (l *Lock) Get(id string) *Entry {
	for i := range l.Plugins {
		if l.Plugins[i].ID == id {
			return &l.Plugins[i]
		}
	}
	return nil
}

func (l *Lock) Put(e Entry) {
	sort.Strings(e.Approved)
	if old := l.Get(e.ID); old != nil {
		*old = e
		return
	}
	l.Plugins = append(l.Plugins, e)
	sort.Slice(l.Plugins, func(i, j int) bool { return l.Plugins[i].ID < l.Plugins[j].ID })
}

func (l *Lock) Delete(id string) {
	out := l.Plugins[:0]
	for _, e := range l.Plugins {
		if e.ID != id {
			out = append(out, e)
		}
	}
	l.Plugins = out
}

func (l *Lock) Save() error {
	var buf bytes.Buffer
	buf.WriteString("# Plugins installed from git: source, commit and what you approved.\n" +
		"# Written by myarch plugins add/update/remove; with config.toml it\n" +
		"# reproduces this desktop elsewhere (myarch plugins sync).\n\n")
	if err := toml.NewEncoder(&buf).Encode(l); err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(LockPath()), 0o755); err != nil {
		return err
	}
	return apply.WriteAtomic(LockPath(), buf.Bytes())
}
