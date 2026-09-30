# Installer

The ISO's installer: this system onto a disk, in a few screens. The
language (the live desktop switches at once), the keyboard (tried right
there), where you are, the disk (all of it: only which one, with what's on
it now said), your account (name, user, password, the computer's name,
from your name), and what you'll use the computer for: profiles, several
at once, each app to see and change for whoever wants to. A review, and
then `mazapan install run` does it with archinstall, from the ISO's own
repository: nothing is downloaded.

Only on the ISO's live system (`[hardware] live = true`). On the first
start, the welcome picks up where it left off, and the apps chosen here
install as soon as there's a connection.

Without disk encryption, `/boot` is on btrfs (snapshots boot with their
own kernel) and the login screen is the `login` plugin's; encrypted, the
disk's password is the login.
