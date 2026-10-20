# IA de los enemigos (zombi)

> Verificado en `LeonU.exe`. Nombres de campo de la Beta 2 → [estructura](estructura-entidad.md).

## Máquina de estados en tres niveles

| Nivel | Campo | Contenido |
|---|---|---|
| Estado principal | `+4` (ROUTINE0) | Tabla `0x52F188` |
| Patrón | `+0x10E & 0x3F` (TYPE) | Lo pone **el script de la sala** al colocar al enemigo (`sce_em_set`). Tabla `0x52F1B0` |
| Subestado | `+5` (ROUTINE1) | Dos tablas paralelas de 16 entradas: **decidir** (`0x52F1E8`) y **ejecutar** (`0x52F228`) |

### Estados principales del zombi

| Estado | Función | Significado |
|---|---|---|
| 0 | `0x449590` | Aparecer (inicializa vida → [vida](vida.md)) |
| 1 | `0x44A110` | Comportamiento (despacha al patrón) |
| 2 | `0x44D3D0` | Recibir daño |
| 3 | `0x450310` | Morir |
| 4 | `0x4F9CC0` | Especial (no identificado) |
| 7 | `0x4521F0` | Especial (no identificado) |

La [función de daño](danio.md) elige la transición: vida < 0 → 3; si no → 2.

### Patrones

- Pares → `0x44A140`: zombi de pie.
- Impares → `0x44A180`: **zombi que se arrastra** (y con la mitad de vida).
- Solo 4 de los 16 subestados de "decidir" tienen lógica; "ejecutar" tiene 16 acciones (no se identificó cada una).

## Percepción en reposo (`0x44A1C0`)

| Condición | Resultado |
|---|---|
| Distancia < **5000** y jugador **fuera** del cono frontal de 90° | Se gira hacia el jugador (subestado 1) |
| Distancia < **3000**, dentro del cono `0x320` (~70°) y flags de acción `& 0x15` | **50%** de lanzarse a agarrar (subestado 12, `0xC01`) |
| Distancia < **2000**, dentro del cono `0x400` (90°) y flags de acción `& 0x17` | **50%** de lanzarse a agarrar |

- Distancia: `+0x1F0` (unidades del juego, ~1000 ≈ 1 m).
- Conos: función `0x4ABBA0`, escala de 4096 = 360°.
- **No hay raycast de línea de visión**: solo distancia, ángulo, estado del jugador y azar.
- "Estado del jugador": byte de flags de acción `0x9944EE`. No se identificó cada bit (probablemente correr, disparar o una acción concreta).
- El bit 8 de `+0x154` habilita la percepción (probablemente "despierto").

## Relación con la cámara

El área de la cámara activa (primer registro RVD de la cámara) la consulta también el código de enemigos (`0x47041B`) → [cambio de cámara](../camaras/cambio-de-camara.md).

## Visor en vivo

El overlay de la Beta 2 dibuja estos radios y conos sobre la pantalla (Num5 / Num7) → [herramientas](../herramientas/herramientas.md).

**Convención de dirección (verificada en la Beta 2):** delante = **(cos θ, −sin θ)** con θ = Y_DIR · 2π / 4096 → [movimiento del jugador](../navegacion/movimiento-jugador.md). Ninguno de los 4 modos de Num7 del overlay (`sin/cos`, `sin/-cos`, `-sin/cos`, `cos/sin`) es exactamente este: hay que añadirlo.
