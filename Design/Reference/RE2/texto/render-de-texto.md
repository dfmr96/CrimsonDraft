# Cómo se dibuja el texto

> Verificado en la Beta 2 (PS1): código de `mess_trans`, texturas extraídas del disco y capturas de DuckStation.

## Glifos

- Fuente en textura: cada carácter es una celda de **16 × 16** píxeles a **4 bpp** (16 colores con paleta).
- El código de mensaje es directamente el índice de celda → [codificación](../formatos/mensajes.md).
- Cada carácter se dibuja como un **sprite** de la GPU con su CLUT (paleta); no hay rasterizado de fuentes en tiempo real.

### Índices de color dentro del glifo (`ST1_FONT.TIM`)

Los índices 1–8 forman **una sola rampa de luminosidad**, de claro (1) a oscuro (8). Medido en todos los píxeles de `ST1_FONT` y `ST2_FONT` (`font_layers.py`, `font_overlay_test.py`):

| Índice | Uso | Regla medida |
|---|---|---|
| 0 | Fondo | Transparente u opaco según la paleta |
| 1–3 | **Relleno** | Tres tonos repartidos de forma irregular: ≈28 % claro (1), 47 % medio (2) y 25 % oscuro (3). **No es un degradado**: el tono apenas depende de la fila ni de la forma del trazo. Solo hay una ligera tendencia a aclarar el píxel superior izquierdo de cada trazo |
| 4–7 | **Suavizado** | Píxeles fuera del trazo, en esquinas interiores y junto a los escalones de las diagonales (sobre todo 6). No son una sombra desplazada |
| 8 | **Borde** | Contorno de 1 px que rodea también en diagonal |
| 9–12 | Sin uso en los glifos | |

### ¿Textura aplicada encima? No

- **No hay una textura común aplicada por posición.** El tono en la misma posición de la celda coincide entre caracteres un 46 %, lo mismo que por azar (45 %). Entre hojas distintas (`ST1` frente a `ST2`), en la misma posición de la hoja y con cualquier desplazamiento de ±16 px, la coincidencia máxima es del 41 %: el azar.
- **El sombreado está horneado en cada glifo**: los caracteres repetidos en varias hojas son copias exactas, píxel a píxel (115 de 116 celdas).
- Los tonos forman pequeñas manchas (un píxel coincide con su vecino un 50 % de las veces, frente a un 36 % por azar; más en vertical, 55 %). Es compatible con pintura a mano o con una herramienta propia que añadiera ruido a cada glifo. No se puede distinguir entre las dos.

El aspecto "suavizado" no es antialiasing calculado: está en la textura. El color del texto **cambia la paleta entera (índices 1–8, borde incluido)** sin tocar la textura → [colores](colores.md).

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
