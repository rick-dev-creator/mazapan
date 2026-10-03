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
sites as apps (web apps); a Mazapán plugin (Markets) is added from the
Plugins panel, which shows what it can do first. No AUR.

Every install and removal is in the History, with its undo. Removing
takes out only what the catalog put there, never what something else
needs. From a terminal: `mazapan apps`, `mazapan apps install ID…` (an
app's id or a profile's), `mazapan apps remove ID…`.

The catalog is `catalog/apps.toml` in Mazapán's repository: data, not
code; add an app or a profile with a pull request.
Others' catalogs, in the same form, are added in config.toml:
`app_catalogs = ["https://example.com/apps.toml"]`; their apps say whose
they are.

A Flatpak app's **Permissions**: what it may reach (the internet, sound and
microphone, devices, your files, all files, Downloads, Bluetooth), each a
switch, from its next start; "Its own again" undoes them. Only Flatpak apps
can be held back: one from the repositories reaches whatever you can.
From a terminal: `mazapan apps permissions ID`, `mazapan apps permit ID
network off`.
