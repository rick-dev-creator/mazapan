package plugin

import (
	"fmt"
	"regexp"
	"strconv"
	"strings"
)

// A requirement: "shell-bar", "shell-bar >= 0.2", "theme-gtk = 1.0.0".
type Requirement struct {
	ID      string
	Op      string // "", ">=", ">", "=", "<=", "<"
	Version string
}

func (r Requirement) String() string {
	if r.Op == "" {
		return r.ID
	}
	return r.ID + " " + r.Op + " " + r.Version
}

var requirement = regexp.MustCompile(`^\s*([a-z0-9][a-z0-9-]*)\s*(?:(>=|<=|>|<|=)\s*(\S+))?\s*$`)

func ParseRequirement(s string) (Requirement, error) {
	m := requirement.FindStringSubmatch(s)
	if m == nil {
		return Requirement{}, fmt.Errorf("requires %q: use \"id\" or \"id >= 1.2\"", s)
	}
	if m[2] != "" {
		if _, err := parseVersion(m[3]); err != nil {
			return Requirement{}, fmt.Errorf("requires %q: %w", s, err)
		}
	}
	return Requirement{ID: m[1], Op: m[2], Version: m[3]}, nil
}

// Satisfied by a plugin at version v?
func (r Requirement) Satisfied(v string) bool {
	if r.Op == "" {
		return true
	}
	c := compareVersions(v, r.Version)
	switch r.Op {
	case ">=":
		return c >= 0
	case ">":
		return c > 0
	case "=":
		return c == 0
	case "<=":
		return c <= 0
	}
	return c < 0
}

var versionPattern = regexp.MustCompile(`^v?[0-9]+(\.[0-9]+)*$`)

func parseVersion(v string) ([]int, error) {
	if !versionPattern.MatchString(v) {
		return nil, fmt.Errorf("version %q is not numbers and dots", v)
	}
	var out []int
	for _, p := range strings.Split(strings.TrimPrefix(v, "v"), ".") {
		n, _ := strconv.Atoi(p)
		out = append(out, n)
	}
	return out, nil
}

// compareVersions: -1, 0 or 1; missing parts count as 0 (1.2 = 1.2.0).
func compareVersions(a, b string) int {
	x, _ := parseVersion(a)
	y, _ := parseVersion(b)
	for i := 0; i < len(x) || i < len(y); i++ {
		var p, q int
		if i < len(x) {
			p = x[i]
		}
		if i < len(y) {
			q = y[i]
		}
		if p != q {
			if p < q {
				return -1
			}
			return 1
		}
	}
	return 0
}

// A Problem is a requirement that isn't met.
type Problem struct {
	Plugin, Requires string
	Text             string
}

func (p Problem) String() string { return p.Text }

// Unmet lists, for the enabled plugins, what they require that isn't
// there: missing, disabled, or at a version that doesn't do. all is every
// plugin found, enabled or not; broken are those that don't load.
func Unmet(enabled, all []*Plugin, broken map[string]error) []Problem {
	on := map[string]*Plugin{}
	for _, p := range enabled {
		on[p.ID()] = p
	}
	found := map[string]*Plugin{}
	for _, p := range all {
		found[p.ID()] = p
	}
	var out []Problem
	for _, p := range enabled {
		for _, s := range p.Meta.Requires {
			r, _ := ParseRequirement(s) // checked when loaded
			dep, ok := on[r.ID]
			pr := Problem{Plugin: p.ID(), Requires: r.String()}
			switch {
			case !ok && broken[r.ID] != nil:
				pr.Text = fmt.Sprintf("%s requires %s, which doesn't load", p.ID(), r)
			case !ok && found[r.ID] != nil:
				pr.Text = fmt.Sprintf("%s requires %s, which is disabled", p.ID(), r)
			case !ok:
				pr.Text = fmt.Sprintf("%s requires %s, which isn't installed", p.ID(), r)
			case !r.Satisfied(dep.Meta.Version):
				pr.Text = fmt.Sprintf("%s requires %s; %s is %s", p.ID(), r, r.ID, dep.Meta.Version)
			default:
				continue
			}
			out = append(out, pr)
		}
	}
	return out
}

// Dependents are the plugins in enabled that require id.
func Dependents(id string, enabled []*Plugin) []string {
	var out []string
	for _, p := range enabled {
		for _, s := range p.Meta.Requires {
			if r, err := ParseRequirement(s); err == nil && r.ID == id && p.ID() != id {
				out = append(out, p.ID())
				break
			}
		}
	}
	return out
}
