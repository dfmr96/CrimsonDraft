# Estructura de entidad (jugador, enemigos, objetos)

> Jugador y enemigos comparten la misma estructura.
> Nombres de campo: tabla de `Member_set` de la Beta 2 (tabla de saltos `0x80010FDC`; nombres de `MAIN.SYM`). Se confirmaron en PC y en la Beta los offsets de posición, vida y planta.

## Campos

| Offset | Nombre (Beta) | Contenido |
|---|---|---|
| `0x00` | POINTER | Bit 0 = entidad activa |
| `0x04`–`0x07` | ROUTINE0–3 | Estado principal, subestado y niveles inferiores de la máquina de estados → [IA](ia.md) |
| `0x08` | ID | Tipo de enemigo (índice de la tabla de daño) |
| `0x0A` | OBJ_NO | |
| `0x0B` | SCE_NO | Índice en el script de la sala |
| `0x10` | ATTRIBUTE | |
| `0x38` / `0x3C` / `0x40` | X / Y / Z_POS | Posición (`s32`; ~1000 u ≈ 1 m) |
| `0x74` / `0x76` / `0x78` | X / Y / Z_DIR | Rotación; **4096 = 360°**. Y_DIR es el rumbo |
| `0x94` / `0x96` / `0x98` | OBJ_OFS | Desplazamiento del colisionador |
| `0x9A` / `0x9C` / `0x9E` | OBJ_W / D / H | Tamaño del colisionador |
| `0x106` | FLOOR | Planta (filtra zonas y AOT) |
| `0x10E` | TYPE | Patrón de comportamiento (`& 0x3F`), lo elige el script de la sala |
| `0x118` / `0x11A` | SCA_OLD | Posición anterior |
| `0x144`–`0x148` | SPEED | Velocidad X/Y/Z |
| `0x14E` | HOKAN | Interpolación de animación (*hokan* = interpolar) |
| `0x154` | STATUS_FLAG | |
| `0x156` | **HP** | Vida actual (`s16`) |
| `0x162` | **HP max** | Vida máxima (`s16`) |
| `0x1C2` | GROUND | Altura del suelo |
| `0x1C4` / `0x1C6` | DEST X / Z | Destino de movimiento |
| `0x1CC` | SCE_FLAG | |
| `0x1D4`–`0x1DA` | SCE_FREE | Variables libres para el script |
| `0x1DE` | PARTS_POS_Y | |
| `0x1F0` | — | **Distancia al jugador** (la usa la percepción) |

En PC, `+5` es el subestado y `+0x155` (bit 8 de la palabra en `+0x154`) habilita la percepción (deducido).

### Campos vistos en la investigación de navegación (Beta 2)

| Offset | Contenido |
|---|---|
| `+0x04` (palabra) | Estado completo: `0x0N01` = rutina 1, subestado N, fase 0 → [movimiento](../navegacion/movimiento-jugador.md) |
| `+0x06` | Fase dentro del subestado |
| `+0x4A` / `+0x4C` / `+0x4E` | Centro del colisionador del frame anterior (s16; decide el eje de choque) |
| `+0x84` / `+0x88` / `+0x8C` | Centro del colisionador (s32) |
| `+0x90` / `+0x92` | Semitamaño en X / Z del mundo (ya girado) |
| `+0x112` | Bit `0x10`: en zona de escalera |
| `+0x144` | En objetos: contador de empuje |
| `+0x151` | Jugador: estado de salud (0 Fine, 1 Caution, 2 Danger) |
| `+0x1E4` | Jugador: objeto sobre el que está subido |

Objetos: flags en `+0x00` (`0x80` empujable, `0x100` escalable) y clase en `+0x08` (2 = caja) → [objetos empujables](../navegacion/objetos-empujables.md).

## Dónde están (Beta 2, `Work_set`)

| Puntero | Contenido |
|---|---|
| `0x800C2744` | Jugador |
| `0x800C2748` | Compañero |
| `0x800C274C` | Enemigos: 16 punteros; los vacíos apuntan a `0x800C6B7C` |
| `0x800C2C54` + i·`0x1F8` | Objetos de la sala (tamaño de registro `0x1F8`) |
| `0x800C73D8` | Puertas |

## PC

- Jugador: `0x9944F0`.
- Lista de enemigos: `0x994714`.

→ [Direcciones de PC](../direcciones/pc-leonu.md), [Direcciones de la Beta 2](../direcciones/beta2-psx.md)
