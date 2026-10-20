# Objetos empujables (cajas, escaleras de mano, estatuas)

> Verificado en la Beta 2: código de `OM.C` / `OMA.C` y una prueba en DuckStation. El usuario empujó la escalera de mano de la sala `103` y se compararon dos volcados de RAM (antes, en reposo y después).
> No se comparó con PC.

## Qué es un empujable

**No es un AOT** (en las salas de Leon no hay ni un `SCE_MOVE`): es un **objeto 3D** creado con `obj_model_set` en el script de la sala, con su propia caja de colisión. Ninguna entrada del [SCA](colision-sca.md) se mueve al empujarlo.

### Campos (objeto en `0x800C2C54` + i·`0x1F8`; nombres según [estructura](../enemigos/estructura-entidad.md))

| Offset | Escalera de la sala 103 | Significado |
|---|---|---|
| `+0x00` | `0x181` | Flags: `0x1` activo, **`0x80` empujable**, **`0x100` escalable**, `0x2` desactivado, `0x200` colisión especial con el jugador |
| `+0x04` | 0 | Rutina (0 normal, 1 cayendo) |
| `+0x08` | **2** | Clase: **2 = caja** (pasa por `Om_move_box` y puede caer) |
| `+0x38` / `+0x3C` / `+0x40` | (−18673, 0, −21096) → (−18673, 0, **−23082**) | Posición |
| `+0x76` | −1024 | Rotación (−90°) |
| `+0x84` / `+0x88` / `+0x8C` | Centro del colisionador | Posición + desplazamiento girado |
| `+0x90` / `+0x92` | 600 / 675 | **Semitamaño en X / Z del mundo** (W y D ya girados) |
| `+0x94` / `+0x98` | 50 / −900 | Desplazamiento local del colisionador (X / Y) |
| `+0x9A` / `+0x9C` / `+0x9E` | 675 / 600 / 900 | Semiancho, semifondo y alto locales |
| `+0x106` | — | Planta (`−Y / 1800`, recalculada cada frame) |
| `+0x118` / `+0x11A` | — | Centro del frame anterior |
| `+0x144` | — | **Contador de empuje** |

## Algoritmo (`Om_move`, `0x80034D44`, una vez por frame desde `Game_loop`)

Por cada objeto activo:

1. Si es caja (`+0x08 == 2`): `Om_move_box` (gravedad / suelo).
2. Recalcula offsets y planta.
3. **Si el jugador no está caminando (estado 1) ni empujando (estado 10), el contador `+0x144` vuelve a 0.**
4. Choca con cada enemigo (`Oma_ck_oba`, modo 0: los expulsa).
5. Con el jugador: `Oma_ck_oba` en modo 2 (solo detectar). Si se tocan, en la misma planta, y **`Oma_ck_front`** dice que está de frente, el objeto es candidato.
6. Si una caja pisa un desnivel del SCA (bit 4): `Om_set_fall_dir` y pasa a la rutina 1 (**cae**, por ejemplo al agua).

Después del bucle, **solo si el jugador toca exactamente un objeto**, ese objeto es candidato y tiene el flag `0x80`:

7. `+0x144` += 1. **Mientras sea < 9, el objeto no se mueve**: hay que empujar 9 frames seguidos (≈ 0,3 s a 30 fps).
8. Desde el frame 9: `Oma_ck_oba` en **modo 1**: el objeto se desplaza fuera del jugador, **lo mismo que avanzó el jugador y en un solo eje**.
9. Si en la nueva posición choca con el escenario (`Sca_ck_hit_om`), con un enemigo o con otro objeto: **vuelve a la posición anterior y el contador pasa a 0**.
10. Si no: activa el bit **`0x2000` de `0x800C250C`** ("empujando este frame"; se borra al empezar cada frame) y el contador queda en 10.

## "De frente" (`Oma_ck_front`)

- Punto de prueba: **1,2 × semiancho del jugador + 128** delante de él (eje +X local girado por `Y_DIR`).
- El jugador debe estar **alineado con una cara** del objeto (dentro de su rango en X o en Z, con 128 de margen): en una esquina no se empuja.
- Si la separación es mayor en X que en Z, se empuja la cara X con el punto proyectado en X; si no, la cara Z. El punto debe caer dentro de la caja del objeto.

## Lado del jugador (estado 10, `pl_mv0a`)

- `pl_mv01_pad` (caminar) pasa a **empujar** en cuanto ve el bit `0x2000`.
- Fases (`+6`):
  1. Animación de empujar según el estado de salud.
  2. **Gira poco a poco hasta el múltiplo de 90° más cercano** (suma o resta `(Y_DIR >> 2) & 0xFF` por frame hasta alinearse). **Siempre se empuja recto, por los ejes del mundo.**
  3. Transición y bucle: sonido (`Snd_se_stad`), animación y avance con la velocidad de la propia animación.
- Si se suelta el botón (bit `0x10`) o se pierde el bit `0x2000` (el objeto se bloqueó), pasa a la fase de parada y vuelve a quieto.

## Lectura de diseño

- **Retardo de 9 frames**: rozar un objeto al caminar no lo mueve; empujar es una intención sostenida.
- **Solo un objeto a la vez y solo en los ejes**: el empuje es determinista y encaja en una rejilla, sin física.
- **Bloqueo limpio**: si algo estorba, el objeto no se mueve ni un píxel (vuelve a la posición anterior) y el jugador sale del estado de empujar.
- La distancia la decide el jugador (en la prueba, 1986 u ≈ 2 m): no hay pasos fijos. Los puzzles se resuelven con AOT o flags en la posición de destino (deducido de los scripts).

## Persistencia

En las salas de PC hay `obj_model_set` con un índice global que no se repite entre salas (por ejemplo `obj_model_set(n, 1, 130, 4, 4, 0, k, …)` en las salas 3xxx y 4xxx). Lo más probable es que ese índice guarde dónde quedó cada objeto empujado (deducido; no verificado).
