# Distractores Visuales

## Concepto

Los distractores visuales no son estética — son penalizaciones mecánicas que degradan la capacidad del jugador de leer el campo y ejecutar el QTE. Se activan progresivamente según el HP del operador activo. A mayor daño recibido, más canales de distracción se activan simultáneamente.

La clave de diseño: el jugador **nunca pierde el control**, pero ejercerlo se vuelve progresivamente más difícil.

---

## Variable central: `damage_ratio`

```
damage_ratio = 1.0 - hp_ratio    [rango: 0.0 a 1.0]
```

Todos los distractores escalan con esta variable. A HP 100% → damage_ratio = 0 → sin distractores.

---

## Heartbeat — Base de todos los distractores

El heartbeat no es un distractor visual por sí mismo, sino el **reloj base** del que dependen los demás. Su patrón sincroniza los picos de distracción con el ritmo cardíaco.

### Frecuencia

```
BPM = 60 + 100 * damage_ratio    [rango: 60–160 BPM]
```

| HP (%) | BPM |
|--------|-----|
| 100%   |  60 |
|  75%   |  85 |
|  50%   | 110 |
|  25%   | 135 |
|  10%   | 150 |

### Patrón de un latido (normalizado a 1 ciclo)

| Phase range | Evento     | Intensidad máx |
|-------------|-----------|----------------|
| 0.00–0.06   | Lub sube  | 0.0 → 1.0      |
| 0.06–0.12   | Lub baja  | 1.0 → 0.0      |
| 0.12–0.18   | Silencio  | 0.0            |
| 0.18–0.23   | Dub sube  | 0.0 → 0.6      |
| 0.23–0.28   | Dub baja  | 0.6 → 0.0      |
| 0.28–1.00   | Silencio  | 0.0            |

`intensity` = valor de la curva anterior en el instante actual.

### Umbral de activación

El heartbeat está activo cuando `damage_ratio > 0.05` (HP < 95%).

---

## Vibración de barras QTE

La posición de las barras QTE (horizontal y vertical) oscila con el latido.

```
vibración_offset = 14 * damage_ratio * intensity    [px]
```

| HP (%)  | Amplitud máxima (en pico lub) |
|---------|------------------------------|
| 100%    | 0 px (inactivo)              |
|  50%    | 7 px                         |
|   0%    | 14 px                        |

El offset se aplica en el eje perpendicular al movimiento de cada barra.

**Umbral:** activo cuando `damage_ratio > 0.05`

---

## Screen Shake

La grilla completa del QTE sacude sincrónicamente con el latido.

```
shake = 8 * damage_ratio * intensity    [px, aleatorio en X e Y cada frame]
```

| HP (%)  | Shake máximo (en pico lub) |
|---------|---------------------------|
| 100%    | 0 px (inactivo)            |
|  50%    | 4 px                       |
|   0%    | 8 px                       |

**Umbral:** activo cuando `damage_ratio > 0.1` (HP < 90%) **y** `intensity > 0.01`

---

## Viñeta de Sangre

Overlay rojo semitransparente desde los 4 bordes de la grilla. Pulsa al ritmo del heartbeat.

```
depth  = int(40 * damage_ratio)                [px desde cada borde]
alpha  = int(180 * damage_ratio * pulse)
pulse  = 0.7 + 0.3 * intensity                 [oscila entre 0.7 y 1.0]
```

Implementada como 4 franjas desde cada borde con alpha decreciente hacia el centro (4 strips, alpha proporcional a `(1 - i/4)` donde i es el índice de franja desde el borde).

| HP (%)  | Profundidad | Alpha base (pulse min) | Alpha en pico (pulse max) |
|---------|-------------|------------------------|--------------------------|
| 100%    | 0 px        | —                      | —                        |
|  85%    | 6 px        | 18                     | 27                       |
|  50%    | 20 px       | 63                     | 90                       |
|   0%    | 40 px       | 126                    | 180                      |

**Umbral:** activo cuando `damage_ratio > 0.15` (HP < 85%)

---

## Ruido Estático

Píxeles aleatorios parpadeantes sobre la grilla. Simulan interferencia visual.

```
densidad = int(150 * damage_ratio)    [cantidad de píxeles 3×3]
alpha    = int(100 * damage_ratio)    [valor base; cada píxel varía ±50%]
```

| HP (%)  | Píxeles de ruido |
|---------|-----------------|
| 100%    | 0 (inactivo)    |
|  75%    | 0 (inactivo)    |
|  50%    | 75              |
|  25%    | 112             |
|   0%    | 150             |

**Umbral:** activo cuando `damage_ratio > 0.25` (HP < 75%)

---

## Ghost Lines (Visión Doble)

Las líneas de la grilla se dibujan con un offset fantasma oscilante, simulando diplopía.

```
ghost_offset = 5 * damage_ratio    [px máximo de desplazamiento]
osc = ghost_offset * sin(time_ms * 0.003)
desplazamiento = (osc, osc * 0.7)
```

El offset es mayor en X que en Y para simular desenfoque principalmente horizontal.

| HP (%)  | Offset máximo |
|---------|--------------|
| 100%    | 0 px         |
|  50%    | 2.5 px       |
|   0%    | 5 px         |

**Umbral:** la función escala desde cualquier HP, pero el loop de juego aplica `ghost_offset = 0` cuando `damage_ratio < 0.15` (HP > 85%).

---

## Parpadeo de Silueta Enemiga

La silueta del enemigo desaparece intermitentemente, dificultando la lectura de hitboxes.

```
flicker_chance   = 0.15 * (damage_ratio - 0.35) / 0.65
period_ms        = int(800 - 400 * (damage_ratio - 0.35) / 0.65)
invisible_window = int(period_ms * flicker_chance)

silueta_invisible = (time_ms % period_ms) < invisible_window
```

| HP (%)  | Probabilidad | Período | Ventana invisible |
|---------|-------------|---------|-----------------|
| 65%     | ~0%         | 800 ms  | ~0 ms           |
|  50%    | 3.5%        | 707 ms  | 24 ms           |
|  25%    | 9.2%        | 615 ms  | 57 ms           |
|   0%    | 15%         | 400 ms  | 60 ms           |

**Umbral:** activo cuando `damage_ratio > 0.35` (HP < 65%)

---

## Resumen de Umbrales

| Distractor         | Umbral activación | Escala con                        |
|--------------------|-------------------|-----------------------------------|
| Vibración barras   | HP < 95%          | damage_ratio × heartbeat_intensity |
| Screen shake       | HP < 90%          | damage_ratio × heartbeat_intensity |
| Viñeta de sangre   | HP < 85%          | damage_ratio × pulse              |
| Ghost lines        | siempre (suave)   | damage_ratio                      |
| Ruido estático     | HP < 75%          | damage_ratio                      |
| Parpadeo silueta   | HP < 65%          | damage_ratio                      |

A **HP 50%**: vibración + shake + viñeta + ghost lines activos.
A **HP 25%**: todos activos excepto parpadeo de silueta.
A **HP 10%**: todos activos simultáneamente, combinación máxima.

---

## Interacción con Krokonil

Cuando una microdosis de [[Krokonil]] está activa, `damage_ratio` se fuerza a `0.0` para el cálculo de todos los distractores. El jugador ve la grilla perfectamente estable independientemente de su HP real.

Cuando el efecto expira, todos los distractores vuelven de golpe al nivel correspondiente al HP real.

---

Ver [[Sistema de Dispersion y Apuntado]] para cómo el HP afecta el radio de dispersión.
Ver [[Sistema de Salud]] para el modelo de HP y hemorragia.

Volver a [[Crimson Draft]] | Ver [[GDD]]
