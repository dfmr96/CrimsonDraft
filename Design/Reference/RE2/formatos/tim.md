# Texturas TIM y paletas (PlayStation)

> Formato estándar de imagen de PlayStation, usado por RE2 en PC y en PS1.

## Estructura

| Offset | Contenido |
|---|---|
| 0 | `0x10` (magic) |
| 4 | Flags: bits 0–2 = modo (0 = 4 bpp, 1 = 8 bpp, 2 = 16 bpp); bit 3 = tiene CLUT |
| 8 | **Bloque CLUT** (si existe): `u32` longitud, `u16 x, y, ancho, alto`, colores |
| … | **Bloque de imagen**: `u32` longitud, `u16 x, y, ancho (en unidades de 16 bits), alto`, píxeles |

`x, y` son la **posición de destino en la VRAM** (1024 × 512).

## Colores (15 bits)

`color = R | (G << 5) | (B << 10)`, 5 bits por canal, más el bit 15 (STP).

| Valor | Significado |
|---|---|
| `0x0000` | **Transparente**: la GPU no dibuja ese píxel |
| `0x8000` | **Negro opaco** (STP activado) |

Las fuentes de RE2 usan esa diferencia: una paleta con fondo transparente y otra idéntica con fondo negro → [render de texto](../texto/render-de-texto.md).

## Identificador de paleta (CLUT ID)

`clut = (fila << 6) | (columna / 16)`. Ejemplo: `0x7810` = fila 480, columna 256.

## Advertencia: la cabecera no siempre manda

La posición de la cabecera es donde se **carga por defecto**, pero en RE2:
- Varias texturas comparten zona y **se pisan** según el orden de carga (`ST_MOJI`, `ST0`, `ST1` y `TEX` usan la columna 256 desde la fila 480).
- `ST_MOJI.TIM` dice fila 480, pero en la VRAM real **sus paletas están en la 488** (verificado con el visor de VRAM de DuckStation) → [mapa de paletas](../texto/vram-paletas.md).

Para saber qué color se ve de verdad hay que mirar la VRAM en ejecución.

## Texturas de la interfaz (Beta 2, `COMMON/DATA`)

| Archivo | Código | Contenido |
|---|---|---|
| `ST0.TIM` / `ST1.TIM` | 161 / 162 | Marcos del inventario, retratos, iconos (8 bpp, 4 paletas) |
| `ST1_FONT.TIM` … `ST7_FONT.TIM` | 163 … | **Fuente de kanji por etapa** (4 bpp, 2 paletas) |
| `ST_MOJI.TIM` | 173 | Palabras y números de la interfaz (4 bpp, 18 paletas) |
| `TEX.TIM` | 174 | Paletas de color del texto y otras texturas |

Los códigos salen de `FILECODE.H` → [disco y símbolos](../beta2/disco-y-simbolos.md).
