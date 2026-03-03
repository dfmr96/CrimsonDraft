# Sistema de Dispersión y Apuntado

## Concepto

El jugador no usa una mira — elige un punto de intención en el QTE bidimensional. Ese punto pasa por tres capas de dispersión antes de convertirse en el punto de impacto final. Las tres capas son independientes y se aplican en orden. El disparo siempre ocurre: nunca hay miss automático por stats.

---

## Capas de Dispersión

### Layer 1 — Dispersión base (HP)

**Se aplica solo al primer disparo de cada ráfaga.**

El radio del círculo de dispersión escala linealmente con el daño recibido:

```
radio = dispersion_base * (1.0 + (1.0 - hp_ratio) * (HP_FACTOR - 1.0))
```

Con `HP_FACTOR = 2.0`:

| HP (%) | Multiplicador | Radio P229 | Radio MP5 | Radio Mk18 | Radio Benelli |
|--------|--------------|-----------|----------|-----------|--------------|
| 100%   | ×1.0         | 12 px     | 14 px    | 6 px      | 40 px        |
| 50%    | ×1.5         | 18 px     | 21 px    | 9 px      | 60 px        |
| 0%     | ×2.0         | 24 px     | 28 px    | 12 px     | 80 px        |

El punto de impacto L1 se selecciona aleatoriamente dentro del disco (distribución uniforme de área):
```
angle = random(0, 2π)
r = radio * sqrt(random(0, 1))    ← sqrt para distribución uniforme en área, no en radio
l1 = intencion + (r·cos(angle), r·sin(angle))
```

### Layer 2 — Desviación mecánica del arma

**Se aplica siempre (incluso al primer disparo).**

Imperfecciones mecánicas del arma: variación aleatoria uniforme en ambos ejes.

```
l2 = l1 + (uniform(-weapon_deviation, weapon_deviation),
            uniform(-weapon_deviation, weapon_deviation))
```

| Arma       | weapon_deviation |
|------------|-----------------|
| P229       | ±2 px           |
| MP5        | ±2 px           |
| Mk18       | ±1 px           |
| Benelli M4 | ±3 px           |

### Layer 3 — Recoil por patrón

**Se aplica desde el segundo disparo en adelante (consecutive_shots ≥ 2).**

Cada arma tiene un patrón predefinido de desplazamiento por disparo. El primer disparo tiene recoil (0, 0). Los siguientes siguen el patrón indexado por `consecutive_shots - 1`.

```
(dx, dy) = recoil_pattern[consecutive_shots - 1]
spread_x = dx * hand + uniform(-pattern_spread, pattern_spread)
spread_y = dy + uniform(-pattern_spread, pattern_spread)
l3 = l2 + (spread_x, spread_y)
```

`hand = +1` (diestro) o `-1` (zurdo) — invierte el componente horizontal.

Si se excede la longitud del patrón, se repite el último punto.

---

## Patrones de Recoil por Arma

### P229 — Forma en "7"
Sube suavemente, luego deriva progresivamente a la derecha. Compensación: tirar abajo-izquierda.

| Disparo | dx | dy  |
|---------|----|-----|
| 1       |  0 |   0 |
| 2       |  0 |  -5 |
| 3       |  2 |  -6 |
| 4       |  3 |  -5 |
| 5       |  4 |  -4 |
| 6       |  5 |  -4 |
| 7       |  5 |  -3 |
| 8       |  6 |  -2 |
| 9       |  6 |  -2 |
| 10      |  6 |  -1 |
| 11      |  5 |  -1 |
| 12      |  5 |   0 |
| 13      |  4 |   0 |

pattern_spread: ±2 px

### MP5 — Forma en "I" con leve desviación derecha
Patrón más predecible del arsenal. Subida vertical controlada, deriva derecha suave a partir del disparo 16. Compensación: tirar suave abajo, casi nada lateral.

| Disparo | dx | dy  |
|---------|----|-----|
| 1       |  0 |   0 |
| 2       |  0 |  -4 |
| 3       |  0 |  -4 |
| 4       |  1 |  -5 |
| 5       |  1 |  -4 |
| 6       |  1 |  -4 |
| 7       |  1 |  -3 |
| 8       |  1 |  -3 |
| 9       |  2 |  -3 |
| 10      |  2 |  -3 |
| 11      |  2 |  -2 |
| 12      |  2 |  -2 |
| 13      |  2 |  -2 |
| 14      |  2 |  -2 |
| 15      |  2 |  -1 |
| 16      |  3 |  -1 |
| 17      |  3 |  -1 |
| 18      |  3 |  -1 |
| 19      |  3 |  -1 |
| 20      |  3 |   0 |
| 21      |  3 |   0 |
| 22      |  3 |   0 |
| 23      |  3 |   0 |
| 24      |  3 |   0 |
| 25      |  3 |  +1 |
| 26      |  3 |  +1 |
| 27      |  3 |  +1 |
| 28      |  2 |  +1 |
| 29      |  2 |   0 |

pattern_spread: ±3 px

### Benelli M4 — Forma en "V invertida"
Patada masiva vertical al segundo disparo, luego cae hacia la derecha. Compensación: tirar fuerte abajo desde el inicio.

| Disparo | dx  | dy   |
|---------|-----|------|
| 1       |  0  |   0  |
| 2       |  0  | -25  |
| 3       |  5  | -20  |
| 4       | 10  | -10  |
| 5       |  8  |  -5  |
| 6       |  5  |   0  |
| 7       |  3  |  +2  |

pattern_spread: ±4 px

### Mk18 — Forma en "J invertida extendida"
Patada vertical fuerte, curva agresiva a la izquierda hasta el disparo 10, luego se aplana. Compensación: tirar abajo-derecha.

| Disparo | dx   | dy   |
|---------|------|------|
| 1       |   0  |   0  |
| 2       |   0  | -14  |
| 3       |   0  | -16  |
| 4       |  -2  | -14  |
| 5       |  -4  | -12  |
| 6       |  -6  | -10  |
| 7       |  -8  |  -8  |
| 8       | -10  |  -6  |
| 9       | -12  |  -4  |
| 10      | -14  |  -2  |
| 11      | -14  |  -1  |
| 12      | -14  |   0  |
| 13      | -13  |   0  |
| 14      | -13  |   0  |
| 15      | -12  |   0  |
| 16      | -12  |  +1  |
| 17      | -11  |  +1  |
| 18      | -11  |  +1  |
| 19      | -10  |  +1  |
| 20      | -10  |  +1  |
| 21      |  -9  |  +1  |
| 22      |  -9  |   0  |
| 23      |  -8  |   0  |
| 24      |  -8  |   0  |
| 25      |  -7  |   0  |
| 26      |  -7  |   0  |
| 27      |  -6  |   0  |
| 28      |  -6  |   0  |
| 29      |  -5  |   0  |

pattern_spread: ±2 px

---

## Resolución del Impacto

Después de aplicar las 3 capas, el punto final se comprueba contra las hitboxes del enemigo.

### Zonas anatómicas

| Zona          | Multiplicador | Efecto especial        |
|---------------|---------------|------------------------|
| CABEZA        | ×2.0          | CRÍTICO / STUN         |
| TORSO         | ×1.0          | DAÑO ESTABLE           |
| BRAZO IZQ/DER | ×0.7          | −PRECISIÓN ENEMIGO     |
| PIERNA IZQ/DER| ×0.6          | −VELOCIDAD ENEMIGO     |

### Multiplicador de precisión dentro de la zona

```
distancia_normalizada = sqrt(dx² + dy²) / sqrt(2)   [donde dx, dy son fracción del radio de la zona]
```

| Distancia | Multiplicador | Label           |
|-----------|---------------|-----------------|
| < 0.2     | ×1.5          | CENTRO PERFECTO |
| < 0.6     | ×1.0          | IMPACTO SÓLIDO  |
| ≥ 0.6     | ×0.75         | BORDE DE ZONA   |

### Fórmula de daño final

```
daño = int(daño_base * flesh_mult * zona_mult * precision_mult * armor_mult)
```

### Modificadores por tipo de munición (9mm)

| Tipo | flesh_mult | vs_chaleco | vs_placas |
|------|-----------|-----------|----------|
| RIP  | ×1.0      | ×0.4      | ×0.2     |
| FMJ  | ×0.8      | ×0.7      | ×0.5     |

RIP destruye carne pero rebota en protección. FMJ penetra mejor pero hace menos daño a carne expuesta.

---

## Integración con el QTE

El jugador fija eje Y (intención vertical) → fija eje X (intención horizontal) → ese punto `(center_x, center_y)` es la entrada a `apply_three_layer_dispersion()`. El punto de impacto final se comprueba contra hitboxes y se calcula el daño.

Ver [[Sistema de Combate en Tiempo Real]] para el flujo completo del QTE.
Ver [[Distractores Visuales]] para cómo el HP degrada visualmente la puntería.
Ver [[Diseño de Combate y Armas]] para el sistema de armadura por capas.

---

Volver a [[Crimson Draft]] | Ver [[GDD]]
