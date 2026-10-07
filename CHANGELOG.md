# Changelog

What changed in each release, newest first. The updates panel shows what's
new since the version a system has. Changes go under "Unreleased" until a
release (a tag `vX.Y.Z`) names them.

## Unreleased

- The Control Center: a plugin can take the place of one of its parts by
  giving its own the same name and a higher number
  (`control/sections/31-media.qml` over `30-media.qml`). With the plugin
  off, the original is back.

## 0.4.1 (2026-10-07)

- After an update that wanted a restart (a new kernel), the Updates panel
  and the bar kept asking for it after the restart too: they read the last
  run's log again. Now a restart asked for before the computer last
  started counts as done.

## 0.4.0 (2026-10-07)

- The plugin registry: plugins others make live in their own repositories,
  and [mazapan-plugins](https://github.com/rick-dev-creator/mazapan-plugins)
  lists them, each by its tag and the commit that was looked at. The
  Plugins panel and `mazapan plugins add` read it from
  mazapan.dev/plugins/index.toml (a copy ships for when it can't be
  reached), install exactly that commit even if the tag moves, and follow
  the registry's newer versions (the Updates panel says when there's one),
  asking again for anything new they'd be able to do.
- mazapan.dev/plugins: every plugin with its icon, screenshots, README and
  what it can do, a page each, and a feed of what's new.
- Markets and Pomodoro moved to their own repositories
  ([mazapan-markets](https://github.com/rick-dev-creator/mazapan-markets),
  [mazapan-pomodoro](https://github.com/rick-dev-creator/mazapan-pomodoro)),
  listed in the registry. Where one was on, the next update installs it
  from there, exactly as it was (its settings and history kept); until
  then its files stay as they are.
- plugin.toml says who made a plugin, where it lives and its license, and
  how it looks (`[gallery]`: an icon, screenshots). `mazapan plugins
  check` asks for them in a plugin to share, refuses a key a built-in
  plugin uses, and says it all as JSON (`--json`).

## 0.3.2 (2026-10-07)

- Apps from their makers: their download is tried again too when the
  network doesn't answer (four times, five seconds apart), not only the
  question for their latest version: a home router's DNS that misses a
  question now and then no longer leaves an app out.

## 0.3.1 (2026-10-07)

- Apps from their makers (Herdr, VS Code, JetBrains…): asked again for a
  few seconds when the network doesn't answer, instead of left out at
  once. The first login's apps start as soon as there's a connection, and
  its DNS can take a moment more: they were left out then ("not
  everything was installed").

## 0.3.0 (2026-10-07)

- Default apps: the terminal is one of them (Settings, Default apps):
  SUPER + Enter, the palette's terminal apps, Plugins, Updates and a
  crash's details open the one chosen (xdg-terminal-exec), not always
  foot. Each kind offers every installed app that opens it, not only a
  fixed list; and in Apps, an installed app that can be the default has
  "Use as default …" beside it.
- Share, like AirDrop: files to a phone or computer nearby over Bluetooth,
  as bubbles with their progress; one not paired is paired right there,
  its code confirmed in the panel. Files a phone sends here: the panel
  opens to accept them, shows them arriving, and they go to Downloads
  (ask, from paired devices without asking, or never). Pairing from the
  phone: the Share switch in the Control Center makes this computer
  visible for three minutes. A send button beside a paired phone in the
  Bluetooth card; "Choose files…" in the panel and the Control Center.
  Pairing a phone from the Bluetooth card now asks for its code too (it
  had nobody to ask).

## 0.2.1 (2026-10-06)

- Plugins: changing a plugin's settings keeps the list where it was and the
  plugin selected, instead of jumping back to it; quick changes in a row are
  applied together, once, instead of one reload each.
- Pomodoro: changing its settings doesn't reload the shell any more; the
  timer, the panel and the bar follow them as they change.

## 0.2.0 (2026-10-06)

- A version is its number: `mazapan --version` says "mazapan 0.2.0", no name beside it.
- Pomodoro, a community plugin: focus in sessions with breaks between them.
  A ring that fills as the time goes, the time left and when it ends, the
  sessions of each set, what you're working on; start, pause, skip, five
  more minutes. Breaks start by themselves (or wait), and so can the next
  session; a sound and a notice say when each phase ends, the notice
  starting the next. Do Not Disturb while you focus. Today against your
  daily goal, minutes focused, the streak of days and the week. In the bar
  while a session is on, in the Control Center, the palette and SUPER +
  ALT + P; every length and switch in its settings.
- The palette: typing a place's name finds the place first ("wallpaper"
  opens the wallpaper picker, before the actions that start with the same
  word). Next wallpaper with no pictures of yours opens the picker, which
  says where to put them; the theme's own wallpaper when it's already the
  one says so, instead of nothing.

## 0.1.0 — Mazapan (2026-10-05)

- The apps chosen in the installer go in with the system: Basic's (the
  browser, files, pictures, documents, video) from the ISO itself, no
  connection needed; the other profiles' packages right after, when the
  installer is online. The first start has them; only apps from Flathub,
  their makers or the web are left for the first login. The Apps menu
  counts them as its own, to remove them later.
- The Wi-Fi joined in the installer connects by itself after the restart:
  it was kept tied to the live system's name for the card, so it was never
  tried and its password was asked again.
- Without a connection at the first login, a notification says the apps you
  chose will be installed as soon as you connect, instead of nothing.
- Coding agents in Apps, the installer and the welcome: a Coding agents
  profile with Claude Code (from Anthropic itself, checked, and it keeps
  itself up to date; one installed by hand counts as there), Codex,
  OpenCode, Gemini CLI and Qwen Code, and two ways to run several at once:
  T3 Code (a window for them, each on its own branch) and Herdr (side by
  side in the terminal), both from their makers' releases on GitHub,
  checked against the checksum GitHub keeps, updated by mazapan update.
  The Development, .NET and Mobile profiles bring them too (all but Qwen
  Code). Found in the palette too ("codex", "agents"). With none installed, the
  Control Center's Agents section offers to install one; with some, "More
  agents".
- The weather, market prices, the check for updates and the agents' limits
  are fetched again as soon as the internet comes back, not at their next
  turn hours or minutes later; a forecast that failed online is tried again
  in a minute. Plugins can follow the connection too (Online, in the plugin
  API).

- The columns' keys work on every keyboard: equal widths is SUPER + W,
  phone width SUPER + P, a window into the next column SUPER + CTRL + ←/→
  (`=`, `[` and `]` need SHIFT or ALTGR on Spanish, Latin American or
  German keyboards, so those keys couldn't be pressed there). Keys you set
  yourself stay as they are.
- Learn (SUPER + F1, or Learn in the palette): short lessons on moving
  around, each a real recording of the move with its keys lit as it
  happens, then tried for real on practice windows of their own and
  ticked off. Holding SUPER shows every key, grouped, until it's let go
  (SUPER + K too). In the palette, an action a lesson teaches shows its
  recording beside it; the welcome offers the tour at the end.
- The overview and the workspace previews let go of their window captures
  when they hide: a screen recording followed by closing a window that had
  been in the overview brought the whole shell down.
- Updates with nothing to update say only that, without Arch's news or a
  warning that looked like a manual update was needed; the news shows
  before an update, each in full, the ones asking for a step by hand
  saying so. An error is said in full under the title, never past the
  window's edge.
- Apps without a window of their own (Lazygit, GitHub CLI, mise) are found
  in the palette as installed, and open in a terminal.
- The command palette, redesigned: your apps first, with their icons;
  results in sections (best match, apps, windows, actions, settings, not
  installed) and filters you can see; every row says what Enter does, and
  a panel beside it says what it is and where it opens. Settings pages and
  switches (Wi-Fi, Bluetooth, night light…) are found there too. An app
  that doesn't open says why instead of doing nothing, and terminal apps
  such as Neovim open in a terminal, from Apps too.
- Joining a Wi-Fi: the password is asked right under the network, with an
  eye to see what you type, Connect and Cancel, "Connecting…" while it
  tries, and the error in place with what you typed kept to fix it. Esc
  cancels only the password. The same in the bar, the welcome and the
  installer.
- Plugins says who made each one: Mazapan, or the Community. Community
  plugins (Markets, to start) come with Mazapan and install from the panel
  even offline; a search finds them, installed or not, whatever the tab,
  and the tabs show how many each holds.
- The apps chosen in the installer are installed for sure: at the first
  login once the repositories answer, and the list is kept until they're
  all there. A cancelled password or a failure offers to try again (and
  the next login tries again); one app that can't be installed (its
  maker unreachable, a package gone from the repositories) no longer
  stops the others. The Wi-Fi joined in the installer comes with the
  installed system, so it starts online.
- A Control Center: the icons on the bar's right become one status pill
  that says when something needs you (an agent waiting, a recording, the
  battery low, an update ready), in its color. A click or SUPER + A grows
  it into one panel: what needs you now, each with its action; Wi-Fi and
  Bluetooth with their lists right there, Do Not Disturb, night light, keep
  awake, power saver and displays as switches; the sound and where it goes,
  the brightness, the music playing, your agents, notifications and the
  power buttons. In the theme, as everything; off in Plugins, and the bar
  is as it was.
- Plugin pages in the Plugins panel are in English in every language (the
  repository's docs are English only); names, descriptions and settings
  are still translated.
- The notification center closes with a click outside it again.
- Notification banners go away after their time again.
- .NET: the HTTPS certificate and Aspire's templates are set up until it
  works (an SDK installed later, or no connection the first time).
- Settings › Default apps lists the apps you have.
- Copying a capture's text takes a second, not twenty.
- Databases: Redis's connection URL logs in.
- History says what an entry changed, in words; undoing your last change
  no longer asks for a password when nothing of the system's changed.
- Updates name the apps they updated besides packages.
- The market ticker no longer moves the icons beside it; reminders show
  an alarm clock, not the notifications' bell.
- The welcome's last step fits in its card.

- Gaming: Steam from Arch (on the system's drivers), and on hybrid laptops
  Steam, Lutris and Heroic on the NVIDIA card by themselves; the 32-bit
  drivers for this computer's GPUs (AMD, Intel, NVIDIA); games drawn with
  the least delay and the screen never dimming mid-game; the Game mode on
  while a game is open; gamescope, ProtonPlus and LACT in Apps.
- Retro and emulators: a profile with RetroArch (the classic consoles and
  arcades), DuckStation, PCSX2, Dolphin, PPSSPP, melonDS, Azahar, Cemu,
  RPCS3, xemu, ScummVM and DOSBox, and ~/Games ready for your games and
  BIOS.
- Seven screensavers: the mazapán bouncing about, an 80s terminal, code
  rain, stars, the Game of Life, pipes and the glow, or one at random; each
  tried from the palette.
- `mazapan apply` from a console or SSH reloads the bar too.
- Checkpoints anyone understands: the system as it was before every
  package change, in the boot menu ("Checkpoints"), encrypted disks too.
  Started from one, a card and a mark in the bar say so, with three
  answers: keep this one (it becomes your system; the one it replaces is
  kept a week), back to my system, or ask an agent what broke (what
  changed, read only). History restores one from the running system.
  `mazapan checkpoint`, and `checkpoints` / `checkpoint_diagnose` for
  agents.
- Niri's way of tiling: a new window opens to the right at half the
  screen and the others keep their width; SUPER + R cycles a column's
  width, SUPER + F gives it all, SUPER + [ ] put windows in a column and
  out, SUPER + Page Up/Down go through workspaces (a new one after the
  last), SUPER + the wheel and three fingers scroll the strip. The bar
  shows where you are on it, and SUPER + Tab shows every workspace at once.
- Capture never hangs: a screen that isn't drawing (turned off) no longer
  keeps it from opening again; it's woken and tried once more, or it says
  which screen. Screen permissions no longer ask about the desktop's own
  tools ("allow grim?", "an unknown app") after turning them on soon after
  logging in, or after an update replaced the screen-sharing portal.
- A fingerprint reader unlocks the screen and allows system changes
  (offered where there's one; fingers added from the palette).

- Close the laptop's lid with another screen connected and keep working on
  it; the brightness keys change external screens too ("External
  screens' brightness" in Plugins).

- Record the screen with your camera in a corner ("Record with camera").

- The theme in kitty, Ghostty and Alacritty (installed from Apps: open
  windows follow at once) and in Obsidian (a Mazapan theme in every vault).
- Apps sets up an app you already had when you ask to install it (its
  theme, its setup).

- Languages in one click in Apps: Python (with uv), Go, Rust, Ruby, PHP,
  Java, Bun, Deno, Zig, Elixir, each with its language server.

- Databases for development in one click (PostgreSQL, MySQL, Redis, SQL
  Server, MongoDB): start, stop, copy the .NET connection string or a URL.
- SUPER + B goes to the browser (opening it if it isn't), SUPER + E to the
  files.

- Containers ready to use: Podman by default in the Development and .NET
  profiles (`docker` and `docker compose` work on it, and Testcontainers,
  devcontainers and Aspire find it), Podman Desktop to see them; Docker
  itself from Apps, its service ready and no sudo needed (as said).
- Mission Center, to see how the computer is doing, in the basic apps.
- Commands in `~/.local/bin` (yours, pip's, npm's, rider, code) work from
  any terminal.

- Ask an agent about a notification (in the center, on the pointer), share
  a capture ("Share" in its bar) or something copied before (Ctrl+S in the
  clipboard history).

- Ask an agent by voice: SUPER + CTRL + A listens, the same key asks, the
  answer in the agent's card (dictation, worked out on this computer).

- Recent projects in the palette: type a project's name to reopen it in
  your editor with the agent's last conversation there.

- An agent's change to the desktop (through mazapan's MCP server) waits
  for you: a card with who asks and the exact diff, Allow or Don't allow.
  History marks its changes with the agent's name and undoes a day's of
  them at once.
  "Always allow" trusts an agent from then on (revoked from the palette).

- `? question` in the palette: a coding agent answers in a card (Claude
  with the account that has room and a look at this desktop's state, never
  changing it), to copy or continue in a terminal. The same about a
  capture ("Ask" in its bar), the selected text, or files from Files.

- Share: what you copied, or files from Files (right click, Scripts,
  Share), to a device nearby with LocalSend or to one of yours with
  Tailscale. Tailscale from Apps comes ready: no sudo, sign in from the
  palette, files sent to you land in Downloads with a notification. The
  firewall lets LocalSend in.
- A plugin turned off now undoes what its files' reloads did (a service it
  started is stopped), for your own files as for system ones.

- Omarchy's themes, all 22 at once ("Themes: import Omarchy's"): the ones
  on this computer, or fetched from Omarchy, or any Omarchy theme's
  repository; each with its palette and wallpaper, every contrast checked.

- Agents: API keys kept in the keyring, not in plain files ("Agents: an
  API key" in the palette); `mazapan agents run` gives them to opencode, pi
  and the others, never to Claude Code or Codex. What OpenRouter, Anthropic
  and OpenAI bill shows in the card and the dashboard, with an OpenRouter
  key's limit. A dot on the workspace where an agent waits for you, works
  or is done.
- Agents: what Claude Code did, from its own OpenTelemetry metrics sent to
  this computer only: lines written and taken out, commits, pull requests,
  your time and the agent's, in the dashboard.

- Agents: Claude Code's hooks can't block a prompt even with an older
  Mazapan (they never fail), and say when work goes on after a permission;
  a minute idle after an answer no longer reads as "waiting for you".
  Limits at 1 % no longer read as full. The dashboard ends when closed,
  keeps a day's bar on its day in any time zone and shows 30 and 90 days
  right. Limit alerts aren't repeated after the shell reloads.
- Apps from their makers: the version before is kept while an open IDE may
  use it; an update and the Apps panel never install or remove the same one
  at once; a stalled download gives up.
- Node.js already installed (an LTS one) is kept by the Mobile profile.
- Hardware: kernel updates are no longer rolled back on Surface, older
  MacBooks and Broadcom wl machines (their checks look at every installed
  kernel), and the installer no longer stops there.

- Mazapan updates itself: its own signed repository, in two channels
  (stable, and edge with every release first): `mazapan channel` says which
  one and switches, `mazapan version` says the version.
- Updates in a few steps, each with its ✓, from the terminal or the panel:
  room and power checked and the machine kept awake, the keyrings first,
  a failed initramfs rolled back, what needs a restart offered, what's new
  in Mazapan shown. A new Mazapan finishes the update it came in.
- The disk encrypted by default, with a recovery key (shown as text and a
  code to photograph) and one password: typed as the computer starts, it
  logs in and opens the keyring.
- Locked before it sleeps: the lid never opens on an unlocked desktop.
- The firewall on: nothing comes in that wasn't asked for.
- Unattended installs: a drive labeled cidata with the installer's answers
  (mazapan.json) installs by itself.
- The menu: SUPER + Space with nothing typed shows a tile for each place
  (Apps, Updates, Settings, Theme…), the power row and the open windows;
  typing finds apps that aren't installed too, to install them.
- The password changed in one place (Settings › Security, or `mazapan
  password`): the disk's, the account's and the keyring's together.
- Privacy dots in the bar while the microphone, the camera or the screen
  is in use.
- Hibernation on laptops: nothing lost when the battery dies.
- Settings › Text: the fonts (each shown in itself) and the text's size,
  over the theme's.
- Settings › Keys: every keybinding in one list, changed by pressing the
  new keys (one already taken is said).
- Updates: firmware (fwupd) and plugin updates in the same panel; updates
  downloaded ahead in the background, on power and unmetered only.
- The boot menu and the boot splash (with the disk's password) in the
  theme's colors.
- Wi-Fi shared as a QR code, and a speed test, from the network card.
- Extras, off until turned on: reminders (a bell in the bar), a crash
  watcher that offers to ask an agent, and a screensaver in the theme.
- The Apps menu takes catalogs from others (`app_catalogs` in config.toml).
- A keyboard picked in Settings is tried first: the one before comes back
  by itself unless it's kept.
- What each Flatpak app may reach (the internet, sound and microphone,
  devices, your files, Bluetooth), switched in Apps › Permissions or with
  `mazapan apps permit`.
- What a Flatpak app asked for through the system (the camera, the
  location…), answered again or forgotten, in Apps › Permissions.
- An app asks before it sees the screen; the desktop's own tools don't.
- Dictation, on the computer itself: speak, and it's typed where you are.
- Capture: what a QR code holds, copied as a secret (never shown nor kept
  in the clipboard history); text read in your language.
- Every built-in plugin has its page in the Plugins panel.
- Installing an app works before a repository was ever fetched (installed
  offline, or Mazapan's repository newly added).
- Agents: every coding agent and account found by itself (Claude Code with
  each of its accounts, opencode, pi, Codex); in the bar, which session
  works and which waits for you (a click goes to its window), each
  account's limits and when they reset, what they used today; a
  dashboard with cost and tokens by day, model and project; Claude
  launched with the account that has room.
- Apps: the .NET profile (ASP.NET Core and Aspire set up: the HTTPS
  certificate trusted, Aspire's templates, containers on Podman, Rider
  and Visual Studio Code) and the Mobile (Expo) one (Node.js, Java 17,
  Android Studio, phones over USB). Apps can come from their makers,
  checked against their published checksums and kept up to date by
  mazapan update.
- 18 hardware fixes as hardware plugins (credited in
  THIRD_PARTY_NOTICES.md), each offered only on the machines that need it
  (Apple, ASUS, Surface, Framework, Broadcom, Intel Wi-Fi 7 and lpmd,
  Vulkan, nouveau).
- The agents' views quieter: each agent its own color, the data in
  neutral tones, amber and red only where something needs you.
- Fixed after an audit: the privacy dots no longer keep a processor busy,
  and no app name can hide them; changing the password checks the current
  one first; a plugin that can't be read keeps its files; hibernation only
  where its swap file fits; updates downloaded ahead in a folder of root's
  own; the text size slider applies; Wi-Fi codes right for any name or
  password; the boot menu never stops grub.cfg from being written.
- A plugin that can't be read is left out and said; the rest of the
  desktop still applies.
