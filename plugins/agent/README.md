# Agents

Your coding agents, in one place: Claude Code (each of your accounts),
opencode, pi, Codex and the rest, found by themselves.

**In the bar**, a robot: orange with a number when sessions wait for you,
in the accent while one works, and the fullest of your limits once it's
past 70 %. A click opens the card:

- *Sessions*: each open one, where and what it's doing (working, waiting
  for you, done), for how long; a click goes to its window.
- *Limits*: each Claude account's 5-hour and weekly windows (and any for
  one model), how full, and when each resets.
- *Today*: what each agent used: answers, tokens, and what the same
  tokens cost at each model's API prices (and what an API key was
  charged).

A notification when a session waits for you (a permission, a question),
when one is done after working a while (`notify_after`, 30 s), and when
an account passes `limit_alert` (80 %) of a limit.

**The dashboard** ("Dashboard" in the card, or the palette): cost or
tokens by day for each agent, by model, by project, when you work
(weekday × hour), the cache's share, and every account's limits, over 7,
30 or 90 days, and what the API providers themselves bill. Its own
window, apart from the bar.

**API keys in the keyring**, not in a plain `.env`: "Agents: an API key"
in the palette (or `mazapan agents keys set openrouter`, the key typed
hidden). `mazapan agents run opencode` (pi, aider…) starts the agent with
them as its variables; Claude Code and Codex sign in and get none. An
OpenRouter key, or an admin key for Anthropic or OpenAI, also shows what
they bill, today and over the period, and an OpenRouter key's limit.

**What Claude Code did**, from its own OpenTelemetry metrics: lines
written and taken out (by repository), commits, pull requests, your time
and the agent's, in the dashboard. Claude Code sends them to a receiver on
this computer (127.0.0.1, `otel_port`), started by systemd when the first
arrives and gone when idle; metrics only, never a prompt, kept in
`~/.local/state/mazapan/agents`. Its settings.json gets the variables for
it (`otel`); telemetry of your own already there is left alone.

**A dot on the workspace** where a session's window is (with the
Workspaces plugin): amber while it waits for you, the text's color while it
works, green when it's done.

**A question from the palette**: type `?` and the question (`? why is
my battery draining`). The first coding agent here answers it in a card,
without opening it: Claude Code with the account that has room and
Mazapán's read-only tools (it can look at this desktop's state, never
change it), else opencode, Codex, Gemini CLI or pi. The answer can be
copied, or Claude's conversation continued in a terminal.

**Ask an agent about this desktop**, from the palette, opens your agent
(`command`; Claude with the first account that has room) with mazapan's
report: state, failing checks, crashes, logged errors. "Copy a report"
puts it on the clipboard. The report goes to the agent you run, and from
there wherever that agent sends what it's given: a hosted one (Claude
Code, Codex) sends it to its company's servers.

How it knows: numbers only, read on this computer from what each agent
keeps (Claude Code's project logs, opencode's database, pi's and Codex's
sessions); prompts and answers are never read. Claude's limits come from
Anthropic with that account's own login, never refreshed by mazapan.
Sessions are told by Claude Code's hooks, which this plugin puts in each
configuration's `settings.json` beside your own (`claude_hooks`; a copy of
the file is kept the first time; off takes them out; turning the whole
plugin off leaves them, harmless: `mazapan agents hooks remove` and
`mazapan agents telemetry off` take them out), and by a small opencode plugin. From a terminal: `mazapan agents`, `mazapan agents usage`,
`mazapan agents limits`, `mazapan agents run claude`.
