# History

What changed on the desktop, in words, newest first, grouped by day
(`SUPER + ALT + H`, or "History" in the palette):
- a theme or accent ("Theme: Gruvbox → Paper", with both palettes);
- a setting ("Volume: Max volume default (1) → 1.25");
- a plugin turned on or off;
- files rewritten with nothing changed in your settings (a plugin's new
  version), by plugin;
- an update (`myarch update`), and how it went;
- a system snapshot, with `hw-snapshots`: the system before and after a
  package change, bootable from the boot menu with `hw-snapshots-grub`.

Each one has its own undo. An older change is undone on its own: what it
changed and is still as it left it goes back (a setting from this
morning, without losing the theme you picked since), through a new apply
that shows up here too. Files only: the last change's. An update: rolled
back in a terminal (it asks for your password).

From a terminal: `myarch timeline` and `myarch timeline undo ID`.
