package plugin

import (
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"
	"text/template/parse"
)

// Capabilities are what a plugin can do, worked out from its manifest, not
// taken from its word: the code it puts where code runs, the commands it
// runs and when, the files it writes, the packages it needs. This is what a
// person approves when adding a plugin from git, and what an update can't
// grow without asking.
//
// A command is shown as it will run: {{template}} calls inlined and
// settings replaced by their defaults, so that changing a define or a
// default changes the capability too. Only the theme (colors, fonts) and
// the places of each targets are left as template, and neither comes from
// the plugin.
func (p *Plugin) Capabilities() []string {
	caps, err := p.capabilities()
	if err != nil {
		// load() already refused plugins whose commands don't resolve.
		return []string{"(unreadable: " + err.Error() + ")"}
	}
	return caps
}

func (p *Plugin) capabilities() ([]string, error) {
	defs, err := p.defines()
	if err != nil {
		return nil, err
	}
	set := map[string]bool{}
	add := func(s string) { set[s] = true }
	command := func(kind, src string) error {
		if src == "" {
			return nil
		}
		cmd, err := p.resolve(src, defs)
		if err != nil {
			return fmt.Errorf("%s %q: %w", kind, src, err)
		}
		add("runs " + kind + ": " + cmd)
		return nil
	}
	for _, t := range p.Targets {
		add(fileCapability(t))
		if err := command("after writing", t.Reload); err != nil {
			return nil, err
		}
	}
	for _, c := range p.Checks {
		if err := command("as a health check", c.Run); err != nil {
			return nil, err
		}
	}
	for _, a := range p.Actions {
		if err := command("when you pick it", a.Run); err != nil {
			return nil, err
		}
	}
	for _, pkg := range p.Packages.Pacman {
		add("needs the package " + pkg)
	}
	out := make([]string, 0, len(set))
	for s := range set {
		out = append(out, s)
	}
	// The riskiest first: code, then commands, then files, then packages.
	rank := func(s string) int {
		switch {
		case strings.HasPrefix(s, "runs "):
			return 1
		case strings.HasPrefix(s, "writes "), strings.HasPrefix(s, "changes "):
			return 2
		case strings.HasPrefix(s, "needs "):
			return 3
		}
		return 0
	}
	sort.Slice(out, func(i, j int) bool {
		if a, b := rank(out[i]), rank(out[j]); a != b {
			return a < b
		}
		return out[i] < out[j]
	})
	return out, nil
}

// fileCapability says what writing a target amounts to. Nearly any config
// can run commands (foot's shell=, hyprlock's cmd[], a .desktop's Exec=,
// VS Code's terminal profiles, Firefox's autoconfig), so a file is full
// access unless it's a kind known to hold only looks.
func fileCapability(t Target) string {
	where := filepath.Clean(t.Output)
	path := where
	if len(t.Each) > 0 {
		where = t.Output + " in each of " + strings.Join(t.Each, ", ")
		// Classify where it really lands: each's places are the markers'
		// folders.
		path = filepath.Join(filepath.Dir(t.Each[0]), t.Output)
	}
	ext := filepath.Ext(path)
	switch {
	case ext == ".qml" || strings.Contains(path, "/quickshell/"):
		return "full access, code in the shell (QML): " + where
	case ext == ".lua" && strings.Contains(path, "/hypr/"):
		return "full access, code in Hyprland (Lua): " + where
	case filepath.Base(path) == "user.js": // Firefox prefs, JavaScript in name only
	case ext == ".lua" || ext == ".vim" || ext == ".py" || ext == ".sh" || strings.Contains(ext, "js") ||
		strings.Contains(path, "/bin/"):
		return "full access, code: " + where
	case inert(path):
		if t.Merge != "" {
			return "changes some settings in " + where
		}
		return "writes " + where
	}
	return "full access, a config that can run commands: " + where
}

// inert: files that only hold looks: stylesheets and color schemes.
func inert(path string) bool {
	switch filepath.Ext(path) {
	case ".css", ".qss", ".theme", ".colors", ".svg", ".png", ".jpg":
		return true
	}
	return strings.Contains(path, "/qt6ct/colors/") || strings.Contains(path, "/qt5ct/colors/") ||
		strings.HasSuffix(path, "/gtk-3.0/settings.ini") || strings.HasSuffix(path, "/gtk-4.0/settings.ini")
}

// defines are the {{define}} blocks of every *.tmpl, which commands can
// call. Later files win, as with the renderer's ParseGlob.
func (p *Plugin) defines() (map[string]*parse.Tree, error) {
	files, _ := filepath.Glob(filepath.Join(p.Dir, "*.tmpl"))
	defs := map[string]*parse.Tree{}
	for _, f := range files {
		b, err := os.ReadFile(f)
		if err != nil {
			return nil, err
		}
		tr := parse.New(filepath.Base(f))
		tr.Mode = parse.SkipFuncCheck
		trees := map[string]*parse.Tree{}
		if _, err := tr.Parse(string(b), "{{", "}}", trees); err != nil {
			return nil, err
		}
		for name, t := range trees {
			if name != filepath.Base(f) {
				defs[name] = t
			}
		}
	}
	return defs, nil
}

var settingRef = regexp.MustCompile(`\.Settings\.([A-Za-z0-9_]+)`)

// resolve writes a command out as it will run (see Capabilities).
func (p *Plugin) resolve(src string, defs map[string]*parse.Tree) (string, error) {
	tr := parse.New("command")
	tr.Mode = parse.SkipFuncCheck
	trees := map[string]*parse.Tree{}
	if _, err := tr.Parse(src, "{{", "}}", trees); err != nil {
		return "", err
	}
	if len(trees) != 1 {
		return "", fmt.Errorf("no {{define}} in a command")
	}
	var b strings.Builder
	if err := p.walk(&b, trees["command"].Root, defs, 0); err != nil {
		return "", err
	}
	out := b.String()
	// Settings used some other way than {{.Settings.x}}: say their defaults.
	var with []string
	seen := map[string]bool{}
	for _, m := range settingRef.FindAllStringSubmatch(out, -1) {
		if v, ok := p.Settings[m[1]]; ok && !seen[m[1]] {
			seen[m[1]] = true
			with = append(with, fmt.Sprintf("%s = %#v", m[1], v))
		}
	}
	if len(with) > 0 {
		sort.Strings(with)
		out += "  (with " + strings.Join(with, ", ") + ")"
	}
	return out, nil
}

func (p *Plugin) walk(b *strings.Builder, n parse.Node, defs map[string]*parse.Tree, depth int) error {
	if depth > 10 {
		return fmt.Errorf("{{template}} nested too deep")
	}
	switch n := n.(type) {
	case nil:
	case *parse.ListNode:
		if n == nil {
			return nil
		}
		for _, c := range n.Nodes {
			if err := p.walk(b, c, defs, depth); err != nil {
				return err
			}
		}
	case *parse.TextNode:
		b.Write(n.Text)
	case *parse.CommentNode:
	case *parse.ActionNode:
		if err := translated(n.Pipe); err != nil {
			return err
		}
		if v, ok := p.plainSetting(n.Pipe); ok {
			fmt.Fprint(b, v)
		} else {
			b.WriteString(n.String())
		}
	case *parse.TemplateNode:
		d := defs[n.Name]
		if d == nil {
			return fmt.Errorf("no {{define %q}} in the plugin's templates", n.Name)
		}
		return p.walk(b, d.Root, defs, depth+1)
	case *parse.IfNode:
		return p.branch(b, "if", &n.BranchNode, defs, depth)
	case *parse.RangeNode:
		return p.branch(b, "range", &n.BranchNode, defs, depth)
	case *parse.WithNode:
		return p.branch(b, "with", &n.BranchNode, defs, depth)
	default:
		b.WriteString(n.String())
	}
	return nil
}

func (p *Plugin) branch(b *strings.Builder, kw string, n *parse.BranchNode, defs map[string]*parse.Tree, depth int) error {
	if err := translated(n.Pipe); err != nil {
		return err
	}
	b.WriteString("{{" + kw + " " + n.Pipe.String() + "}}")
	if err := p.walk(b, n.List, defs, depth); err != nil {
		return err
	}
	if n.ElseList != nil {
		b.WriteString("{{else}}")
		if err := p.walk(b, n.ElseList, defs, depth); err != nil {
			return err
		}
	}
	b.WriteString("{{end}}")
	return nil
}

// plainSetting: {{.Settings.x}} alone is shown as x's default.
func (p *Plugin) plainSetting(pipe *parse.PipeNode) (any, bool) {
	if len(pipe.Decl) > 0 || len(pipe.Cmds) != 1 || len(pipe.Cmds[0].Args) != 1 {
		return nil, false
	}
	f, ok := pipe.Cmds[0].Args[0].(*parse.FieldNode)
	if !ok || len(f.Ident) != 2 || f.Ident[0] != "Settings" {
		return nil, false
	}
	v, ok := p.Settings[f.Ident[1]]
	return v, ok
}

// translated refuses t and tq in commands: the text comes from the
// plugin's locale files, which no capability shows, and neither is quoted
// for a shell.
func translated(pipe *parse.PipeNode) error {
	var err error
	var visit func(parse.Node)
	visit = func(n parse.Node) {
		switch n := n.(type) {
		case *parse.PipeNode:
			for _, c := range n.Cmds {
				visit(c)
			}
		case *parse.CommandNode:
			for _, a := range n.Args {
				visit(a)
			}
		case *parse.IdentifierNode:
			if n.Ident == "t" || n.Ident == "tq" {
				err = fmt.Errorf("commands can't use translated text (%s)", n.Ident)
			}
		}
	}
	if pipe != nil {
		visit(pipe)
	}
	return err
}

// NewCapabilities: the ones in now that weren't approved.
func NewCapabilities(approved, now []string) []string {
	ok := map[string]bool{}
	for _, a := range approved {
		ok[a] = true
	}
	var out []string
	for _, n := range now {
		if !ok[n] {
			out = append(out, n)
		}
	}
	return out
}
