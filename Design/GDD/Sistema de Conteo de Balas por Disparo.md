---
estado: borrador
ultima-revision: 2026-03-04
tags:
  - game-design
---

# Sistema de Conteo de Balas por Disparo

Define el flujo de `Shoot` con selección de cantidad de balas antes del QTE, y la resolución multi-bala con cálculo de impacto, daño y feedback por bala.

---

## Diseño

### Objetivo del sistema

Permitir que el jugador decida cuántas balas gastar en un disparo y traducir esa decisión en una resolución visible y medible dentro del mismo ciclo de combate.

### Flujo de `Shoot`

Secuencia obligatoria del comando:

```
OperatorSelection
  -> CommandPanel (Shoot)
  -> BulletCountPanel
  -> TargetSelection
  -> QTE (AimView)
  -> Resolución multi-bala en secuencia corta
  -> Resultado final de daño
```

`Shoot` ya no entra directo a target/QTE. Siempre pasa por el panel contador.

### Panel de conteo de balas

| Variable | Regla |
|---|---|
| `min_shot_bullets` | 1 |
| `max_shot_bullets` | 6 |
| `max_disponible` | `min(6, balas_en_cargador)` |
| `valor_inicial` | 1 |

Input del jugador dentro del panel:

| Input | Acción |
|---|---|
| Izquierda | `selected_bullets = max(1, selected_bullets - 1)` |
| Derecha | `selected_bullets = min(max_disponible, selected_bullets + 1)` |
| Confirm | Acepta cantidad y avanza a `TargetSelection` |
| Cancel | Cierra panel y vuelve a `CommandPanel` sin disparar |

Si `balas_en_cargador == 0`, `Shoot` no avanza a panel/QTE.

### Resolución multi-bala tras QTE

Después de confirmar el QTE y calcular la bala 1:

- Se generan `N = selected_bullets` trayectorias de bala.
- Bala 1 usa la dispersión estándar (random dentro de `dispersionRadius`).
- Bala `i` (desde 2) usa offset acumulado en Y respecto a la bala 1.

| Bala | Posición |
|---|---|
| 1 | `(x1, y1)` (random estándar) |
| 2 | `(x1, y1 + 5)` |
| 3 | `(x1, y1 + 10)` |
| ... | `(x1, y1 + 5 * (i - 1))` |

Cada bala resuelve en forma independiente:
1. `ShotZone`
2. daño por zona
3. `ShotMarker`
4. `feedback text`

### Daño por bala y daño total

Daño por bala:

```
damage_i = base_damage * zone_multiplier(ShotZone_i)
```

Daño total del disparo:

```
damage_total = Σ damage_i   para i en [1..N]
```

El daño aplicado al enemigo objetivo es `damage_total`.

### Secuencia visual de balas y feedback

Las balas no aparecen simultáneamente. Se presentan en secuencia corta, incluyendo la primera.

| Parámetro | Valor MVP |
|---|---|
| `bullet_sequence_delay` | 0.03 s |
| Orden | Bala 1 -> Bala 2 -> ... -> Bala N |

Por cada paso de la secuencia aparece el par visual del disparo:
- `ShotMarker` de esa bala
- `feedback text` de esa bala

### Casos borde

| Caso | Resultado esperado |
|---|---|
| `balas_en_cargador = 0` al accionar `Shoot` | No abre panel contador |
| `max_disponible = 1` | Panel permite solo valor 1 |
| Bala fuera de silueta | `ShotZone = Miss`, daño 0, feedback de fallo |
| Enemigo muere antes de terminar secuencia | El daño restante no se aplica; la secuencia visual restante se omite |

### Relación con otros sistemas

- Extiende el flujo de [[Sistema de Combate en Tiempo Real]].
- Usa detección por bala de [[Sistema de Detección de Impacto]].
- Genera feedback por bala en [[Sistema de Feedback de Daño de Disparo]].

---

## Intención

> El jugador no solo apunta: también decide cuántos recursos quemar en un solo instante de presión.

El contador previo al QTE agrega una decisión táctica concreta: conservar munición o forzar daño alto inmediato. No es un menú cosmético, es un riesgo explícito.

La resolución por bala mantiene legibilidad causal. Cada bala deja evidencia visual propia (marker + texto), evitando que el jugador perciba el disparo como un número abstracto agregado sin explicación.

La secuencia corta de impactos refuerza ritmo y lectura: se ve una ráfaga controlada, no un bloque instantáneo difícil de interpretar.

---

## Pendiente

- [ ] Definir feedback cuando el cargador está en 0 al presionar `Shoot`
- [ ] Definir si el panel contador muestra también munición restante (`N / cargador`)
- [ ] Ajustar `bullet_sequence_delay` con playtest de legibilidad

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Combate en Tiempo Real]] | Ver [[Sistema de Detección de Impacto]] | Ver [[Sistema de Feedback de Daño de Disparo]]
