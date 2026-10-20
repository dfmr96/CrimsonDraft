# AOT: áreas de disparo ("hitboxes")

> AOT = *Area Of Trigger*. Todo lo interactuable o disparable de una sala.

## Almacenamiento

- Se declaran en el script de inicialización: `aot_set`, `door_aot_se`, `item_aot_set` y sus variantes `_4p`.
- **Máximo 32 por sala**: array de 32 punteros en `0x992E50` (PC), que se vacía al entrar (`0x4E7650`).
- **No se copian**: cada entrada **apunta a los bytes del propio script**. Por eso `aot_reset` y `aot_on` pueden modificarlas o dispararlas en caliente.

## Forma

- Rectángulo (x, z, ancho, fondo) o, en las variantes `_4p`, cuadrilátero de 4 puntos.
- El manejador `_4p` marca el bit **`0x80` del campo SAT**. La prueba de contacto usa `0x4C4CF0` (rectángulo) o `0x4C4C40` (cuadrilátero) según ese bit (visto en el bucle del agua, `0x4EDA1F`).
- Solo afectan a quien está en la misma **planta**.

## Tipo (SCE)

`AUTO, DOOR, ITEM, NORMAL, MESSAGE, EVENT, FLAG_CHG, WATER, MOVE, SAVE, ITEMBOX, DAMAGE, STATUS, HIKIDASHI, WINDOWS`

## Quién y cómo la activa (SAT)

| Flag | Significado (por nombre y uso) |
|---|---|
| `PL` | Jugador |
| `EM` | Enemigos |
| `SPL` | Compañero (Ada / Sherry) |
| `OB` | Objetos |
| `MANUAL` | Hace falta pulsar Acción |
| `FRONT` | Se comprueba el área **delante** del personaje |
| `UNDER` | Se comprueba la posición **del personaje** (al pisar) |
| (ninguno) | Inerte: solo se dispara con `aot_on` |

`FRONT`/`UNDER` son **deducidos** del nombre y del uso; no se leyó la rutina de comprobación.

## Combinaciones (250 salas de Leon)

| Uso | SAT | Tipos | Cuántas |
|---|---|---|---|
| Interactuar con botón | `PL \| MANUAL \| FRONT` | Puertas, ítems, mensajes, eventos, guardado, baúles | ~1300 |
| Disparador al pisar | `PL \| UNDER` | Eventos, daño, flags | ~130 |
| "Usar ítem aquí" | `PL \| FRONT` | `FLAG_CHG` con `FG_USE` | 42 |
| Puerta al pisar | `PL \| MANUAL \| UNDER` | Escaleras, huecos | 21 |
| Solo enemigos | `EM \| UNDER` | `NORMAL` | 26 |
| Compañero | `SPL \| UNDER` | Eventos | 13 |
| Agua | `PL \| EM \| (SPL) \| OB \| UNDER` | `WATER` | 27 |
| Inertes | `SAT_AUTO` | Pickups escondidos y AOT especiales | 28 |

## Uso de ítems en una zona

Cada ítem aceptado se declara con un `FLAG_CHG` del grupo `FG_USE` (dato 0 = 10, dato 2 = ID del ítem). Coinciden 1:1 con las salas que comparan `V_USED_ITEM` → [receptáculos](receptaculos.md).
