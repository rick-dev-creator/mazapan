# Expo, set up

Expo apps on Android from the first try. The Mobile (Expo) profile in Apps
installs Node.js (Arch's, or the LTS one already there), Java 17, Android Studio and the udev rules for
phones over USB, and turns this on; it then sets, for everything started
from the desktop, `ANDROID_HOME` (`sdk`, `~/Android/Sdk`), `JAVA_HOME`
(Java 17, what Android's Gradle builds with) and adb and the emulator on
the PATH.

Open Android Studio once: its first start downloads the SDK into
`~/Android/Sdk` and lets you make an emulator. Then "Expo: a new app" in
the palette runs `npx create-expo-app` in `~/Projects`, and `npx expo
start --android` runs it in the emulator or on a phone plugged in.

Its checks say whether Node.js, Java 17, the SDK and the processor's
virtualization (KVM, for a fast emulator) are in order. Watchman isn't
installed (only the AUR has it); Expo works without it.
