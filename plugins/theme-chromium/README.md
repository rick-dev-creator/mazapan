# Chromium theme

Chromium, Chrome, Brave and Edge in the theme, in every profile: each one
is set to its GTK mode, so its frame, tabs, toolbar and font come from the
GTK theme (plugin GTK theme). Other apps built on Chromium keep their own
look.

That setting lives in each profile's Preferences, a file the browser
rewrites all the time: only that one key is Mazapán's. While the browser
is open its file is left alone (it would write its own copy back), and
it's set the next time, with the browser closed.

The accent stays the browser's own: only a system policy can set it, and
a policy that sets a theme color (BrowserThemeColor) wins over GTK mode.
