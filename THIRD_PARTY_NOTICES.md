# Third-party notices

Mazapán is written from scratch, but some of what it knows comes from
others' work. Their licenses ask for their notices to travel with it.

## Omarchy

https://github.com/basecamp/omarchy — the idea of a ready, opinionated Arch
+ Hyprland desktop, and in particular the hardware fixes the `hw-*`
plugins carry (kernel parameters, module options, udev rules and similar
settings for specific laptops), ported from Omarchy's scripts.

Its themes: `mazapan themes import-omarchy` ("Themes: import Omarchy's"
in the palette) reads Omarchy's theme format and, when asked, downloads
Omarchy's themes from its repository (or uses the ones of an Omarchy
install) and turns them into Mazapán themes on that computer. None of
them ships in this repository or on the ISO; the ones imported keep their
own authors and licenses. Mazapán's own themes (Amber, Gruvbox, Paper,
Phosphor) don't come from Omarchy.

```
Copyright (c) David Heinemeier Hansson

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

## Gruvbox

https://github.com/morhetz/gruvbox (MIT, Pavel Pertsev) — the palette of
the Gruvbox theme (`themes/gruvbox`).

## pam_fde_boot_pw

`pkg/pam-fde-boot-pw` builds it from its own source (MIT), and installs its
LICENSE with the package.

## Packages and apps

Everything Mazapán installs (Arch's packages, Flatpaks from Flathub, apps
from their makers) comes from where it's published, under its own license;
none of it is part of this repository. Mazapán never ships games, ROMs or
consoles' BIOS.
