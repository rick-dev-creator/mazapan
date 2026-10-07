# The plugin store

What a plugin needs to be in the store (mazapan.dev/plugins and the
Plugins panel), what it may and may not do, and what the store shows
about it. How to publish one, step by step, is in
[publishing-plugins.md](publishing-plugins.md); this page is the rules
that page points to.

> **Being built.** Rules marked *(store)* need a Mazapan that reads them:
> `[store] goal`, `[gallery] cover` and `accent`. Until it's out, leave
> those keys out: today's Mazapan refuses a plugin.toml with keys it
> doesn't know. Everything else here applies now.

The store is goal-first: people look for what they want to get done
("see what's on my ports", "listen to the radio"), not for a kind of
widget. Every rule here is there so a listing says plainly what a plugin
does, shows it doing it, and keeps its word.

## Who publishes

A plugin's author is the owner of its repository on GitHub: a person or
an organization. The store shows their public GitHub profile (picture,
name, @login, bio, company, location, website, since when), refreshed
every hour, and never their email, even when public. `author` in
plugin.toml is shown next to it as a display name, never instead of it.

Reviews are written with a GitHub account too, so an author's answer to a
review is marked as theirs by itself.

## The listing

### Name

`name` in plugin.toml, in each language it has.

- What it is, in up to 24 characters: `Ports`, `Radio`, `Pomodoro`.
- No "Mazapan" in it (it would read as official), and nobody else's
  brand unless the plugin is for it, said that way: `Radio for TuneIn`.
- No keywords piled on (`Ports Monitor Docker Network Dev Tool`), no
  "best", "pro", "ultimate", no emoji, no all-caps.

### One line

`description`: the goal it serves, in up to 80 characters, as a person
would say it: "What's running on each port, stopped with a click". Not
how it's built ("A Quickshell widget using ss(8)").

### Goal *(store)*

`goal` under `[store]`: the one shelf it sits on in "What do you want to
do?":

| `goal` | For |
|---|---|
| `build` | Writing software: ports, containers, git, deploys |
| `listen` | Music, radio, podcasts |
| `machine` | The computer itself: its state, power, disks |
| `work` | Tasks, mail, calendars, the services you work in |
| `devices` | Things you plug in or pair: keyboards, mice, phones, headphones |
| `follow` | What you keep an eye on: feeds, markets, scores |
| `look` | Themes and how the desktop looks |
| `play` | Games and toys |

`categories` (bar, panel, …) stays what kind of plugin it is; `goal` is
where people find it.

### README

The plugin's page. Its first paragraph is the store's description: what
you get, not how it works. Then, in this order:

1. **How to use it**: its keys, where it shows up, its settings.
2. **What it reaches and why**: every file, network address, command or
   package it uses, and what for. Required when it uses any; the review
   checks it against what the plugin can actually do.
3. Anything else (credits, how to build, changelog).

## Pictures

All in the repository, named in `[gallery]`, PNG, JPEG or WebP (the icon
may be SVG).

| Picture | Size | Up to | Required |
|---|---|---|---|
| `icon` | Square: SVG, or 512×512 or larger | 256 KB | Yes |
| `cover` *(store)* | 1920×1080 (16:9) | 1 MB | To be featured |
| `screenshots` | 1920×1080 or 2560×1440 (16:9) | 2 MB each, 1 to 8 | At least 1 |
| `accent` *(store)* | A color, `#rrggbb` | | No |

```toml
[gallery]
icon = "media/icon.svg"
cover = "media/cover.webp"
screenshots = ["media/panel.webp", "media/bar.webp", "media/palette.webp"]
accent = "#7847eb"
```

- **Icon.** One clear shape that still reads at 32 px; no words, no
  screenshot shrunk down. A transparent background is fine; it's shown on
  dark and light.
- **Cover.** The plugin's key art: the store's banner when it's featured,
  and its tile everywhere else (cropped to the middle). The plugin doing
  its thing, drawn big, or the moment it's for. **No text in it**: the
  store writes the name over its left side, so keep the left 40% calm.
  Without a cover, the tile is the icon on `accent`.
- **Screenshots.** Real ones, from Mazapan, with the plugin doing what
  the README says; the first is the one shown in lists. In any theme
  Mazapan ships. Nobody's personal data in them (names, mail, tokens,
  addresses); made-up data is fine and better. No frames, arrows or
  captions drawn on top.
- **Accent.** The color behind its tile and its page when there's no
  cover; dark enough for white text on it.

## What a plugin may not do

The review refuses, and the store takes down, a plugin that:

- **Does more than it says**, or something else: everything it reaches is
  in its README, and nothing happens without the person knowing.
- **Sends anything about the person anywhere** (usage, files, what's on
  screen) unless that's what it's for, the README says so, and it's off
  until they turn it on.
- **Runs code that isn't in its commit**: nothing downloaded and run
  later. Packages it needs are declared, and installed by Mazapan.
- **Keeps secrets in plain files**: tokens and passwords go in the
  system's keyring (Secret Service).
- **Mines, shows ads, or rewrites links** to earn from them.
- **Passes as something else**: Mazapan's name or logo, another plugin's
  name or icon, a company it isn't.
- **Holds content** that's hateful, sexual, or against the law.

## What the store shows

- **Recommended by**: the share of 👍 among 👍 and 👎 on its reviews
  discussion, with how many voted; shown from 5 votes on.
- **Reviews**: the latest ones, each with its version, and the author's
  answer under it.
- **On how many computers**: counted once a week by Mazapan's update check
  with no identifier (see [roadmap.md](roadmap.md)); shown from 10 on.
- **Checked**: the commit the registry reviewed, and what the plugin can
  do, as Mazapan asks before installing it.

## Featured

Picked by hand, a few each month, among plugins that:

- have a cover and at least 3 screenshots;
- say what they are in at least 2 languages;
- are recommended by most of those who voted, with their author answering
  reviews;
- pass `mazapan plugins check` with the current Mazapan.

## When a plugin falls behind

A plugin that stops passing the checks with a new Mazapan stays listed
but leaves Discover and featured, with a note on its page, until a new
version passes. Taken down when it's still failing 90 days later; copies
already installed keep working.
