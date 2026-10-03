# Vulkan on AMD graphics

Vulkan on AMD Radeon graphics, with Mesa's RADV driver
(`vulkan-radeon`): games, Steam and Proton, and apps that draw with
Vulkan run on the GPU instead of failing to start or falling back to the
CPU. Video decoding on AMD needs nothing more: Mesa has it.

Offered on AMD graphics, built in or a card. It writes no files: only
the package, installed as root. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-vulkan-radeon && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` uninstalls it, unless something else needs it
by then.
