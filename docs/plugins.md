# Built-in plugins

Every part of Mazapan is a plugin, with the same API a plugin of yours
uses ([plugin-api.md](plugin-api.md)). `mazapan plugins` lists them on a machine;
`mazapan plugins show ID`, or its page in the Plugins panel (`SUPER + SHIFT + P`),
says what each needs, what it can do, and its settings.

100 plugins, by what they're for.

## Agents

| Plugin | What it does |
|---|---|
| [`agent`](../plugins/agent) | Your coding agents: in the bar, which is working and which waits for you (a click goes to its window), each account's limits and when they reset, and what they've used today, for Claude Code, opencode, pi, Codex… every account found by itself; and from the palette, ask one about this desktop with mazapan's report |

## Shell and bar

| Plugin | What it does |
|---|---|
| [`shell-bar`](../plugins/shell-bar) | A bar on every monitor (Quickshell), filled with the widgets other plugins put in its left/center/right slots; ships a kit of components for them |
| [`bar-workspaces`](../plugins/bar-workspaces) | Workspace numbers in the bar, with a live preview on hover whose windows can be dragged out, and an overview of every workspace at once (as Niri's) |
| [`bar-window-title`](../plugins/bar-window-title) | The focused window's title in the bar, next to the workspaces |
| [`bar-clock`](../plugins/bar-clock) | Day, date and time in the middle of the bar, in the system's language; click for a calendar |
| [`bar-weather`](../plugins/bar-weather) | Current weather in the middle of the bar; click for details and the forecast |
| [`bar-battery`](../plugins/bar-battery) | The battery in the bar (only where there is one): its level, time left, health, the power profile and the screen's brightness; it says when it's getting low |
| [`bar-network`](../plugins/bar-network) | Network state in the bar; click for Wi-Fi (scan, join, forget) and wired connections |
| [`bar-bluetooth`](../plugins/bar-bluetooth) | Bluetooth in the bar; click to connect, pair and forget devices |
| [`bar-volume`](../plugins/bar-volume) | Output volume in the bar: scroll to change, middle-click to mute, click for outputs and inputs |
| [`notifications`](../plugins/notifications) | Every app's notifications, in the theme: banners that stop under the pointer, with their actions and replies; a quiet center stacked by app; Do Not Disturb by hand, on a schedule or in full screen |
| [`osd`](../plugins/osd) | The volume, microphone, brightness and media keys, and what they did shown for a moment on screen |
| [`privacy`](../plugins/privacy) | Dots in the bar while the microphone, the camera or the screen is in use, as macOS shows them; which app, on a click |
| [`power`](../plugins/power) | Lock, suspend, log out, reboot and shut down: a button in the bar, the palette, and a lock screen in the theme |
| [`polkit`](../plugins/polkit) | The polkit agent: when an app needs rights it doesn't have (mount a disk, change the time), it asks for your password here, in the theme |

## Finding and changing things

| Plugin | What it does |
|---|---|
| [`palette`](../plugins/palette) | One palette for apps, open windows, every plugin's actions and keybindings, each showing the command it runs |
| [`settings`](../plugins/settings) | The settings everyone needs, a page each: keyboard, touchpad and mouse, default apps, language and time zone, the screens and the look; every other one in the Plugins panel |
| [`history`](../plugins/history) | What changed on the desktop, in words (a theme, a setting, a plugin on or off, an update), newest first, each with its own undo |
| [`welcome`](../plugins/welcome) | The first login's welcome: language, keyboard, time zone, Wi-Fi, the look and apps by profile, a screen each; again from the palette any time |
| [`plugin-manager`](../plugins/plugin-manager) | Find, install and set up plugins: the built-in ones, yours and the catalogs', each with its page (README, what it can do, settings) |
| [`login`](../plugins/login) | The login screen in the theme: the clock, your name, your password (greetd, with a Hyprland of its own and a Quickshell greeter). The installer turns it on; on a system with another login manager, it takes its place |

## Windows, workspaces and screens

| Plugin | What it does |
|---|---|
| [`hypr-base`](../plugins/hypr-base) | Hyprland entry point: monitors, input, core bindings; loads every plugin fragment in ~/.config/hypr/mazapan/ |
| [`columns`](../plugins/columns) | Windows as columns on a strip that scrolls, as Niri does: each opens to the right at its own width, the others untouched; or equal widths, phone-width, a focus layout. Workspaces one after another, touchpad gestures |
| [`window-titles`](../plugins/window-titles) | One key shows or hides a title bar on every window |
| [`monitors`](../plugins/monitors) | Monitor profiles matched by EDID, applied on their own when screens come and go; a visual manager to arrange them |
| [`ddc-brightness`](../plugins/ddc-brightness) | The brightness keys (and the palette's Brighter and Dimmer) change external screens too, over the cable (DDC/CI), as the laptop's own; on a desktop, they're the only ones. From the next start |

## Look

| Plugin | What it does |
|---|---|
| [`themes`](../plugins/themes) | Pick a theme and an accent, previewed live on the desktop, with each theme's contrast checked |
| [`wallpaper`](../plugins/wallpaper) | The wallpaper: drawn from the theme, the theme's picture, or yours (per screen, filled, tinted with the theme, in turn, day and night), with a picker that previews live and makes a theme from any picture |
| [`screensaver`](../plugins/screensaver) | After a while without use, every screen shows the time over one of seven scenes in the theme's colors (the mazapán bouncing about, an 80s terminal, code rain, stars, the Game of Life, pipes, a glow), moving so nothing stays burnt in; a key or the mouse and it's gone. A video or a call keeps it away |
| [`night-light`](../plugins/night-light) | The screens warmer at night, fading in and out slowly: by the hours you give, or from sunset to sunrise where you are |
| [`theme-hyprland`](../plugins/theme-hyprland) | Borders, gaps, rounding, background and animations from the theme |
| [`theme-quickshell`](../plugins/theme-quickshell) | Theme tokens as a QML singleton (Theme.qml) for every shell widget |
| [`theme-gtk`](../plugins/theme-gtk) | GTK4/libadwaita and GTK3 (via adw-gtk3) colors, fonts and dark mode |
| [`theme-qt`](../plugins/theme-qt) | Qt 6 apps (KDE's too) in the theme: palette, font and style through qt6ct, and KDE's color scheme |
| [`theme-foot`](../plugins/theme-foot) | foot terminal: font, palette, opacity and a blinking block cursor in the accent color; open terminals recolored live |
| [`theme-kitty`](../plugins/theme-kitty) | kitty terminal in the theme: font, palette, opacity, tabs and borders; open windows recolored at once; your own kitty.conf kept |
| [`theme-alacritty`](../plugins/theme-alacritty) | Alacritty terminal in the theme: font, palette, opacity; open windows follow at once; your own alacritty.toml kept |
| [`theme-ghostty`](../plugins/theme-ghostty) | Ghostty terminal in the theme: font, palette, opacity; open windows reload it at once; your own config kept |
| [`theme-btop`](../plugins/theme-btop) | btop in the theme: its boxes, graphs and meters from the theme's colors, over the terminal's background |
| [`theme-neovim`](../plugins/theme-neovim) | A Neovim colorscheme from the theme (syntax, Treesitter, LSP, diagnostics, git, Telescope), used when you haven't chosen another; open Neovims recolor on apply |
| [`theme-vscode`](../plugins/theme-vscode) | VS Code, Code - OSS and VSCodium in the theme: its colors over VS Code's default theme (workbench, syntax, semantic tokens, terminal), following the system's dark/light; open windows follow at once |
| [`theme-obsidian`](../plugins/theme-obsidian) | Obsidian in the theme, in every vault: its colors, accent, fonts and corners as a theme of its own (Mazapan), picked once in its settings and following every change after |
| [`theme-firefox`](../plugins/theme-firefox) | Firefox and its forks (Zen, LibreWolf, Floorp, Waterfox) in the theme, in every profile: the browser's frame and its own pages |
| [`theme-chromium`](../plugins/theme-chromium) | Chromium, Chrome, Brave and Edge follow the theme through GTK (theme-gtk), in every profile: frame, tabs, toolbar and font |
| [`theme-plymouth`](../plugins/theme-plymouth) | The screen while the computer starts, and the disk's password asked there, in the theme's colors and your language (Plymouth): one design from the first screen to the desktop. The installer turns it on |
| [`theme-grub`](../plugins/theme-grub) | GRUB's menu (and the snapshots in it) in the theme's colors: one design from the first screen to the desktop. The installer turns it on |

## Everyday tools

| Plugin | What it does |
|---|---|
| [`capture`](../plugins/capture) | One capture overlay: pick a region or a window on a frozen screen, then copy, save, copy its text (OCR), annotate or record it |
| [`clipboard`](../plugins/clipboard) | What you copied, text and pictures, kept and searchable; pick one and it's pasted where you were. what's marked as secret is never kept |
| [`color-picker`](../plugins/color-picker) | Pick any color on screen: it's copied, and a notification says which |
| [`emoji`](../plugins/emoji) | Every emoji, by category and found by name in your language; the one you pick is typed where you were (and copied) |
| [`dictation`](../plugins/dictation) | Speak instead of typing: a key starts listening, the same key stops, and what you said is typed where you are. Worked out on this computer (whisper.cpp): nothing you say leaves it |
| [`reminders`](../plugins/reminders) | A bell in the bar: what to remember and when (in ten minutes, at five, tomorrow morning), said as a notification then. Missed while the computer was off, said at the next start |
| [`share`](../plugins/share) | Files, a picture or text from the clipboard to a device nearby (LocalSend) or one of your devices on your tailnet (Taildrop), from the palette or Files' right click |
| [`webapps`](../plugins/webapps) | A site as an app of its own (WhatsApp, Gmail…): its own window and icon, in the palette and the launcher; opened again, its window comes forward |
| [`default-apps`](../plugins/default-apps) | What opens links, PDFs, pictures, videos, music, text, folders, mail, documents and archives: the first installed of the usual ones, unless you picked one; what you set in an app stays |
| [`input-method`](../plugins/input-method) | Typing Chinese, Japanese and Korean (fcitx5, with Pinyin, Mozc and Hangul), in every app; CTRL + SPACE switches. The installer turns it on for those languages |
| [`modes`](../plugins/modes) | The whole desktop changed at once: quiet but for some apps, the theme, night light, the power profile, keep awake, what the bar shows; by hand, on a schedule or with a second screen, and back as it was when it's over |
| [`idle`](../plugins/idle) | After a while without use: the screens dim, then lock, turn off, and the computer suspends (sooner on battery); a video or a call keeps it awake, and so can you |
| [`hibernate`](../plugins/hibernate) | Nothing lost when the battery dies: the lid's sleep turns into hibernation after a while, and a battery about to die hibernates instead of cutting out. The installer turns it on where there's a battery, with a swap file for it |
| [`crash-watch`](../plugins/crash-watch) | When an app closes unexpectedly (the desktop itself included), a notification says which, with "Ask an agent": the agent gets the report, the crash in it |

## Apps and development

| Plugin | What it does |
|---|---|
| [`apps`](../plugins/apps) | Install apps by what you'll use the computer for (Development, Gaming, Creative…) or one by one, with one button: what it will do said first, no terminal, each undoable |
| [`dev-dotnet`](../plugins/dev-dotnet) | ASP.NET Core and Aspire ready to use: the HTTPS development certificate trusted (by .NET, and by Chromium and Firefox), Aspire's templates installed, its containers on Podman, dotnet's tools on the PATH, its telemetry off. The .NET profile in Apps turns it on |
| [`dev-expo`](../plugins/dev-expo) | Expo apps on Android from the first try: Android's SDK and emulator found (ANDROID_HOME, adb and the emulator on the PATH), Java 17 for Gradle, phones over USB, and a new Expo app one action away. The Mobile profile in Apps turns it on |
| [`dev-databases`](../plugins/dev-databases) | PostgreSQL, MySQL, Redis, SQL Server and MongoDB one click away, in containers on this computer alone, their data kept: start, stop, copy the connection string (.NET's, or a URL). The .NET and Development profiles in Apps turn it on |
| [`podman`](../plugins/podman) | Containers ready to use without root or a service: `docker` commands work (on Podman), and what looks for Docker finds it (Testcontainers, devcontainers, Compose, Aspire). Podman in Apps turns it on |
| [`docker`](../plugins/docker) | Docker itself, ready: its service started with the first command, you in the docker group so it needs no sudo (as powerful as root: said), Compose and Buildx. Docker in Apps turns it on; Podman is the lighter choice |
| [`tailscale`](../plugins/tailscale) | Tailscale ready to use: its service started, you its operator (no sudo to sign in or send files), signing in from the palette, and files sent to you with Taildrop in Downloads with a notification. Installing Tailscale in Apps turns it on |

## Gaming

| Plugin | What it does |
|---|---|
| [`gaming`](../plugins/gaming) | Games at their best: on the NVIDIA card on hybrid laptops, the 32-bit drivers Windows games need, drawn with the least delay, and the screen never dimming mid-game (with Modes, the Game mode comes on while one is open) |
| [`emulation`](../plugins/emulation) | Retro and console games, ready: ~/Games with a folder per console for your games and one for BIOS, RetroArch set up on them the first time, and emulators' windows never dimmed and drawn with the least delay |

## System, updates and safety

| Plugin | What it does |
|---|---|
| [`updates`](../plugins/updates) | Updates you see: the bar says when there are (the whole system's and the Flatpak apps'), a panel shows what changes, the news to read first and what needs a restart; updated with a click, a snapshot before, rolled back if the desktop breaks |
| [`update-ahead`](../plugins/update-ahead) | The updates there are, downloaded in the background while plugged in and on a connection that isn't metered, so updating takes moments. Nothing is installed until you say. The installer turns it on |
| [`hw-checkpoints`](../plugins/hw-checkpoints) | The system as it was before every change, in the boot menu (encrypted disks too): started from one, it says so, and it can become your system; an agent can tell what broke |
| [`hw-snapshots`](../plugins/hw-snapshots) | A btrfs snapshot of the system before and after every package change (an update, an install), the last ones kept; listed in the desktop's History |
| [`firewall`](../plugins/firewall) | A firewall, as macOS has one switch for: nothing comes in that this computer didn't ask for, everything it asks for goes out (ufw). Printers and devices on the network are still found. The installer turns it on |
| [`installer`](../plugins/installer) | The ISO's installer: this system onto a disk in a few screens (language, keyboard, where you are, the disk, your account, what you'll use it for), then mazapan install does it |

## Hardware fixes (on the machines that need them)

| Plugin | What it does |
|---|---|
| [`hw-apple-brcmfmac-wpa`](../plugins/hw-apple-brcmfmac-wpa) | Macs whose Broadcom Wi-Fi runs on the brcmfmac driver (2015 on, T2 Macs too) join WPA2/WPA3 networks that otherwise turn the right password down: the handshake done by wpa_supplicant, not by the card's firmware |
| [`hw-apple-fnkeys`](../plugins/hw-apple-fnkeys) | F1–F12 as function keys, media keys with Fn, on keyboards the hid_apple driver runs: Apple's, and others in Mac mode (Keychron…) |
| [`hw-apple-nvme-suspend`](../plugins/hw-apple-nvme-suspend) | Keeps the NVMe drive of the 2015-2017 MacBook and MacBook Pro out of its deepest power state (D3cold), which it fails to wake from after sleep |
| [`hw-apple-spi-keyboard`](../plugins/hw-apple-spi-keyboard) | The built-in keyboard and touchpad of the 2015-2017 MacBook and MacBook Pro (on SPI) working from the start of boot, so the disk password can be typed |
| [`hw-asus-b9406-panel-replay`](../plugins/hw-asus-b9406-panel-replay) | The ASUS ExpertBook B9406 (Intel Panther Lake) screen no longer freezes on its last frame: Panel Replay, which this panel never wakes from, turned off |
| [`hw-asus-ptl-backlight`](../plugins/hw-asus-ptl-backlight) | Screen brightness that really changes on the ASUS ExpertBook B9406 and Zenbook UX5406AA (Intel Panther Lake), not only full or off: the panel's backlight through DisplayPort AUX (DPCD) |
| [`hw-asus-rog`](../plugins/hw-asus-rog) | asusctl on ASUS ROG laptops: performance profiles, fan curves, a battery charge limit and the keyboard's lighting |
| [`hw-asus-rog-audio`](../plugins/hw-asus-rog-audio) | Volume on ASUS ROG laptops set in software (WirePlumber's soft mixer), away from the Realtek codec's hardware mixer quirks that muffle the speakers; and the ALC285's Master unmuted |
| [`hw-asus-z13-touchpad`](../plugins/hw-asus-z13-touchpad) | The touchpad of the ASUS ROG Flow Z13's (GZ302) detachable keyboard ignored while typing, as a built-in one is: no more stray taps that jump the cursor |
| [`hw-broadcom-wl`](../plugins/hw-broadcom-wl) | Broadcom's wl driver for the BCM4360 and BCM4331 Wi-Fi chips (2012-2015 MacBooks, and other laptops), which the kernel's own drivers run poorly or not at all |
| [`hw-fingerprint`](../plugins/hw-fingerprint) | A finger unlocks the screen and allows system changes (the password still works); fingers added and removed from the palette. With the lid closed, the password. Offered where there's a reader fprintd supports |
| [`hw-framework13-amd-mic`](../plugins/hw-framework13-amd-mic) | The built-in microphones of the Framework Laptop 13 with AMD Ryzen working: its sound card on the profile that has them (HiFi: Mic1, Mic2, Speaker) |
| [`hw-framework16-keyboard`](../plugins/hw-framework16-keyboard) | Lets you, not only root, talk to the Framework Laptop 16's keyboard, to set its RGB lighting and keys (qmk_hid, or the VIA configurator in a browser) |
| [`hw-intel-lpmd`](../plugins/hw-intel-lpmd) | Intel's Low Power Mode Daemon on laptops with a hybrid Intel processor (Alder Lake, Raptor Lake, Meteor Lake, Lunar Lake, Panther Lake): light work kept on the efficient cores, so the battery lasts longer |
| [`hw-intel-video`](../plugins/hw-intel-video) | Hardware video decoding (VA-API) on Intel graphics: both drivers, so libva picks the one for your GPU (iHD from Broadwell on, i965 before) |
| [`hw-intel-wifi7-eht`](../plugins/hw-intel-wifi7-eht) | Turns Wi-Fi 7 off on Intel BE200 and BE211 cards (Dell XPS 14 and 16 on Panther Lake, and others): with it on, the driver's broken receive path drops the access point to its slowest rate and Wi-Fi is unusable; Wi-Fi 6 works at full speed |
| [`hw-multi-gpu-cursor`](../plugins/hw-multi-gpu-cursor) | Draws the cursor in software when the machine has several GPUs (one renders, another drives screens: a dock on the iGPU, a laptop's panel): the hardware cursor flickers on the other GPU's screens |
| [`hw-nouveau-cursor`](../plugins/hw-nouveau-cursor) | Draws the cursor in software where an NVIDIA GPU runs the open nouveau driver: on many older NVIDIA GPUs nouveau doesn't show the hardware cursor, and the mouse pointer is invisible |
| [`hw-nvidia`](../plugins/hw-nvidia) | NVIDIA's open driver for GeForce RTX 20 and newer: packages, kernel modesetting loaded early, and Hyprland's variables for it |
| [`hw-surface-keyboard`](../plugins/hw-surface-keyboard) | The built-in keyboard of Microsoft Surface laptops working from the start of boot, so the disk password can be typed: the Surface Aggregator's modules in the initramfs |
| [`hw-surface-wifi`](../plugins/hw-surface-wifi) | The firmware for the Marvell Wi-Fi and Bluetooth in Microsoft Surface devices (linux-firmware-marvell), which Arch's linux-firmware doesn't bring along |
| [`hw-synaptics-intertouch`](../plugins/hw-synaptics-intertouch) | Synaptics touchpads through InterTouch (SMBus) instead of PS/2: smooth scrolling and gestures on ThinkPads and other laptops |
| [`hw-vulkan-intel`](../plugins/hw-vulkan-intel) | Vulkan on Intel graphics (Mesa's driver): games, Steam and Proton, and apps that draw with Vulkan, on the GPU instead of failing or falling back to the CPU |
| [`hw-vulkan-radeon`](../plugins/hw-vulkan-radeon) | Vulkan on AMD Radeon graphics (Mesa's RADV driver): games, Steam and Proton, and apps that draw with Vulkan, on the GPU instead of failing or falling back to the CPU |
| [`hw-yoga-pro7-bass`](../plugins/hw-yoga-pro7-bass) | Turns on the bass speakers of the Lenovo Yoga Pro 7 (14IAH10), silent without the right codec pin model |
