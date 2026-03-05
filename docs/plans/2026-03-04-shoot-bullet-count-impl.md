# Shoot Bullet Count + Multi-Bullet Resolve — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implementar el flujo `Shoot -> contador de balas -> target -> QTE -> resolución multi-bala`, con cálculo de `ShotZone`/daño por bala y feedback secuencial (`ShotMarker` + texto) por cada bala.

**Architecture:**
- `CombatMenuController` agrega un estado intermedio de selección de cantidad de balas y guarda la cantidad elegida para el disparo actual.
- Se introduce una vista dedicada `ShotCountView` (panel pequeño) para mostrar/editar la cantidad con `Left/Right`.
- `IAimView` deja de reportar un único disparo y pasa a reportar una secuencia de disparos resueltos (uno por bala).
- `AimViewController` calcula N disparos (bala 1 random en radio, balas siguientes con `+5` en Y acumulativo), resuelve zona/daño por bala y reproduce visuales en secuencia corta.
- `CombatMenuController` consume la secuencia, aplica daño total al enemigo target y mantiene el cierre de estado existente.

**Spec:** Implements [[Sistema de Conteo de Balas por Disparo]] (`Sistema de Conteo de Balas por Disparo.md`)

**Tech Stack:** Unity 6, C# 9, uGUI + TMP, DOTween, UniTask, NUnit EditMode

---

## Task 1 — Add Shot Count View Contract + Data DTOs

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IShotCountView.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/ShotCountView.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/ResolvedShot.cs`

### Step 1: Crear `ResolvedShot`

Definir DTO inmutable para representar el resultado de cada bala:
- `Index`
- `NormalizedPos`
- `ShotZone`
- `Damage`

### Step 2: Crear `IShotCountView`

Contrato mínimo:
- `void Show(RectTransform commandPanelRect, int initial, int max);`
- `void Hide();`
- `void Increment();`
- `void Decrement();`
- `int Value { get; }`
- `int MaxValue { get; }`

### Step 3: Implementar `ShotCountView`

Comportamiento:
- Renderiza valor actual en `TextMeshProUGUI`.
- `Increment/Decrement` clamp entre `[1..MaxValue]`.
- `Show` posiciona panel relativo al `CommandPanel`.

### Step 4: Compile check

Esperar errores de compilación hasta wiring en `CombatMenuController`/`CombatScope`.

---

## Task 2 — Extend Aim Contract to Multi-Bullet Sequence

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IAimView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/AimViewController.cs`

### Step 1: Cambiar evento de salida

Reemplazar evento único por secuencia:

```csharp
event Action<ResolvedShot[]>? OnShotsResolved;
```

### Step 2: Añadir configuración de cantidad para el disparo actual

Agregar al contrato:

```csharp
void SetShotCount(int shotCount);
```

### Step 3: Implementar cálculo multi-bala en `AimViewController`

En resolución de QTE:
- Bala 1: random dentro de `dispersionRadius` (actual).
- Bala `i >= 2`: usar posición de bala 1 con offset Y acumulado `+5 * (i-1)`.
- Para cada bala:
  - `NormalizeShotLocal`
  - `SampleSilhouette`
  - `ComputeShotDamage(zone)`
  - construir `ResolvedShot`.

### Step 4: Reproducir secuencia visual corta

En `WaitingDismiss`:
- Mostrar `ShotMarker` + `ShowShotFeedback` para cada `ResolvedShot`.
- Delay corto entre balas (MVP: `0.03f`).
- Al finalizar secuencia, invocar `OnShotsResolved(resolvedShots)`.

### Step 5: Exponer helpers testables

Agregar helpers `internal static` para:
- offset de bala por índice (`index -> y + 5 * index`),
- construcción de posición local de bala N a partir de la bala 1.

### Step 6: Compile check

Esperar errores de compilación hasta actualizar consumidores/tests.

---

## Task 3 — Wire Shot Count Flow in CombatMenuController

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CombatScope.cs`

### Step 1: Añadir nuevo estado de menú

Extender enum:
- `ShotCountSelection`

### Step 2: Inyectar `IShotCountView`

Agregar dependencia en constructores y registro en `CombatScope`.

### Step 3: Cambiar flujo de `Shoot`

Antes:
- `Shoot -> TargetSelection`

Después:
- `Shoot -> ShotCountSelection -> TargetSelection`

### Step 4: Control de input en contador

En estado `ShotCountSelection`:
- `Navigate.x < 0` -> `Decrement()`
- `Navigate.x > 0` -> `Increment()`
- `Confirm` -> guardar `selectedShotCount` y avanzar
- `Cancel` -> cerrar panel y volver a `CommandPanel`

### Step 5: Límite por munición disponible (MVP)

Como no existe sistema de cargador aún en runtime de combate, introducir estado temporal local por operador en `CombatMenuController`:
- inicializar cargador por operador en `6`.
- `max_disponible = min(6, ammo[selectedOperator])`.
- al resolver disparo, consumir `selectedShotCount`.
- si `max_disponible == 0`, no abrir contador.

### Step 6: Resolver secuencia de disparos

Cambiar handler:
- de `HandleShotFired(Vector2, ShotZone)`
- a `HandleShotsResolved(ResolvedShot[] shots)`

Regla de daño:
- `damage_total = sum(shots[i].Damage)`
- aplicar una sola vez al target actual.

Mantener:
- lógica de victoria al quedarse sin enemigos,
- cierre de UI al terminar.

---

## Task 4 — Update/Extend EditMode Tests

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/AimViewControllerTests.cs`

### Step 1: Adaptar fakes al nuevo contrato

- `FakeAimView`: reemplazar `OnShotFired` por `OnShotsResolved`.
- Añadir `SetShotCount(int)` y capturar valor para asserts.
- Crear `FakeShotCountView` con `Value`, `MaxValue`, `Increment/Decrement`, `Show/Hide`.

### Step 2: Nuevos tests de flujo del contador

Agregar:
1. `ShootCommand_opensShotCountPanel_beforeTargetSelection`
2. `ShotCount_confirm_advancesToTargetSelection`
3. `ShotCount_cancel_returnsToCommandPanel`
4. `ShotCount_navigate_clampsBetweenOneAndMaxAvailable`

### Step 3: Nuevos tests de daño multi-bala

Agregar:
1. `ShotsResolved_appliesSummedDamageToTarget`
2. `ShotsResolved_withNoDamage_keepsEnemyHp`
3. `ShotsResolved_whenAllEnemiesDead_publishesVictoryTrue`

### Step 4: Tests puros de offset multi-bala en `AimViewControllerTests`

Agregar tests para helper `index -> offsetY`:
- índice 0 => `+0`
- índice 1 => `+5`
- índice 2 => `+10`

### Step 5: Run EditMode tests

Esperar:
- suite completa en verde.

---

## Task 5 — Unity Scene Wiring

**Files (assets):**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Combat.unity`
- Create: `Game/CrimsonDraft/Assets/Prefabs/UI/ShotCountPanel.prefab` (si prefieren prefab)

### Step 1: Crear panel de contador

UI mínima:
- root panel pequeño
- label título (`BULLETS`)
- valor actual (TMP)
- hint de inputs (`< >`, confirm/cancel)

### Step 2: Agregar `ShotCountView` en escena/prefab

Asignar referencias TMP y offset relativo al command panel.

### Step 3: Verificar DI

`CombatScope` debe registrar `ShotCountView` como `IShotCountView`.

### Step 4: Smoke test Play Mode

Casos:
1. `Shoot` abre contador en 1.
2. Left/Right cambia valor, clamp 1..6.
3. Confirm lleva a target + QTE.
4. Tras QTE, aparecen N balas en secuencia corta (incluida la primera) con markers y textos.
5. Daño final equivale a suma por bala.

---

## Acceptance Criteria

1. `Shoot` abre panel de contador antes de target/QTE.
2. Contador funciona con Left/Right, Confirm y Cancel.
3. Rango de selección clamped a `1..min(6, balas_en_cargador)`.
4. Resolución post-QTE calcula N balas:
   - bala 1 random en radio,
   - balas siguientes con `+5` acumulativo en Y.
5. Cada bala resuelve su `ShotZone`, daño, marker y feedback text.
6. Visuales de balas/textos aparecen en secuencia corta, incluida la primera.
7. Daño aplicado al enemigo = suma de daños por bala.
8. EditMode tests actualizados y en verde.

---

## Commit Strategy

1. `feat(combat-ui): add shot count panel and state before target selection`
2. `feat(combat-aim): resolve multi-bullet sequence with per-shot zone and damage`
3. `test(combat): cover shot count flow and multi-bullet damage aggregation`

