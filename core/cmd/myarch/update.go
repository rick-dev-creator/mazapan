package main

import (
	"bufio"
	"errors"
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"syscall"
	"time"
	"unsafe"

	"myarch/internal/apply"
	"myarch/internal/health"
	"myarch/internal/news"
	"myarch/internal/pacman"
	"myarch/internal/update"
)

const (
	bold  = "\033[1m"
	dim   = "\033[2m"
	red   = "\033[31m"
	green = "\033[32m"
	amber = "\033[33m"
	reset = "\033[0m"
)

var kernel = regexp.MustCompile(`^linux(-[a-z0-9]+)?$`)

func isKernel(name string) bool {
	return kernel.MatchString(name) && !strings.HasSuffix(name, "-headers") &&
		!strings.HasSuffix(name, "-firmware") && !strings.HasSuffix(name, "-docs")
}

// critical is what the desktop can't do without: every package a plugin
// declares, plus the kernel and the graphics stack.
func critical(s *session) map[string]bool {
	m := map[string]bool{"mesa": true, "systemd": true, "glibc": true}
	for _, p := range s.plugins {
		for _, pkg := range p.Packages.Pacman {
			m[pkg] = true
		}
	}
	return m
}

func header(title string) { fmt.Printf("\n%s%s%s\n", bold, title, reset) }

// One reader for every question, so answers piped in aren't lost to a
// reader's buffer.
var stdin = bufio.NewReader(os.Stdin)

func confirm(question string) bool {
	fmt.Printf("\n%s [y/N] ", question)
	line, _ := stdin.ReadString('\n')
	a := strings.ToLower(strings.TrimSpace(line))
	return a == "y" || a == "yes" || a == "s" || a == "si" || a == "sí"
}

func cmdUpdate(args []string) error {
	fs := flag.NewFlagSet("update", flag.ExitOnError)
	yes := fs.Bool("y", false, "don't ask: update right away")
	check := fs.Bool("check", false, "show what an update would do, change nothing")
	noRollback := fs.Bool("no-rollback", false, "if a check fails, leave things as they are")
	fs.Parse(args)

	s, err := load()
	if err != nil {
		return err
	}

	// --- preview -------------------------------------------------------
	fmt.Println("Checking for updates…")
	since := update.LastSuccess()
	sinceLabel := "your last update"
	if since.IsZero() {
		since = time.Now().AddDate(0, -1, 0)
		sinceLabel = "a month ago"
	}
	// Both go to the network and don't depend on each other.
	type newsResult struct {
		items []news.Item
		err   error
	}
	newsCh := make(chan newsResult, 1)
	go func() {
		items, err := news.Since(since)
		newsCh <- newsResult{items, err}
	}()
	pending, err := pacman.Pending()
	if err != nil {
		return err
	}
	changes, orphans, owned, err := s.plan()
	if err != nil {
		return err
	}
	nr := <-newsCh
	items, newsErr := nr.items, nr.err

	crit := critical(s)
	kernelChanges := false
	if len(pending) > 0 {
		header(fmt.Sprintf("Packages (%d)", len(pending)))
		for _, p := range pending {
			mark := " "
			if crit[p.Name] || isKernel(p.Name) {
				mark = amber + "★" + reset
			}
			kernelChanges = kernelChanges || isKernel(p.Name)
			fmt.Printf("  %s %-28s %s%s%s → %s\n", mark, p.Name, dim, p.From, reset, p.To)
		}
		fmt.Printf("  %s★ your desktop depends on it: checked after the update%s\n", dim, reset)
		if kernelChanges {
			fmt.Printf("  %sThe new kernel is used after a reboot.%s\n", amber, reset)
		}
	}

	switch {
	case newsErr != nil:
		header("Arch news")
		fmt.Printf("  %scouldn't read the news (%v): check https://archlinux.org/news/ before updating%s\n", amber, newsErr, reset)
	case len(items) > 0:
		header("Arch news since " + sinceLabel)
		for _, it := range items {
			mark := " "
			if it.NeedsAction() {
				mark = red + "!" + reset
			}
			fmt.Printf("  %s %s  %s\n    %s%s%s\n", mark, it.Date.Format("2006-01-02"), it.Title, dim, it.Link, reset)
		}
	}

	// What an apply would write: busy and unreadable files wait, they're
	// not an update.
	fileChanges := 0
	for _, c := range changes {
		switch c.State {
		case apply.New, apply.Changed, apply.Conflict:
			fileChanges++
		}
	}
	if fileChanges+len(orphans) > 0 {
		header("Generated files")
		printPlan(changes, orphans, true)
	}

	if len(pending) == 0 && fileChanges+len(orphans) == 0 {
		fmt.Println("\nEverything is up to date.")
		return nil
	}
	if *check {
		return nil
	}
	if !*yes && !confirm("Update now?") {
		return nil
	}

	// --- baseline ------------------------------------------------------
	// What already fails before the update isn't the update's doing: only
	// checks that pass now and fail afterwards can roll it back.
	baseline := runChecks(s, "Checks before updating")

	// --- update ----------------------------------------------------------
	// The record exists before anything changes, so an update that fails
	// or is interrupted half way still shows up in `history` and can be
	// rolled back: before.json lets the rollback work out what changed.
	rec := update.New()
	rec.Outcome = update.InProgress
	before, err := pacman.Installed()
	if err != nil {
		return err
	}
	if err := rec.BackupFiles(owned); err != nil {
		return fmt.Errorf("backing up generated files: %w", err)
	}
	if err := rec.SaveBefore(before); err != nil {
		return err
	}
	if err := rec.Save(); err != nil {
		return err
	}

	// You already said yes to the preview: pacman shouldn't ask again. A
	// question that needs a real answer (a conflict) makes it stop without
	// changing anything, which is recorded as a failed update. With no
	// packages pending (only generated files changed) pacman isn't run: a
	// sync now could install packages the preview never showed.
	var upErr error
	if len(pending) > 0 {
		upErr = pacman.Upgrade()
	}
	after, err := pacman.Installed()
	if err != nil {
		rec.Note = "reading the installed packages after the upgrade: " + err.Error()
		rec.Save()
		return err
	}
	rec.Changes = pacman.Diff(before, after)
	if upErr != nil && len(rec.Changes) == 0 {
		rec.Outcome, rec.Note, rec.Finished = update.Failed, upErr.Error(), time.Now()
		rec.Save()
		return fmt.Errorf("the upgrade failed and changed nothing: %w", upErr)
	}
	if upErr != nil {
		rec.Note = "the upgrade stopped half way: " + upErr.Error()
	}
	// The mirror can publish more between the preview and the upgrade.
	shown := map[string]bool{}
	for _, p := range pending {
		shown[p.Name] = true
	}
	var extra []string
	for _, c := range rec.Changes {
		if !shown[c.Name] {
			extra = append(extra, c.Name)
		}
	}
	if len(extra) > 0 {
		fmt.Printf("\n  %salso changed (published after the preview, or pulled in): %s%s\n", dim, strings.Join(extra, ", "), reset)
	}
	rec.Save()

	// Something to put right, or "" if the update is good.
	problem := ""
	if upErr != nil {
		problem = rec.Note
	}

	header("Applying the configuration")
	s, err = load()
	if err == nil {
		changes, orphans, owned, err = s.plan()
	}
	if err != nil {
		// Without a configuration there is nothing to check against.
		problem = "the configuration doesn't load after the update: " + err.Error()
		fmt.Printf("  %s%s%s\n", red, problem, reset)
	} else {
		var ce *apply.ConflictError
		switch err := s.write(changes, orphans, owned, false); {
		case errors.As(err, &ce):
			// Files you edited by hand: nothing broke, but the configuration
			// wasn't re-applied. Say so, loudly, and keep it in the record.
			rec.Note = strings.TrimPrefix(rec.Note+"; configuration not re-applied: you edited "+
				plural(len(ce.Paths), "generated file", "generated files")+" (myarch apply --adopt)", "; ")
			fmt.Printf("  %s%v%s\n", amber, err, reset)
		case err != nil:
			problem = "the configuration couldn't be written: " + err.Error()
			fmt.Printf("  %s%s%s\n", red, problem, reset)
		}
		rec.Checks = runChecks(s, "Checks after updating")
		if broke := regressions(s, baseline, rec.Checks); len(broke) > 0 && problem == "" {
			problem = plural(len(broke), "check failed", "checks failed")
		}
	}
	// What the generated files are after the update, written or not: a
	// later rollback must tell them apart from what a later apply wrote.
	if now, err := apply.LoadOwned(apply.StatePath()); err == nil {
		rec.AfterOwned = now
	}
	rec.Save()

	if problem == "" {
		rec.Outcome, rec.Finished = update.OK, time.Now()
		if err := rec.Save(); err != nil {
			return err
		}
		what := plural(len(rec.Changes), "package", "packages")
		if len(rec.Changes) == 0 {
			what = "the configuration"
		}
		if failing := len(health.Failed(rec.Checks)); failing > 0 {
			fmt.Printf("\n%sUpdated %s; it broke nothing (%s already failing before).%s\n", green, what, plural(failing, "check was", "checks were"), reset)
		} else {
			fmt.Printf("\n%sUpdated %s; everything checks out.%s\n", green, what, reset)
		}
		if strings.Contains(rec.Note, "not re-applied") {
			fmt.Printf("%sBut the configuration wasn't re-applied: see above.%s\n", amber, reset)
		}
		if anyKernel(rec.Changes) {
			fmt.Printf("%sReboot to use the new kernel.%s\n", amber, reset)
		}
		return nil
	}

	if *noRollback {
		rec.Outcome, rec.Finished = update.Failed, time.Now()
		rec.Save()
		fmt.Printf("\nLeft as it is (--no-rollback). To undo it: myarch rollback %s\n", rec.ID)
		return fmt.Errorf("%s", problem)
	}
	fmt.Printf("\n%s%s: rolling the update back.%s\n", red, capitalize(problem), reset)
	if err := rollback(rec); err != nil {
		return err
	}
	// The system is fine, but the update didn't happen: say so to scripts.
	return fmt.Errorf("update rolled back: %s", problem)
}

// regressions are the checks that passed before the update and fail after
// it (or are new with it), even when tried again a few seconds later (a network that's still
// coming back, a service still restarting). What failed before too is
// reported and left out: it isn't the update's doing.
func regressions(s *session, before, after []health.Result) []health.Result {
	passed := map[string]bool{} // present before: did it pass?
	for _, r := range before {
		passed[r.Plugin+"\x00"+r.Name] = r.OK
	}
	var out []health.Result
	for _, r := range health.Failed(after) {
		// A check that came with the update (a new plugin version) had no
		// chance to fail before: it counts.
		if ok, was := passed[r.Plugin+"\x00"+r.Name]; was && !ok {
			fmt.Printf("  %s%s was already failing before the update: not the update's doing%s\n", dim, r.Name, reset)
			continue
		}
		if passesAgain(s, r) {
			fmt.Printf("  %s%s passes when tried again: a hiccup, not a regression%s\n", dim, r.Name, reset)
			continue
		}
		out = append(out, r)
	}
	return out
}

func passesAgain(s *session, r health.Result) bool {
	time.Sleep(3 * time.Second)
	for _, c := range s.out.Checks {
		if c.Plugin == r.Plugin && c.Name == r.Name {
			return health.RunOne(c).OK
		}
	}
	return false
}

func capitalize(s string) string {
	if s == "" {
		return s
	}
	return strings.ToUpper(s[:1]) + s[1:]
}

func anyKernel(changes []pacman.Change) bool {
	for _, c := range changes {
		if isKernel(c.Name) && c.To != "" {
			return true
		}
	}
	return false
}

// runChecks runs every plugin check and prints each result as it goes.
func runChecks(s *session, title string) []health.Result {
	header(title)
	if len(s.out.Checks) == 0 {
		fmt.Println("  (no plugin declares checks)")
		return nil
	}
	if !health.InSession() {
		fmt.Printf("  %sOutside the graphical session: checks that need it are skipped.%s\n", dim, reset)
	}
	tty := isTerminal(os.Stdout)
	var results []health.Result
	for _, c := range s.out.Checks {
		if tty {
			fmt.Printf("  … %s", c.Name)
		}
		r := health.RunOne(c)
		results = append(results, r)
		if tty {
			fmt.Print("\r\033[K") // replace the "…" line
		}
		if r.Skipped {
			fmt.Printf("  %s– %s (skipped: needs the session)%s\n", dim, r.Name, reset)
		} else if r.OK {
			fmt.Printf("  %s✓%s %s %s(%s)%s\n", green, reset, r.Name, dim, r.Plugin, reset)
		} else {
			fmt.Printf("  %s✗%s %s %s(%s)%s\n", red, reset, r.Name, dim, r.Plugin, reset)
			for _, l := range strings.Split(r.Output, "\n") {
				fmt.Printf("      %s%s%s\n", dim, l, reset)
			}
		}
	}
	return results
}

// rollback puts back the packages an update changed and the generated
// files from before it, then checks again. It keeps going when one part
// fails, so as much as possible is put back, and says what wasn't.
func rollback(rec *update.Record) error {
	header("Rolling back " + rec.ID)
	var problems []string
	rec.WorkOutChanges(pacman.Installed)

	n, missing, err := pacman.Revert(rec.Changes)
	if err != nil {
		problems = append(problems, "reverting packages: "+err.Error())
		fmt.Printf("  %scouldn't put the packages back: %v%s\n", red, err, reset)
	} else {
		fmt.Printf("  %s back to the previous version\n", plural(n, "package", "packages"))
	}
	for _, m := range missing {
		fmt.Printf("  %s%s: previous version not in the cache nor the Arch archive%s\n", amber, m, reset)
	}
	if len(missing) > 0 {
		problems = append(problems, plural(len(missing), "package couldn't be put back", "packages couldn't be put back"))
	}

	if owned, err := apply.LoadOwned(apply.StatePath()); err != nil {
		problems = append(problems, "reading generated files: "+err.Error())
	} else if restored, err := rec.RestoreFiles(owned); err != nil {
		// Record what was done so far: ownership has to match the disk.
		restored.Owned.Save(apply.StatePath())
		problems = append(problems, "restoring generated files: "+err.Error())
	} else if err := restored.Owned.Save(apply.StatePath()); err != nil {
		problems = append(problems, "saving generated files: "+err.Error())
	} else {
		for _, p := range restored.Later {
			fmt.Printf("  %s%s changed after the update (myarch apply): left as it is%s\n", dim, tilde(p), reset)
		}
		if restored.NoBackup {
			fmt.Printf("  %sno copy of the generated files in this record: left as they are%s\n", amber, reset)
		} else if restored.Files > 0 {
			fmt.Printf("  %s as they were\n", plural(restored.Files, "generated file", "generated files"))
		}
		for p, bak := range restored.Backups {
			fmt.Printf("  %syou had edited %s: your version is in %s%s\n", amber, tilde(p), filepath.Base(bak), reset)
		}
		for _, p := range restored.LeftBehind {
			fmt.Printf("  %sleft %s in place: you edited it%s\n", amber, tilde(p), reset)
		}
		for _, p := range restored.Shared {
			fmt.Printf("  %sleft %s in place: it's its app's file too%s\n", dim, tilde(p), reset)
		}
		for _, p := range restored.Gone {
			fmt.Printf("  %s%s: its folder is gone, not brought back%s\n", dim, tilde(p), reset)
		}
	}

	incomplete := func(more ...string) error {
		problems = append(problems, more...)
		note := strings.Join(problems, "; ")
		rec.Outcome, rec.Finished = update.RollbackIncomplete, time.Now()
		rec.Note = strings.TrimPrefix(rec.Note+"; "+note, "; ")
		rec.Save()
		fmt.Printf("\n%sRolled back, but not everything is as it was: %s%s\n", amber, note, reset)
		return fmt.Errorf("rollback incomplete: %s", note)
	}

	// Let everything that reads those files pick them up again, and check.
	s, err := load()
	if err != nil {
		return incomplete("the configuration doesn't load: " + err.Error())
	}
	var all []apply.Change
	for _, f := range s.out.Files {
		all = append(all, apply.Change{File: f})
	}
	for cmd, e := range apply.Reload(all) {
		fmt.Fprintf(os.Stderr, "  warning: reload %q failed: %v\n", cmd, e)
	}
	failed := health.Failed(runChecks(s, "Checks"))
	if len(problems) > 0 {
		return incomplete()
	}
	// Everything is back as it was. A check that still fails isn't
	// something rolling back again would fix (it may have been failing
	// before the update): it's noted, and the update counts as undone, so
	// older ones can still be rolled back.
	rec.Outcome, rec.Finished = update.RolledBack, time.Now()
	if len(failed) > 0 {
		rec.Note = strings.TrimPrefix(rec.Note+"; "+plural(len(failed), "check still failed", "checks still failed")+" after rolling back", "; ")
	}
	rec.Save()
	fmt.Printf("\n%sRolled back: the system is as it was before %s.%s\n", green, rec.ID, reset)
	if len(failed) > 0 {
		fmt.Printf("%s%s still failing: see myarch doctor.%s\n", amber, plural(len(failed), "check is", "checks are"), reset)
	}
	return nil
}

func cmdDoctor() error {
	s, err := load()
	if err != nil {
		return err
	}
	if failed := health.Failed(runChecks(s, "Checks")); len(failed) > 0 {
		return fmt.Errorf("%d checks failed", len(failed))
	}
	return nil
}

func cmdHistory() error {
	records, err := update.List()
	if err != nil {
		return err
	}
	if len(records) == 0 {
		fmt.Println("No updates yet.")
		return nil
	}
	for _, r := range records {
		color := green
		if r.Outcome != update.OK {
			color = amber
		}
		failed := len(health.Failed(r.Checks))
		checks := fmt.Sprintf("%d checks", len(r.Checks))
		if failed > 0 {
			checks = fmt.Sprintf("%d/%d checks failed", failed, len(r.Checks))
		}
		fmt.Printf("%s  %s%-19s%s %-13s %s\n", r.ID, color, r.Outcome, reset, plural(len(r.Changes), "package", "packages"), checks)
		if r.Note != "" {
			fmt.Printf("    %s%s%s\n", dim, r.Note, reset)
		}
	}
	return nil
}

func cmdRollback(args []string) error {
	records, err := update.List() // newest first
	if err != nil {
		return err
	}
	var rec *update.Record
	var newer []*update.Record // live updates on top of the chosen one
	for _, r := range records {
		if len(args) == 0 {
			if r.Live() {
				rec = r
				break
			}
			continue
		}
		if r.ID == args[0] {
			rec = r
			break
		}
		if r.Live() {
			newer = append(newer, r)
		}
	}
	switch {
	case rec == nil && len(args) > 0:
		return fmt.Errorf("no update %s (see myarch history)", args[0])
	case rec == nil:
		return fmt.Errorf("no update to roll back")
	case !rec.Live():
		return fmt.Errorf("update %s was already rolled back", rec.ID)
	case len(newer) > 0:
		// Undoing an old update under newer ones would leave some packages
		// at old versions and others at new ones: a partial downgrade.
		ids := make([]string, len(newer))
		for i, r := range newer {
			ids[i] = r.ID
		}
		return fmt.Errorf("newer updates are still in place (%s): roll those back first, newest first",
			strings.Join(ids, ", "))
	}

	rec.WorkOutChanges(pacman.Installed)
	header(fmt.Sprintf("Update %s changed %s", rec.ID, plural(len(rec.Changes), "package", "packages")))
	for _, c := range rec.Changes {
		fmt.Printf("  %-28s %s → %s\n", c.Name, c.To, orNone(c.From))
	}
	if !confirm("Put them back, and the generated files too?") {
		return nil
	}
	rec.Note = strings.TrimPrefix(rec.Note+"; rolled back by hand", "; ")
	return rollback(rec)
}

// isTerminal: a real terminal, not just a character device (/dev/null is
// one too): only a terminal can take termios.
func isTerminal(f *os.File) bool {
	var t syscall.Termios
	_, _, errno := syscall.Syscall(syscall.SYS_IOCTL, f.Fd(), syscall.TCGETS, uintptr(unsafe.Pointer(&t)))
	return errno == 0
}

func plural(n int, one, many string) string {
	if n == 1 {
		return "1 " + one
	}
	return fmt.Sprintf("%d %s", n, many)
}

func orNone(v string) string {
	if v == "" {
		return "(stays installed)"
	}
	return v
}
