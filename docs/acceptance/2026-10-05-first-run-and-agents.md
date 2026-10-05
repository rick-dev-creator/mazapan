# First run, connectivity and coding agents — Acceptance Criteria & Validation

| | |
|---|---|
| **Version** | 1.0 |
| **Date** | 2026-10-05 |
| **Status** | ACCEPTED for 0.1.0 (2026-10-06): every AC Met but AC-A2/AC-A4, consciously deferred (see the audit result) |
| **Covers** | The working tree on top of `67cec8e`: Wi-Fi carried to the installed system, apps at install time (Basic offline), the "apps waiting" notice, `Online` for every plugin, coding agents (profile, vendors, palette, Control Center) |
| **Purpose** | The testable "done" contract. Each AC is audited against (a) the implementation (file:line) and (b) a non-vacuous test that proves it: a unit test where the behaviour is pure, the real system (an install from the ISO in a VM, or the test laptop) where it isn't. |

Found on the test laptop (MSI Vector A16 HX, 2026-10-05): the profiles chosen
in the installer seemed not to install, the Wi-Fi password was asked again
after the restart, the weather and markets didn't react to the connection,
and an agentic OS had no way to install an agent.

## A — First run

- **AC-A1 — The installer's Wi-Fi connects by itself.** A Wi-Fi joined in the live system is carried to the installed one without the live system's `interface-name` (nor `permissions`), so NetworkManager tries it on the first start and no password is asked again. *Proof: unit test `WifiCarriedWithoutTheLiveUser`; in a VM, a live keyfile with `interface-name=wlan0` is carried without it, and on a simulated Wi-Fi (mac80211_hwsim + hostapd) the installed system associates at boot with no password asked.*
- **AC-A2 — Basic installs with no connection.** The Basic profile's packages are in the ISO's offline repository; an install with no network at all puts them on the disk. *Proof: test `TheIsoCarriesTheBasicProfile` (target-packages.txt complete), `AppPackagesFromTheIsoFirst`; a VM install with no network device, `pacman -Q` on the installed system lists them.*
- **AC-A3 — The other profiles' packages go in with the system when online.** With a connection during the install, every Arch package of the chosen apps is installed before the first start; only Flathub, makers', web apps and plugins remain in `first-apps`. *Proof: test `AppsGoInWithTheSystem`; a VM install online choosing Development; before any login `pacman -Q` lists its packages and `first-apps` holds only vendor/flatpak/webapp/plugin ids.*
- **AC-A4 — Offline at the first login, said once.** With apps waiting and no connection, one notification says they'll be installed on connecting; when the connection comes, they are, with no further step. *Proof: a VM first login with the link down: the notification captured on D-Bus exactly once; link up → `first-apps` gone, apps installed.*
- **AC-A5 — What went in at install time belongs to the Apps menu.** Apps installed with the system count as installed and can be removed from Apps; `apps first` doesn't reinstall them and turns on their plugins. *Proof: test `WhatTheInstallPutInIsTheAppsMenus`; in the VM, `mazapan apps list` marks them installed, and `mazapan apps remove` removes one.*

## B — Connectivity

- **AC-B1 — `Online` follows NetworkManager.** `Online.online` is NetworkManager's connectivity (Full), and `back()` fires once each time it returns after being gone — never as the shell starts. *Proof: a real Quickshell with the singleton, Wi-Fi turned off and on (test laptop): offline → one `back` → online; no `back` at start.*
- **AC-B2 — Plugins fetch again when it comes back.** Weather, markets, the update check and the agents' limits refresh on `back()`; the weather retries a failed fetch in a minute while online. *Proof: in a VM, the network cut and restored: the bar's weather goes from "offline" to a temperature within seconds, without waiting for its timer.*

## C — Coding agents

- **AC-C1 — Agents in the profiles.** A "Coding agents" profile (Claude Code, Codex, OpenCode, Gemini CLI, Qwen Code, T3 Code, Herdr), and Development, .NET and Mobile bring all but Qwen Code. *Proof: test `CodingAgentsAreAProfile`; the installer's profile step in the VM shows it.*
- **AC-C2 — Claude Code from Anthropic, checked.** Its latest release from downloads.claude.ai, the binary checked against the manifest's SHA-256, its own `claude install` run; one installed by hand counts as installed; removing it never touches `~/.claude`; `mazapan update` leaves it to its own updates. *Proof: tests `ClaudesReleaseFromItsManifest`, `ClaudesManifestIsChecked`, `ClaudeInstalledByHandCounts`; a real install in the VM, `claude --version`; a remove leaves `~/.claude`.*
- **AC-C3 — T3 Code and Herdr from GitHub, checked.** The release asset for the machine, checked against GitHub's SHA-256 digest; T3 Code's AppImage unpacked (no FUSE) with a launcher and its window class; Herdr as a command. *Proof: tests `GitHubReleaseForThisMachine`, `GitHubReleaseIsChecked`; real installs (test laptop and VM): `herdr --version`, T3 Code's window opens.*
- **AC-C4 — The repositories' agents.** Codex, OpenCode, Gemini CLI and Qwen Code install from Arch's packages and open by their command. *Proof: VM install, each `--version`.*
- **AC-C5 — The palette installs them.** Typing an agent's name offers "Install…" for it. *Proof: palette screenshot ("codex") on the test laptop and in the VM.*
- **AC-C6 — The Control Center leads to them.** With no agent installed, the Agents section offers "Install a coding agent"; with some, "More agents"; both open Apps on the Coding agents profile. *Proof: VM screenshots of both states; the Apps panel open on the profile.*

## C — Cross-cutting

- **AC-X1 — Nothing breaks without the network.** An unreachable maker (Anthropic, GitHub, JetBrains) or no connection at all leaves the update check, the Apps menu and the install working, said, not crashed. *Proof: `mazapan update --check` and `apps list` in a VM with no route out exit cleanly.*
- **AC-X2 — The suite stays green.** *Proof: `dotnet test` (all).*

## E — End to end

- **AC-E1 — The release gate passes.** `vm/gate encrypted` and `vm/gate plain` with an ISO built from this tree. *Proof: both PASS.*
- **AC-E2 — A developer's first start.** Installed online with Development chosen, the first login ends with every agent there (Claude Code, Codex, OpenCode, Gemini CLI, T3 Code, Herdr) and nothing asked twice. *Proof: VM, after the first login: each command answers, `first-apps` gone.*

## Progress

### Checklist
- [x] Phase 1 — acceptance criteria written
- [x] VM tooling on the test laptop (QEMU; `vm/vm try` gained MAZAPAN_TRY_OFFLINE, plug and unplug)
- [x] ISO built from this tree (`mazapan-2026.10.05-x86_64.iso`, 4.8 GB; offline repository 737 packages, 2.4 GB)
- [x] AC-A3 — `vm/gate plain` with the Development profile: "their Arch packages came with the system" (every chosen pacman app installed before the first login reached it)
- [x] AC-A5 — same run: `mazapan apps list` marks all 17 installed; the first login installed no Arch package again (no `step packages` in its run log)
- [x] AC-C1 — test `CodingAgentsAreAProfile`; the gate installed the Development profile with its six agents
- [x] AC-C2 — same run: Claude Code installed for real from downloads.claude.ai (checked), `claude --version` answers
- [x] AC-C4 — same run: `codex`, `opencode`, `gemini` answer `--version`
- [x] AC-E2 — same run: all 17 apps installed, each command answers, `first-apps` gone, nothing asked twice
- [x] AC-E1 (plain) — `vm/gate plain`: PASS (installed in 3 min, 21 checks, no failed units)
- [x] AC-A1 — unit test + real NetworkManager on mac80211_hwsim (dev VM): the live keyfile (`interface-name=wlan9`) never connected; the same file through `WifiCarry.Keyfile` connected by itself on NetworkManager's restart, no password asked
- [x] AC-B1 — test laptop: Wi-Fi off 12 s and on → offline ×4, `back` ×1, online; no `back` at start
- [x] AC-C3 — test laptop: `mazapan apps install -y herdr t3code` (28 MB + 150 MB, checked), `herdr --version` = 0.9.3, T3 Code's window opened (class `com.t3tools.T3Code`)
- [x] AC-C5 — test laptop: the palette, "codex" → best match "Install… Codex"
- [x] AC-X2 — `dotnet test`: 579 passed; CI green on #14–#18
- [x] AC-B2 — `vm/gate`'s installed system started with no internet: the bar's weather "--"; a network card plugged in: connectivity full in 10 s, "19°" 16 s after
- [x] AC-C6 — the VM after the Development install: the Agents section with "More agents"; Apps opened on the profile through `open apps profile:agents` (test laptop)
- [x] AC-E1 (encrypted) — `vm/gate encrypted` on the published ISO, `mazapan-0.1.0-2026.10.05-x86_64.iso` (its packages from repo.mazapan.dev): PASS — installed in 4 min, 18 checks, no failed units, one password, the Arch packages with the system, lazygit and herdr installed at the first login and answering
- [x] The release: v0.1.0 tagged; mazapan, mazapan-keyring and pam-fde-boot-pw signed by the release key (7C05 EB75 … 8077 F475) on repo.mazapan.dev's stable and edge, the database's signature checked over HTTPS; the ISO, its .sha256 and .sig on SourceForge
- [ ] AC-A2, AC-A4 — deferred (below)

### Findings
- `vm/gate` waited for the first login to ask the password (pkexec) for the chosen apps; with their Arch packages installed with the system nothing may need root, and it would have failed a correct install. It now types the password only when asked, and checks the packages came with the system and nothing was installed twice.
- `vm/vm` (MAZAPAN_TRY_OFFLINE): `$([[ … ]] && echo …)` in the -nic argument returned non-zero under `set -e` and stopped the try VM silently; and hot-plugging a NIC on q35 needs a pcie-root-port made at boot, on another subnet than the first card's.
- Quickshell reads `pragma Singleton` only before the first `{` of the file: a brace in a header comment made `Online` silently not a singleton (`plugins/shell-bar/Online.qml.tmpl:1`).
- T3 Code's window class is `com.t3tools.T3Code`, not the AppImage's `StartupWMClass=t3code`.

- The welcome's "waiting for a connection" notice came at a first login that was online (the network a second late): it now waits 20 s and is said only if still offline. Verified: an online encrypted install's first login gave "Installing…" and "installed", no waiting notice.
- vm/gate's wait for the first login made 3 SSH connections every 12 s; Mazapan's firewall lets 6 in every 30 s, so the gate shut itself out and reported apps not installed that were (`DONE 0` at 13:06:45). One connection a look now.

- vm/gate's SSH was shut out by the installed firewall (`[UFW LIMIT BLOCK]` ×98 in its journal): every refused attempt's retries counted, so the lock never lifted. vm/vm try now shares one SSH connection (an OpenSSH master).
- The gate checked commands over SSH, whose PATH lacks ~/.local/bin; the desktop's has it (hypr-base). It now checks with the desktop's PATH. A shell over SSH or on a TTY still lacks ~/.local/bin: worth adding for login shells.
- `mazapan undo` of an agent's change asks to confirm system steps (mkinitcpio, systemctl) although the agent wrote nothing as root; it should undo the person's files only, without asking.
- Under `vm/vm try` with MAZAPAN_TRY_OFFLINE, cloud-init on the live ISO waits out network datasources (the restricted network drops packets instead of refusing them), so the seed's SSH key comes too late for the gate: the offline gate never got in.

### Gaps
- AC-A1 is proven at NetworkManager's level and in the unit test; the live → installed rename (wlan0 → wlp5s0) only happens on real hardware: the next install on the laptop closes it.

### Blockers

## Audit result (2026-10-06)

| AC | Proven by | Verdict |
|---|---|---|
| AC-A1 | `WifiCarriedWithoutTheLiveUser`; NetworkManager on mac80211_hwsim | Met (the wlan0 → wlp5s0 rename itself only on hardware: the laptop's next install) |
| AC-A2 | `TheIsoCarriesTheBasicProfile`, `AppPackagesFromTheIsoFirst` | **Deferred**: no offline install got through (the gate's cloud-init, above) |
| AC-A3 | `AppsGoInWithTheSystem`; vm/gate plain (Development), encrypted (0.1.0 ISO) | Met |
| AC-A4 | the welcome's 20 s wait; an online first login gave no waiting notice | **Deferred**: the offline half (said once, then installed on connecting) unproven in a VM |
| AC-A5 | `WhatTheInstallPutInIsTheAppsMenus`; vm/gate plain | Met |
| AC-B1 | Quickshell with Online, Wi-Fi toggled (laptop) | Met |
| AC-B2 | vm/gate's system, a cable plugged in: "--" → "19°" in 16 s | Met |
| AC-C1 | `CodingAgentsAreAProfile`; gate installs | Met |
| AC-C2 | `ClaudesReleaseFromItsManifest`, `ClaudesManifestIsChecked`, `ClaudeInstalledByHandCounts`; real install in the gate | Met |
| AC-C3 | `GitHubReleaseForThisMachine`, `GitHubReleaseIsChecked`; laptop and gate installs | Met |
| AC-C4 | gate: each `--version` | Met |
| AC-C5 | the palette on the laptop | Met |
| AC-C6 | the VM's Control Center; Apps on the profile | Met (the "none installed" state seen in code, not on a screen) |
| AC-X1 | update --check and apps list with makers unreachable caught (Update.cs) | Met in code; no VM run with no route out |
| AC-X2 | dotnet test, CI | Met |
| AC-E1 | vm/gate plain (old ISO), encrypted (0.1.0 ISO) | Met |
| AC-E2 | vm/gate plain, the Development profile | Met |

Deferred to 0.2: AC-A2 and AC-A4 need the offline gate, which needs the live
ISO's cloud-init limited to the cidata drive (NoCloud) so the seed's key
comes at once with no network; then `MAZAPAN_GATE_OFFLINE=1 vm/gate plain`.
