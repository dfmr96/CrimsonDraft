# Research Report: Chrono Trigger ATB — Active vs Wait

_Date: 2026-05-18_

---

## Executive Summary

Chrono Trigger usa el sistema **Active Time Battle (ATB)** creado por Hiroyuki Ito para Square. Es un híbrido entre tiempo real y por turnos: cada personaje y enemigo tiene un gauge que se llena con el tiempo, y cuando está lleno puede actuar. La distinción **Active / Wait** controla si los timers se pausan cuando el jugador navega submenús.

---

## 1. Cómo funciona el ATB gauge

### Fórmula de llenado

```
max_atb = (1 + battle_speed) × (25 - char_speed)
```

- **battle_speed**: setting global del juego (0–7), inversamente proporcional a la velocidad — mayor valor = batalla más rápida.
- **char_speed**: stat del personaje (varía por personaje y nivel).
- `max_atb` = número de **frames** que tarda el gauge en llenarse.

Cuanto mayor es `char_speed` o menor es `max_atb`, más rápido actúa ese personaje/enemigo.

### El update loop

Cada frame el motor hace:
1. Incrementa `tickCounter`.
2. Llama a `draw()`.
3. Limpia el registro de daño.
4. **Chequea condiciones de Wait** — si está en modo Wait y hay un menú abierto, solo se ejecutan acciones de PC ya encoladas; los status timers NO avanzan.
5. Decrementa cada timer de status activo (veneno, haste, etc.).
6. Actualiza los valores del gauge ATB de cada unidad.

> El ATB de cada unidad se trackea igual que cualquier status (Veneno, Haste) — misma arquitectura, diferente acción al dispararse.

El motor trackea **13 status diferentes** por unidad en combate.

---

## 2. Active vs Wait — diferencia exacta

### Active mode

- Los timers de **todos** los actores (PCs y enemigos) corren **siempre**, incluso mientras el jugador navega cualquier menú.
- El jugador siente presión constante — los enemigos atacan aunque estés eligiendo un ítem.
- Emula tiempo real puro.

### Wait mode

- Los timers se **congelan** únicamente cuando el jugador está dentro de un **submenú profundo** (lista de Techs, lista de Items, selección de objetivo).
- En el menú principal de comando (el primer nivel), los timers **siguen corriendo** — los enemigos aún atacan mientras el jugador decide si usar "Attack", "Tech" o "Item".
- Una vez que entras al submenú (ej: eligiendo qué tech usar), todo se pausa.

```
Active:  [Main menu] → TIMERS CORREN  |  [Sub menu] → TIMERS CORREN
Wait:    [Main menu] → TIMERS CORREN  |  [Sub menu] → TIMERS PAUSADOS
```

**Conclusión práctica:** Wait no es "pausa total". La diferencia solo importa una vez que ya navegaste al submenú. El momento de presión en el menú principal es idéntico en ambos modos.

---

## 3. ATB Penalty por usar Techs

Después de ejecutar una tech:

```
max_atb = max_atb + (max_atb × atb_pen) / 10
```

Esto incrementa `max_atb` en `atb_pen × 10%` — el gauge siguiente tarda **más** en llenarse.

- `atb_pen` se deriva del **Tech ID** (cada tech tiene un penalty hardcodeado).
- Techs más poderosas tienen mayor penalty.
- Atacar con el comando básico tiene penalty = 0.

Esto introduce un trade-off real: usar techs fuertes te deja vulnerable por más tiempo.

---

## 4. Diseño de juego — por qué importa

### Active: diseño para presión
- Recompensa al jugador rápido.
- Cada segundo que tardas en decidir = un ataque enemigo más.
- Apto para jugadores que buscan tensión táctica.

### Wait: diseño para reflexión
- Permite leer descripciones de items/techs sin penalización.
- Nuevos jugadores aprenden el sistema sin castigo de tiempo.
- Aun así mantiene presión en el menú raíz — no es "pause menu".

### La diferencia como dial de dificultad
Cambiar Active/Wait es efectivamente el ajuste de dificultad más directo del juego — no hay setting de dificultad formal.

---

## 5. Implicaciones para CrimsonDraft

El sistema actual de CrimsonDraft ya usa un modelo parecido al **Active puro**:
- `EnemyAttackScheduler` corre en `Update()` sin pausa.
- Los timers enemigos corren mientras el jugador navega todos los menús.

Para implementar un **Wait mode equivalente**:
- Se necesitaría una flag `isPlayerInSubMenu` que pause `EnemyAttackScheduler.TryScheduleAttack()` (o no avance los timers internos).
- La decisión de dónde pausar (solo submenús vs menú raíz también) replicaría exactamente la distinción Active/Wait de Chrono Trigger.

---

---

## 6. Cola de ataques — serialización (una acción a la vez)

### El mecanismo central: Animation Lock

CT garantiza que nunca se ejecuten dos acciones simultáneamente mediante un **animation lock global**:

- Cuando una acción empieza a ejecutarse (animación de ataque, tech, ítem), el motor **congela los timers** de todos los demás actores durante la animación.
- Los timers ATB solo retoman una vez que la animación actual termina completamente.
- Esto convierte el sistema de "tiempo continuo" en una cadena secuencial de eventos en el momento de la ejecución.

```
[Gauge lleno] → [Acción entra a la cola] → [Lock activo?]
                                                  ↓ sí → espera
                                                  ↓ no → ejecuta + activa lock
                                            [Animación termina] → lock liberado
                                            [Siguiente acción de la cola]
```

### Qué pasa cuando múltiples gauges se llenan al mismo tiempo

Cuando dos o más actores tienen el gauge lleno simultáneamente:
- Sus acciones entran a una **lista ordenada** (queue).
- La queue es simple — no hay prioridad sofisticada.
- Se ejecutan una por una, en el orden en que ingresaron.

**Bug documentado (CT original):** si múltiples acciones resuelven muy juntas, el orden de la lista puede "scramblearse" (corromperse), retrasando el acceso al menú y desordenando los turnos. Esto fue suficientemente grave como para generar un ROM hack correctivo.

### Input buffering del jugador

Chrono Trigger tiene **input buffering** para el jugador:
- Si el jugador da un comando mientras una animación está en progreso, el comando **se guarda** — no se descarta.
- Cuando el lock se libera, el comando encolado se ejecuta inmediatamente.
- Esto preserva la responsividad del jugador sin romper la serialización.

### Dual queue: enemigos vs jugadores

El motor mantiene dos flujos paralelos que se serializan en ejecución:

| Flujo | Cómo se agenda | Cómo se serializa |
|-------|---------------|-------------------|
| Jugador | Input del usuario cuando gauge está lleno | Input buffering — espera al lock |
| Enemigo | Timer ATB determina cuándo actúa | Encola automáticamente cuando gauge llena |
| Ejecución | — | Un solo lock global — alternancia natural |

### Techs combinadas (doble/triple)

Las techs combinadas requieren que **múltiples gauges estén llenos al mismo tiempo**. El motor usa comandos de sincronización explícitos en los scripts de animación:

```
23 ss  → waits for subsection ss (sync entre objetos)
24 ss  → waits for section ss (sync entre objetos)
```

Esto significa que los scripts de animación tienen su propio mini-lenguaje secuencial con puntos de sincronización, garantizando que las partes de una tech combinada se coordinen sin solaparse.

---

## 7. Implicaciones para CrimsonDraft — cola de ataques

### Lo que ya existe

`EnemyAttackScheduler` ya implementa el concepto de lock:

```csharp
attackLockUntil = Time.time + result.LockDuration;
// TryScheduleAttack retorna false si Time.time < attackLockUntil
```

Esto es equivalente al animation lock de CT: ningún enemigo puede atacar mientras otro ya está en animación.

### Lo que falta

1. **No hay cola** — si dos enemigos tienen el gauge lleno al mismo tiempo, el sistema actual elige uno al azar y el otro **pierde su turno** hasta la próxima oportunidad natural.
2. **No hay input buffering** — si el jugador actúa durante un ataque enemigo, la acción se procesa inmediatamente (el lock enemigo no bloquea al jugador).
3. **No hay serialización jugador↔enemigo** — ambos pueden "actuar" en el mismo frame en teoría.

### Opción simple para serializar (sin cola completa)

Si se quiere garantizar que nunca se superpongan visualmente:
- Agregar una flag global `bool isBattleActionPlaying`.
- Cuando empieza cualquier acción (jugador o enemigo) → `isBattleActionPlaying = true`.
- `EnemyAttackController` chequea esta flag además del lock propio.
- Al terminar la animación → `isBattleActionPlaying = false`.

Esto replica el animation lock global de CT sin necesidad de una cola FIFO completa.

---

## Sources

- [GameFAQs — Active vs Wait mode Q&A](https://gamefaqs.gamespot.com/snes/563538-chrono-trigger/answers/354553-whats-the-difference-between-wait-and-active-battle-modes)
- [Chrono Compendium — Active Time Battle Code and Delay](https://www.chronocompendium.com/Term/Active_Time_Battle_Code_and_Delay.html)
- [GameFAQs — Speed/ATB Mechanics thread](https://gamefaqs.gamespot.com/boards/563538-chrono-trigger/64502272)
- [Final Fantasy Wiki — Active Time Battle](https://finalfantasy.fandom.com/wiki/Active_Time_Battle)
- [RomHack Plaza — Chrono Trigger ATB Fixes](https://romhackplaza.org/romhacks/chrono-trigger-atb-fixes-snes/)
- [Data Crystal — CT Tech and Attack Documentation](https://datacrystal.tcrf.net/wiki/Chrono_Trigger_(SNES)/Tech_and_Attack_Documentation)
