# Cómo se dibuja el texto

> Verificado en la Beta 2 (PS1): código de `mess_trans`, texturas extraídas del disco y capturas de DuckStation.

## Glifos

- Fuente en textura: cada carácter es una celda de **16 × 16** píxeles a **4 bpp** (16 colores con paleta).
- El código de mensaje es directamente el índice de celda → [codificación](../formatos/mensajes.md).
- Cada carácter se dibuja como un **sprite** de la GPU con su CLUT (paleta); no hay rasterizado de fuentes en tiempo real.

### Índices de color dentro del glifo (`ST1_FONT.TIM`)

| Índice | Uso |
|---|---|
| 0 | Fondo |
| 1–3 | **Relleno** (degradado de claro a oscuro) |
| 4–7 | **Sombra** |
| 8 | **Borde** (contorno) |
| 9–12 | Sin uso en los glifos |

El aspecto "suavizado" no es antialiasing calculado: está **pintado a mano píxel a píxel** (degradado en el relleno, sombra y contorno). Cambiar de paleta cambia el color del relleno sin tocar la textura → [colores](colores.md).

## Dos paletas en `ST1_FONT.TIM`

Son idénticas salvo el **índice 0**:

| Paleta | Índice 0 | Resultado |
|---|---|---|
| 0 | `0x0000` | Fondo **transparente** (texto sobre la escena) |
| 1 | `0x8000` | Fondo **negro opaco** |

→ [TIM y transparencia](../formatos/tim.md)

## Fuente por etapa

Hay `ST1_FONT.TIM` … `ST7_FONT.TIM`. Lo más probable (deducido) es que cada una lleve **solo los kanji de esa parte del juego**, porque la VRAM no admite la fuente japonesa entera. Las letras latinas y los kana ocupan las mismas celdas en todas.

## Textos de interfaz que no son mensajes

Las palabras del menú y los números del inventario **no** usan la fuente: son sprites de `ST_MOJI.TIM` → [menú de comandos](menu-comandos.md), [números del inventario](../items/numeros-inventario.md).
