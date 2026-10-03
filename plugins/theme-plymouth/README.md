# Boot screen in the theme

The screen while the computer starts in the theme's colors, and with an
encrypted disk its password asked right there, in your language (Plymouth).
No pictures: it's drawn from the theme's colors, so any theme has it. Its
text is plain ASCII ("Mazapan", a star per letter typed): what draws it
before the disk is open knows nothing past that.

A new theme reaches it on the next `mazapan apply --system`, which rebuilds
the initramfs once (that's where the boot screen lives).
