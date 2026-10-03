# Hibernation

Nothing lost when the battery dies, as on a Mac. With the lid closed on
battery the computer sleeps, and after a while (two hours, a setting)
hibernates: what's open is written to the disk and the computer turns
off, to come back as it was. A battery about to die hibernates rather
than cutting out. Plugged in, the lid only puts it to sleep.

The installer turns it on where there's a battery, and makes what it
needs: a swap file as big as the memory, in a subvolume of its own (a
snapshot's rollback leaves it alone), below the compressed swap in memory.
With an encrypted disk the hibernation is encrypted too.
