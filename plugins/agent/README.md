# Agent

Ask a coding agent about this desktop, from the palette: "Ask an agent
about this desktop" opens it in a terminal, with a report of the desktop
as its first message. Then ask it what's wrong, or how to change
something.

The report is `mazapan report`: the desktop's state (theme, language,
plugins), the checks that fail and their output, generated files that
differ, the last update, recent crashes and the errors Hyprland and the
shell logged. It also tells the agent to change things through mazapan
(preview, apply, undo), never by editing the generated files.

The report goes to the agent you run, and from there wherever that agent
sends what it's given: a hosted one (Claude Code, Codex) sends it to its
company's servers. Nothing in it is a password or a key, but it does name
your apps, files that differ and what was logged; "Copy a report" lets you
read it first.

"Copy a report of what's wrong" puts the same report on the clipboard: to
read it first, or to paste it wherever someone is helping you.

The agent is `command` (`claude`, Claude Code). Any agent that takes a
prompt as its argument works (`codex`, `opencode run`…). To let it use
mazapan directly (status, previews, apply, undo), register mazapan's MCP
server with it:

    claude mcp add --scope user mazapan -- mazapan mcp
