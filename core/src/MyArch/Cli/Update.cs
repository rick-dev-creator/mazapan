using System.Text.RegularExpressions;
using MyArch.Applying;
using MyArch.Health;
using MyArch.News;
using MyArch.Pacman;
using MyArch.Updates;
using MyArch.Util;
using Result = MyArch.Health.Result;

namespace MyArch.Cli;

public static partial class Program
{
    [GeneratedRegex(@"^linux(-[a-z0-9]+)?\z")]
    private static partial Regex Kernel();

    static bool IsKernel(string name) => Kernel().IsMatch(name) && !name.EndsWith("-headers") &&
        !name.EndsWith("-firmware") && !name.EndsWith("-docs");

    /// <summary>
    /// Critical is what the desktop can't do without: every package a plugin
    /// declares, plus the kernel and the graphics stack.
    /// </summary>
    static HashSet<string> Critical(Session s)
    {
        var m = new HashSet<string> { "mesa", "systemd", "glibc" };
        foreach (var p in s.Plugins) m.UnionWith(p.Pacman);
        return m;
    }

    /// <summary>
    /// What an update would bring, as data, for the updates panel: packages
    /// (the desktop's own marked, the kernel's saying a restart), Arch's news
    /// since the last update (those asking for something marked), and the
    /// Flatpak apps with a newer version. Changes nothing.
    /// </summary>
    /// <summary>The account's Flatpak apps with a newer version: id and name.</summary>
    static List<(string Id, string Name)> PendingFlatpaks()
    {
        var out_ = new List<(string, string)>();
        if (Exec.LookPath("flatpak") == null) return out_;
        var (code, text, _) = AppsCapture("flatpak", "remote-ls", "--updates", "--user", "--app", "--columns=application,name");
        if (code != 0) return out_;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            if (line.Split('\t') is [var id, .. var rest] && id != "Application ID")
                out_.Add((id, rest.Length > 0 ? rest[0] : id));
        return out_;
    }

    static bool UpdateFlatpaks()
    {
        Header("Flatpak apps");
        var ok = AppsRun("flatpak", "update", "--user", "-y", "--noninteractive") == 0;
        Console.WriteLine(ok ? "Flatpak apps updated." : "The Flatpak apps didn't update.");
        return ok;
    }

    static int UpdateCheckJson()
    {
        var since = History.LastSuccess();
        if (since == default) since = DateTimeOffset.Now.AddMonths(-1);
        var newsTask = Task.Run(() => Feed.Since(since));
        var s = Load();
        var crit = Critical(s);
        string error = "";
        List<MyArch.Pacman.Change> pending = [];
        try { pending = Packages.Pending(); }
        catch (MyArchException e) { error = e.Message; }
        List<Item> news = [];
        var newsError = "";
        try { news = newsTask.GetAwaiter().GetResult(); }
        catch (Exception e) { newsError = e.Message; }
        var flatpaks = PendingFlatpaks().Select(f => (object)new Fields { { "id", f.Id }, { "name", f.Name } }).ToList();
        Console.WriteLine(GoJson.Marshal(new Fields
        {
            { "version", 1 },
            { "packages", pending.Select(p => (object)new Fields
                { { "name", p.Name }, { "from", p.From }, { "to", p.To }, { "desktop", crit.Contains(p.Name) }, { "kernel", IsKernel(p.Name) } }).ToList() },
            { "restart", pending.Any(p => IsKernel(p.Name)) },
            { "news", news.Select(n => (object)new Fields
                { { "title", n.Title }, { "link", n.Link }, { "date", n.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) }, { "action", n.NeedsAction() } }).ToList() },
            { "flatpaks", flatpaks },
            { "error", error },
            { "news_error", newsError },
        }));
        return 0;
    }

    static int CmdUpdate(string[] args)
    {
        var fs = new Flags("update")
            .Bool("y", "don't ask: update right away")
            .Bool("check", "show what an update would do, change nothing")
            .Bool("no-rollback", "if a check fails, leave things as they are")
            .Bool("json", "with --check: what there is, as data (the updates panel)")
            .Bool("gui", "from the updates panel: the password through polkit (pkexec), no terminal")
            .Parse(args);
        if (fs.IsSet("json")) return UpdateCheckJson();
        Packages.Gui = fs.IsSet("gui");
        var s = Load();

        // --- preview -------------------------------------------------------
        Console.WriteLine("Checking for updates…");
        var since = History.LastSuccess();
        var sinceLabel = "your last update";
        if (since == default)
        {
            since = DateTimeOffset.Now.AddMonths(-1);
            sinceLabel = "a month ago";
        }
        // Both go to the network and don't depend on each other.
        var newsTask = Task.Run(() => Feed.Since(since));
        var pending = Packages.Pending();
        var (changes, orphans, owned) = s.Plan();
        List<Item> items = [];
        Exception? newsErr = null;
        try
        {
            items = newsTask.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            newsErr = e;
        }

        var crit = Critical(s);
        if (pending.Count > 0)
        {
            Header($"Packages ({pending.Count})");
            var kernelChanges = false;
            foreach (var p in pending)
            {
                var mark = crit.Contains(p.Name) || IsKernel(p.Name) ? Style.Amber + "★" + Style.Reset : " ";
                kernelChanges = kernelChanges || IsKernel(p.Name);
                Console.WriteLine($"  {mark} {p.Name,-28} {Style.Dim}{p.From}{Style.Reset} → {p.To}");
            }
            Console.WriteLine($"  {Style.Dim}★ your desktop depends on it: checked after the update{Style.Reset}");
            if (kernelChanges) Console.WriteLine($"  {Style.Amber}The new kernel is used after a reboot.{Style.Reset}");
        }

        if (newsErr != null)
        {
            Header("Arch news");
            Console.WriteLine($"  {Style.Amber}couldn't read the news ({newsErr.Message}): check https://archlinux.org/news/ before updating{Style.Reset}");
        }
        else if (items.Count > 0)
        {
            Header("Arch news since " + sinceLabel);
            foreach (var it in items)
            {
                var mark = it.NeedsAction() ? Style.Red + "!" + Style.Reset : " ";
                Console.WriteLine($"  {mark} {it.Date:yyyy-MM-dd}  {it.Title}\n    {Style.Dim}{it.Link}{Style.Reset}");
            }
        }

        // What an apply would write: busy and unreadable files wait, they're
        // not an update.
        var fileChanges = changes.Count(c => c.State is State.New or State.Changed or State.Conflict);
        if (fileChanges + orphans.Count > 0)
        {
            Header("Generated files");
            PrintPlan(changes, orphans, true);
        }

        var flatpaks = PendingFlatpaks();
        if (flatpaks.Count > 0)
        {
            Header($"Flatpak apps ({flatpaks.Count})");
            foreach (var (_, name) in flatpaks) Console.WriteLine($"    {name}");
        }
        if (pending.Count == 0 && fileChanges + orphans.Count == 0)
        {
            // Only the Flatpak apps: nothing of the system's changes, no record.
            if (flatpaks.Count > 0 && !fs.IsSet("check") && (fs.IsSet("y") || Confirm("Update now?")))
                return UpdateFlatpaks() ? 0 : 1;
            if (flatpaks.Count == 0) Console.WriteLine("\nEverything is up to date.");
            return 0;
        }
        if (fs.IsSet("check")) return 0;
        if (!fs.IsSet("y") && !Confirm("Update now?")) return 0;

        // --- baseline ------------------------------------------------------
        // What already fails before the update isn't the update's doing: only
        // checks that pass now and fail afterwards can roll it back.
        var baseline = RunChecks(s, "Checks before updating");

        // --- update --------------------------------------------------------
        // The record exists before anything changes, so an update that fails
        // or is interrupted half way still shows up in `history` and can be
        // rolled back: before.json lets the rollback work out what changed.
        var rec = Record.New();
        rec.Outcome = Outcomes.InProgress;
        var before = Packages.Installed();
        try
        {
            rec.BackupFiles(owned);
        }
        catch (MyArchException e)
        {
            throw new MyArchException("backing up generated files: " + e.Message);
        }
        rec.SaveBefore(before);
        rec.Save();

        // You already said yes to the preview: pacman shouldn't ask again. A
        // question that needs a real answer (a conflict) makes it stop without
        // changing anything, which is recorded as a failed update. With no
        // packages pending (only generated files changed) pacman isn't run: a
        // sync now could install packages the preview never showed.
        string? upErr = null;
        if (pending.Count > 0)
        {
            try
            {
                Packages.Upgrade();
            }
            catch (MyArchException e)
            {
                upErr = e.Message;
            }
        }
        // The Flatpak apps too (the account's, as the Apps menu installs them):
        // their own runtime, no root, not part of what a rollback undoes. Not
        // when the system's part didn't go through (a password refused).
        if (upErr == null && flatpaks.Count > 0 && !UpdateFlatpaks()) rec.Note = "the Flatpak apps didn't update (flatpak update --user)";
        Dictionary<string, string> after;
        try
        {
            after = Packages.Installed();
        }
        catch (MyArchException e)
        {
            rec.Note = "reading the installed packages after the upgrade: " + e.Message;
            TrySave(rec);
            throw;
        }
        rec.Changes = Packages.Diff(before, after);
        if (upErr != null && rec.Changes.Count == 0)
        {
            rec.Outcome = Outcomes.Failed;
            rec.Note = upErr;
            rec.Finished = DateTimeOffset.Now;
            TrySave(rec);
            throw new MyArchException("the upgrade failed and changed nothing: " + upErr);
        }
        if (upErr != null) rec.Note = "the upgrade stopped half way: " + upErr;
        // The mirror can publish more between the preview and the upgrade.
        var shown = pending.Select(p => p.Name).ToHashSet();
        var extra = rec.Changes.Where(c => !shown.Contains(c.Name)).Select(c => c.Name).ToList();
        if (extra.Count > 0)
            Console.WriteLine($"\n  {Style.Dim}also changed (published after the preview, or pulled in): {string.Join(", ", extra)}{Style.Reset}");
        TrySave(rec);

        // Something to put right, or "" if the update is good.
        var problem = upErr != null ? rec.Note : "";

        Header("Applying the configuration");
        try
        {
            s = Load();
            (changes, orphans, owned) = s.Plan();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Without a configuration there is nothing to check against.
            problem = "the configuration doesn't load after the update: " + e.Message;
            Console.WriteLine($"  {Style.Red}{problem}{Style.Reset}");
            s = null;
        }
        if (s != null)
        {
            try
            {
                using (ApplyLock.Take()) s.Write(changes, orphans, owned, false);
            }
            catch (ConflictException ce)
            {
                // Files you edited by hand: nothing broke, but the configuration
                // wasn't re-applied. Say so, loudly, and keep it in the record.
                rec.Note = (rec.Note + "; configuration not re-applied: you edited " +
                    Plural(ce.Paths.Count, "generated file", "generated files") + " (myarch apply --adopt)").TrimStart(';', ' ');
                Console.WriteLine($"  {Style.Amber}{ce.Message}{Style.Reset}");
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                problem = "the configuration couldn't be written: " + e.Message;
                Console.WriteLine($"  {Style.Red}{problem}{Style.Reset}");
            }
            try
            {
                rec.Checks = RunChecks(s, "Checks after updating");
                var broke = Regressions(s, baseline, rec.Checks);
                if (broke.Count > 0 && problem == "") problem = Plural(broke.Count, "check failed", "checks failed");
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                if (problem == "") problem = "the checks couldn't run: " + e.Message;
            }
        }
        // What the generated files are after the update, written or not: a
        // later rollback must tell them apart from what a later apply wrote.
        try
        {
            rec.AfterOwned = Apply.LoadOwned(Apply.StatePath());
        }
        catch (MyArchException) { }
        TrySave(rec);

        if (problem == "")
        {
            rec.Outcome = Outcomes.OK;
            rec.Finished = DateTimeOffset.Now;
            rec.Save();
            var what = rec.Changes.Count == 0 ? "the configuration" : Plural(rec.Changes.Count, "package", "packages");
            var failing = Checks.Failed(rec.Checks).Count;
            if (failing > 0)
                Console.WriteLine($"\n{Style.Green}Updated {what}; it broke nothing ({Plural(failing, "check was", "checks were")} already failing before).{Style.Reset}");
            else
                Console.WriteLine($"\n{Style.Green}Updated {what}; everything checks out.{Style.Reset}");
            if (rec.Note.Contains("not re-applied"))
                Console.WriteLine($"{Style.Amber}But the configuration wasn't re-applied: see above.{Style.Reset}");
            if (rec.Changes.Any(c => IsKernel(c.Name) && c.To != ""))
                Console.WriteLine($"{Style.Amber}Reboot to use the new kernel.{Style.Reset}");
            return 0;
        }

        if (fs.IsSet("no-rollback"))
        {
            rec.Outcome = Outcomes.Failed;
            rec.Finished = DateTimeOffset.Now;
            TrySave(rec);
            Console.WriteLine($"\nLeft as it is (--no-rollback). To undo it: myarch rollback {rec.ID}");
            throw new MyArchException(problem);
        }
        Console.WriteLine($"\n{Style.Red}{Capitalize(problem)}: rolling the update back.{Style.Reset}");
        Rollback(rec);
        // The system is fine, but the update didn't happen: say so to scripts.
        throw new MyArchException("update rolled back: " + problem);
    }

    /// <summary>Go's `rec.Save()` with the error ignored.</summary>
    static void TrySave(Record rec)
    {
        try
        {
            rec.Save();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { }
    }

    /// <summary>
    /// Regressions are the checks that passed before the update and fail after
    /// it (or are new with it), even when tried again a few seconds later (a
    /// network that's still coming back, a service still restarting). What
    /// failed before too is reported and left out: it isn't the update's doing.
    /// </summary>
    static List<Result> Regressions(Session s, List<Result> before, List<Result> after)
    {
        var passed = new Dictionary<string, bool>(); // present before: did it pass?
        foreach (var r in before) passed[r.Plugin + "\0" + r.Name] = r.OK;
        var out_ = new List<Result>();
        foreach (var r in Checks.Failed(after))
        {
            // A check that came with the update (a new plugin version) had no
            // chance to fail before: it counts.
            if (passed.TryGetValue(r.Plugin + "\0" + r.Name, out var ok) && !ok)
            {
                Console.WriteLine($"  {Style.Dim}{r.Name} was already failing before the update: not the update's doing{Style.Reset}");
                continue;
            }
            if (PassesAgain(s, r))
            {
                Console.WriteLine($"  {Style.Dim}{r.Name} passes when tried again: a hiccup, not a regression{Style.Reset}");
                continue;
            }
            out_.Add(r);
        }
        return out_;
    }

    static bool PassesAgain(Session s, Result r)
    {
        Thread.Sleep(TimeSpan.FromSeconds(3));
        var c = s.Out.Checks.FirstOrDefault(c => c.Plugin == r.Plugin && c.Name == r.Name);
        return c != null && Checks.RunOne(c).OK;
    }

    static string Capitalize(string s) => s == "" ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>RunChecks runs every plugin check and prints each result as it goes.</summary>
    static List<Result> RunChecks(Session s, string title)
    {
        Header(title);
        if (s.Out.Checks.Count == 0)
        {
            Console.WriteLine("  (no plugin declares checks)");
            return [];
        }
        if (!Checks.InSession())
            Console.WriteLine($"  {Style.Dim}Outside the graphical session: checks that need it are skipped.{Style.Reset}");
        var tty = IsTerminal(1);
        var results = new List<Result>();
        foreach (var c in s.Out.Checks)
        {
            if (tty) Console.Write($"  … {c.Name}");
            var r = Checks.RunOne(c);
            results.Add(r);
            if (tty) Console.Write("\r\u001b[K"); // replace the "…" line
            if (r.Skipped)
                Console.WriteLine($"  {Style.Dim}– {r.Name} (skipped: needs the session){Style.Reset}");
            else if (r.OK)
                Console.WriteLine($"  {Style.Green}✓{Style.Reset} {r.Name} {Style.Dim}({r.Plugin}){Style.Reset}");
            else
            {
                Console.WriteLine($"  {Style.Red}✗{Style.Reset} {r.Name} {Style.Dim}({r.Plugin}){Style.Reset}");
                foreach (var l in r.Output.Split('\n')) Console.WriteLine($"      {Style.Dim}{l}{Style.Reset}");
            }
        }
        return results;
    }

    /// <summary>
    /// Rollback puts back the packages an update changed and the generated
    /// files from before it, then checks again. It keeps going when one part
    /// fails, so as much as possible is put back, and says what wasn't.
    /// </summary>
    static void Rollback(Record rec)
    {
        Header("Rolling back " + rec.ID);
        var problems = new List<string>();
        rec.WorkOutChanges(Packages.Installed);

        RevertResult rv;
        try
        {
            rv = Packages.Revert(rec.Changes);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            rv = new RevertResult(0, [], e.Message);
        }
        if (rv.Error != null)
        {
            problems.Add("reverting packages: " + rv.Error);
            Console.WriteLine($"  {Style.Red}couldn't put the packages back: {rv.Error}{Style.Reset}");
        }
        else
            Console.WriteLine($"  {Plural(rv.Count, "package", "packages")} back to the previous version");
        foreach (var m in rv.Missing)
            Console.WriteLine($"  {Style.Amber}{m}: previous version not in the cache nor the Arch archive{Style.Reset}");
        if (rv.Missing.Count > 0)
            problems.Add(Plural(rv.Missing.Count, "package couldn't be put back", "packages couldn't be put back"));

        Owned? owned = null;
        try
        {
            owned = Apply.LoadOwned(Apply.StatePath());
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            problems.Add("reading generated files: " + e.Message);
        }
        if (owned != null)
        {
            Restored? restored = null;
            try
            {
                restored = rec.RestoreFiles(owned);
            }
            catch (RestoreException e)
            {
                // Record what was done so far: ownership has to match the disk.
                try
                {
                    e.Partial.Owned.Save(Apply.StatePath());
                }
                catch (Exception) { }
                problems.Add("restoring generated files: " + e.Message);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                problems.Add("restoring generated files: " + e.Message);
            }
            if (restored != null)
            {
                try
                {
                    restored.Owned.Save(Apply.StatePath());
                    foreach (var p in restored.Later)
                        Console.WriteLine($"  {Style.Dim}{Tilde(p)} changed after the update (myarch apply): left as it is{Style.Reset}");
                    if (restored.NoBackup)
                        Console.WriteLine($"  {Style.Amber}no copy of the generated files in this record: left as they are{Style.Reset}");
                    else if (restored.Files > 0)
                        Console.WriteLine($"  {Plural(restored.Files, "generated file", "generated files")} as they were");
                    foreach (var (p, bak) in restored.Backups.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                        Console.WriteLine($"  {Style.Amber}you had edited {Tilde(p)}: your version is in {Paths.Base(bak)}{Style.Reset}");
                    foreach (var p in restored.LeftBehind)
                        Console.WriteLine($"  {Style.Amber}left {Tilde(p)} in place: you edited it{Style.Reset}");
                    foreach (var p in restored.Shared)
                        Console.WriteLine($"  {Style.Dim}left {Tilde(p)} in place: it's its app's file too{Style.Reset}");
                    foreach (var p in restored.Gone)
                        Console.WriteLine($"  {Style.Dim}{Tilde(p)}: its folder is gone, not brought back{Style.Reset}");
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    problems.Add("saving generated files: " + e.Message);
                }
            }
        }

        void Incomplete(params string[] more)
        {
            problems.AddRange(more);
            var note = string.Join("; ", problems);
            rec.Outcome = Outcomes.RollbackIncomplete;
            rec.Finished = DateTimeOffset.Now;
            rec.Note = (rec.Note + "; " + note).TrimStart(';', ' ');
            TrySave(rec);
            Console.WriteLine($"\n{Style.Amber}Rolled back, but not everything is as it was: {note}{Style.Reset}");
            throw new MyArchException("rollback incomplete: " + note);
        }

        // Let everything that reads those files pick them up again, and check.
        Session s;
        try
        {
            s = Load();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Incomplete("the configuration doesn't load: " + e.Message);
            return;
        }
        List<Result> failed;
        try
        {
            foreach (var (cmd, e) in Apply.Reload(s.Out.Files.Where(f => !f.System).Select(f => new Applying.Change(f))))
                Console.Error.WriteLine($"  warning: reload {GoFormat.Quote(cmd)} failed: {e}");
            failed = Checks.Failed(RunChecks(s, "Checks"));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Incomplete("reloading and checking: " + e.Message);
            return;
        }
        if (problems.Count > 0) Incomplete();
        // Everything is back as it was. A check that still fails isn't
        // something rolling back again would fix (it may have been failing
        // before the update): it's noted, and the update counts as undone, so
        // older ones can still be rolled back.
        rec.Outcome = Outcomes.RolledBack;
        rec.Finished = DateTimeOffset.Now;
        if (failed.Count > 0)
            rec.Note = (rec.Note + "; " + Plural(failed.Count, "check still failed", "checks still failed") + " after rolling back").TrimStart(';', ' ');
        TrySave(rec);
        Console.WriteLine($"\n{Style.Green}Rolled back: the system is as it was before {rec.ID}.{Style.Reset}");
        if (failed.Count > 0)
            Console.WriteLine($"{Style.Amber}{Plural(failed.Count, "check is", "checks are")} still failing: see myarch doctor.{Style.Reset}");
    }

    static int CmdHistory()
    {
        var records = History.List();
        if (records.Count == 0)
        {
            Console.WriteLine("No updates yet.");
            return 0;
        }
        foreach (var r in records)
        {
            var color = r.Outcome != Outcomes.OK ? Style.Amber : Style.Green;
            var failed = Checks.Failed(r.Checks).Count;
            var checks = failed > 0 ? $"{failed}/{r.Checks.Count} checks failed" : $"{r.Checks.Count} checks";
            Console.WriteLine($"{r.ID}  {color}{r.Outcome,-19}{Style.Reset} {Plural(r.Changes.Count, "package", "packages"),-13} {checks}");
            if (r.Note != "") Console.WriteLine($"    {Style.Dim}{r.Note}{Style.Reset}");
        }
        return 0;
    }

    static int CmdRollback(string[] args)
    {
        var records = History.List(); // newest first
        Record? rec = null;
        var newer = new List<Record>(); // live updates on top of the chosen one
        foreach (var r in records)
        {
            if (args.Length == 0)
            {
                if (r.Live())
                {
                    rec = r;
                    break;
                }
                continue;
            }
            if (r.ID == args[0])
            {
                rec = r;
                break;
            }
            if (r.Live()) newer.Add(r);
        }
        if (rec == null && args.Length > 0) throw new MyArchException($"no update {args[0]} (see myarch history)");
        if (rec == null) throw new MyArchException("no update to roll back");
        if (!rec.Live()) throw new MyArchException($"update {rec.ID} was already rolled back");
        // Undoing an old update under newer ones would leave some packages
        // at old versions and others at new ones: a partial downgrade.
        if (newer.Count > 0)
            throw new MyArchException($"newer updates are still in place ({string.Join(", ", newer.Select(r => r.ID))}): roll those back first, newest first");

        rec.WorkOutChanges(Packages.Installed);
        Header($"Update {rec.ID} changed {Plural(rec.Changes.Count, "package", "packages")}");
        foreach (var c in rec.Changes)
            Console.WriteLine($"  {c.Name,-28} {c.To} → {(c.From == "" ? "(stays installed)" : c.From)}");
        if (!Confirm("Put them back, and the generated files too?")) return 0;
        rec.Note = (rec.Note + "; rolled back by hand").TrimStart(';', ' ');
        Rollback(rec);
        return 0;
    }
}
