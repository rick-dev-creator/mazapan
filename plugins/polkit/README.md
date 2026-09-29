# Password prompts

When an app needs rights it doesn't have (mount a disk, change the time,
install something), polkit asks for a password. This is what asks: a card
over the dimmed desktop, in the theme, saying what's asked for (in the
app's words) and which action it is. A wrong password shakes it and lets
you try again; Esc or Cancel refuses. Several admins: pick whose password.
A fingerprint reader's prompts show there too.

Without an agent, those requests fail without a word. Polkit takes one
agent per session: if another one (GNOME's, KDE's, hyprpolkitagent) got
there first, `myarch doctor` says so.

The password goes to polkit and nowhere else: the field is emptied as soon
as it's sent.
