# Espacio navegable del jugador

> Verificado en la Beta 2: `Sca_ck_hit`, `Om_move`, `Oma_ck_oba`, `Oma_pull`, `Oma_get_on_om`, `Oma_pull_hosei_x/z`. La altura de la escalera de mano se comprobó en RAM. No se comparó con PC.

## No hay navmesh

El jugador **no navega sobre una malla ni una rejilla**. Se mueve libremente en el plano XZ (con la velocidad de su animación → [movimiento](movimiento-jugador.md)) y, cada frame, se le **expulsa** de todo lo que bloquea:

1. **Escenario** (`Sca_ck_hit`): las formas del [SCA](colision-sca.md) de su planta.
2. **Objetos** (`Oma_ck_oba` en `Om_move`): cada objeto lleva su caja → [empujables](objetos-empujables.md).
3. **Otros personajes**: misma función, con los enemigos y el compañero.

El espacio navegable es **lo que queda libre**: el suelo de la sala menos esas formas. Nadie lo define de forma explícita.

## Altura y plantas

- **Planta = −Y / 1800** (se recalcula cada frame en objetos y personajes).
- Cada entrada del SCA tiene una **máscara de plantas**: solo choca con quien está en una planta incluida. Así una misma sala tiene paredes distintas abajo y arriba.
- La altura del suelo en un punto la da `Sca_get_ground`, que tiene casos para rampa (`Sca_get_slope_high`), escalera (`Sca_get_stairs_high`) y curva (no se desensambló entera).
- Los desniveles se marcan con formas especiales del SCA: **9** = borde al que se puede subir, **10** = desnivel para bajar o caer.

## Encima de un objeto escalable

Un objeto escalable (flags `0x101`) **no necesita datos extra para su superficie**: la parte de arriba **es su propia caja de colisión**.

### Altura de la tapa

```
tapa = centro_colisionador_Y (+0x88) − alto (+0x9E)
```

Escalera de la sala 103: centro Y = −900, alto = 900 → **tapa = −1800**, justo una planta. Por eso subir siempre es de 1800 u → [subir y bajar](subir-y-bajar.md).

### ¿Está encima? (`Oma_get_on_om`)

Recorre los objetos escalables y devuelve el primero que cumpla:

- **Y del jugador == tapa del objeto** (igualdad exacta: el estado de subir deja la Y en ese valor).
- El centro del jugador está dentro del **rectángulo útil de la tapa**:

```
semiancho_útil_X = semitamaño_X del objeto (+0x90) − semiancho del jugador (+0x9A) / 2
semiancho_útil_Z = semitamaño_Z del objeto (+0x92) − semiancho del jugador / 2
```

Si ninguno cumple, **mantiene el objeto anterior** (`+0x1E4`): el jugador no "se cae" por salirse un poco.

### Mantenerlo dentro (`Oma_pull`, cada frame, para el objeto en `+0x1E4`)

1. Comprueba si el jugador se sale del rectángulo útil, en X y en Z, ahora y en el frame anterior.
2. Si se sale por un lado, busca **otro objeto escalable con la tapa a la misma altura**, que solape en el otro eje y esté **a menos de 200 u** de hueco en esa dirección. Si existe y el jugador ya está sobre su rectángulo útil, **pasa a ese objeto**: se puede caminar de una caja a otra contigua.
3. Si no hay vecino, **lo devuelve al borde** (`Oma_pull_hosei_x` / `_z` fijan X o Z al límite del rectángulo útil). Caminar contra el borde solo hace deslizarse a lo largo.
4. Actualiza `+0x1E4` con `Oma_get_on_om`.

**Consecuencia:** desde arriba **no se puede caer por el borde caminando**. Para bajar hay que pulsar Acción (`Oma_pl_updown_ck` con `+0x1E4 ≠ 0`).

Mientras el jugador está encima, `Om_move` no considera ese objeto para empujar: lo trata con `Oma_pull`.

## Ejemplo de la sala 103

| Dato | Valor |
|---|---|
| Caja de la escalera (mundo) | 1200 × 1350 (semitamaños 600 / 675) |
| Semiancho de Leon | 450 → resta 225 por lado |
| **Superficie útil arriba** | **750 × 900** u alrededor del centro |
| Altura de la tapa | −1800 (planta 1) |

En el SCA de esta sala hay entradas con máscara de plantas `0x1FF` cerca de la escalera; esas también afectan arriba. Que la planta del jugador pase a 1 al subir es deducido (por la fórmula), no comprobado en RAM.
