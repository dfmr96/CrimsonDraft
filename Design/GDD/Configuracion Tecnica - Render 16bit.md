# Configuración Técnica — Render 16-bit

## Contexto

Crimson Draft utiliza una estética pixel art de 16 bits inspirada en la Sega Genesis y la SNES, implementada en Unity 6 con Universal Render Pipeline (URP) 2D. Esta decisión busca evocar el estilo de los RPGs y aventuras de acción de los años 90 (Shadowrun SNES, Snatcher, Metal Gear 2) sin ser una emulación exacta de ningún hardware específico.

La referencia principal para la configuración es la guía oficial de Unity para juegos retro 16-bit, que documenta las prácticas establecidas por Mega Cat Studios — empresa especializada en desarrollar juegos que corren en hardware original de Genesis y SNES.

> Fuente: [2D Pixel Perfect: How to set up your Unity project for retro 16-bit games](https://unity.com/blog/games/2d-pixel-perfect-how-to-set-up-your-unity-project-for-retro-16-bit-games)

---

## Decisiones de Configuración

### 1. Resolución de Referencia: 320×180

**Valor configurado:** `320 × 180 px`

**Alternativas consideradas:**

| Resolución | Ratio | Escala a 1080p | Origen |
|-----------|-------|----------------|--------|
| 256 × 224 | ~8:7 | ×4 incompleto | SNES nativo |
| 320 × 224 | ~10:7 | ×4 incompleto | Genesis nativo |
| 398 × 224 | ~16:9 | ×4 + bandas | 224px vertical en 16:9 |
| **320 × 180** | **16:9** | **×6 exacto** | Estándar moderno 16:9 |
| 384 × 216 | 16:9 | ×5 exacto | Alternativa 16:9 mayor |

**Justificación:**

La guía de Unity señala explícitamente que para uso general 16:9 sin assets heredados de hardware antiguo, `320×180` es la opción recomendada. Reproduce la densidad visual de la Genesis (320px horizontal) manteniendo el aspect ratio moderno y permitiendo **integer scaling perfecto a ×6 en 1080p y ×4 en 720p**, eliminando el blurring y las bandas negras.

Crimson Draft no hereda assets de ningún hardware específico — los sprites se diseñan desde cero. Por tanto, la restricción de 224px verticales (altura nativa de SNES/Genesis) no aplica. La elección de 180px verticales es pragmática: escala limpia, resolución estándar para el ecosistema indie moderno.

---

### 2. Pixels Per Unit: 16

**Valor configurado:** `16 PPU` en todos los sprites

**Justificación:**

El estándar 16-bit usa tiles de 16×16 píxeles como unidad base (Genesis, SNES). Con PPU = 16, un tile ocupa exactamente **1 unidad Unity**, lo que simplifica el diseño de niveles con tilemaps y mantiene las colisiones en coordenadas enteras.

Con esta configuración, la pantalla de 320×180 muestra:
- **20 tiles horizontales**
- **11.25 tiles verticales** (el 0.25 queda fuera de cámara, irrelevante con pixel-snapping)

Esto es coherente con los anchos de sala típicos de los RPGs 16-bit (14–20 tiles).

---

### 3. Pixel Perfect Camera

**Componente:** `Pixel Perfect Camera` (paquete `com.unity.2d.pixel-perfect`)

| Parámetro | Valor | Razón |
|-----------|-------|-------|
| Reference Resolution | 320 × 180 | Ver sección 1 |
| Upscale Render Texture | ✅ | Renderiza a 320×180 y luego escala; garantiza que cada píxel del juego sea un píxel perfecto en pantalla |
| Pixel Snapping | ✅ | Snap automático de sprites a la grilla de píxeles, elimina el pixel-crawl en movimiento |
| Crop Frame | None | La resolución ya es 16:9 exacto; no se necesita recorte |
| Stretch Fill | ❌ | Activarlo causaría escala no-entera en pantallas no-múltiplo |

**Upscale Render Texture** es la decisión más importante: sin ella, Unity escala la escena directamente a la resolución de pantalla usando la resolución interna del motor, lo que puede producir subpíxeles y artefactos de interpolación. Con ella activada, Unity renderiza todo a 320×180 y escala ese buffer final — comportamiento idéntico al de un chip de video retro outputeando a un TV.

---

### 4. Import Settings de Sprites

| Parámetro | Valor | Razón |
|-----------|-------|-------|
| Pixels Per Unit | 16 | Consistente con PPU global |
| Filter Mode | Point (no filter) | Sin bilinear/trilinear que suavice los píxeles |
| Compression | None | Evita artefactos de compresión en imágenes de baja resolución |
| Generate Mip Maps | ❌ | Sin uso en 2D; generarlos consume memoria innecesariamente |

**Filter Mode: Point** es crítico. Cualquier otro modo interpola entre píxeles vecinos al escalar, produciendo el efecto borroso que destruye la estética retro. La guía de Unity lo establece como requerimiento fundamental.

---

### 5. Cinemachine + Pixel Perfect

Con Cinemachine activo junto al Pixel Perfect Camera, es necesario agregar el componente **`CinemachinePixelPerfect`** (extension de Cinemachine) a la Virtual Camera. Sin él, Cinemachine puede posicionar la cámara en coordenadas no enteras, causando jitter en el tilemap.

El componente se encarga de redondear la posición de la cámara al pixel grid antes de renderizar.

---

## Resumen de Valores Finales

```
Reference Resolution : 320 × 180
Pixels Per Unit      : 16
Integer Scaling      : ×6 en 1080p, ×4 en 720p
Tile size            : 16 × 16 px = 1 unidad Unity
Tiles en pantalla    : 20 × 11 (aprox.)
Filter Mode          : Point
Compression          : None
Upscale RT           : Activado
Pixel Snapping       : Activado
Cinemachine ext.     : CinemachinePixelPerfect
```

---

## Relación con el Diseño del Juego

La resolución baja es una decisión de diseño, no solo técnica. A 320×180 con PPU 16:

- Los personajes ocupan visualmente entre 2 y 4 tiles de alto — proporción coherente con los RPGs de acción 16-bit
- El espacio de pantalla obliga a diseñar mapas compactos, reforzando la claustrofobia del barco
- La UI del QTE (barras verticales y horizontales) tiene suficientes píxeles para ser legible sin necesitar fuentes grandes
- Los documentos encontrables (estilo RE/MSX) pueden mostrarse en una fuente monospace de 6×8px sin perder legibilidad
