# Tabla de atributos de ítems

> Verificado en `LeonU.exe` (PC) y en `PSX.EXE` (Beta 2). Mismo formato en ambas.
> RE2 **no tiene clases de ítem**: el comportamiento sale de estos datos y del rango de ID.

## Ubicación

| Versión | Dirección | Cómo se encontró |
|---|---|---|
| PC | VA `0x547EA8` (archivo `0x1462A8`) | Firma de capacidades de cargador por ID |
| Beta 2 | `0x8009CB38` | Misma firma |

## Registro (8 bytes por ítem, índice = ID)

| Byte | Campo | Valores |
|---|---|---|
| 0 | **Capacidad** | Arma: cargador (Handgun 18, Shotgun 5, Magnum 8). Munición, Ink Ribbon y Small Key: 250. Porcentaje: 100. Resto: 1 |
| 1 | **Nombre tras examinar** | Índice de nombre alternativo (Spade 1, Diamond 2, Heart 4, Club 5, Red Jewel 6, Crank 7, Down 8, Up 9, Special 10) |
| 2 | **Contador** | Bits bajos: color (0–3). `4` = sin número. `0x80` = porcentaje. `0x10`: arma infinita (escrito en tiempo de ejecución) |
| 3 | Nº de combinaciones | |
| 4–7 | Puntero a la lista de combinaciones | → [combinaciones](combinaciones.md) |

## Byte 2 en la Beta 2 (colores verificados en VRAM)

| ID | Ítem | Byte 2 | Se ve |
|---|---|---|---|
| 2, 5 | Pistola, Magnum | `00` | Verde |
| 9 | GL explosivo | `00` (en PC: `03`) | Verde |
| 10 | GL incendiario | `01` | Rojo oscuro |
| 11 | GL ácido | `02` | Amarillo oliva |
| 14 | Sparkshot | `80` | Porcentaje |
| 20, 21, 24 | Munición pistola / escopeta / explosiva | `03` | Azul |
| 25 / 26 | Munición incendiaria / ácido | `01` / `02` | Rojo / Oliva |
| 30 | Ink Ribbon | `00` | Verde |
| 38 | Hierba verde | `04` | Sin número |

→ [números del inventario](numeros-inventario.md)

## Nombres genéricos hasta examinar (PC)

En la tabla de nombres del motor, las cuatro llaves de la comisaría se llaman "Precinct Key", las Up/Down Key "C. Panel Key", las mezclas de hierbas "Mixed Herb" y los rollos "Film". El nombre específico aparece al examinar (byte 1).

## Tabla de nombres

| Versión | Ubicación | Formato |
|---|---|---|
| PC | `.data`, alrededor de `0x1375C5` (archivo) | Tabla de offsets `u16` + nombres separados por `F7` |
| Beta 2 | Nombres en `0x80093C20` (1233 bytes), offsets en `0x800940F6` (136 × `u16`) | Igual, en japonés |

El orden de los nombres coincide entre la Beta y PC (los repetidos están en las mismas posiciones).
