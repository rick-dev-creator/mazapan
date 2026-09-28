package install

import (
	"bytes"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strings"
	"time"

	"myarch/internal/plugin"
)

// git runs with none of the person's or the system's git config (no
// hooks, filters, fsmonitor or aliases a repository could lean on) and
// never stops to ask for a password.
func git(dir string, args ...string) (string, error) {
	out, err := gitEnv(dir, nil, args...)
	return strings.TrimSpace(out), err
}

func gitEnv(dir string, env []string, args ...string) (string, error) {
	full := append([]string{"-C", dir, "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=false",
		"-c", "core.symlinks=false", "-c", "protocol.ext.allow=never"}, args...)
	cmd := exec.Command("git", full...)
	cmd.Env = append(os.Environ(), "GIT_CONFIG_GLOBAL=/dev/null", "GIT_CONFIG_NOSYSTEM=1",
		"GIT_TERMINAL_PROMPT=0", "GIT_ASKPASS=true", "GIT_LFS_SKIP_SMUDGE=1", "LC_ALL=C")
	cmd.Env = append(cmd.Env, env...)
	var out, errb bytes.Buffer
	cmd.Stdout, cmd.Stderr = &out, &errb
	if err := cmd.Run(); err != nil {
		msg := strings.TrimSpace(errb.String())
		if msg == "" {
			msg = err.Error()
		}
		return "", fmt.Errorf("git %s: %s", args[0], msg)
	}
	return out.String(), nil
}

// ParseSource splits "url#ref" (a branch, tag or commit).
func ParseSource(s string) (source, ref string, err error) {
	source, ref, _ = strings.Cut(s, "#")
	if source == "" || strings.HasPrefix(source, "-") {
		return "", "", fmt.Errorf("%q is not a git URL or folder", s)
	}
	if err := CheckRef(ref); err != nil {
		return "", "", err
	}
	return source, ref, nil
}

var refPattern = regexp.MustCompile(`^[A-Za-z0-9._/+-]*$`)

// CheckRef refuses what can't be a branch, tag or commit, or could be
// taken for a git option.
func CheckRef(ref string) error {
	if strings.HasPrefix(ref, "-") || strings.Contains(ref, "..") || !refPattern.MatchString(ref) {
		return fmt.Errorf("%q is not a branch, tag or commit", ref)
	}
	return nil
}

// Head is the commit a checkout is at.
func Head(dir string) (string, error) { return git(dir, "rev-parse", "HEAD") }

// safe refuses a commit with symlinks (they'd pull files from outside the
// commit into templates) or submodules (unpinned code).
func safe(dir, commit string) error {
	out, err := git(dir, "ls-tree", "-r", "--full-tree", commit)
	if err != nil {
		return err
	}
	for _, l := range strings.Split(out, "\n") {
		name := l[strings.IndexByte(l, '\t')+1:]
		switch {
		case strings.HasPrefix(l, "120000 "):
			return fmt.Errorf("it has a symlink (%s); plugins from git can't", name)
		case strings.HasPrefix(l, "160000 "):
			return fmt.Errorf("it has a submodule (%s); plugins from git can't", name)
		}
	}
	return nil
}

// Changed lists the files that differ from the commit: edited, new, even
// ignored ones (every *.tmpl in the folder is parsed). It hashes the
// folder into a fresh index and compares trees, so no index flag
// (assume-unchanged, skip-worktree) can hide a change.
func Changed(dir string) ([]string, error) {
	f, err := os.CreateTemp("", "myarch-index-")
	if err != nil {
		return nil, err
	}
	f.Close()
	os.Remove(f.Name()) // git makes it
	defer os.Remove(f.Name())
	env := []string{"GIT_INDEX_FILE=" + f.Name()}
	if _, err := gitEnv(dir, env, "add", "--all", "--force", "--", "."); err != nil {
		return nil, err
	}
	tree, err := gitEnv(dir, env, "write-tree")
	if err != nil {
		return nil, err
	}
	tree = strings.TrimSpace(tree)
	head, err := git(dir, "rev-parse", "HEAD^{tree}")
	if err != nil {
		return nil, err
	}
	if tree == head {
		return nil, nil
	}
	out, err := git(dir, "diff-tree", "-r", "--name-only", "--no-renames", head, tree)
	if err != nil || out == "" {
		return []string{"?"}, err
	}
	return strings.Split(out, "\n"), nil
}

var hexCommit = regexp.MustCompile(`^[0-9a-f]{7,64}$`)

// resolve finds the commit of ref after a fetch: a branch of origin, a tag,
// or a commit; no ref is the default branch.
func resolve(dir, ref string) (string, error) {
	if err := CheckRef(ref); err != nil {
		return "", err
	}
	var tries []string
	if ref == "" {
		tries = []string{"refs/remotes/origin/HEAD"}
	} else {
		tries = []string{"refs/remotes/origin/" + ref, "refs/tags/" + ref}
		if hexCommit.MatchString(ref) {
			tries = append(tries, ref)
		}
	}
	for _, t := range tries {
		if c, err := git(dir, "rev-parse", "--verify", "--quiet", "--end-of-options", t+"^{commit}"); err == nil && c != "" {
			return c, nil
		}
	}
	if ref == "" {
		return "", errors.New("the repository has no default branch")
	}
	return "", fmt.Errorf("no branch, tag or commit %q", ref)
}

func checkout(dir, commit string) error {
	_, err := git(dir, "-c", "advice.detachedHead=false", "checkout", "--quiet", "--detach", commit)
	return err
}

// fetch gets what's new from where the plugin came from.
func fetch(dir string) error {
	_, err := git(dir, "fetch", "--quiet", "--tags", "--force", "origin")
	return err
}

// fetchCommit makes sure a commit is there, asking for it by name when
// it's on no branch or tag any more.
func fetchCommit(dir, commit string) error {
	if _, err := git(dir, "cat-file", "-e", "--end-of-options", commit+"^{commit}"); err == nil {
		return nil
	}
	if _, err := git(dir, "fetch", "--quiet", "origin", "--end-of-options", commit); err != nil {
		return fmt.Errorf("commit %s is gone from where it came from", short(commit))
	}
	return nil
}

// stage makes a staging folder beside the plugin folder, not in it:
// discovery must not see it, and the move into place must be a rename on
// the same filesystem. Leftovers of interrupted runs go.
func stage() (string, error) {
	base := filepath.Dir(Dir())
	if err := os.MkdirAll(base, 0o755); err != nil {
		return "", err
	}
	old, _ := filepath.Glob(filepath.Join(base, "plugin-add-*"))
	for _, o := range old {
		if st, err := os.Stat(o); err == nil && time.Since(st.ModTime()) > time.Hour {
			os.RemoveAll(o)
		}
	}
	return os.MkdirTemp(base, "plugin-add-")
}

// Staged is a plugin cloned next to the plugin folder, not in it yet: it
// can be looked at (capabilities, requirements) before it's accepted.
type Staged struct {
	Plugin *plugin.Plugin
	Commit string
	tmp    string
}

func (s *Staged) Discard() { os.RemoveAll(s.tmp) }

// Accept moves it into the plugin folder under its id.
func (s *Staged) Accept() error {
	dest := filepath.Join(Dir(), s.Plugin.ID())
	if err := os.MkdirAll(Dir(), 0o755); err != nil {
		return err
	}
	if _, err := os.Lstat(dest); err == nil {
		return fmt.Errorf("%s already exists", dest)
	}
	if err := os.Rename(s.Plugin.Dir, dest); err != nil {
		return err
	}
	s.Plugin.Dir = dest
	os.RemoveAll(s.tmp)
	return nil
}

// Clone fetches source at ref (or at commit, when given: sync) into a
// staging folder and reads its manifest.
func Clone(source, ref, commit string) (*Staged, error) {
	if st, err := os.Stat(source); err == nil && st.IsDir() {
		// A folder on this machine: git runs elsewhere, so make it absolute.
		source, _ = filepath.Abs(source)
	}
	tmp, err := stage()
	if err != nil {
		return nil, err
	}
	s := &Staged{tmp: tmp}
	fail := func(err error) (*Staged, error) {
		s.Discard()
		return nil, err
	}
	repo := filepath.Join(tmp, "repo")
	if _, err := git(tmp, "clone", "--quiet", "--no-checkout", "--", source, repo); err != nil {
		return fail(err)
	}
	want := commit
	if want == "" {
		if want, err = resolve(repo, ref); err != nil {
			return fail(err)
		}
	} else if err := fetchCommit(repo, want); err != nil {
		return fail(err)
	}
	if err := safe(repo, want); err != nil {
		return fail(err)
	}
	if err := checkout(repo, want); err != nil {
		return fail(err)
	}
	if s.Commit, err = Head(repo); err != nil {
		return fail(err)
	}
	if s.Plugin, err = plugin.Load(repo); err != nil {
		// Where it came from means more than the staging folder.
		return fail(errors.New(strings.ReplaceAll(err.Error(), repo, source)))
	}
	return s, nil
}

// Move is a newer commit of an installed plugin, checked out beside it to
// be looked at; the plugin itself moves only on Keep.
type Move struct {
	Plugin   *plugin.Plugin
	From, To string
	Log      string // commits between From and To, one per line
	dir, tmp string
}

// Update fetches the latest commit of ref for an installed plugin. It
// returns nil when it's already there. The checkout must be what the lock
// says.
func Update(e *Entry, ref string) (*Move, error) {
	dir := filepath.Join(Dir(), e.ID)
	if err := Verify(dir, e, nil); err != nil {
		return nil, err
	}
	if err := fetch(dir); err != nil {
		return nil, err
	}
	to, err := resolve(dir, ref)
	if err != nil {
		return nil, err
	}
	if to == e.Commit {
		return nil, nil
	}
	if err := safe(dir, to); err != nil {
		return nil, err
	}
	tmp, err := stage()
	if err != nil {
		return nil, err
	}
	m := &Move{From: e.Commit, To: to, dir: dir, tmp: tmp}
	m.Log, _ = git(dir, "log", "--oneline", "--no-decorate", "-n", "20", e.Commit+".."+to)
	wt := filepath.Join(tmp, e.ID)
	if _, err := git(dir, "worktree", "add", "--quiet", "--detach", wt, to); err != nil {
		m.Discard()
		return nil, err
	}
	if m.Plugin, err = plugin.Load(wt); err != nil {
		m.Discard()
		return nil, err
	}
	if m.Plugin.ID() != e.ID {
		m.Discard()
		return nil, fmt.Errorf("the new version calls itself %q, not %q", m.Plugin.ID(), e.ID)
	}
	return m, nil
}

// Discard drops the look at the new version; the plugin stays as it was.
func (m *Move) Discard() {
	git(m.dir, "worktree", "remove", "--force", filepath.Join(m.tmp, filepath.Base(m.dir)))
	os.RemoveAll(m.tmp)
	git(m.dir, "worktree", "prune")
}

// Keep moves the plugin to the new commit.
func (m *Move) Keep() error {
	m.Discard()
	if err := checkout(m.dir, m.To); err != nil {
		return err
	}
	m.Plugin.Dir = m.dir
	return nil
}

// Back puts the plugin back at From, after a Keep whose lock couldn't be
// saved.
func (m *Move) Back() error { return checkout(m.dir, m.From) }

// Goto moves a clean checkout to the locked commit (sync).
func Goto(e *Entry) (*plugin.Plugin, error) {
	dir := filepath.Join(Dir(), e.ID)
	if files, err := Changed(dir); err != nil {
		return nil, err
	} else if len(files) > 0 {
		return nil, changedError(e.ID, files)
	}
	if err := fetchCommit(dir, e.Commit); err != nil {
		if err := fetch(dir); err != nil {
			return nil, err
		}
		if err := fetchCommit(dir, e.Commit); err != nil {
			return nil, err
		}
	}
	if err := safe(dir, e.Commit); err != nil {
		return nil, err
	}
	if err := checkout(dir, e.Commit); err != nil {
		return nil, err
	}
	return plugin.Load(dir)
}

func changedError(id string, files []string) error {
	if len(files) > 5 {
		files = append(files[:5], "…")
	}
	return fmt.Errorf("%s was changed outside myarch (%s); put it back with git -C %s stash --all, or remove and add it again",
		id, strings.Join(files, ", "), filepath.Join(Dir(), id))
}

// Verify says whether an installed plugin is what plugins.lock says: at its
// commit, unchanged, and needing nothing that wasn't approved. p is the
// loaded plugin (nil: don't check capabilities).
func Verify(dir string, e *Entry, p *plugin.Plugin) error {
	head, err := Head(dir)
	if err != nil {
		return fmt.Errorf("%s: %w", e.ID, err)
	}
	if head != e.Commit {
		return fmt.Errorf("%s is at %s, but plugins.lock says %s (run: myarch plugins sync)", e.ID, short(head), short(e.Commit))
	}
	if err := safe(dir, head); err != nil {
		return fmt.Errorf("%s: %w", e.ID, err)
	}
	files, err := Changed(dir)
	if err != nil {
		return fmt.Errorf("%s: %w", e.ID, err)
	}
	if len(files) > 0 {
		return changedError(e.ID, files)
	}
	if p != nil {
		if extra := plugin.NewCapabilities(e.Approved, p.Capabilities()); len(extra) > 0 {
			return fmt.Errorf("%s needs what you didn't approve:\n    %s", e.ID, strings.Join(extra, "\n    "))
		}
	}
	return nil
}

func short(c string) string {
	if len(c) > 10 {
		return c[:10]
	}
	return c
}
