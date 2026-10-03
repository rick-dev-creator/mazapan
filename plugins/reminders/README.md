# Reminders

A bell in the bar. A click: write what to remember, then pick when — in
10 or 30 minutes, in an hour, tonight, tomorrow morning, or at a time
(17:30). When it's time it's said as a notification. One that came due
while the computer was off or asleep is said at the next start, with the
time it was for.

Optional: it's off until you turn it on (Settings › Plugins). From the
palette, "Remind me of something" opens the card; from a script:

    qs ipc -c mazapan call reminders add "Call Ana" 30

They're kept in `~/.local/state/mazapan/reminders.json`, on this computer
only.
