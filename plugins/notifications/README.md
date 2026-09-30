# Notifications

Every app's notifications, in the theme.

- **Banners** come in at the top right of the focused screen, three at
  a time (the rest take their turn; all are in the center). The pointer on them stops their
  time; their actions show then, and Reply for apps that take one. An
  urgent one has a red edge and stays until you dismiss it. A click does
  what the app offers first, or brings its window; a right click puts it
  away.
- **The center** (`SUPER + N`, or the bell in the bar): only the
  notifications, stacked by app (a click opens a stack), with their time,
  actions and replies, and Clear all. Per app (⋯): banners or only the
  center; allowed during Do Not Disturb or not.
- **Do Not Disturb** (`SUPER + SHIFT + N`, a right click on the bell): by
  hand, every day between two times (`quiet_from`, `quiet_to`), and while
  a window is full screen. What arrives meanwhile waits in the center, and
  one banner says how much once it's over. Urgent ones still show
  (`quiet_critical`).
- The bell is in the bar only when something's unread or Do Not Disturb
  is on.

It's the desktop's notification server (`org.freedesktop.Notifications`):
another one running or installed (mako, dunst…) would take its place;
`mazapan doctor` says so. Banners hold back while a screenshot or a
recording is made. Apps are known by the name they give: a rule for one
(Do Not Disturb) is for whatever uses its name. Notifications are kept in
`~/.local/state/mazapan-notifications/`. What they say is shown as text:
no links opened, no images fetched from anywhere.
