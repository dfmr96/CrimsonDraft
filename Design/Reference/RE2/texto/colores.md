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

## Colores según `TEX.TIM`

| `p` | Fila | Color |
|---|---|---|
| 0 | 480 | Blanco (normal) |
| 1 | 482 | **Verde** (nombres de ítems; verificado con captura) |
| 2 | 484 | Rojo |
| 3 | 486 | Gris |
| 4 | 488 | Azul |
| 5–8 | 490–496 | Negro |

Los valores de `p ≥ 4` caen en filas que, con el inventario abierto, ocupa `ST_MOJI` (→ [mapa de paletas](vram-paletas.md)): el color real depende de lo cargado en ese momento. En los mensajes de las salas solo se vio `F9 01`.

## Dato colateral

Algunos RDT de sala llevan en sus datos paletas verdes propias, pero el verde de los nombres de ítem sale de la paleta global de `TEX.TIM` (fila 482).

## Errores descartados durante la investigación

- No es un tinte de color de la GPU (los sprites no usan color de modulación para esto).
- No sale de `ST_MOJI.TIM`: se comprobó comparando con la captura.
