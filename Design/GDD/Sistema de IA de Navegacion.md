---
estado: revision
ultima-revision: 2026-05-24
tags:
  - game-design
---

# Sistema de IA de Navegación

Los enemigos en modo exploración patrullan, detectan al jugador por sonido y vista, y persiguen hasta disparar el combate. El comportamiento de referencia es la trilogía clásica RE1-RE3.

---

## Diseño

### Concepto Central

Los enemigos existen como NPCs físicos dentro de cada habitación activa. No son zonas de colisión estáticas — se mueven, detectan, y persiguen. Cuando atrapan al jugador, cargan la escena de combate exactamente como lo haría un **CombatTrigger**.

La IA está **limitada a la habitación activa**. Solo existe una habitación activa a la vez. Cuando el jugador cambia de habitación, todos los enemigos de la habitación anterior se desactivan con ella y vuelven a su estado inicial en la próxima visita.

No hay estado de pérdida de pista (*LOST*). Una vez en persecución, el enemigo sigue la posición actual del jugador hasta que este abandone el cuarto.

> El único escape dentro de una habitación es la geometría y la puerta. No hay mechanic de escondite.

---

## Diseño — Máquina de Estados

Tres estados posibles por tipo de enemigo. Los estados mapean directamente al enum `GuardAlertState` existente.

```
PATROL
  → recorrer waypoints a velocidad de patrulla
  → evaluar detección cada frame
  → si detecta + suspiciousEnabled activo: → SUSPICIOUS
  → si detecta + suspiciousEnabled inactivo: → ALERT

SUSPICIOUS (opcional — guardias entrenados)
  → detenerse, rotar hacia la fuente de detección
  → esperar suspiciousDuration segundos
  → si detecta de nuevo antes del timeout: → ALERT
  → si el timer llega a 0 sin confirmación: → PATROL

ALERT
  → seguir posición actual del jugador mediante NavMesh
  → velocidad = chaseSpeed
  → si distancia < catchRadius: → disparar combate
```

**Regla de persecución:** antes de transicionar a ALERT, el agente verifica que existe un camino válido de NavMesh hasta el jugador. Si no existe ruta completa, permanece en PATROL.

**`suspiciousEnabled`** está desactivado por defecto. Los infectados/mutados van directamente a ALERT. Los guardias militares lo activan.

---

## Diseño — Sistema de Detección

El sensor evalúa tres mecanismos en orden. Basta que uno detecte para que el agente reaccione.

### 1. Proximidad (siempre activa)

Radio de contacto físico. Omnidireccional — no depende de vista ni sonido.

Usa **histeresis** para evitar flickering en el borde de detección:

| Condición | Resultado |
|---|---|
| Distancia < `detectRadius` | Detectado |
| Distancia > `undetectRadius` | Pierde detección por proximidad |
| `detectRadius` ≤ distancia ≤ `undetectRadius` | Mantiene estado anterior |

### 2. Sonido (radio variable por velocidad del jugador)

El sensor lee `Rigidbody.linearVelocity.magnitude` del jugador y selecciona el radio activo:

| Velocidad del jugador | Radio de detección |
|---|---|
| < `playerDeadzone` (≈ 0.1 u/s) | Sin detección por sonido — jugador quieto |
| `playerDeadzone` a `playerRunThreshold` (≈ 5.5 u/s) | `walkSoundRadius` — caminando |
| > `playerRunThreshold` | `runSoundRadius` — corriendo |

Referencia de velocidades del jugador: caminar = 4 u/s, correr = 7 u/s (ver [[Sistema de Movimiento]]).

### 3. Vista (cono + raycast de dos pasos)

El enemigo solo ve lo que está dentro de su cono frontal y tiene línea de visión libre.

**Condición previa:** distancia < `visualRange` y ángulo al jugador < `visualFov / 2`.

```
Paso 1: Raycast desde eyePoint hacia el jugador con máscara obstructionMask
        → si impacta: línea de visión bloqueada → no detecta por vista

Paso 2: Raycast desde eyePoint hacia el jugador con máscara targetMask
        → si impacta: jugador visible → detectado
```

Si `eyePoint` no está asignado, se usa `transform.position`.

---

## Diseño — Parámetros por Tipo de Enemigo

Todos los valores viven en el ScriptableObject **NavigationEnemyData**, uno por tipo de enemigo.

### Valores por defecto (infectado base)

| Grupo | Parámetro | Valor | Descripción |
|---|---|---|---|
| **Combate** | `encounterId` | — | Qué encuentro se carga al atrapar al jugador |
| **Movimiento** | `patrolSpeed` | 2.0 u/s | Velocidad de patrulla |
| | `chaseSpeed` | 3.5 u/s | Velocidad de persecución |
| | `waypointStopDistance` | 0.3 u | Distancia para considerar un waypoint alcanzado |
| | `catchRadius` | 0.8 u | Distancia que dispara el combate |
| **Proximidad** | `detectRadius` | 1.8 u | Radio de contacto (detección garantizada) |
| | `undetectRadius` | 2.4 u | Radio exterior para perder detección por proximidad |
| **Sonido** | `playerDeadzone` | 0.1 u/s | Velocidad mínima para producir sonido |
| | `playerRunThreshold` | 5.5 u/s | Umbral walk → run |
| | `walkSoundRadius` | 3.5 u | Radio de detección caminando |
| | `runSoundRadius` | 9.0 u | Radio de detección corriendo |
| **Vista** | `visualRange` | 7.0 u | Rango máximo de visión |
| | `visualFov` | 110° | Campo de visión frontal |
| | `obstructionMask` | — | Capas que bloquean la vista |
| | `targetMask` | — | Capa del jugador |
| **Suspicious** | `suspiciousEnabled` | `false` | Activar para guardias entrenados |
| | `suspiciousDuration` | 2.0 s | Tiempo de espera antes de volver a PATROL |

---

## Diseño — Relación con el Sistema de Cuartos

Los enemigos son **hijos del GameObject del cuarto** que habitan. No requieren lógica de gestión externa:

- Cuando `RoomController.Deactivate()` apaga el cuarto, todos los hijos (incluidos los agentes) se desactivan.
- Cuando el cuarto se activa nuevamente, los agentes reinician en PATROL.
- Los enemigos derrotados (desactivados tras victoria en combate) permanecen desactivados entre visitas — comportamiento fiel a RE clásico.

```
Room_X (RoomController)
├── Enemy_Infected_01 (EnemyNavAgent + sensor + NavMeshAgent)
└── PatrolPoints (EnemyPatrolPath con waypoints hijos)
```

**Pre-requisito de escena:** el NavMesh debe estar bakeado con todos los cuartos activos (Window → AI → Navigation → Bake). Los pisos se marcan como NavMesh Static.

---

## Intención

El modo navegación debe generar tensión constante sin hacer la exploración injugable. El jugador necesita aprender que puede elegir entre avanzar con cuidado o arriesgar la velocidad.

> Caminar es seguro pero lento. Correr llega antes pero anuncia tu posición a toda la sala.

La diferencia entre `walkSoundRadius` (3.5 u) y `runSoundRadius` (9.0 u) es la mecánica central de tensión en exploración. No es un número arbitrario — es la distancia que separa "maniobrable" de "situación comprometida".

El enemigo no necesita ser invencible para generar miedo. Necesita ser **predecible pero inexorable**: el jugador entiende cómo funciona la detección, sabe que el enemigo lo perseguirá sin parar, y aún así siente que el cuarto es un espacio hostil.

> La IA de RE clásico no era sofisticada. Era consistente. El jugador aprendía las reglas y las usaba. Eso es exactamente lo que este sistema replica.

La histeresis en proximidad y el check de reachability evitan las situaciones injustas donde el enemigo detecta o persigue al jugador a través de geometría de forma inesperada.

---

## Pendiente

- [ ] Valores definitivos de radios de sonido (requieren playtest en cuartos reales)
- [ ] Definir layerMasks de obstruction y target en el proyecto Unity
- [ ] Configuración de NavigationEnemyData por tipo de enemigo (infectado base, guardia MSRT, mutado)
- [ ] Animaciones de patrol, alert y grab del enemigo en navegación
- [ ] Sonido de alerta al detectar al jugador (audio feedback)
- [ ] Reset de estado al re-entrar en cuarto (si se decide implementar respawn de enemigos)

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Movimiento]] | Ver [[Sistema de Combate en Tiempo Real]] | Ver [[Acto I - Diseño Detallado]]
