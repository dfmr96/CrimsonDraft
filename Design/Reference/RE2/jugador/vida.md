# Vida del jugador

> Verificado en `LeonU.exe` (PC).

## Dónde está

La estructura del jugador está en **`0x9944F0`** (mismo formato de entidad → [estructura](../enemigos/estructura-entidad.md)):

| Offset | Absoluta | Campo |
|---|---|---|
| +0x156 | `0x994646` | Vida actual (`s16`) |
| +0x162 | `0x994652` | **Vida máxima** (`s16`) |

## Valor

| Personaje (ID de PLD) | Vida máxima |
|---|---|
| Leon, Claire, Ada (`0E`), Sherry (`0F`), HUNK (`0C`) | **200** |
| **TOFU** (`0D`) | **400** (`0x4D8FA6`: `cmp 0x0D` → 400) |

- En el código de inicialización no se vio ninguna rama por dificultad para la vida máxima (no se buscó de forma exhaustiva).
- La curación se calcula como porcentaje de la vida máxima → [curación](../items/curacion.md).

## Otros puntos del código

| Dirección | Qué hace |
|---|---|
| `0x4E2171` | Cambio a la compañera (Ada/Sherry): guarda vida y veneno del protagonista; la compañera empieza al máximo. Al volver, el protagonista se restaura tal como estaba |
| `0x4EC48B` | Cuando Sherry deja de ser controlable, vida y vida máxima = `FFFF` (−1): no puede morir. Al volver a controlarla recupera la vida guardada (máx. 200) |
| `0x4EC78B` | Opcode `heal` de los scripts de sala: vida al máximo y quita el veneno |

Los IDs de personaje se identificaron convirtiendo a PNG la textura de cada `PLxx.PLD`. TOFU solo lleva cuchillo; el doble de vida es su compensación.

## Estados de salud (Fine / Caution / Danger)

Verificado en la **Beta 2** (`pl_mv00_pad`): se guardan en `+0x151` del jugador.

| Valor | Estado | Condición |
|---|---|---|
| 0 | Fine | Vida > 100 |
| 1 | Caution | Vida ≤ 100, o si `0x800C2740` ≠ 0 (probablemente veneno) |
| 2 | Danger | Vida ≤ 40 |

Cambian la animación y la **velocidad de giro** (40 / 32 / 16 unidades por frame) → [movimiento](../navegacion/movimiento-jugador.md). En PC no se comprobaron los umbrales (la vida máxima es 200).
