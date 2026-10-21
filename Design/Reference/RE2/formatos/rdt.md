# Formato RDT (archivos de sala)

> Cada sala es un `ROOMsrrp.RDT`: `s` = etapa (hex), `rr` = sala (hex), `p` = escenario (0 Leon, 1 Claire).
> Base: `Rdt2.cs` de IntelOrca/biohazard-utils, comprobado contra los archivos de PC y de la Beta 2.

## Cabecera

| Offset | Tamaño | Campo |
|---|---|---|
| `0x00` | 8 bytes | `nSprite, nCut, nOmodel, nItem, nDoor, nRoom_at, Reverb_lv, unknown` |
| `0x08` | 23 × `int32` | Tabla de offsets a los bloques |

`nCut` (byte 1) = número de cámaras de la sala.

## Tabla de offsets (RE2)

| Índice | Bloque | Notas |
|---|---|---|
| 0–4 | Sonido (`EDT`, `VH`, `VB`, versiones de prueba) | |
| 5 | `OVA` | Desconocido |
| 6 | `SCA` | Colisiones → [formato](../navegacion/colision-sca.md) |
| **7** | **`RID`** | **Cámaras** → [FOV](../camaras/fov.md) |
| **8** | **`RVD`** | **Zonas de cambio de cámara** → [cambio de cámara](../camaras/cambio-de-camara.md) |
| 9 | `LIT` | Luces |
| 10 | Tabla de modelos de objeto | |
| 11 | `FLR` | Suelo |
| 12 | `BLK` | Bloques |
| **13** | **Mensajes** | En la Beta 2 y en PC: **japonés** (el que muestra la Beta) |
| **14** | **Mensajes** | **Inglés** (el que muestra PC; la Beta lo trae en 40 salas sin usarlo) |
| 15 | Textura de scroll | |
| **16** | **Script de inicialización (SCD)** | Declara AOT, enemigos, ítems |
| **17** | **Script principal (SCD)** | Procedimientos de eventos |
| 18–22 | Efectos, texturas de objetos, animaciones | |

## Bloque de mensajes (13 / 14)

- Empieza con una tabla de `uint16`. El primer valor / 2 = número de mensajes.
- Cada offset es relativo al inicio del bloque. Cada mensaje termina en `FE`.
- Codificación → [mensajes](mensajes.md).

## Bloques de script (16 / 17)

- Tabla de `uint16` con offsets a procedimientos (el primero / 2 = número de procedimientos).
- El 16 tiene un procedimiento: la inicialización de la sala.
- El 17 contiene los procedimientos llamados por eventos (`gosub`, AOT de evento).
- Opcodes → [scd-opcodes](scd-opcodes.md).

## Bloque RID (cámaras): 32 bytes por cámara

| Offset | Tipo | Campo |
|---|---|---|
| 0 | `u16` | Marca (vale 1 en exactamente una cámara por sala; función desconocida) |
| 2 | `u16` | **Lente**: `H = lente >> 7` (distancia de proyección) |
| 4 | 3 × `i32` | Posición de la cámara (`from`) |
| 16 | 3 × `i32` | Punto al que mira (`to`) |
| 28 | `u32` | Puntero a las máscaras del fondo |

## Bloque RVD (zonas de cámara): 20 bytes por zona

| Byte | Campo |
|---|---|
| 0 | Activa (0 = desactivada) |
| 1 | Planta (`FF` = cualquiera) |
| 2 | Cámara origen |
| 3 | Cámara destino |
| 4–19 | Cuadrilátero: 4 × (x, z) `i16` |

Termina con `FFFFFFFF`. El primer registro de cada cámara es **su área**, no un disparador.

## Diferencias en la Beta 2

- Mismo formato. La Beta 2 trae **125 salas** de Leon.
- La tabla 13 está en 102 salas, la 14 en 40 (26 completamente en inglés).
