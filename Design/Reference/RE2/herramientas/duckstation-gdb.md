# Conexión a DuckStation (GDB)

## Activar

DuckStation → **Settings → Advanced → Enable GDB Server**. Puerto por defecto: **`127.0.0.1:2345`**.

## Limitaciones

- **Un solo cliente a la vez.** Si el visor de memoria y el overlay van por separado, el segundo no conecta. Solución: `overlay.py --beta --mem` comparte una conexión.
- El servidor responde mientras el juego corre; no hace falta pausar.

## Protocolo usado

Solo el paquete de lectura `m<dirección>,<longitud>` del protocolo remoto de GDB (`gdbmem.py`). **Nunca se escribe en el emulador.**

## Usos en esta investigación

- Leer sala, cámara, jugador y enemigos de la Beta 2 en vivo → [direcciones](../direcciones/beta2-psx.md).
- Confirmar qué ISO estaba cargada (la original o la parcheada) leyendo un mensaje en RAM.
- Visor de IA y regla sobre la pantalla del emulador.

## Otras herramientas de DuckStation útiles

- **Debug → Show VRAM**: muestra la VRAM entera. Así se confirmó dónde están de verdad las paletas → [mapa de paletas](../texto/vram-paletas.md).
- Capturas en `Documents\DuckStation\screenshots`.
- Menú de depuración propio de la Beta: Select en el mando 1 → [menú debug](../beta2/menu-debug.md).
