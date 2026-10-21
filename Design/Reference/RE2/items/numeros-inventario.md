# Números del inventario (contadores y colores)

> Verificado en la Beta 2: código (`St_disp_num`, `print_num`), texturas y la VRAM real con el inventario abierto.

## Flujo

1. **`St_disp_num` (`0x8006BD3C`)** lee el byte 2 del ítem en la tabla de atributos. Si tiene el bit `0x80` (porcentaje), añade un 10 en la parte alta del parámetro.
2. **`print_num` (`0x8006C0DC`)**:
   - Si el valor es **4**, no dibuja nada (hierbas, llaves).
   - Separa el número en centenas, decenas y unidades (divisiones por multiplicación: `0x51EB851F` = /100, `0xCCCCCCCD` = /10).
   - Dibuja cada dígito como **sprite** (`AddPrim`, `0x80086F60`).
   - Paleta: **`GetClut(256, 490 + (color & 0xF))`** (`0x80086E70`).

## Glifos

Fila de `ST_MOJI.TIM`, y = 24–36: **`0 1 2 3 4 5 6 7 8 9 % ∞`**.
- El "%" es el glifo 10: el porcentaje suma 10 al índice.
- "∞" (glifo 11), probablemente para la munición infinita (deducido).

## Colores (verificado en VRAM)

| Color (byte 2) | Fila VRAM | Paleta real | Se ve | Ítems |
|---|---|---|---|---|
| 0 | 490 | `ST_MOJI` paleta 2 | **Verde** | Armas, Ink Ribbon, GL explosivo |
| 1 | 491 | `ST_MOJI` paleta 3 | **Rojo oscuro** | GL y munición incendiaria |
| 2 | 492 | `ST_MOJI` paleta 4 | **Amarillo oliva** | GL y munición ácida |
| 3 | 493 | `ST_MOJI` paleta 5 | **Azul** | Munición de pistola, escopeta, explosiva |

**El color del lanzagranadas indica la granada cargada**, igual que su munición.

## Por qué paleta "color + 2"

En la VRAM real, las paletas de `ST_MOJI.TIM` empiezan en la **fila 488**, aunque su cabecera diga 480 → [mapa de paletas](../texto/vram-paletas.md). No se encontró en el código dónde se decide esa posición (no hay llamadas a `LoadClut`).
