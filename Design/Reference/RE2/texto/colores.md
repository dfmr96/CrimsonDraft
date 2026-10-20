# Colores del texto (código `F9`)

> Verificado en la Beta 2: manejador de `F9` y comparación con una captura del juego (nombre de ítem en verde, "F. Aid Spray").

## Mecanismo

- `mess_trans` despacha los códigos de control con una tabla de saltos en `0x8001087C`.
- El manejador de **`F9 p`** está en **`0x80030F88`** y calcula:

```
clut = (p << 7) | 0x7810
```

- `0x7810` = fila **480**, columna **256**. Cada `p` suma `0x80` = **2 filas**.
- Resultado: **`F9 p` → paleta en la fila 480 + 2p**, que corresponde a `TEX.TIM`.
- Lo único que cambia es la CLUT del sprite de cada glifo: la textura no cambia → [render de texto](render-de-texto.md).

## Paletas de `TEX.TIM` (leídas del archivo en la ISO original)

**Cada paleta tiñe los índices 1–8 del glifo, borde incluido**: es una rampa de 8 pasos del mismo color, de claro (1) a oscuro (8). Cada color ocupa dos filas: la par con fondo transparente (índice 0 = `0x0000`) y la impar con fondo negro opaco, como las dos paletas de `ST1_FONT` → [render de texto](render-de-texto.md).

| `p` | Fila | Color | 1 (relleno claro) | 3 (relleno oscuro) | 6 (suavizado) | 8 (borde) |
|---|---|---|---|---|---|---|
| 0 | 480 | Blanco hueso (normal) | 216, 216, 200 | 168, 168, 152 | 80, 80, 80 | **56, 48, 72** (púrpura oscuro) |
| 1 | 482 | **Verde** (nombres de ítems; verificado con captura) | 0, 184, 40 | 0, 136, 24 | 0, 80, 8 | 0, 40, 0 |
| 2 | 484 | Granate | 152, 0, 72 | 120, 0, 40 | 72, 0, 0 | 16, 0, 0 |
| 3 | 486 | Gris oscuro (texto apagado) | 104, 104, 104 | 72, 72, 72 | 24, 24, 24 | 8, 8, 8 |
| 4 | 488 | Azul | 32, 80, 232 | 24, 64, 200 | 16, 32, 104 | 8, 8, 64 |
| 5–9 | 490–498 | **Vacías: todo transparente** (el texto no se vería) | — | — | — | — |

Lectura de diseño: el borde no es negro, es **el tono más oscuro del mismo color**. Solo en el blanco tira a púrpura.

Los valores de `p ≥ 4` caen en filas que, con el inventario abierto, ocupa `ST_MOJI` (→ [mapa de paletas](vram-paletas.md)): el color real depende de lo cargado en ese momento. En los mensajes de las salas solo se vio `F9 01`.

## Dato colateral

Algunos RDT de sala llevan en sus datos paletas verdes propias, pero el verde de los nombres de ítem sale de la paleta global de `TEX.TIM` (fila 482).

## Errores descartados durante la investigación

- No es un tinte de color de la GPU (los sprites no usan color de modulación para esto).
- No sale de `ST_MOJI.TIM`: se comprobó comparando con la captura.
- Una versión anterior de este documento decía que las paletas 5–8 eran negras y que el color solo cambiaba el relleno. Las dos cosas eran falsas (corregido leyendo `TEX.TIM`).
