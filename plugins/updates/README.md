# Updates

Updates you see. The bar shows an arrow and how many there are (the whole
system's packages, and the Flatpak apps'), looked for every few hours;
the panel shows what changes (★ what your desktop depends on), Arch's news
to read before updating (! the ones that ask you to do something), and
whether a new kernel needs a restart. "Update now" runs `mazapan update`
with the password in polkit's dialog: a snapshot before, the desktop's
checks after, rolled back if they fail.

While it runs, the panel shows its steps, each with its ✓ (getting ready,
the keys, the packages, the configuration, the checks), and offers a
restart when the kernel or Hyprland changed. When the update brings a new
Mazapan, what's new in it comes first, from its changelog.
