# Expo, configurado

Apps de Expo en Android a la primera. El perfil Móvil (Expo) de Apps
instala Node.js (el de Arch, o el LTS que ya haya), Java 17, Android Studio y las reglas udev para
teléfonos por USB, y enciende esto; entonces define, para todo lo que se
abre desde el escritorio, `ANDROID_HOME` (`sdk`, `~/Android/Sdk`),
`JAVA_HOME` (Java 17, con lo que compila el Gradle de Android) y pone adb y
el emulador en el PATH.

Abre Android Studio una vez: su primer arranque descarga el SDK en
`~/Android/Sdk` y te deja crear un emulador. Después, «Expo: una app nueva»
en la paleta ejecuta `npx create-expo-app` en `~/Projects`, y `npx expo
start --android` la abre en el emulador o en un teléfono conectado.

Sus comprobaciones dicen si Node.js, Java 17, el SDK y la virtualización
del procesador (KVM, para un emulador rápido) están en orden. Watchman no
se instala (solo está en el AUR); Expo funciona sin él.
