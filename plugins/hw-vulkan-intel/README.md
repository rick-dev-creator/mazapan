# Vulkan on Intel graphics

Vulkan on Intel graphics, with Mesa's driver (`vulkan-intel`): games,
Steam and Proton, and apps that draw with Vulkan run on the GPU instead
of failing to start or falling back to the CPU. Video decoding is
another plugin, `hw-intel-video`.

Offered on Intel graphics. It writes no files: only the package,
installed as root. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-vulkan-intel && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` uninstalls it, unless something else needs it
by then.
