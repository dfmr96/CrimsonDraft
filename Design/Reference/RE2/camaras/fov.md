# FOV y proyección de las cámaras

> Verificado en `LeonU.exe` y en los RID de las 250 salas de Leon. La proyección se replicó en el overlay y
> se validó: el punto de llegada de cada puerta se proyecta dentro de la pantalla en el 97,7% de los casos.

## Cómo se aplica una cámara (`0x4C46E9`)

```
H = camara.lente >> 7             ← distancia de proyección (equivalente a SetGeomScreen de PSX)
SetProjection(H)                  ← 0x449070
LookAt(camara.from, camara.to)    ← 0x5093F0
```

- Pantalla lógica: **320 × 240** (constante `0xF00140` en la misma zona del código).
- **FOV vertical = 2·atan(120 / H)**; **FOV horizontal = 2·atan(160 / H)**.
- Eje Y del mundo hacia **abajo** (las plantas superiores tienen Y negativa: 0, −1800…).

## Distribución (2511 cámaras, escenario de Leon)

| H | Cámaras | % | FOV vertical | FOV horizontal |
|---|---|---|---|---|
| 208 | 1121 | 44,6% | **60°** | 75,1° |
| 231 | 808 | 32,2% | **55°** | 69,4° |
| 257 | 328 | 13,1% | **50°** | 63,8° |
| 188 | 88 | 3,5% | 65° | 80,8° |
| 290 | 86 | 3,4% | 45° | 57,8° |
| 321 | 20 | 0,8% | 41° | 53,0° |
| 221 | 10 | 0,4% | 57° | 71,8° |
| 330 | 8 | 0,3% | 40° | 51,7° |
| 171 | 6 | 0,2% | 70° | 86,2° |
| 448 | 4 | 0,2% | 30° | 39,3° |
| 84 | 4 | 0,2% | 110° | 124,6° |
| otros | 26 | 1,0% | 42°–85° | — |

## Observaciones

- El **90%** de las cámaras usan **60°, 55° o 50°** de FOV vertical.
- Los valores caen en **grados enteros de FOV vertical**: los artistas trabajaban en grados y el juego guarda la distancia de proyección (`lente / 128`).
- La vista 3D del inventario usa **H = 208 (60°)** (`0x50818E`).
- En la Beta 2 (PS1) la sala `ROOM11A0` usa 55° en sus 4 cámaras: mismo sistema.

## Para Unity

Unity también usa FOV **vertical**: los valores se pasan tal cual a `Camera.fieldOfView` con aspecto 4:3. Para replicar el encuadre horizontal de RE2 (~75°) en 16:9, el FOV vertical debería ser ~46°.
