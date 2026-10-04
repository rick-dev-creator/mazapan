# Changelog

What changed in each release, newest first. The updates panel shows what's
new since the version a system has. Changes go under "Unreleased" until a
release (a tag `vX.Y.Z`) names them.

## Unreleased

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
  firewall lets LocalSend in, as Omarchy does.
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
  Mazapán (they never fail), and say when work goes on after a permission;
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

- Mazapán updates itself: its own signed repository, in two channels
  (stable, and edge with every release first): `mazapan channel` says which
  one and switches, `mazapan version` says the version.
- Updates in a few steps, each with its ✓, from the terminal or the panel:
  room and power checked and the machine kept awake, the keyrings first,
  a failed initramfs rolled back, what needs a restart offered, what's new
  in Mazapán shown. A new Mazapán finishes the update it came in.
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
  offline, or Mazapán's repository newly added).
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
- 18 hardware fixes from Omarchy as hardware plugins, each offered only
  on the machines that need it (Apple, ASUS, Surface, Framework, Broadcom,
  Intel Wi-Fi 7 and lpmd, Vulkan, nouveau).
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
