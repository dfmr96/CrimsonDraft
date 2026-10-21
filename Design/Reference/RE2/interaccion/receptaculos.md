# Receptáculos y transformadores

## Patrón general

1. Un AOT `EVENT` (`PL | MANUAL | FRONT`) en el mueble: revisarlo ejecuta un procedimiento.
2. Un AOT `FLAG_CHG` del grupo `FG_USE` por cada ítem aceptado, en la misma zona: declara "aquí se puede usar X".
3. Al usar un ítem desde el inventario dentro de la zona, el motor guarda el ítem en **`V_USED_ITEM`** y ejecuta el procedimiento de uso.
4. Con las manos vacías, `V_USED_ITEM` vale 0 y se muestra la descripción, la pista y el progreso ("The wolf medal is in place.").
5. El ítem se quita con `sce_item_lost`.

## Fuse Case → Main Fuse (sala `60D0`): flujo completo

| AOT | Tipo | Función |
|---|---|---|
| `AOT_2` | `SCE_ITEM` | La Fuse Case (se recoge en la misma sala) |
| `AOT_4` | `SCE_EVENT` | La caja de control |
| `AOT_6` | `FLAG_CHG` (`FG_USE`, ítem 77) | "Aquí se puede usar la Fuse Case" |
| `AOT_1` | `SCE_ITEM` | La Main Fuse, en (32000, 32000): fuera del alcance |

Secuencia al usar la Fuse Case:
1. Marca el puzzle resuelto (`FG_COMMON 119`) y bloquea el control.
2. Corte de cámara y animación del mecanismo.
3. `sce_item_lost(FuseCase)`: se quita **justo antes de entregar el resultado**.
4. `aot_on(AOT_1)`: dispara el pickup escondido, con el diálogo de recogida normal.
5. Si el jugador no la toma, al revisar la caja de nuevo se vuelve a disparar `aot_on(AOT_1)`. **El resultado nunca se pierde.**

## Cuarto oscuro (sala `2080`)

Rollo de película → `sce_item_lost(FilmX)` → `aot_on` de un pickup "Developed Film X" (un **documento**) colocado en (20000, 20000). Mismo truco: el resultado se entrega con el flujo de recogida de siempre.

## Receptáculos del escenario de Leon

Red Jewel (`10B0`), Unicorn Medal (`2000`), Eagle/Wolf Medal (`4070`), piezas de ajedrez (`3030`), Red Card (`2110`), Crank y Cog (`1110`), Cord (`2040`, `20F0`), Manhole Opener (`21A0`), Main Fuse (`6080`, `6110`), Up/Down Key (`5030`/`5040`), MO Disk (`6160`), Joint Plugs (`7020`), Valve Handle (`1080`, `4080`, `4090`, no se consume), Lighter (5 salas, no se consume).
