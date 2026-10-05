# Plugins

Where you find, install and set up plugins, like an editor's extensions
view (`SUPER + SHIFT + P`, or Plugins on the palette's first screen).
Every plugin Mazapan knows about, each marked by who made it:
**Mazapan** (its own, in `plugins/`), **Community** (made by others:
shipped with Mazapan in `community/`, so they install offline, or listed
in a catalog and fetched from git), or yours. Tabs for the installed
ones, the ones to install, the ones for this machine, and all, each with
its count; below them, who made it (anyone, Mazapan, Community). `/`
searches, and a search looks everywhere, installed or not, whatever the
tab. When a catalog can't be reached (offline), the panel says so.

Each plugin has its page: its README (without images or HTML), what it
can do (the risky parts marked), and its settings, changed in place.
Installing one shows what it can do first; "Allow and install" installs
the very version you looked at.

Turning plugins on and off and changing settings go through `mazapan
apply`, so `mazapan undo` takes them back. What asks for a password or
an approval (a hardware plugin's parts that run as root, an update that
can do more than before) opens in a terminal, and the panel comes back
when it's done.

"Update plugins from git", in the palette, runs `mazapan plugins update`
in a terminal.

Settings: `key` (`SUPER + SHIFT + P`) opens the panel; `terminal`
(`foot --hold`) is the terminal for what asks, kept open to read.
