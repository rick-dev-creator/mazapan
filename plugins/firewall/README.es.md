# Firewall

Un firewall que simplemente está encendido, como en macOS: no entra nada
que esta computadora no haya pedido, y sale todo lo que pide. Las
impresoras y otros dispositivos de la red se siguen encontrando (las
reglas de ufw dejan pasar mDNS y SSDP). Un puerto queda abierto: el de
LocalSend (53317), para que los móviles y equipos cercanos puedan enviar
a este, como en Omarchy ("Dejar entrar LocalSend"; nada escucha ahí si
LocalSend no está abierto). El de SSH no, salvo con "Dejar entrar SSH" (el
instalador lo enciende cuando recibió llaves SSH).

El instalador lo enciende. Quitar el plugin apaga el firewall. Lo que
hace es de ufw: `sudo ufw status verbose` lo muestra.

Docker publica los puertos de sus contenedores por encima de cualquier
firewall (escribe sus propias reglas): con Docker, cuida lo que publicas.
