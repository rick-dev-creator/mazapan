// Package pacman is the little of pacman that updates need: what's pending,
// what's installed, and putting the previous versions back from the cache.
package pacman

import (
	"bufio"
	"bytes"
	"errors"
	"fmt"
	"net/http"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strings"
	"time"
)

// Change is one package going from one version to another. From is empty
// for a newly installed package, To for a removed one.
type Change struct {
	Name string `json:"name"`
	From string `json:"from,omitempty"`
	To   string `json:"to,omitempty"`
}

// Pending lists available updates without touching the system's package
// database (checkupdates syncs a private copy).
func Pending() ([]Change, error) {
	cmd := exec.Command("checkupdates")
	var out, stderr bytes.Buffer
	cmd.Stdout, cmd.Stderr = &out, &stderr
	err := cmd.Run()
	var exit *exec.ExitError
	switch {
	case errors.As(err, &exit) && exit.ExitCode() == 2:
		return nil, nil // nothing to update
	case errors.Is(err, exec.ErrNotFound):
		return nil, fmt.Errorf("checkupdates not found: install pacman-contrib")
	case err != nil:
		return nil, fmt.Errorf("checkupdates: %v: %s", err, strings.TrimSpace(stderr.String()))
	}
	return parsePending(out.String()), nil
}

// parsePending reads checkupdates' "name old -> new" lines.
func parsePending(s string) []Change {
	var out []Change
	sc := bufio.NewScanner(strings.NewReader(s))
	for sc.Scan() {
		f := strings.Fields(sc.Text())
		if len(f) >= 4 && f[2] == "->" {
			out = append(out, Change{Name: f[0], From: f[1], To: f[3]})
		}
	}
	return out
}

// Installed maps every installed package to its version.
func Installed() (map[string]string, error) {
	out, err := exec.Command("pacman", "-Q").Output()
	if err != nil {
		return nil, fmt.Errorf("pacman -Q: %w", err)
	}
	m := map[string]string{}
	sc := bufio.NewScanner(bytes.NewReader(out))
	for sc.Scan() {
		if f := strings.Fields(sc.Text()); len(f) == 2 {
			m[f[0]] = f[1]
		}
	}
	return m, nil
}

// Diff lists what changed between two Installed snapshots, by name.
func Diff(before, after map[string]string) []Change {
	var out []Change
	for name, v := range after {
		if old, ok := before[name]; !ok || old != v {
			out = append(out, Change{Name: name, From: before[name], To: v})
		}
	}
	for name, v := range before {
		if _, ok := after[name]; !ok {
			out = append(out, Change{Name: name, From: v})
		}
	}
	sort.Slice(out, func(i, j int) bool { return out[i].Name < out[j].Name })
	return out
}

// CacheDirs are pacman's package caches (pacman-conf), or the default.
func CacheDirs() []string {
	out, err := exec.Command("pacman-conf", "CacheDir").Output()
	if err != nil || len(bytes.TrimSpace(out)) == 0 {
		return []string{"/var/cache/pacman/pkg/"}
	}
	return strings.Fields(string(out))
}

// CachedFile finds the package file for name at version in the caches.
// "foot-1.28.0-2-x86_64.pkg.tar.zst" matches foot 1.28.0-2 but not
// foot-terminfo, because the version has to follow the name directly.
func CachedFile(dirs []string, name, version string) (string, bool) {
	for _, d := range dirs {
		matches, _ := filepath.Glob(filepath.Join(d, name+"-"+version+"-*.pkg.tar.*"))
		for _, m := range matches {
			if !strings.HasSuffix(m, ".sig") {
				return m, true
			}
		}
	}
	return "", false
}

// run executes a pacman command as root, attached to the terminal so sudo
// can ask for a password and pacman can show its progress.
func run(args ...string) error {
	cmd := exec.Command("sudo", append([]string{"pacman"}, args...)...)
	cmd.Stdin, cmd.Stdout, cmd.Stderr = os.Stdin, os.Stdout, os.Stderr
	return cmd.Run()
}

// Upgrade runs a full system upgrade without asking: the caller has
// already asked. A question that needs a real answer (a conflict) makes
// pacman stop without changing anything.
func Upgrade() error {
	return run("-Syu", "--noconfirm")
}

// ArchiveURL finds the package file for name at version on the Arch Linux
// Archive, which keeps every version ever published (signed, so pacman
// verifies it like any other download). The architecture isn't known, so
// it tries x86_64 and then any.
func ArchiveURL(name, version string) (string, bool) {
	if name == "" {
		return "", false
	}
	client := &http.Client{Timeout: 8 * time.Second}
	for _, arch := range []string{"x86_64", "any"} {
		u := fmt.Sprintf("https://archive.archlinux.org/packages/%c/%s/%s-%s-%s.pkg.tar.zst",
			name[0], name, name, url.PathEscape(version), arch)
		resp, err := client.Head(u)
		if err != nil {
			return "", false
		}
		resp.Body.Close()
		if resp.StatusCode == http.StatusOK {
			return u, true
		}
	}
	return "", false
}

// Revert puts back the previous version of every changed package, in one
// transaction so pacman keeps them consistent with each other. Previous
// versions come from pacman's cache, or from the Arch Linux Archive when
// the cache no longer has them. A package the update newly installed stays,
// unless it conflicts with one being put back (a replacement): then it's
// removed. `missing` lists what couldn't be found anywhere.
func Revert(changes []Change) (reverted int, missing []string, err error) {
	dirs := CacheDirs()
	var sources []string
	for _, c := range changes {
		if c.From == "" {
			continue
		}
		if f, ok := CachedFile(dirs, c.Name, c.From); ok {
			sources = append(sources, f)
		} else if u, ok := ArchiveURL(c.Name, c.From); ok {
			sources = append(sources, u)
		} else {
			missing = append(missing, c.Name+" "+c.From)
		}
	}
	if len(sources) == 0 {
		return 0, missing, nil
	}
	// --noconfirm answers "remove the conflicting package?" with no, which
	// would abort the whole transaction when the update replaced a package;
	// --ask=4 (ALPM_QUESTION_CONFLICT_PKG) answers it with yes.
	args := append([]string{"-U", "--noconfirm", "--ask=4"}, sources...)
	if err := run(args...); err != nil {
		return 0, missing, err
	}
	return len(sources), missing, nil
}
