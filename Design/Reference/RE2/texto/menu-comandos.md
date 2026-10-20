# Menú de comandos del inventario

> Verificado en la Beta 2: código, texturas y el parche de traducción probado en DuckStation.

El menú que aparece al elegir un ítem (Equipar / Usar / Examinar / Combinar / Compañero) **no es texto**: son **sprites** recortados de una textura.

## Origen de los sprites

- Textura `ST_MOJI.TIM`, fila **y = 56**, celdas de **48 × 16** píxeles.
- Orden de celdas: **EQUIP, USE, CHECK, COMBINE, PARTNER** (en la Beta, en japonés).
- Tabla de coordenadas UV de los sprites: **`0x8009DD40`** (hay tres variantes de la tabla entre `0x8009DD00` y `0x8009DD66`).
- Funciones que lo dibujan: `St_disp_menu0` / `St_disp_menu1`.
- Índices de color en las celdas: **14 = fondo**; las letras usan una rampa de suavizado **6 (sólido) → 12 (borde tenue)**, pintada en la textura.

## Copias en otras texturas

Las mismas palabras están **duplicadas** en `ST0.TIM` y `ST1.TIM`. Esas texturas se suben después a una zona de VRAM que se solapa y **pisan** las celdas de `ST_MOJI`. Por eso, al traducir solo `ST_MOJI` el menú siguió en japonés:

| Textura | Formato | Dónde |
|---|---|---|
| `ST_MOJI.TIM` | 4 bpp | Fila y = 56 |
| `ST1.TIM` | 8 bpp | Filas completas (se sustituyeron enteras) |
| `ST0.TIM` | 8 bpp, stride 128 bytes | Offset de archivo `75904 + (y − 60) · 128`; 4 celdas (96 bytes por fila) |

→ [traducción](../beta2/traduccion.md)

## Ya en inglés en el original

Los botones de la pantalla de estado **FILE / MAP / ITEM / EXIT** ya estaban en inglés en la Beta.
