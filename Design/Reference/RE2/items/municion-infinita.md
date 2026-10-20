# Munición infinita y truco de botones

## Representación

- En una casilla del inventario, **cantidad `FF` = infinita** (verificado en el código: las armas de recompensa se escriben con `FF`).
- Encaja con la tabla de atributos: ninguna pila pasa de 250 (`FA`), así que 251–255 quedan libres como marcadores.
- Un arma con `FF` nunca se vacía, así que tampoco se le puede cambiar la granada.

## Campo de bits de armas infinitas (`0x998FC4`, PC)

- Un bit por ID de arma (ordenación MSB-first: el bit 0 es `0x80000000`).
- Al abrir el inventario (`0x5081EC`), el juego recorre los IDs 1–19 y copia cada bit al **bit `0x10` del byte de contador** de la tabla de atributos: **el juego modifica su propia tabla de datos**.
- La Gatling Gun (ID `0x12`) se marca siempre como infinita al empezar partida (`0x50787D`).

## Truco de botones (`0x5092E0`)

Detector de secuencias de 9 pasos: avanza un índice (`0x6901D4`) con cada pulsación correcta y vuelve a cero si se pulsa otra cosa. Con el mapa de bits del mando de PlayStation:

**↑ ↑ ↓ ↓ ← → ← → R1**

- Se comprueba con la pantalla de ítems abierta (deducido por el contexto de la llamada, `0x508336`).
- **Activar:** todas las armas (IDs 0–23) infinitas (`or 0xFFFFFF00`).
- **Repetir:** se desactiva, pero la Gatling sigue infinita.
- Interruptor: `0x69C540`.
- **No probado en el juego**; sale de leer el código. En PC, "R1" corresponde a la acción de apuntar.
- Es el **único** detector de secuencias confirmado (una búsqueda de otros por patrón no fue concluyente).

## Armas de recompensa al empezar (`0x4B13FB`)

Según los flags persistentes `0x9A0188`, el juego escribe directamente en el inventario (`0x999360`), desde la casilla 3:

| Flag | Arma | Cantidad |
|---|---|---|
| `0x10` | SMG | `FF` |
| `0x20` | Rocket Launcher | `FF` |
| `0x40` | Gatling Gun | 1 + marcada infinita |

→ condiciones en [rango y desbloqueos](../jugador/rango-y-desbloqueos.md)

## Glifo "∞"

En `ST_MOJI.TIM` (Beta 2), la fila de dígitos es `0123456789 % ∞`. El "%" es el glifo 10 (el código de porcentaje suma 10). El "∞" sería el 11, para la cantidad `FF` (deducido).
