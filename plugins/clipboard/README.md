# Clipboard history

What you copied, kept: text and pictures, newest first, searchable
(`SUPER + CTRL + V`). ↵ (or a click) puts it back and pastes it where you
were (`paste`, with wtype); a right click or Delete removes one; Ctrl+P
pins one (kept however many come after, and on top); Clear all keeps the
pinned ones.

The same thing copied again moves up instead of showing twice, and one
copy is one entry (a picture copied in a browser isn't also kept as its
HTML). What's marked as secret is never kept: KeePassXC marks it (the
x-kde-passwordManagerHint type), and so does `wl-copy --sensitive`; not
every password manager does, so check yours. Nor copies larger than
`max_kb`. The history is in `~/.local/state/mazapan-clipboard/`, readable
only by you; `keep` entries at most (100), `keep_images` of them
pictures (20).
