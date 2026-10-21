# Mapa de paletas en VRAM (columna 256)

> Verificado con el visor de VRAM de DuckStation (*Debug → Show VRAM*) con el inventario abierto en la Beta 2.

## Lo que hay en la VRAM real

| Filas | Contenido |
|---|---|
| 480–487 | `TEX.TIM`: paletas del texto (código `F9`) |
| **488–497** | `ST_MOJI.TIM`: paletas 0–9 |

Las filas 490, 496 y 497 no coincidían exactamente con el archivo: probablemente son **paletas animadas** que el juego reescribe (deducido).

## La cabecera no coincide

| Textura | Fila en la cabecera | Fila real |
|---|---|---|
| `ST_MOJI.TIM` | 480 | **488** |
| `ST1_FONT.TIM` | 480 | — |
| `ST0.TIM` / `ST1.TIM` | 480 (columna 0, 256 colores) | — |

- Varias texturas declaran la misma zona, así que se pisan según el orden de carga.
- **No se encontró en el código por qué `ST_MOJI` acaba en la 488**: no hay llamadas a `LoadClut`. Se vieron coordenadas de subida `(256, 488)` en el código de la pantalla de estado, pero no se siguió hasta el final.

## Consecuencias

- **Números del inventario:** `GetClut(256, 490 + color)` = paleta `color + 2` de `ST_MOJI` → [números](../items/numeros-inventario.md).
- **Texto `F9`:** con `p ≥ 4` cae en filas de `ST_MOJI` → [colores](colores.md).

## Fórmula del CLUT ID

`clut = (fila << 6) | (columna >> 4)`. Ejemplos: `0x7810` = (480, 256); `0x7A90` = (490, 256).

## Lección

Para saber qué color sale en pantalla, **la VRAM en ejecución manda** sobre la cabecera del archivo. Dos hipótesis anteriores (tinte de GPU, paletas de `ST_MOJI` en la 480) se descartaron así.
