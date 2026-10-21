# Movimiento del jugador ("tank controls")

> Verificado en la Beta 2: `PLAYER.C` (`Pl_move` y `pl_mvXX`), `Add_speedXZ`, `Spd_get` y lectura de RAM en DuckStation. No se comparó con PC.

## Máquina de estados

`Pl_move` (`0x8003A6C4`) usa el subestado `+5` para indexar **dos tablas paralelas**:

1. `0x80097B78`: `pl_mvXX_pad`, lee el mando y decide el siguiente estado.
2. `0x80097BA8`: `pl_mvXX`, ejecuta la animación y el movimiento.

El estado se escribe como palabra en `+4`: `0x0N01` = rutina 1 (moverse), subestado N, fase 0.

| N | Estado | Identificado por |
|---|---|---|
| 0 | **Quieto** | Desde él se pasa a todos los demás |
| 1 | **Caminar** | Adelante sin correr |
| 2 | **Correr** | Adelante + correr (bit `0x200`) |
| 3 | **Retroceder** | Atrás; usa `Lim_dir_ck` (línea contra el escenario) |
| 4 | **Girar en el sitio** | Izquierda o derecha sin adelante |
| 5 | **Apuntar** (`pl_mv05`, en `PL_WEP2.C`) | Botón de apuntar con arma equipada (`+0x10E & 0xFFF ≠ 0`) |
| 6, 7, 11 | Sin identificar (el 7 usa `Goto01`: ir a un punto, ¿movimiento por script?) | — |
| 8 | **Subir o bajar** de un borde u objeto | → [subir y bajar](subir-y-bajar.md) |
| 9 | Sin identificar (usa suelo, objetos y agua; ¿caída?) | — |
| 10 | **Empujar** | → [objetos empujables](objetos-empujables.md) |

### Botones (máscara del motor, no la del mando de PS1)

| Bit | Acción |
|---|---|
| `0x01` | Adelante |
| `0x02` / `0x08` | Girar (`+` / `−` sobre `Y_DIR`) |
| `0x04` | Atrás |
| `0x80` | Acción (examinar, subir, escaleras) |
| `0x100` | Apuntar |
| `0x200` | Correr |
| `0x10` | Lo comprueba el estado de empujar para seguir empujando (¿adelante en otra máscara?) |

Al pulsar Acción, en este orden: **escalera** (si `+0x112 & 0x10`) → **subir o bajar** (`Oma_pl_updown_ck`) → si no, activa la búsqueda de AOT (bit `0x100000` de `0x800C250C`) → [AOT](../interaccion/aot.md).

## Giro

`Y_DIR` (`+0x76`, 4096 = 360°) cambia cada frame según el **estado de salud** (`+0x151`), tabla `0x80010CAC`:

| Estado de salud | Unidades/frame | Grados/frame |
|---|---|---|
| 0 Fine | 40 | ≈ 3,5° |
| 1 Caution | 32 | ≈ 2,8° |
| 2 Danger | 16 | ≈ 1,4° |

El estado de salud lo recalcula `pl_mv00_pad`: **Fine si vida > 100, Caution si ≤ 100 (o si `0x800C2740` ≠ 0, probablemente veneno), Danger si ≤ 40**. Son umbrales de la Beta; no se comprobaron en PC.

## Velocidad: viene de la animación

- No hay constante de velocidad de caminar o correr. **`Spd_get`** lee el desplazamiento entre fotogramas clave de la animación actual y lo guarda en `SPEED` (`+0x144`/`+0x146`/`+0x148`): *root motion*.
- **`Add_speedXZ`** gira ese vector con `RotMatrix(0, Y_DIR, 0)` y lo suma a la posición.
- Las animaciones se eligen según el estado de salud (visto en el estado de empujar), así que la velocidad también cambia con la salud (deducido).

## Convención de dirección (verificado)

El "delante" de un modelo es su **eje local +X**. Con la matriz de PS1 (`x' = x·cos θ + z·sin θ`, `z' = −x·sin θ + z·cos θ`):

```
delante = (cos θ, −sin θ)      θ = Y_DIR · 2π / 4096
```

Comprobado con la escalera empujada: el jugador con `Y_DIR = 1024` (90°) la desplazó hacia −Z. También lo confirman `Oma_ck_front` (proyecta el punto delante en +X local) y el desplazamiento del colisionador de la escalera (`+50` en X local → +50 en Z con `Y_DIR = −1024`).

## Colisión del jugador

- Semitamaño en `+0x9A` (Leon en la Beta: **450**; alto `+0x9E` = 1530).
- Contra el escenario: [SCA](colision-sca.md). Contra objetos: `Oma_ck_oba` → [objetos empujables](objetos-empujables.md).
- Pisadas: `Snd_se_walk` → `Floor_check` (bloque FLR del RDT: sonido según el suelo).
- Plantas: altura de **1800 u** por planta (`planta = −Y / 1800`, `0x91A2B3C5` es la división por 1800).
