---
estado: borrador
ultima-revision: 2026-05-18
tags:
  - game-design
---

# Sistema ATB de Combate

El combate usa Active Time Battle (ATB) en modo Wait, inspirado en Chrono Trigger. Todos los actores — operadores y enemigos — comparten el mismo modelo de gauge. Las acciones se serializan a través de una cola global FIFO. Los timers se pausan cuando el jugador navega submenús profundos.

Reemplaza al [[Sistema de Combate en Tiempo Real]] y al [[Sistema de Ataque de Enemigos]] como documento de referencia del timing de combate.

---

## Diseño

### Principio central: todos los actores son iguales

Operadores y enemigos son **actores ATB**. Cada uno tiene un gauge que se llena pasivamente. Cuando el gauge llega al 100%, el actor está listo para actuar. La lógica de qué pasa al actuar es diferente, pero el mecanismo de tiempo es idéntico para todos.

> Inspiración directa: el motor de Chrono Trigger trackea el turn timer de cada unidad de la misma manera que un estado de Veneno o Haste — misma arquitectura, distinta acción al dispararse.

---

### ATBActorState — datos por actor

| Variable | Tipo | Descripción |
|---|---|---|
| `gauge` | float | Progreso de 0.0 a 1.0 |
| `gauge_per_second` | float | Tasa de llenado |
| `kind` | enum | `Operator` \| `Enemy` |
| `slot_index` | int | Slot en roster o battlefield |
| `is_dead` | bool | Si es true no participa del sistema |

`is_ready` equivale a `gauge >= 1.0`.

El gauge se congela en 1.0 hasta que el actor actúa. Al actuar, resetea a 0.0 e inicia el siguiente ciclo.

---

### Cálculo de gauge_per_second

**Operadores:**

```
gauge_per_second = speed / 100.0
```

`speed` es un stat entero (1–99) definido en los datos del operador. Un operador con `speed = 80` llena su gauge en 1.25 segundos. Con `speed = 40`, en 2.5 segundos.

**Enemigos:**

```
gauge_per_second = 1.0 / attack_base_sec
```

El jitter se recalcula en cada reset del gauge:

```
gauge_per_second = 1.0 / max(0.1, attack_base_sec + rand(-attack_jitter_sec, +attack_jitter_sec))
```

---

### CombatOrchestrator — el loop central

Un único orquestador ejecuta el siguiente ciclo cada frame:

```
cada frame:
  si timers_pausados → saltar paso 1

  1. avanzar gauge de todos los actores vivos en deltaTime * gauge_per_second
     (clampear a 1.0)

  2. para cada enemigo con is_ready == true:
       target = operador_vivo_al_azar()
       encolar EnemyAttack(slot, target, damage) en CombatActionQueue
       resetear gauge del enemigo con nuevo jitter

  3. si animation_lock_until > now → saltar paso 4

  4. si CombatActionQueue no está vacía:
       procesar cabezal (ver sección de procesamiento)
```

El orquestador no maneja input. Cuando necesita que el jugador configure una acción, delega en `CombatMenuController` y espera la respuesta.

---

### Speed — stat de operadores

Campo nuevo en los datos del operador:

| Variable | Tipo | Rango | Default |
|---|---|---|---|
| `speed` | int | 1–99 | 50 |

Este campo no existía en iteraciones anteriores. Los valores por facción y rol se definen en la sección Pendiente.

---

### CombatActionQueue — cola global FIFO

Una sola cola ordenada por tiempo de llegada. No hay prioridad por tipo de actor — FIFO estricto. Operadores y enemigos comparten la misma cola.

#### Estructura de PendingAction

| Campo | Tipo | Descripción |
|---|---|---|
| `kind` | enum | `Operator` \| `Enemy` |
| `slot_index` | int | Quién actúa |
| `type` | enum | Tipo de acción |
| `payload` | union | Datos específicos por tipo |

#### Tipos de acción

| Tipo | Payload al encolar | Configuración en cabezal |
|------|-------------------|--------------------------|
| `Shoot` | vacío | balas → target → aim |
| `Reload` | `ammo_box_index` | ninguna — ejecuta directo |
| `UseItem` | `item_index` | ninguna — ejecuta directo |
| `Defend` | vacío | ninguna — ejecuta directo |
| `EnemyAttack` | `target_slot` + `damage` | ninguna — ejecuta directo |

`Shoot` es el único tipo cuyo payload se completa en el cabezal, no al encolar. El jugador ya comprometió el turno del operador al encolar; la configuración ocurre bajo la presión del combate activo.

#### Cuándo se encola cada acción

| Acción | Se encola cuando | Quién encola |
|--------|-----------------|--------------|
| `Shoot` | Jugador selecciona Shoot en CommandPanel | `CombatMenuController` |
| `Reload` | Jugador confirma ammo en submenú | `CombatMenuController` |
| `UseItem` | Jugador confirma ítem en submenú | `CombatMenuController` |
| `Defend` | Jugador selecciona Defend | `CombatMenuController` |
| `EnemyAttack` | Orquestador detecta enemigo READY | `CombatOrchestrator` |

---

### Procesamiento del cabezal

```
cabezal = CombatActionQueue.Peek()

si cabezal.type == Shoot:
  → activar Wait mode
  → CombatMenuController.ConfigureShoot(slot)
  → jugador elige cantidad de balas → target → aim → resolución
  → Dequeue()
  → animation_lock_until = now + shoot_lock_duration

si cabezal.type == Reload:
  → aplicar recarga con payload.ammo_box_index
  → Dequeue()
  → animation_lock_until = now + reload_lock_duration

si cabezal.type == UseItem:
  → aplicar ítem con payload.item_index
  → Dequeue()
  → animation_lock_until = now + use_item_lock_duration

si cabezal.type == Defend:
  → aplicar estado Defend al operador
  → Dequeue()
  → animation_lock_until = now + defend_lock_duration

si cabezal.type == EnemyAttack:
  → aplicar payload.damage al payload.target_slot
  → disparar feedback visual (vibración enemigo, texto flotante, flash ECG)
  → Dequeue()
  → animation_lock_until = now + payload.attack_duration_sec
```

---

### Animation lock

Garantiza que **nunca se ejecutan dos acciones simultáneamente**.

Cada acción al ejecutarse setea `animation_lock_until = now + duración`. Mientras el lock esté activo, el paso 4 del loop se salta — el cabezal espera.

Duración del lock por tipo de acción (valores a ajustar en balance):

| Tipo | Duración base |
|------|--------------|
| `Shoot` | duración del QTE de apuntado |
| `Reload` | `attack_duration_sec` del arma |
| `UseItem` | 0.5s (fijo, a confirmar) |
| `Defend` | 0.3s (fijo, a confirmar) |
| `EnemyAttack` | `attack_duration_sec` del enemigo |

---

### Wait Mode — cuándo se pausan los timers

| Situación | Timers |
|-----------|--------|
| Navegando entre operadores READY | Corriendo |
| CommandPanel abierto (eligiendo tipo de comando) | Corriendo |
| Submenú de Reload abierto (eligiendo ammo) | **Pausados** |
| Submenú de Items abierto (eligiendo ítem) | **Pausados** |
| Shoot en cabezal: configuración activa (balas, target, aim) | **Pausados** |
| Ejecución de cabezal (animation lock activo) | Corriendo |

La pausa aplica a **todos** los actores sin excepción: operadores y enemigos.

---

### Flujo completo — operador actúa

```
1. Gauge del operador llega a 1.0 → queda congelado en READY
2. Jugador navega al operador (puede haber varios READY simultáneamente)
3. CombatMenuController abre CommandPanel para ese operador
4. Jugador elige comando:
   ├─ Shoot  → encola PendingAction(Shoot, vacío) → gauge resetea
   ├─ Reload → abre submenú ammo (Wait mode ON)
   │            → jugador elige ammo
   │            → encola PendingAction(Reload, ammo_index) → gauge resetea
   ├─ Items  → abre submenú ítems (Wait mode ON)
   │            → jugador elige ítem
   │            → encola PendingAction(UseItem, item_index) → gauge resetea
   └─ Defend → encola PendingAction(Defend) → gauge resetea
```

El gauge siempre resetea **después** de encolar, no antes.

---

### Flujo completo — enemigo actúa

```
1. Gauge del enemigo llega a 1.0
2. CombatOrchestrator elige target vivo al azar
3. Encola EnemyAttack(slot, target, damage)
4. Gauge del enemigo resetea con nuevo jitter
5. Cuando llega al cabezal: aplica daño, feedback, animation lock
```

---

### Panel de debug

Panel de UI activo solo en `UNITY_EDITOR` o build con flag `DEBUG_COMBAT`. Solo lee — no modifica el estado de combate.

**Bloque actores ATB**

```
[OPERADORES]
Slot 0  Cruz      SPD:65  ████████░░  80%  FILLING
Slot 1  Mora      SPD:40  ██████████ 100%  READY

[ENEMIGOS]
Slot 0  Guardia A  0.33/s  ███░░░░░░░  30%  FILLING
Slot 1  Guardia B  0.50/s  ██████████ 100%  READY → encolando...
```

**Bloque cola global**

```
[COLA]  lock: 0.42s restantes
  [0] ► EnemyAttack  Slot1 → OpSlot0  dmg:25    ← CABEZAL
  [1]   Shoot        OpSlot1           sin config
  [2]   Reload       OpSlot0           ammo#2
```

**Bloque timeline**

```
Timeline  RUNNING   Wait mode: OFF
Animation lock: ON  resta: 0.42s
```

---

## Intención

El ATB en modo Wait define el **ritmo de presión** del combate. Los gauges avanzan mientras el jugador piensa. Cada segundo de deliberación es tiempo real cedido a los enemigos.

> El jugador no juega contra el reloj. Juega contra su propia velocidad de decisión.

El modo Wait es el equilibrio entre tensión y legibilidad: elegir el ítem correcto o el ammo adecuado tiene un costo real aunque el jugador no lo vea en ese momento — los gauges avanzaron.

La distinción entre encolar Shoot y configurarlo impone una **decisión de compromiso**: al encolar, el operador ya gastó su turno. La pregunta de cuántas balas y a quién llega cuando el combate sigue corriendo.

> CT es "pienso → actúo". Crimson Draft es "me comprometo → pienso bajo presión → actúo".

El panel de debug expone todo lo que el jugador final no verá. Es la herramienta para diseñar, balancear y ajustar `speed`, duraciones de lock y `attack_base_sec` hasta que el ritmo se sienta correcto.

---

## Pendiente

- [ ] Definir duraciones de animation lock por tipo de acción (Reload, UseItem, Defend)
- [ ] Definir valores de `speed` por facción y rol de operador
- [ ] Definir qué pasa si un operador muere mientras tiene acciones en la cola
- [ ] Validar que CommandPanel con timers corriendo no genera confusión de percepción con Wait mode
- [ ] Diseñar indicador visual de gauge para operadores (no como número — como feedback sutil)
- [ ] Definir si la cola tiene tamaño máximo o es ilimitada
- [ ] Actualizar [[Sistema de Combate en Tiempo Real]] para reflejar que este doc es el de referencia
- [ ] Actualizar [[Sistema de Ataque de Enemigos]] para referenciar el ATBActorState unificado

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Combate en Tiempo Real]] | Ver [[Sistema de Ataque de Enemigos]] | Ver [[Sistema de Salud]] | Ver [[Sistema ECG de Operadores]]
