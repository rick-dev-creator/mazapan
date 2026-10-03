# Crash watcher

When an app closes unexpectedly — the desktop itself included — a
notification says which and how, with **Ask an agent**: that opens the
agent plugin's agent in a terminal with mazapan's report, the crashes of
the last day in it.

Crashes are read from systemd-coredump (`coredumpctl`), since the last one
said, so one that took the desktop down is said at the next login. The same
app crashing over and over is said once every ten minutes.

Optional: it's off until you turn it on (Settings › Plugins).
