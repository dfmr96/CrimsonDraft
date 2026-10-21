# Subir y bajar (escaleras de mano, cajas, bordes)

> Verificado en la Beta 2: `Oma_pl_updown_ck` (`0x80036364`), `Oma_on_check`, `Sca_ck_hit` y tablas del ejecutable. No se comparó con PC.

## Cuándo se comprueba

Al pulsar **Acción** estando quieto, caminando, corriendo o girando (`pl_mv00/01/02/04_pad`), antes de buscar AOT → [movimiento](movimiento-jugador.md).

## Subir

1. Dirección **redondeada al múltiplo de 90° más cercano**: `(Y_DIR + 0x200) & ~0x3FF`.
2. Punto de prueba = posición + esa dirección × **distancia de subida** (por personaje) y **1800 u más arriba** (una planta).
3. Se puede subir si:
   - el punto toca un **borde escalable del SCA** (forma 9, bit 2) → [colisión](colision-sca.md), **o**
   - hay delante un **objeto escalable** (flags `0x101`: activo + `0x100`), en la misma planta y de frente (`Oma_front_ck`).
4. Además: el suelo en el punto no puede estar más alto que esa planta y no debe haber objetos estorbando (`Oma_obj_ck_all`).
5. Resultado: estado **8** (`pl_mv08`), fase 0. El objeto al que se sube se guarda en **`+0x1E4`** del jugador (0 si es un borde del escenario).

Solo hasta la planta 30 (`planta < 31`).

## Bajar

1. Punto de prueba = posición + dirección redondeada × **distancia de bajada**, a la misma altura.
2. Se puede bajar si el suelo allí está **más bajo** que el jugador y:
   - el punto toca un **desnivel del SCA** (forma 10, bit 4), **o** el jugador está encima de un objeto (`+0x1E4 ≠ 0`);
   - y no hay otro objeto en el punto (`Omd_in_check`, `Oma_obj_ck_all`).
3. Resultado: estado **8**, fase 2; `+0x1E4` vuelve a 0.

## Distancias de prueba (tablas `0x800960D4` y `0x800960FC`, índice = id de personaje + 4)

| Personaje | Subir | Bajar |
|---|---|---|
| id 0 (Leon) | 800 | 1000 |
| Otros ids | 800 | 1000 (los primeros índices de la tabla tienen otros valores: 884, 1251, 581, 971 / 884, 653, 854, 581) |

## Mantenerse encima

La superficie de arriba es la propia caja del objeto: tapa = centro Y − alto. El jugador queda confinado a ese rectángulo (menos medio ancho suyo), puede pasar a otra caja contigua de la misma altura y no puede caerse por el borde caminando → [espacio navegable](espacio-navegable.md).

## Lectura de diseño

- **Subir es una acción explícita** (botón Acción), no automática al caminar.
- **Altura fija de una planta (1800 u)**: todo lo escalable mide lo mismo, así que una sola animación sirve para todo.
- **Dirección redondeada a 90°**: el jugador no tiene que estar perfectamente alineado.
- **Escalera de mano = empujable + escalable** (flags `0x80 | 0x100`): el puzzle típico es empujarla hasta un sitio y subirse.
