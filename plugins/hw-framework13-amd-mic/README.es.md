# Micrófonos del Framework Laptop 13 (AMD)

En el Framework Laptop 13 con AMD Ryzen la tarjeta de sonido puede
arrancar en un perfil sin los micrófonos integrados, y no graba nada.
Esto la pone en el que los tiene, "HiFi (Mic1, Mic2, Speaker)".

Se ofrece en el Framework Laptop 13 con gráficos AMD.

Nada de lo que escribe necesita root: un archivo de WirePlumber,
`~/.config/wireplumber/wireplumber.conf.d/mazapan-framework13-amd-mic.conf`,
que prefiere ese perfil para la tarjeta analógica (el HD Audio "Family
17h/19h" de AMD) siempre que WirePlumber no tenga uno guardado para
ella. También se pone al momento, lo que lo guarda. Al desactivarlo, la
tarjeta sigue en ese perfil hasta que elijas otro en los ajustes de
sonido. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-framework13-amd-mic &&
mazapan apply`.
