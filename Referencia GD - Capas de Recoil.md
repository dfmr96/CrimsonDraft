# Capas de Recoil — Referencia Técnica para GD

---

## Flujo general

El jugador elige un punto de intención en el QTE. Ese punto pasa por tres capas antes de convertirse en el impacto real. Las capas se aplican en orden y cada una parte del resultado de la anterior.

```
Intención del jugador (x, y)
        │
        ▼
┌───────────────────────────┐
│  Capa 1 — Dispersión HP   │  Solo en el primer disparo de cada ráfaga
└───────────────────────────┘
        │
        ▼
┌───────────────────────────┐
│  Capa 2 — Ruido mecánico  │  En todos los disparos
└───────────────────────────┘
        │
        ▼
┌───────────────────────────┐
│  Capa 3 — Patrón recoil   │  Desde el segundo disparo en adelante
└───────────────────────────┘
        │
        ▼
  Punto de impacto final → hitbox → daño
```

El disparo siempre aterriza en algún punto. No existe miss automático por stats.

---

## Capa 1 — Dispersión por HP

### Cuándo actúa
Solo en el **primer disparo** de cada ráfaga. Si el jugador suelta el gatillo y vuelve a disparar, la Capa 1 se aplica de nuevo al primero de la nueva ráfaga.

### Qué hace
Desplaza el punto de intención a una posición aleatoria dentro de un disco. El radio de ese disco crece a medida que el operador recibe daño.

### Fórmula
```
radio = dispersion_base × (1.0 + (1.0 − hp_ratio) × (HP_FACTOR − 1.0))

hp_ratio = hp_actual / hp_maximo          [entre 0.0 y 1.0]

angle = random(0, 2π)
r     = radio × sqrt(random(0, 1))        [distribución uniforme en área]

punto_capa1 = intención + (r·cos(angle), r·sin(angle))
```

> El `sqrt` en el cálculo de `r` es importante: sin él, los impactos se concentrarían en el centro del disco en lugar de distribuirse uniformemente.

### Variables

| Variable | Tipo | Descripción |
|---|---|---|
| `dispersion_base` | px | Radio del disco a HP 100%. Define la precisión base del arma. |
| `HP_FACTOR` | multiplicador | Cuánto crece el radio a HP 0%. Con `2.0`, el radio se duplica al morir. **Global, no por arma.** |

### Comportamiento según `dispersion_base`

| HP del operador | Radio = `dispersion_base × factor` |
|---|---|
| 100% | `dispersion_base × 1.0` |
| 75%  | `dispersion_base × 1.25` |
| 50%  | `dispersion_base × 1.5` |
| 25%  | `dispersion_base × 1.75` |
| 0%   | `dispersion_base × 2.0` |

---

## Capa 2 — Ruido mecánico del arma

### Cuándo actúa
En **todos los disparos**, incluido el primero. Es independiente de ráfaga, HP y recoil.

### Qué hace
Añade un desplazamiento aleatorio uniforme en X y en Y por separado. No acumula entre disparos — cada disparo obtiene su propio ruido.

### Fórmula
```
offset_x = uniform(−weapon_deviation, +weapon_deviation)
offset_y = uniform(−weapon_deviation, +weapon_deviation)

punto_capa2 = punto_capa1 + (offset_x, offset_y)
```

### Variables

| Variable | Tipo | Descripción |
|---|---|---|
| `weapon_deviation` | px | Margen máximo de ruido mecánico en cada eje. Valores altos hacen el arma errática incluso al primer disparo. |

---

## Capa 3 — Patrón de recoil

### Cuándo actúa
Desde el **segundo disparo** en adelante (`consecutive_shots ≥ 2`). El primer disparo de cada ráfaga tiene recoil `(0, 0)`.

### Qué hace
Aplica un desplazamiento predefinido disparo a disparo. La tabla `recoil_pattern` define la "forma" de la curva de recoil del arma. Sobre ese valor exacto se aplica además un margen de aleatoriedad (`pattern_spread`) para que la compensación no sea mecánica.

Al agotar la tabla, el último valor se repite indefinidamente.

### Fórmula
```
(dx, dy) = recoil_pattern[consecutive_shots − 1]

spread_x = dx × hand + uniform(−pattern_spread, +pattern_spread)
spread_y = dy        + uniform(−pattern_spread, +pattern_spread)

punto_capa3 = punto_capa2 + (spread_x, spread_y)
```

`hand = +1` para diestros, `−1` para zurdos. Invierte solo el componente horizontal.

### Variables

| Variable | Tipo | Descripción |
|---|---|---|
| `recoil_pattern[]` | tabla `(dx, dy)` | Desplazamiento acumulado por disparo. Define la forma y magnitud del recoil. |
| `pattern_spread` | px | Aleatoriedad aplicada sobre cada paso del patrón en ambos ejes. A mayor valor, más impredecible la compensación. |

### Cómo leer la tabla

| Valor | Significado |
|---|---|
| `dy` negativo | El impacto sube en pantalla |
| `dy` positivo | El impacto baja en pantalla |
| `dx` positivo | Deriva a la derecha (para diestros) |
| `dx` negativo | Deriva a la izquierda (para diestros) |
| Fila 1 siempre `(0, 0)` | Sin recoil en el primer disparo |

### Ejemplo de tabla (P229)

| Disparo | dx | dy  | Descripción |
|---------|-----|-----|---|
| 1       |  0  |  0  | Sin recoil |
| 2       |  0  | −5  | Sube recto |
| 3       |  2  | −6  | Empieza a derivar derecha |
| 4       |  3  | −5  | Continúa |
| ...     | ... | ... | |
| 13      |  4  |  0  | Se aplana |

El patrón forma una "7": subida vertical seguida de deriva progresiva a la derecha.

---

## Resumen de variables por arma

| Variable | P229 | MP5 | Mk18 | Benelli M4 |
|---|---|---|---|---|
| `dispersion_base` | 12 px | 14 px | 6 px | 40 px |
| `weapon_deviation` | ±2 px | ±2 px | ±1 px | ±3 px |
| `pattern_spread` | ±2 px | ±3 px | ±2 px | ±4 px |
| Pasos en el patrón | 13 | 29 | 29 | 7 |
| Forma | "7" | "I" | "J invertida" | "V invertida" |

---

## Variables globales (no modificables por arma)

| Variable | Valor | Descripción |
|---|---|---|
| `HP_FACTOR` | 2.0 | Ratio máximo de crecimiento de `dispersion_base` al llegar a HP 0%. Cambiarlo afecta todas las armas. |
| Distribución del disco (Capa 1) | Uniforme por área | El `sqrt` garantiza que el centro no sea estadísticamente favorecido. |
| Inversión de `dx` para zurdos | Automática | El sistema lo resuelve con `hand`; el diseñador solo define la tabla para diestros. |
| Repetición al agotar el patrón | Automática | Siempre se usa el último paso de la tabla cuando `consecutive_shots` lo supera. |
