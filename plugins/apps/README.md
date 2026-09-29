# Apps

Install apps by what you'll use the computer for, as Windows and macOS
ask: cards for Development, Gaming, Creative, Office, Streaming, Trading…
(`SUPER + ALT + A`, or "Apps" in the palette). One button installs a
whole profile, or choose several (Development + Gaming + Office) and
install them together; a click on the card shows its apps, each with a
box, for whoever wants to choose. Or every app by kind, with a search; what's
installed has Open and Remove.

Before it runs, what it will do in one line (6 apps, 546 MB to download,
1.8 GB on disk), every package a click away. The password through the
polkit dialog, progress here, no terminal. Apps come from Arch's official
repositories, or Flathub for what they don't have (Steam, Heroic), or are
sites as apps (web apps); a myarch plugin (Markets) is added from the
Plugins panel, which shows what it can do first. No AUR.

Every install and removal is in the History, with its undo. Removing
takes out only what the catalog put there, never what something else
needs. From a terminal: `myarch apps`, `myarch apps install ID…` (an
app's id or a profile's), `myarch apps remove ID…`.

The catalog is `catalog/apps.toml` in myarch's repository: data, not
code; add an app or a profile with a pull request.
