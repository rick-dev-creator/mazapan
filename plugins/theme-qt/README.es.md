# Tema de Qt

Las apps de Qt 6 con el tema, también las de KDE (Dolphin, Kdenlive,
Okular…): su paleta, fuentes y estilo de widgets mediante qt6ct, y el
esquema de colores de KDE, que las apps de KDE suman encima. A Hyprland se
le pide que las apps de Qt usen qt6ct. Las apps de Qt 5 quedan fuera.

`style` (Fusion) es el estilo de widgets de Qt: Fusion sigue la paleta por
completo. `icon_theme` (Adwaita) son los iconos que usan las apps de Qt.

Las apps de Qt leen todo esto al arrancar: las abiertas conservan sus
colores hasta que se vuelven a abrir. qt6ct y las apps de KDE escriben en
los mismos archivos (`~/.config/qt6ct/qt6ct.conf`, `~/.config/kdeglobals`):
solo las claves que escribe Mazapán son suyas, el resto sigue siendo de
ellas y tuyo.

Necesita qt6ct; su comprobación lo avisa cuando no está instalado.
