---
estado: borrador
ultima-revision: 2026-03-06
tags:
  - game-design
---

# Sistema de Ataque de Enemigos

Define cómo y cuándo atacan los enemigos en combate en tiempo real, incluyendo el scheduler, el lock de ataque y el feedback al operador.

---

## Diseño

### Variables por enemigo (data)

| Variable | Tipo | Descripción |
|---|---|---|
| `attack_base_sec` | float | Cooldown base entre ataques (segundos) |
| `attack_jitter_sec` | float | Variación aleatoria ± sobre el cooldown |
| `attack_duration_sec` | float | Duración del ataque que bloquea al scheduler |
| `attack_damage` | int | Daño directo aplicado al operador |

### Estado runtime por enemigo

| Variable | Tipo | Descripción |
|---|---|---|
| `next_attack_time` | float | Próximo instante en que puede atacar |
| `is_dead` | bool | Si está muerto no participa del scheduler |

### Estado runtime global

| Variable | Tipo | Descripción |
|---|---|---|
| `attack_lock_until` | float | Instante hasta el que el scheduler está bloqueado |

### Scheduler de ataque (reglas)

1. Al iniciar combate: para cada enemigo vivo, `next_attack_time = now + attack_base_sec ± attack_jitter_sec`.
2. Si `now < attack_lock_until`, **nadie puede atacar**.
3. Si el scheduler está libre, se elige al enemigo con **menor** `next_attack_time` entre vivos.
4. Si hay empate de tiempos, se desempata al azar.
5. Al atacar:
   - Se aplica daño **instantáneo** al operador objetivo.
   - Se dispara feedback visual (ver sección de feedback).
   - Se bloquea el scheduler: `attack_lock_until = now + attack_duration_sec`.
   - Se recalcula `next_attack_time = now + attack_base_sec ± attack_jitter_sec` para ese enemigo.

### Selección de objetivo

- El enemigo elige un **operador vivo al azar**.
- Si no hay operadores vivos, el scheduler se detiene.

### Daño a operadores (MVP)

- El ataque enemigo aplica **daño directo** a `hp` del operador según `attack_damage`.
- No hay zonas ni hemorragia en este MVP (se define en iteraciones posteriores).
- La lectura de estado del operador se refleja en el [[Sistema de Salud]] y el [[Sistema ECG de Operadores]].

### Feedback visual y UI

| Feedback | Regla |
|---|---|
| Vibración del enemigo atacante | Tween breve (0.15–0.25s) al iniciar ataque |
| Texto flotante en operador | Aparece `-X` con el daño aplicado |
| ECG del operador | **Flash rojo** breve (0.15–0.20s) en el widget |

### Pseudocódigo del scheduler

```text
if now < attack_lock_until:
  return

attacker = enemigo_vivo_con_menor(next_attack_time)
if attacker.next_attack_time > now:
  return

target = operador_vivo_al_azar()
apply_damage(target, attacker.attack_damage)
play_feedback(attacker, target)
attack_lock_until = now + attacker.attack_duration_sec
attacker.next_attack_time = now + attacker.attack_base_sec + rand(-jitter, +jitter)
```

### Casos borde

| Caso | Resultado esperado |
|---|---|
| Todos los enemigos muertos | Scheduler sin actividad |
| Todos los operadores muertos | Scheduler se detiene |
| Varios enemigos listos durante lock | Se atienden al liberar el lock, por menor `next_attack_time` |

---

## Intención

El ataque enemigo marca el **ritmo de presión** del combate. La regla de un solo atacante activo garantiza legibilidad y un pulso claro, mientras que el jitter evita patrones mecánicos. El feedback es inmediato y contundente: el jugador siente el impacto sin perder control del flujo de decisiones.

> Un ataque enemigo debe sentirse inevitable y clínico, no caótico. El jugador pierde vida, no la claridad.

---

## Pendiente

- [ ] Definir probabilidad de hemorragia y zonas para ataques enemigos.
- [ ] Ajustar valores por tipo de enemigo en data real.
- [ ] Validar timings del lock con la duración de QTEs activos.

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Combate en Tiempo Real]] | Ver [[Sistema de Salud]] | Ver [[Sistema ECG de Operadores]] | Ver [[Sistema de Feedback de Daño de Disparo]]
