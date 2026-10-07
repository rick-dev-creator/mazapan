# Share

Files to another device, and files a phone sends here: like AirDrop.

**Sending.** The Share panel shows where it can go, as bubbles: phones and
computers nearby over Bluetooth, a device nearby through LocalSend (its
window opens with the files, to pick it there), and your devices on your
tailnet (Taildrop). A click sends; the bubble's ring shows how far it's
gone, and the panel can close meanwhile (a notification says when it's
sent, or why not). A phone you haven't paired shows up while the panel is
open (with its Bluetooth settings open on the phone): a click pairs it,
the code to confirm shows in the panel and on the phone, then it's sent.

How to share:
- **Files**: "Share…" in the palette, then "Choose files…"; or the Share
  switch in the Control Center, its arrow, "Choose files…"; or in Files,
  right click, Scripts, Share. From a terminal:
  `~/.local/share/mazapan/bin/share FILE…`.
- **To one device**: the send button beside a paired phone in the
  Bluetooth card (the bar's or the Control Center's): choose files, and
  they go to it.
- **What you copied**: "Share what I copied" in the palette or the Control
  Center. A picture or text becomes a file first (in the runtime folder,
  yours alone, gone at logout).
- **A capture**: "Share" in the capture's bar. **Something copied
  before**: in the clipboard history, Ctrl+S.

**Receiving.** A phone sends a file over Bluetooth to this computer: the
Share panel opens (without taking the keyboard) to accept it or not, shows
it arriving, then opens it or its folder. It goes to Downloads. The
`receive` setting, also in the panel: `ask` (the default), `paired`
(accepted from paired devices without asking) or `off`. With Taildrop,
the Tailscale plugin puts what arrives in Downloads; LocalSend's own window
receives.

**Pairing from the phone.** The Share switch in the Control Center (or
"Visible as…" in the panel) makes this computer visible to phones for
three minutes; pair from the phone, and the panel opens with the code to
confirm.

The process goes on in the panel; a notification only says what happened
(arrived, sent, couldn't be sent), when the panel isn't open to show it.

How it works: the nearby service (`mazapan-nearby.service`, started by its
socket when the shell connects) is BlueZ's agent for pairing (the codes)
and obexd's for receiving, and sends with OBEX Object Push. The shell talks
to it through `$XDG_RUNTIME_DIR/mazapan-nearby.sock`; with nobody there to
ask, what it would ask is declined. obexd receives into its own folder
(`~/.cache/obexd`), then the file goes to Downloads.

What it writes: the panel (`~/.config/quickshell/mazapan/panels/share.qml`),
its part of the shell (`components/share/`, with its settings in
`settings.json`, read live), the Control Center's switch
(`control/tiles/25-share.qml`), the nearby service
(`~/.local/share/mazapan/bin/nearby`, `mazapan-nearby.service` and
`.socket` in `~/.config/systemd/user`), `~/.local/share/mazapan/bin/share`,
and Files' script (`~/.local/share/nautilus/scripts/Share`).
