package main

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"myarch/internal/config"
	"myarch/internal/install"
	"myarch/internal/plugin"
)

func cmdPlugins(args []string) error {
	yes := false
	var rest []string
	for _, a := range args {
		if a == "-y" || a == "--yes" {
			yes = true
		} else {
			rest = append(rest, a)
		}
	}
	sub := "list"
	if len(rest) > 0 {
		sub, rest = rest[0], rest[1:]
	}
	need := func(min, max int, what string) error {
		if len(rest) < min || (max >= 0 && len(rest) > max) {
			return fmt.Errorf("usage: myarch plugins %s %s", sub, what)
		}
		return nil
	}
	var err error
	switch sub {
	case "list":
		if err = need(0, 0, ""); err == nil {
			err = pluginsList()
		}
	case "show":
		if err = need(1, 1, "ID"); err == nil {
			err = pluginsShow(rest[0])
		}
	case "enable":
		if err = need(1, -1, "ID..."); err == nil {
			err = pluginsEnable(rest)
		}
	case "disable":
		if err = need(1, -1, "ID..."); err == nil {
			err = pluginsDisable(rest)
		}
	case "add":
		if err = need(1, 1, "URL[#REF] [-y]"); err == nil {
			err = pluginsAdd(rest[0], yes)
		}
	case "update":
		err = pluginsUpdate(rest, yes)
	case "remove":
		if err = need(1, -1, "ID..."); err == nil {
			err = pluginsRemove(rest)
		}
	case "sync":
		if err = need(0, 0, ""); err == nil {
			err = pluginsSync()
		}
	default:
		err = fmt.Errorf("plugins %s: list, show, enable, disable, add, update, remove or sync", sub)
	}
	return err
}

// catalog is everything the plugin commands look at.
type catalog struct {
	cfg    *config.Config
	lock   *install.Lock
	all    []*plugin.Plugin
	broken map[string]error
}

func loadCatalog() (*catalog, error) {
	cfg, err := config.Load()
	if err != nil {
		return nil, err
	}
	lock, err := install.LoadLock()
	if err != nil {
		return nil, err
	}
	all, broken := plugin.Discover(pluginDirs())
	return &catalog{cfg, lock, all, broken}, nil
}

func (c *catalog) find(id string) *plugin.Plugin {
	for _, p := range c.all {
		if p.ID() == id {
			return p
		}
	}
	return nil
}

// enabled are the plugins not disabled, besides skip.
func (c *catalog) enabled(skip ...string) []*plugin.Plugin {
	var out []*plugin.Plugin
	for _, p := range c.all {
		if !c.cfg.IsDisabled(p.ID()) && !contains(skip, p.ID()) {
			out = append(out, p)
		}
	}
	return out
}

// origin: built-in, local (a folder someone put in the plugin folder), or
// the git source it was added from.
func (c *catalog) origin(p *plugin.Plugin) string {
	if e := lockedEntry(c.lock, p); e != nil {
		return "git " + e.Source + " @ " + e.Commit[:10]
	}
	if filepath.Dir(p.Dir) == install.Dir() {
		return "local"
	}
	return "built-in"
}

func contains(list []string, s string) bool {
	for _, x := range list {
		if x == s {
			return true
		}
	}
	return false
}

func pluginsList() error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	for _, p := range c.all {
		state := "enabled"
		if c.cfg.IsDisabled(p.ID()) {
			state = "disabled"
		}
		fmt.Printf("%-18s %-8s %-9s %s\n", p.ID(), p.Meta.Version, state, p.Meta.Description)
		if o := c.origin(p); o != "built-in" {
			fmt.Printf("%-18s from %s\n", "", o)
		}
	}
	for _, id := range sortedKeys(c.broken) {
		fmt.Printf("%-18s %-8s %-9s %s\n", id, "?", "broken", c.broken[id])
	}
	if _, err := enabledPlugins(c.cfg); err != nil {
		fmt.Println()
		return err
	}
	return nil
}

func pluginsShow(id string) error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	p := c.find(id)
	if p == nil {
		if err := c.broken[id]; err != nil {
			return err
		}
		return fmt.Errorf("no plugin %q (myarch plugins lists them)", id)
	}
	state := "enabled"
	if c.cfg.IsDisabled(id) {
		state = "disabled"
	}
	fmt.Printf("%s%s%s %s, %s, %s\n", bold, p.Meta.Name, reset, p.Meta.Version, state, c.origin(p))
	if p.Meta.Description != "" {
		fmt.Println(p.Meta.Description)
	}
	e := lockedEntry(c.lock, p)
	if e != nil && e.Ref != "" {
		fmt.Printf("follows %s\n", e.Ref)
	}
	fmt.Println(tilde(p.Dir))
	if len(p.Meta.Requires) > 0 {
		fmt.Printf("\nrequires: %s\n", strings.Join(p.Meta.Requires, ", "))
	}
	if d := plugin.Dependents(id, c.all); len(d) > 0 {
		fmt.Printf("required by: %s\n", strings.Join(d, ", "))
	}
	settings, err := p.Resolve(c.cfg.Plugins[id])
	if err != nil {
		return err
	}
	if len(settings) > 0 {
		fmt.Println("\nsettings ([plugins." + id + "] in config.toml):")
		for _, k := range sortedKeys(settings) {
			mark := ""
			if _, set := c.cfg.Plugins[id][k]; set {
				mark = "  (set in config.toml)"
			}
			fmt.Printf("  %s = %#v%s\n", k, settings[k], mark)
		}
	}
	fmt.Println("\nit can:")
	for _, cap := range p.Capabilities() {
		mark := ""
		if e != nil && !contains(e.Approved, cap) {
			mark = "  (not approved)"
		}
		fmt.Printf("  - %s%s\n", cap, mark)
	}
	if err := trusted(c.lock, p); err != nil {
		fmt.Println()
		return err
	}
	return nil
}

// problemsOf keeps the problems of the given plugins.
func problemsOf(problems []plugin.Problem, ids ...string) []string {
	var out []string
	for _, pr := range problems {
		if contains(ids, pr.Plugin) {
			out = append(out, pr.Text)
		}
	}
	return out
}

func pluginsEnable(ids []string) error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	for _, id := range ids {
		if err := c.broken[id]; err != nil {
			return err
		}
		if c.find(id) == nil {
			return fmt.Errorf("no plugin %q", id)
		}
	}
	var disabled []string
	for _, d := range c.cfg.Disabled {
		if !contains(ids, d) {
			disabled = append(disabled, d)
		}
	}
	c.cfg.Disabled = disabled
	if pr := problemsOf(plugin.Unmet(c.enabled(), c.all, c.broken), ids...); len(pr) > 0 {
		return fmt.Errorf("nothing enabled:\n  %s", strings.Join(pr, "\n  "))
	}
	if err := c.cfg.Save(); err != nil {
		return err
	}
	fmt.Printf("enabled %s; apply it with: myarch apply\n", strings.Join(ids, ", "))
	return nil
}

func pluginsDisable(ids []string) error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	for _, id := range ids {
		if c.find(id) == nil && c.broken[id] == nil && !c.cfg.IsDisabled(id) {
			return fmt.Errorf("no plugin %q", id)
		}
	}
	rest := c.enabled(ids...)
	var problems []string
	for _, id := range ids {
		if d := plugin.Dependents(id, rest); len(d) > 0 {
			problems = append(problems, fmt.Sprintf("%s is needed by %s", id, strings.Join(d, ", ")))
		}
	}
	if len(problems) > 0 {
		// Everything that would have to go too, however indirectly.
		all := append([]string{}, ids...)
		for i := 0; i < len(all); i++ {
			for _, d := range plugin.Dependents(all[i], c.enabled(all...)) {
				if !contains(all, d) {
					all = append(all, d)
				}
			}
		}
		return fmt.Errorf("nothing disabled:\n  %s\n  to disable them all: myarch plugins disable %s",
			strings.Join(problems, "\n  "), strings.Join(all, " "))
	}
	for _, id := range ids {
		if !c.cfg.IsDisabled(id) {
			c.cfg.Disabled = append(c.cfg.Disabled, id)
		}
	}
	if err := c.cfg.Save(); err != nil {
		return err
	}
	fmt.Printf("disabled %s; myarch apply takes away what it generated\n", strings.Join(ids, ", "))
	return nil
}

// approve shows what a plugin could do and asks. Without a terminal to
// ask on, only -y says yes.
func approve(question string, caps []string, yes bool) (bool, error) {
	for _, cap := range caps {
		fmt.Printf("  - %s\n", cap)
	}
	if yes {
		return true, nil
	}
	if !isTerminal(os.Stdin) {
		return false, errors.New("no terminal to ask on; read the list above and approve it with -y")
	}
	return confirm(question), nil
}

func pluginsAdd(arg string, yes bool) error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	source, ref, err := install.ParseSource(arg)
	if err != nil {
		return err
	}
	if st, err := os.Stat(source); err == nil && st.IsDir() {
		source, _ = filepath.Abs(source)
	}
	fmt.Printf("fetching %s…\n", source)
	st, err := install.Clone(source, ref, "")
	if err != nil {
		return err
	}
	defer st.Discard()
	p := st.Plugin
	id := p.ID()
	switch old := c.find(id); {
	case old != nil && c.origin(old) == "built-in":
		return fmt.Errorf("it's called %q, like a built-in plugin; it can't replace it", id)
	case old != nil:
		return fmt.Errorf("%s is already installed (%s); update it with: myarch plugins update %s", id, c.origin(old), id)
	case c.broken[id] != nil:
		return fmt.Errorf("a plugin called %s is already there, and doesn't load: %w", id, c.broken[id])
	case c.lock.Get(id) != nil:
		return fmt.Errorf("%s is in plugins.lock already; install it with: myarch plugins sync", id)
	}
	if pr := problemsOf(plugin.Unmet(append(c.enabled(), p), append(c.all, p), c.broken), id); len(pr) > 0 {
		return fmt.Errorf("not installed:\n  %s", strings.Join(pr, "\n  "))
	}
	fmt.Printf("\n%s%s%s %s (%s) at %s\n", bold, p.Meta.Name, reset, p.Meta.Version, id, st.Commit[:10])
	if p.Meta.Description != "" {
		fmt.Println(p.Meta.Description)
	}
	fmt.Println("\nIt will be able to:")
	caps := p.Capabilities()
	ok, err := approve("Install it, and allow all this?", caps, yes)
	if err != nil || !ok {
		if err == nil {
			fmt.Println("nothing installed")
		}
		return err
	}
	// The lock first: a checkout in the plugin folder without its entry
	// would be refused (or worse, taken for your own).
	c.lock.Put(install.Entry{ID: id, Source: source, Ref: ref, Commit: st.Commit, Approved: caps})
	if err := c.lock.Save(); err != nil {
		return err
	}
	if err := st.Accept(); err != nil {
		c.lock.Delete(id)
		c.lock.Save()
		return err
	}
	fmt.Printf("installed %s; apply it with: myarch apply\n", id)
	return nil
}

func pluginsUpdate(args []string, yes bool) error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	refs := map[string]*string{}
	var ids []string
	for _, a := range args {
		id, ref, switchRef := strings.Cut(a, "#")
		if switchRef {
			if err := install.CheckRef(ref); err != nil {
				return err
			}
			refs[id] = &ref
		}
		ids = append(ids, id)
	}
	if len(ids) == 0 {
		for _, e := range c.lock.Plugins {
			ids = append(ids, e.ID)
		}
		if len(ids) == 0 {
			fmt.Println("no plugins from git (built-in ones update with myarch)")
			return nil
		}
	}
	// What others require first, so a newer version they need is there
	// when they're looked at.
	ids = dependenciesFirst(ids, c)
	var failed []string
	for _, id := range ids {
		if err := updateOne(c, id, refs[id], yes); err != nil {
			failed = append(failed, err.Error())
		}
	}
	if len(failed) > 0 {
		return errors.New(strings.Join(failed, "\n"))
	}
	return nil
}

func dependenciesFirst(ids []string, c *catalog) []string {
	var out []string
	done := map[string]bool{}
	var visit func(id string, depth int)
	visit = func(id string, depth int) {
		if done[id] || depth > len(ids) {
			return
		}
		if p := c.find(id); p != nil {
			for _, s := range p.Meta.Requires {
				if r, err := plugin.ParseRequirement(s); err == nil && contains(ids, r.ID) {
					visit(r.ID, depth+1)
				}
			}
		}
		if !done[id] {
			done[id] = true
			out = append(out, id)
		}
	}
	for _, id := range ids {
		visit(id, 0)
	}
	return out
}

func problemKeys(prs []plugin.Problem) map[string]bool {
	keys := map[string]bool{}
	for _, pr := range prs {
		keys[pr.Plugin+"\x00"+pr.Requires] = true
	}
	return keys
}

func updateOne(c *catalog, id string, newRef *string, yes bool) error {
	e := c.lock.Get(id)
	if e == nil {
		if p := c.find(id); p != nil {
			return fmt.Errorf("%s is %s, not from git", id, c.origin(p))
		}
		return fmt.Errorf("no plugin %q from git", id)
	}
	next := *e
	if newRef != nil {
		next.Ref = *newRef
	}
	save := func() error {
		old := *e
		c.lock.Put(next)
		if err := c.lock.Save(); err != nil {
			c.lock.Put(old)
			return err
		}
		return nil
	}
	before := problemKeys(plugin.Unmet(c.enabled(), c.all, c.broken))
	m, err := install.Update(e, next.Ref)
	if err != nil {
		return err
	}
	if m == nil {
		if next.Ref != e.Ref {
			if err := save(); err != nil {
				return err
			}
			fmt.Printf("%s: up to date, now following %s\n", id, next.Ref)
			return nil
		}
		fmt.Printf("%s: up to date\n", id)
		return nil
	}
	defer m.Discard()
	// The plugins as they would be: does everything still fit together?
	all := make([]*plugin.Plugin, 0, len(c.all))
	for _, p := range c.all {
		if p.ID() == id {
			p = m.Plugin
		}
		all = append(all, p)
	}
	after := &catalog{c.cfg, c.lock, all, c.broken}
	var broken []string
	for _, pr := range plugin.Unmet(after.enabled(), all, c.broken) {
		if !before[pr.Plugin+"\x00"+pr.Requires] {
			broken = append(broken, pr.Text)
		}
	}
	if len(broken) > 0 {
		return fmt.Errorf("%s not updated:\n  %s", id, strings.Join(broken, "\n  "))
	}
	fmt.Printf("\n%s%s%s %s → %s (%s)\n", bold, id, reset, m.From[:10], m.To[:10], m.Plugin.Meta.Version)
	for _, l := range strings.Split(m.Log, "\n") {
		if l != "" {
			fmt.Println("  " + l)
		}
	}
	caps := m.Plugin.Capabilities()
	if extra := plugin.NewCapabilities(e.Approved, caps); len(extra) > 0 {
		fmt.Println("\nThis version also wants to:")
		ok, err := approve("Update it, and allow this too?", extra, yes)
		if err != nil {
			return fmt.Errorf("%s not updated: %w", id, err)
		}
		if !ok {
			return fmt.Errorf("%s not updated", id)
		}
	}
	next.Commit, next.Approved = m.To, caps
	if err := m.Keep(); err != nil {
		return fmt.Errorf("%s not updated: %w", id, err)
	}
	if err := save(); err != nil {
		if berr := m.Back(); berr != nil {
			return fmt.Errorf("%s: %v; and it couldn't go back to %s: %v (run: myarch plugins sync)", id, err, m.From[:10], berr)
		}
		return err
	}
	// Later updates see this version (a plugin that requires it).
	c.all = all
	fmt.Printf("updated %s; apply it with: myarch apply\n", id)
	return nil
}

func pluginsRemove(ids []string) error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	for _, id := range ids {
		p := c.find(id)
		switch {
		case c.lock.Get(id) != nil:
			// From git: removable, installed or not, loading or not.
		case p != nil && c.origin(p) == "local", c.broken[id] != nil && p == nil:
			return fmt.Errorf("%s is your own plugin, not from git: delete %s yourself",
				id, tilde(filepath.Join(install.Dir(), id)))
		case p != nil:
			return fmt.Errorf("%s is built in; disable it instead: myarch plugins disable %s", id, id)
		default:
			return fmt.Errorf("no plugin %q", id)
		}
	}
	rest := c.enabled(ids...)
	for _, id := range ids {
		if d := plugin.Dependents(id, rest); len(d) > 0 {
			return fmt.Errorf("nothing removed: %s is needed by %s", id, strings.Join(d, ", "))
		}
	}
	for _, id := range ids {
		c.lock.Delete(id)
	}
	// The lock first: a folder left behind without its entry is refused.
	if err := c.lock.Save(); err != nil {
		return err
	}
	for _, id := range ids {
		if err := os.RemoveAll(filepath.Join(install.Dir(), id)); err != nil {
			return err
		}
		if _, had := c.cfg.Plugins[id]; had {
			delete(c.cfg.Plugins, id)
			fmt.Printf("dropped [plugins.%s] from config.toml\n", id)
		}
		var disabled []string
		for _, d := range c.cfg.Disabled {
			if d != id {
				disabled = append(disabled, d)
			}
		}
		c.cfg.Disabled = disabled
	}
	if err := c.cfg.Save(); err != nil {
		return err
	}
	fmt.Printf("removed %s; myarch apply takes away what it generated\n", strings.Join(ids, ", "))
	return nil
}

// pluginsSync makes the plugin folder match plugins.lock: each plugin at its
// commit, needing nothing beyond what was approved. It never asks: the
// approvals are in the lock.
func pluginsSync() error {
	c, err := loadCatalog()
	if err != nil {
		return err
	}
	var failed []string
	for i := range c.lock.Plugins {
		if msg, err := syncOne(&c.lock.Plugins[i]); err != nil {
			failed = append(failed, err.Error())
		} else {
			fmt.Printf("%-18s %s\n", c.lock.Plugins[i].ID, msg)
		}
	}
	// Checkouts the lock doesn't list (dropped on another machine) are
	// refused by apply; say so here.
	entries, _ := os.ReadDir(install.Dir())
	for _, d := range entries {
		dir := filepath.Join(install.Dir(), d.Name())
		if c.lock.Get(d.Name()) == nil && isCheckout(dir) {
			failed = append(failed, fmt.Sprintf("%s isn't in plugins.lock: delete %s, or add it again", d.Name(), tilde(dir)))
		}
	}
	if len(c.lock.Plugins) == 0 && len(failed) == 0 {
		fmt.Println("plugins.lock lists no plugins")
	}
	if len(failed) > 0 {
		return errors.New(strings.Join(failed, "\n"))
	}
	return nil
}

func syncOne(e *install.Entry) (string, error) {
	dir := filepath.Join(install.Dir(), e.ID)
	check := func(p *plugin.Plugin) error {
		if p.ID() != e.ID {
			return fmt.Errorf("%s: the commit in plugins.lock is plugin %q", e.ID, p.ID())
		}
		if extra := plugin.NewCapabilities(e.Approved, p.Capabilities()); len(extra) > 0 {
			return fmt.Errorf("%s needs what plugins.lock doesn't approve:\n    %s", e.ID, strings.Join(extra, "\n    "))
		}
		return nil
	}
	if _, err := os.Lstat(dir); errors.Is(err, os.ErrNotExist) {
		st, err := install.Clone(e.Source, "", e.Commit)
		if err != nil {
			return "", fmt.Errorf("%s: %w", e.ID, err)
		}
		defer st.Discard()
		if err := check(st.Plugin); err != nil {
			return "", err
		}
		if err := st.Accept(); err != nil {
			return "", err
		}
		return "installed", nil
	}
	if head, err := install.Head(dir); err == nil && head == e.Commit {
		p, err := plugin.Load(dir)
		if err != nil {
			return "", err
		}
		if err := install.Verify(dir, e, p); err != nil {
			return "", err
		}
		return "ok", nil
	}
	p, err := install.Goto(e)
	if err != nil {
		return "", fmt.Errorf("%s: %w", e.ID, err)
	}
	if err := check(p); err != nil {
		return "", err
	}
	return "moved to " + e.Commit[:10], nil
}
