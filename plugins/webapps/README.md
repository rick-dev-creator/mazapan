# Web apps

A site as an app of its own: its own window without the browser's bars,
its own icon (the site's, fetched when you add it), in the palette and
the launcher like any app. Opened again, the window already open comes
forward instead of a second one.

"Web apps" in the palette lists the ones you have (a click opens one, the
bin removes it), adds one from a name and an address, and suggests a few
(WhatsApp, Gmail, Calendar…) to add with a click. None is added unless
you ask.

They open in your default browser if it's Chromium-based, else in the
first one installed (Chromium, Chrome, Brave, Vivaldi, Edge…); Firefox
has no app windows. Logins are the browser's: signed in there, signed in
here.

Each one is yours, not myarch's: `~/.local/share/applications/myarch-webapp-*.desktop`
and its icon in `~/.local/share/myarch-webapps/`; they stay if the plugin
is turned off. From a terminal: `sh ~/.local/share/myarch/bin/webapp add
NAME URL`, `remove ID`, `list`, `open ID`.
