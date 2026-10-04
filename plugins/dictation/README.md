# Dictation

Speak instead of typing. `SUPER + CTRL + D` starts listening; the same key
again stops, and what you said is typed in the window you're in (or
copied, to paste, with `type` off). After two minutes it stops by itself.

`SUPER + CTRL + A` (`ask_key`) asks a coding agent instead: what you said
goes to the Agents plugin's card, its answer there (without that plugin,
it's typed as ever).

It's worked out on this computer, by whisper.cpp: nothing you say leaves
it. The first time, the model it understands with is downloaded once from
whisper.cpp's own (Hugging Face) and checked against its known checksum:
`base` (148 MB) by default, `tiny` (75 MB) for older computers, `small`
(488 MB) to understand best. It's kept in `~/.local/share/mazapan/whisper`.

It listens in the desktop's language, or the one you set in `language`
("auto" guesses it each time). While it listens, the bar's orange dot
says the microphone is in use.

Optional: it's off until you turn it on (Settings › Plugins).
