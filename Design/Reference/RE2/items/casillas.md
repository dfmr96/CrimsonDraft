# Casillas del inventario

## Formato (4 bytes)

`[ID del ítem] [cantidad] [parte] [00]`

- **Cantidad:** unidades, porcentaje (armas de tipo %) o `FF` = infinito.
- **Parte:** las armas grandes (Rocket Launcher, SMG) ocupan **dos casillas** marcadas `01` y `02`, cuyas cantidades bajan juntas. En el baúl se guardan como `03` en una sola casilla.

Fuente del formato: guía de edición de partidas guardadas ([alexfung.info](http://alexfung.info/favorite/game/re2code.htm)). Coherente con el código de recompensas, que escribe ID, cantidad y parte.

## Ubicación (PC)

- Inventario del jugador: **`0x999360`** (array de casillas de 4 bytes).
- Copia de seguridad durante el guardado: `0x999334`, restaurada si se cancela → [guardado](../jugador/guardado.md).

## Capacidad

- BioRand indica 8 casillas para ambos personajes. La guía de partidas guardadas menciona 6 u 8 según el *side pack*.
- El *side pack* se recoge con un Sí/No en el escenario: "With a side pack, I can carry more items."
