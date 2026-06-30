# Enemy Health MVP — Implementation Plan

> [!WARNING]
> **DEPRECADO** — Este plan pertenece al sistema de salud anterior. No usar como referencia de implementación.

**Goal:** Implementar salud de enemigos en combate para que cada disparo resuelto por `ShotZone` aplique daño al objetivo, pueda matarlo (ocultando sprite) y dispare victoria cuando no queden enemigos vivos.

**Architecture:**  
- `EnemyData` pasa a definir `maxHp`.  
- `BattlefieldView` mantiene estado runtime de HP por slot enemigo y expone operaciones de daño a través de `IBattlefieldView`.  
- `CombatMenuController` aplica daño al target seleccionado cuando recibe `OnShotFired(..., zone)` usando `baseDamage` fijo y multiplicadores por zona.  
- Si no quedan enemigos vivos, publica `CombatEndedEvent { Victory = true }`.

**Spec:** Implements [[Sistema de Combate en Tiempo Real#Salud de Enemigos (MVP)]]

**Tech Stack:** Unity 6, C# 9, VContainer, MessagePipe, NUnit EditMode

---

## Task 1 — Add Enemy HP Data

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Data/EnemyData.cs`

### Step 1: Add `maxHp` to `EnemyData`

Add serialized field:

```csharp
[SerializeField] private int maxHp = 100;
public int MaxHp => this.maxHp;
```

### Step 2: Guard invalid values in runtime (not in ScriptableObject setter)

No custom setter in SO. Runtime will clamp (`Mathf.Max(1, enemy.MaxHp)`) when initializing combat state.

### Step 3: Compile check

Expected: no errors.

---

## Task 2 — Extend Battlefield Runtime Enemy State

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/IBattlefieldView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/BattlefieldView.cs`

### Step 1: Extend interface

Add:

```csharp
EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int damage);
bool HasAliveEnemies();
```

### Step 2: Add runtime state structures in `BattlefieldView`

In `BattlefieldView`, store:
- `Dictionary<int, EnemyRuntimeState> enemyStateBySlot`
- `Dictionary<int, GameObject> enemyGoBySlot`

Create local/runtime structs:

```csharp
public readonly struct EnemyDamageResult
{
    public int  SlotIndex { get; }
    public int  DamageApplied { get; }
    public int  RemainingHp { get; }
    public bool IsDead { get; }
}
```

```csharp
private sealed class EnemyRuntimeState
{
    public EnemyData Data = null!;
    public int CurrentHp;
    public bool IsDead;
}
```

### Step 3: Initialize state in `Populate(EncounterData encounter)`

For each non-null enemy slot:
- Instantiate sprite as today.
- Initialize runtime state:
  - `CurrentHp = Mathf.Max(1, enemy.MaxHp)`
  - `IsDead = false`
- Register slot in both dictionaries.

Clear both dictionaries at start of `Populate`.

### Step 4: Implement `ApplyDamageToEnemy`

Rules:
- Invalid slot, missing state, already dead => no-op result.
- Clamp damage to `>= 0`.
- Apply HP subtraction.
- If HP reaches 0:
  - mark dead,
  - hide sprite GameObject (`SetActive(false)`),
  - remove slot from `occupiedEnemySlots`.

### Step 5: Implement `HasAliveEnemies()`

Return `occupiedEnemySlots.Length > 0`.

### Step 6: Compile check

Expected: no errors.

---

## Task 3 — Wire Damage in CombatMenuController

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`

### Step 1: Add constants and target tracking

Add fields:

```csharp
private const int BaseDamage = 20;
private int currentTargetSlot = -1;
```

### Step 2: Capture target slot in `ConfirmTarget()`

Before showing Aim:
- `currentTargetSlot = occupiedEnemySlots[enemyTargetCursor];`
- Keep existing `ConfigureHitMask(...)`.

### Step 3: Add damage resolver helper

Add internal static helper:

```csharp
internal static int ComputeShotDamage(ShotZone zone)
{
    float multiplier = zone switch
    {
        ShotZone.Head  => 2.0f,
        ShotZone.Torso => 1.0f,
        ShotZone.Arms  => 0.7f,
        ShotZone.Legs  => 0.8f,
        ShotZone.Hit   => 1.0f,
        _              => 0.0f,
    };
    return Mathf.RoundToInt(BaseDamage * multiplier);
}
```

### Step 4: Apply damage in `HandleShotFired`

Flow in handler:
1. If `currentTargetSlot >= 0`:
   - `damage = ComputeShotDamage(zone)`
   - `result = battlefieldView.ApplyDamageToEnemy(currentTargetSlot, damage)`
2. If `!battlefieldView.HasAliveEnemies()`:
   - `combatEndedPublisher.Publish(new CombatEndedEvent { Victory = true });`
3. Reset `currentTargetSlot = -1`.
4. Keep existing UI state reset (`Hide Aim`, `Hide CommandPanel`, return to `OperatorSelection`).

Debug logging only in editor:

```csharp
#if UNITY_EDITOR
Debug.Log($"[Combat] Enemy slot={currentTargetSlot} zone={zone} damage={damage} hp={result.RemainingHp} dead={result.IsDead}");
#endif
```

### Step 5: Compile check

Expected: no errors.

---

## Task 4 — Update Tests (EditMode)

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/AimViewControllerTests.cs` (no new feature logic here; keep existing tests intact)

### Step 1: Update test fakes for new battlefield methods

`FakeBattlefieldView`:
- implement `ApplyDamageToEnemy`
- implement `HasAliveEnemies`
- add simple per-slot HP map for test assertions.

`FakePublisher`:
- keep `Published` and add `CombatEndedEvent? LastEvent`.

### Step 2: Add tests for damage multipliers

In `CombatMenuControllerTests`, add:
- `ComputeShotDamage_head_returns40`
- `ComputeShotDamage_torso_returns20`
- `ComputeShotDamage_arms_returns14`
- `ComputeShotDamage_legs_returns16`
- `ComputeShotDamage_miss_returns0`

### Step 3: Add flow tests

Add:
1. `ShotFired_appliesDamageToSelectedEnemy`
2. `ShotFired_whenEnemyHpReachesZero_marksEnemyDead`
3. `ShotFired_whenAllEnemiesDead_publishesVictoryTrue`

Use reflection call to `ConfirmTarget()` (as in existing private-method tests) or drive via state transitions.

### Step 4: Run EditMode tests

Run all EditMode tests.
Expected:
- existing tests still pass,
- new damage/victory tests pass.

---

## Task 5 — Unity Editor Manual Wiring

**Files (assets):**
- Enemy data assets under `Game/CrimsonDraft/Assets/Art/Data/*.asset`

### Step 1: Set `maxHp` in enemy assets

For each enemy asset:
- assign initial `maxHp` (recommendación MVP: 100 para todos).

### Step 2: Smoke test in Play Mode

Scenario:
1. Enter combat with at least 1 enemy.
2. Shoot repeatedly with known target.
3. Verify enemy disappears when HP reaches 0.
4. Kill all enemies and verify combat exits with victory.

---

## Acceptance Criteria

1. Enemies have runtime HP initialized from `EnemyData.MaxHp`.
2. `ShotZone` drives damage using fixed `baseDamage=20` and defined multipliers.
3. Dead enemies are hidden (no corpse asset).
4. No alive enemies => `CombatEndedEvent.Victory == true`.
5. Debug logs for enemy damage are editor-only (`#if UNITY_EDITOR`).
6. EditMode tests pass, including new damage/victory coverage.

---

## Commit Strategy

1. `feat(combat): add enemy max HP and battlefield runtime enemy damage state`
2. `feat(combat): apply shot-zone damage to target and trigger victory on enemy wipe`
3. `test(combat): add enemy health/damage/victory editmode coverage`

