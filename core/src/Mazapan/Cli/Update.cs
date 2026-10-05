using System.Text.RegularExpressions;
using Mazapan.Applying;
using Mazapan.Health;
using Mazapan.News;
using Mazapan.Pacman;
using Mazapan.Updates;
using Mazapan.Util;
using Result = Mazapan.Health.Result;

namespace Mazapan.Cli;

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
        foreach (var p in s.Plugins) m.UnionWith(p.PacmanHere);
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
        // The name it shows installed ("LocalSend"): the remote's is often the
        // id's last part ("localsend_app").
        var names = new Dictionary<string, string>();
        var (lc, listed, _) = AppsCapture("flatpak", "list", "--user", "--app", "--columns=application,name");
        if (lc == 0)
            foreach (var line in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                if (line.Split('\t') is [var id, var name, ..] && name != "") names[id] = name;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            if (line.Split('\t') is [var id, .. var rest] && id != "Application ID")
                out_.Add((id, names.GetValueOrDefault(id) ?? (rest.Length > 0 ? rest[0] : id)));
        return out_;
    }

    /// <summary>Apps from their makers (Rider, VS Code) with a newer release: the app, and that release.</summary>
    static List<(Store.App App, Store.VendorRelease Release)> PendingVendorUpdates()
    {
        var out_ = new List<(Store.App, Store.VendorRelease)>();
        List<Store.App> apps;
        try { apps = AppsCatalog(false).Apps; }
        catch (MazapanException) { return out_; }
        // One that keeps itself up to date (Claude Code) is left to it.
        foreach (var a in apps.Where(a => a.Vendor != "" && !Store.Vendor.MakerOf(a.Vendor).SelfInstalls && Store.Vendor.Installed(a.Id, a.Vendor) != ""))
        {
            try
            {
                var latest = Store.Vendor.Latest(a.Vendor);
                if (latest.Version != Store.Vendor.Installed(a.Id, a.Vendor)) out_.Add((a, latest));
            }
            // Its maker unreachable now: next time.
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or MazapanException) { }
        }
        return out_;
    }

    static bool UpdateVendorApps(List<(Store.App App, Store.VendorRelease Release)> updates)
    {
        if (updates.Count == 0) return true;
        Header("From their makers");
        // Not while the Apps panel installs or removes one (the same folders).
        FileStream lk;
        try { lk = AppsLock(say: false); }
        catch (MazapanException e) { Console.WriteLine(e.Message); return false; }
        using var _ = lk;
        var ok = true;
        foreach (var (a, rel) in updates)
        {
            try
            {
                Store.Vendor.Install(a.Id, Store.Vendor.MakerOf(a.Vendor), rel, a.Name);
                Console.WriteLine($"{a.Name} {rel.Version}");
            }
            catch (Exception e) when (e is MazapanException or HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"{a.Name}: {e.Message}");
                ok = false;
            }
        }
        return ok;
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
        crit.Add("mazapan");
        string error = "";
        List<Mazapan.Pacman.Change> pending = [];
        try { pending = Packages.Pending(); }
        catch (MazapanException e) { error = e.Message; }
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
            { "whats_new", WhatsNew(pending).Select(r => (object)new Fields
                { { "version", r.Version }, { "items", r.Items.Cast<object>().ToList() } }).ToList() },
            { "news", news.Select(n => (object)new Fields
                { { "title", n.Title }, { "link", n.Link }, { "date", n.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) }, { "action", n.NeedsAction() } }).ToList() },
            { "flatpaks", flatpaks },
            // Apps from their makers (Rider, VS Code): updated with the rest.
            { "vendor", PendingVendorUpdates().Select(v => (object)new Fields
                { { "id", v.App.Id }, { "name", v.App.Name }, { "from", Store.Vendor.Installed(v.App.Id, v.App.Vendor) }, { "to", v.Release.Version } }).ToList() },
            // Plugins from git with a newer version: mazapan plugins update brings them.
            { "plugins", PendingPluginUpdates().Select(p => (object)new Fields
                { { "id", p.Id }, { "from", p.From }, { "to", p.To } }).ToList() },
            // Firmware (fwupd): shown, updated by fwupdmgr update (it may finish at the next start).
            { "firmware", PendingFirmware().Select(f => (object)new Fields
                { { "device", f.Device }, { "from", f.From }, { "to", f.To }, { "summary", f.Summary } }).ToList() },
            { "error", error },
            { "news_error", newsError },
        }));
        return 0;
    }

    static int CmdUpdate(string[] args)
    {
        var fs = new Flags("update")
            .Bool("y", "don't ask: update right away")
            .Bool("check", "show what an update would do, every package, change nothing")
            .Bool("no-rollback", "if a check fails, leave things as they are")
            .Bool("json", "with --check: what there is, as data (the updates panel)")
            .Bool("gui", "from the updates panel: the password through polkit (pkexec), no terminal")
            .String("continue", "(internal) finish update ID: the new mazapan, once the packages brought it")
            .Parse(args);
        if (fs.IsSet("json")) return UpdateCheckJson();
        var gui = fs.IsSet("gui");
        Packages.Gui = gui;
        var ask = !fs.IsSet("y") && !gui;
        if (fs.Get("continue") is { Length: > 0 } cont) return Continue(cont, gui, ask, fs.IsSet("no-rollback"));
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
        var flatpaks = PendingFlatpaks();
        var vendor = PendingVendorUpdates();
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
        // What an apply would write: busy and unreadable files wait, they're
        // not an update.
        var fileChanges = changes.Count(c => c.State is State.New or State.Changed or State.Conflict);
        if (pending.Count == 0 && fileChanges + orphans.Count == 0 && flatpaks.Count == 0 && vendor.Count == 0)
        {
            Console.WriteLine("\nEverything is up to date.");
            return 0;
        }

        // One line first: what it is, in short.
        var crit = Critical(s);
        crit.Add("mazapan");
        var summary = new List<string>();
        var self = pending.FirstOrDefault(p => p.Name == "mazapan");
        // Its version as it calls it, without pacman's release number.
        static string V(string v) => v.Contains('-') ? v[..v.LastIndexOf('-')] : v;
        if (self != null) summary.Add($"Mazapan {V(self.From)} → {V(self.To)}");
        if (pending.Count > 0) summary.Add(Plural(pending.Count, "package", "packages"));
        if (flatpaks.Count > 0) summary.Add(Plural(flatpaks.Count, "Flatpak app", "Flatpak apps"));
        if (vendor.Count > 0) summary.Add(string.Join(", ", vendor.Select(v => $"{v.App.Name} {v.Release.Version}")));
        if (pending.Any(p => IsKernel(p.Name))) summary.Add("a restart (kernel)");
        if (summary.Count > 0) Console.WriteLine($"\n{Style.Bold}{string.Join(" · ", summary)}{Style.Reset}");

        if (pending.Count > 0)
        {
            // What matters, by name; the rest counted (--check lists them all).
            var all = fs.IsSet("check") || pending.Count <= 12;
            var shown = all ? pending : pending.Where(p => crit.Contains(p.Name) || IsKernel(p.Name)).ToList();
            foreach (var p in shown)
            {
                var mark = crit.Contains(p.Name) || IsKernel(p.Name) ? Style.Amber + "★" + Style.Reset : " ";
                Console.WriteLine($"  {mark} {p.Name,-28} {Style.Dim}{p.From}{Style.Reset} → {p.To}");
            }
            if (shown.Count < pending.Count)
                Console.WriteLine($"    {Style.Dim}and {pending.Count - shown.Count} more (mazapan update --check lists them){Style.Reset}");
            if (shown.Any(p => crit.Contains(p.Name) || IsKernel(p.Name)))
                Console.WriteLine($"  {Style.Dim}★ your desktop depends on it: checked after the update{Style.Reset}");
        }

        foreach (var r in WhatsNew(pending))
        {
            Header("What's new in Mazapan" + (r.Version == "Unreleased" ? "" : " " + r.Version));
            foreach (var it in r.Items) Console.WriteLine($"  • {it}");
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

        // Firmware is said, not updated here: fwupdmgr update does it (often at
        // the next start, outside the system's own rollback).
        var firmware = PendingFirmware();
        if (firmware.Count > 0)
        {
            Header($"Firmware ({firmware.Count})");
            foreach (var f in firmware) Console.WriteLine($"    {f.Device,-28} {Style.Dim}{f.From}{Style.Reset} → {f.To}");
            Console.WriteLine($"  {Style.Dim}fwupdmgr update installs it (some at the next start){Style.Reset}");
        }

        // Plugins from git: their own update, which asks for what's new they'd do.
        var pluginUpdates = PendingPluginUpdates();
        if (pluginUpdates.Count > 0)
        {
            Header($"Plugins ({pluginUpdates.Count})");
            foreach (var p in pluginUpdates) Console.WriteLine($"    {p.Id,-28} {Style.Dim}{p.From}{Style.Reset} → {p.To}");
            Console.WriteLine($"  {Style.Dim}mazapan plugins update brings them (asking again for anything new they'd do){Style.Reset}");
        }

        if (fileChanges + orphans.Count > 0)
        {
            Header("Generated files");
            PrintPlan(changes, orphans, true);
        }

        if (flatpaks.Count > 0 && (fs.IsSet("check") || flatpaks.Count <= 12))
        {
            Header($"Flatpak apps ({flatpaks.Count})");
            foreach (var (_, name) in flatpaks) Console.WriteLine($"    {name}");
        }
        if (vendor.Count > 0)
        {
            Header($"From their makers ({vendor.Count})");
            foreach (var v in vendor) Console.WriteLine($"    {v.App.Name,-28} {Style.Dim}{Store.Vendor.Installed(v.App.Id, v.App.Vendor)}{Style.Reset} → {v.Release.Version}");
        }
        if (fs.IsSet("check")) return 0;
        if (pending.Count == 0 && fileChanges + orphans.Count == 0)
        {
            // Only the Flatpak apps and the makers' apps: nothing of the
            // system's changes, no record.
            if (!ask || Confirm("Update now?"))
                return (flatpaks.Count == 0 || UpdateFlatpaks()) & UpdateVendorApps(vendor) ? 0 : 1;
            return 0;
        }
        if (ask && !Confirm("Update now?")) return 0;

        var steps = new Steps(gui);

        // --- ready ---------------------------------------------------------
        steps.Start("ready", "Getting ready");
        var (notReady, readyDetail) = Ready();
        if (notReady != null)
        {
            steps.Fail("ready", notReady);
            throw new MazapanException(notReady);
        }
        using var awake = new KeepAwake();
        // What already fails before the update isn't the update's doing: only
        // checks that pass now and fail afterwards can roll it back.
        var baseline = RunChecks(s, "", quiet: true);
        steps.Ok("ready", readyDetail + (awake.Held ? ", kept awake" : ""));

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
        catch (MazapanException e)
        {
            throw new MazapanException("backing up generated files: " + e.Message);
        }
        rec.Baseline = baseline;
        rec.SaveBefore(before);
        rec.Save();

        // You already said yes to the preview: pacman shouldn't ask again. A
        // question that needs a real answer (a conflict) makes it stop without
        // changing anything, which is recorded as a failed update. With no
        // packages pending (only generated files changed) pacman isn't run: a
        // sync now could install packages the preview never showed.
        string? upErr = null;
        var logStart = Packages.LogLength();
        if (pending.Count > 0)
        {
            var keyrings = pending.Select(p => p.Name).Where(n => Packages.KeyringPackages.Contains(n)).ToList();
            if (keyrings.Count > 0)
            {
                steps.Start("keys", "Keys");
                try
                {
                    Packages.Keyrings(keyrings);
                    steps.Ok("keys", string.Join(", ", keyrings));
                }
                catch (MazapanException e)
                {
                    upErr = e.Message;
                    steps.Fail("keys", e.Message);
                }
            }
            if (upErr == null)
            {
                steps.Start("packages", "Packages");
                try
                {
                    Packages.Upgrade();
                }
                catch (MazapanException e)
                {
                    upErr = e.Message;
                }
            }
        }
        // The Flatpak apps too (the account's, as the Apps menu installs them):
        // their own runtime, no root, not part of what a rollback undoes. Not
        // when the system's part didn't go through (a password refused).
        if (upErr == null && flatpaks.Count > 0)
        {
            if (UpdateFlatpaks()) rec.Apps.AddRange(flatpaks.Select(f => f.Name));
            else rec.Note = "the Flatpak apps didn't update (flatpak update --user)";
        }
        if (upErr == null && vendor.Count > 0)
        {
            if (UpdateVendorApps(vendor)) rec.Apps.AddRange(vendor.Select(v => v.App.Name));
            else rec.Note = (rec.Note == "" ? "" : rec.Note + "; ") + "an app from its maker didn't update";
        }
        Dictionary<string, string> after;
        try
        {
            after = Packages.Installed();
        }
        catch (MazapanException e)
        {
            rec.Note = "reading the installed packages after the upgrade: " + e.Message;
            TrySave(rec);
            throw;
        }
        rec.Changes = Packages.Diff(before, after);
        // Only the keyrings went in before the upgrade stopped: nothing to roll
        // back (they're kept: they're what the next try needs).
        var onlyKeys = upErr != null && rec.Changes.Count > 0 && rec.Changes.All(c => Packages.KeyringPackages.Contains(c.Name));
        if (onlyKeys) upErr += " (the keyrings were updated; the package lists are newer than the packages: mazapan update again)";
        if (upErr != null && (rec.Changes.Count == 0 || onlyKeys))
        {
            rec.Outcome = Outcomes.Failed;
            rec.Note = upErr;
            rec.Finished = DateTimeOffset.Now;
            TrySave(rec);
            if (pending.Count > 0 && !upErr.StartsWith("the keyrings: ", StringComparison.Ordinal)) steps.Fail("packages", "nothing changed: " + upErr);
            throw new MazapanException("the upgrade failed and changed nothing: " + upErr);
        }
        if (upErr != null) rec.Note = "the upgrade stopped half way: " + upErr;
        // A system started with a broken initramfs doesn't come up: that's a
        // failed update, whatever pacman's exit status said.
        var initramfs = rec.Changes.Count > 0 ? Packages.InitramfsFailed(Packages.LogSince(logStart)) : null;
        if (initramfs != null) rec.Note = (rec.Note + "; the initramfs failed to build: " + initramfs).TrimStart(';', ' ');
        if (pending.Count > 0)
        {
            if (upErr != null || initramfs != null) steps.Fail("packages", upErr ?? "the initramfs failed to build: " + initramfs);
            else steps.Ok("packages", Plural(rec.Changes.Count, "package changed", "packages changed"));
        }
        // The mirror can publish more between the preview and the upgrade.
        var shownNames = pending.Select(p => p.Name).ToHashSet();
        var extra = rec.Changes.Where(c => !shownNames.Contains(c.Name)).Select(c => c.Name).ToList();
        if (extra.Count > 0)
            Console.WriteLine($"  {Style.Dim}also changed (published after the preview, or pulled in): {string.Join(", ", extra)}{Style.Reset}");
        TrySave(rec);

        // Something to put right, or "" if the update is good.
        var problem = upErr != null ? rec.Note : initramfs != null ? "the initramfs failed to build (" + initramfs + ")" : "";
        // A new Mazapan came with it: the new one writes the configuration and
        // runs the checks (its plugins may need its own code), and rolls back.
        // This one waits, holding the machine awake.
        if (problem == "" && rec.Changes.Any(c => c.Name == "mazapan" && c.To != "") &&
            Paths.Clean(Root()) == Repository.Installed && File.Exists("/usr/bin/mazapan"))
        {
            var psi = new System.Diagnostics.ProcessStartInfo("/usr/bin/mazapan") { UseShellExecute = false };
            foreach (var x in new[] { "update", "--continue", rec.ID }) psi.ArgumentList.Add(x);
            if (gui) psi.ArgumentList.Add("--gui");
            if (!ask) psi.ArgumentList.Add("-y");
            if (fs.IsSet("no-rollback")) psi.ArgumentList.Add("--no-rollback");
            int exit;
            try
            {
                using var child = System.Diagnostics.Process.Start(psi)!;
                child.WaitForExit();
                exit = child.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                exit = -1;
                Console.WriteLine($"  {Style.Dim}the new mazapan didn't start: {e.Message}{Style.Reset}");
            }
            // It finished the update, well or not: its word. Left in progress
            // (it crashed, or doesn't know --continue): this one finishes,
            // and a new mazapan that can't do so much is a failed update.
            if (History.List().FirstOrDefault(r => r.ID == rec.ID) is { } after2 && after2.Outcome != Outcomes.InProgress) return exit;
            problem = $"the new mazapan couldn't finish the update (exit {exit})";
        }
        return Finish(rec, problem, steps, ask, fs.IsSet("no-rollback"));
    }

    /// <summary>
    /// `update --continue ID`: the rest of an update whose packages brought a
    /// new mazapan, run by the new one. The record has what it needs.
    /// </summary>
    static int Continue(string id, bool gui, bool ask, bool noRollback)
    {
        var rec = History.List().FirstOrDefault(r => r.ID == id) ?? throw new MazapanException($"no update {id}");
        if (rec.Outcome != Outcomes.InProgress) throw new MazapanException($"update {id} is already finished ({rec.Outcome})");
        rec.WorkOutChanges(Packages.Installed);
        if (!rec.Changes.Any(c => c.Name == "mazapan" && c.To != ""))
            throw new MazapanException($"update {id} didn't bring a new mazapan: nothing to continue (mazapan rollback {id})");
        using var awake = new KeepAwake(); // the first one's, if it died
        return Finish(rec, "", new Steps(gui), ask, noRollback);
    }

    /// <summary>
    /// The update's second half, once the packages are in: the configuration
    /// written again, the checks, and then done, or rolled back.
    /// </summary>
    static int Finish(Record rec, string problem, Steps steps, bool ask, bool noRollback)
    {
        var baseline = rec.Baseline;
        Session? s;
        List<Applying.Change> changes = [];
        List<string> orphans = [];
        Owned owned = new();
        steps.Start("config", "Configuration");
        try
        {
            s = Load();
            (changes, orphans, owned) = s.Plan();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Without a configuration there is nothing to check against.
            problem = "the configuration doesn't load after the update: " + e.Message;
            steps.Fail("config", problem);
            s = null;
        }
        if (s != null)
        {
            try
            {
                using (ApplyLock.Take()) s.Write(changes, orphans, owned, false);
                steps.Ok("config", "re-applied");
            }
            catch (ConflictException ce)
            {
                // Files you edited by hand: nothing broke, but the configuration
                // wasn't re-applied. Say so, loudly, and keep it in the record.
                rec.Note = (rec.Note + "; configuration not re-applied: you edited " +
                    Plural(ce.Paths.Count, "generated file", "generated files") + " (mazapan apply --adopt)").TrimStart(';', ' ');
                steps.Skip("config", ce.Message);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                problem = "the configuration couldn't be written: " + e.Message;
                steps.Fail("config", problem);
            }
            steps.Start("checks", "Checks");
            try
            {
                rec.Checks = RunChecks(s, "", quiet: true);
                var broke = Regressions(s, baseline, rec.Checks);
                if (broke.Count > 0 && problem == "") problem = Plural(broke.Count, "check failed", "checks failed");
                var ran = rec.Checks.Count(c => !c.Skipped);
                foreach (var b in broke)
                {
                    Console.WriteLine($"  {Style.Red}✗{Style.Reset} {b.Name} {Style.Dim}({b.Plugin}){Style.Reset}");
                    foreach (var l in b.Output.Split('\n')) Console.WriteLine($"      {Style.Dim}{l}{Style.Reset}");
                }
                if (broke.Count > 0) steps.Fail("checks", string.Join(", ", broke.Select(b => b.Name)) + " broke");
                else steps.Ok("checks", ran == 0 ? "none to run here" : Plural(ran, "check passed", "checks passed")
                    + (Checks.Failed(rec.Checks).Count is var f and > 0 ? $" ({f} already failing before)" : ""));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                if (problem == "") problem = "the checks couldn't run: " + e.Message;
                steps.Fail("checks", e.Message);
            }
        }
        // What the generated files are after the update, written or not: a
        // later rollback must tell them apart from what a later apply wrote.
        try
        {
            rec.AfterOwned = Apply.LoadOwned(Apply.StatePath());
        }
        catch (MazapanException) { }
        TrySave(rec);

        if (problem == "")
        {
            rec.Outcome = Outcomes.OK;
            rec.Finished = DateTimeOffset.Now;
            rec.Save();
            // What it updated: packages, Flatpak apps, apps from their makers
            // (those that didn't update are in the note); else the configuration.
            var parts = new List<string>();
            if (rec.Changes.Count > 0) parts.Add(Plural(rec.Changes.Count, "package", "packages"));
            if (rec.Apps.Count > 0) parts.Add(rec.Apps.Count <= 3 ? string.Join(", ", rec.Apps) : Plural(rec.Apps.Count, "app", "apps"));
            var what = parts.Count == 0 ? "the configuration" : string.Join(" and ", parts);
            Console.WriteLine($"\n{Style.Green}Updated {what}; everything checks out.{Style.Reset}");
            if (rec.Note.Contains("not re-applied"))
                Console.WriteLine($"{Style.Amber}But the configuration wasn't re-applied: see above.{Style.Reset}");
            Afterwards(rec, steps, ask);
            return 0;
        }

        if (noRollback)
        {
            rec.Outcome = Outcomes.Failed;
            rec.Finished = DateTimeOffset.Now;
            TrySave(rec);
            Console.WriteLine($"\nLeft as it is (--no-rollback). To undo it: mazapan rollback {rec.ID}");
            throw new MazapanException(problem);
        }
        Console.WriteLine($"\n{Style.Red}{Capitalize(problem)}: rolling the update back.{Style.Reset}");
        steps.Start("rollback", "Rolling back");
        Rollback(rec);
        steps.Ok("rollback", "as it was before");
        // The system is fine, but the update didn't happen: say so to scripts.
        throw new MazapanException("update rolled back: " + problem);
    }

    /// <summary>
    /// After a good update: what needs a restart (asked, from a terminal),
    /// and the packages nothing needs anymore (offered, never removed alone).
    /// </summary>
    static void Afterwards(Record rec, Steps steps, bool ask)
    {
        RestartReplaced(rec.Changes);
        var reasons = RebootReasons(rec.Changes);
        foreach (var r in reasons) steps.Reboot(r);
        if (reasons.Count > 0)
        {
            var why = reasons.Contains("kernel") ? "The kernel changed" : "Hyprland changed under the running desktop";
            Console.WriteLine($"{Style.Amber}{why}: restart to use the new one.{Style.Reset}");
            if (ask && Confirm("Restart now?"))
            {
                Exec.Run("systemctl", ["reboot"]);
                return;
            }
        }
        if (!ask) return;
        var orphans = Packages.Orphans();
        if (orphans.Count == 0) return;
        Header(Plural(orphans.Count, "package nothing needs anymore", "packages nothing needs anymore"));
        Console.WriteLine("  " + string.Join(" ", orphans));
        if (!Confirm("Remove them?")) return;
        var pre = Packages.Installed();
        if (!Packages.RemoveOrphans(orphans))
            Console.WriteLine($"{Style.Amber}They weren't removed (pacman -Rns {string.Join(" ", orphans)}).{Style.Reset}");
        // Part of this update: a rollback puts them back with the rest.
        rec.Changes.AddRange(Packages.Diff(pre, Packages.Installed()));
        TrySave(rec);
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
    static List<Result> RunChecks(Session s, string title, bool quiet = false)
    {
        if (!quiet) Header(title);
        if (s.Out.Checks.Count == 0)
        {
            if (!quiet) Console.WriteLine("  (no plugin declares checks)");
            return [];
        }
        if (!Checks.InSession() && !quiet)
            Console.WriteLine($"  {Style.Dim}Outside the graphical session: checks that need it are skipped.{Style.Reset}");
        var tty = IsTerminal(1) && !quiet;
        var results = new List<Result>();
        foreach (var c in s.Out.Checks)
        {
            if (tty) Console.Write($"  … {c.Name}");
            var r = Checks.RunOne(c);
            results.Add(r);
            if (tty) Console.Write("\r\u001b[K"); // replace the "…" line
            if (quiet) continue;
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
                        Console.WriteLine($"  {Style.Dim}{Tilde(p)} changed after the update (mazapan apply): left as it is{Style.Reset}");
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
            throw new MazapanException("rollback incomplete: " + note);
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
        RestartReplaced(rec.Changes);
        Console.WriteLine($"\n{Style.Green}Rolled back: the system is as it was before {rec.ID}.{Style.Reset}");
        if (failed.Count > 0)
            Console.WriteLine($"{Style.Amber}{Plural(failed.Count, "check is", "checks are")} still failing: see mazapan doctor.{Style.Reset}");
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
        if (rec == null && args.Length > 0) throw new MazapanException($"no update {args[0]} (see mazapan history)");
        if (rec == null) throw new MazapanException("no update to roll back");
        if (!rec.Live()) throw new MazapanException($"update {rec.ID} was already rolled back");
        // Undoing an old update under newer ones would leave some packages
        // at old versions and others at new ones: a partial downgrade.
        if (newer.Count > 0)
            throw new MazapanException($"newer updates are still in place ({string.Join(", ", newer.Select(r => r.ID))}): roll those back first, newest first");

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
