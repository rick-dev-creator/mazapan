# Share

Files, or what you copied (a picture, files copied in Files, text), to
another device: one nearby through LocalSend, or one of your own on your
tailnet through Tailscale's Taildrop. Only the ways this computer has are
offered: install LocalSend or Tailscale from Apps.

How to share:
- **What you copied**: "Share what I copied" in the palette. A picture or
  text becomes a file first (in the runtime folder, yours alone, gone at
  logout).
- **Files**: in Files, select them, right click, Scripts, Share. Or from a
  terminal: `~/.local/share/mazapan/bin/share FILE…`.
- **A capture**: "Share" in the capture's bar.
- **Something copied before**: in the clipboard history, Ctrl+S.

The Share panel lists where it can go. **A device nearby** opens LocalSend
with the files, to pick the device in its window (it finds phones and
computers on the same network). **Your tailnet** lists your devices that
are online; a click sends at once (Taildrop) and a notification says when
it arrived. Sending with Taildrop without root needs you to be Tailscale's
operator: the Tailscale plugin does it.

Receiving: LocalSend's own window receives; with Taildrop, the Tailscale
plugin puts what arrives in Downloads and says so.

What it writes: the panel (`~/.config/quickshell/mazapan/panels/share.qml`),
`~/.local/share/mazapan/bin/share`, and Files' script
(`~/.local/share/nautilus/scripts/Share`). No settings.
