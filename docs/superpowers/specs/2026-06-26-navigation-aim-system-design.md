# Navigation Aim System — Design Spec

**Date:** 2026-06-26  
**Status:** Approved  
**Scope:** Navigation — player aiming, target selection, and first-strike ATB advantage

---

## Overview

The player can enter an Aim mode during navigation by holding the Aim button (X). While aiming the player cannot move; they auto-rotate toward the nearest enemy and can cycle between visible enemies with A/D. Pressing Fire (C) launches a raycast: if it hits the selected enemy with clear line of sight, combat begins immediately and all operators start with a full ATB gauge. Releasing Aim returns the player to normal movement.

This is implemented as a companion `PlayerAimController` MonoBehaviour on the same GameObject as `PlayerController`, keeping movement logic isolated and making a future state-machine refactor straightforward.

---

## Input Changes

| Action | Map | Binding | Status |
|--------|-----|---------|--------|
| `Aim` | Gameplay | X key / gamepad button | Already defined — add binding |
| `AimFire` | Gameplay | C key / gamepad button | **New action** |

`Move` (WASD / left stick) remains in the Gameplay map and is reused for A/D target cycling while aiming.

`IInputService` and `InputService` gain:
- `InputAction AimFire { get; }` wired from `gameplayMap["AimFire"]`

---

## Components

### `PlayerController` — minimal changes

- Gains `bool IsAiming { get; private set; }` and `internal void SetAiming(bool value)`.
- `FixedUpdate`: if `IsAiming` is true, sets `rb.linearVelocity = Vector3.zero`, sets animator `Speed` to 0, and returns early — no movement processed.
- `SetAiming` also sets an `IsAiming` bool parameter on the `Animator` to trigger the aiming animation blend.

### `PlayerAimController` — new MonoBehaviour

Lives on the same GameObject as `PlayerController`. Registered in `NavigationScope`.

**Injected via `[Inject] Construct(...)`:**
- `IInputService`
- `ISceneTransitionService`
- `IEncounterContext`

**Resolved in `Start()`:**
- `PlayerController` — via `GetComponent<PlayerController>()` on same GameObject

**Serialized fields:**
- `float aimTurnSpeed = 180f` — degrees per second for rotation toward target
- `float aimRange = 20f` — maximum raycast distance
- `LayerMask obstaclesMask` — geometry that blocks the shot
- `LayerMask enemyMask` — colliders on enemy GameObjects

---

## Target Management

On entering Aim mode, `PlayerAimController` collects all active `EnemyNavAgent` instances in the scene (those whose `GameObject.activeSelf` is true and whose `NavMeshAgent` is enabled). The list is sorted by distance to the player (ascending). The current target index starts at 0.

**Cycling with A/D:**  
Reads `inputService.Move.ReadValue<Vector2>().x` each frame. Uses edge detection with a 0.3 s cooldown: when the axis crosses ±0.5 from zero, it advances the index by +1 (D) or −1 (A). The list is circular (wraps at both ends).

The target list is rebuilt each time Aim is entered. If the list is empty, aim mode is still entered but Fire has no effect.

---

## Rotation

While aiming, every frame `PlayerAimController` rotates `transform` toward the current target using:

```
direction = (target.transform.position - player.transform.position).XZ().normalized
targetRotation = Quaternion.LookRotation(direction)
transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, aimTurnSpeed * Time.deltaTime)
```

Only the Y axis is affected (XZ plane, same convention as `EnemyNavAgent.UpdateSuspicious`).

---

## Shoot & Combat Trigger

When `AimFire` is pressed (`WasPressedThisFrame`) and a target exists:

1. Compute ray origin: `player.transform.position + Vector3.up * 0.8f`, direction: `player.transform.forward`.
2. `Physics.Raycast(origin, forward, out hit, aimRange, obstaclesMask | enemyMask)`.
3. If `hit.collider` belongs to the current target's GameObject (check via `hit.collider.GetComponentInParent<EnemyNavAgent>() == currentTarget`):
   - Call `encounterContext.SetAdvantage(true)`.
   - Call `sceneTransitionService.StartCombatAsync(currentTarget.EncounterId, currentTarget.EncounterData)`.
4. Otherwise (geometry hit first, or no hit): no effect.

`EnemyNavAgent` already exposes `EncounterId` (public property) and the `encounterData` field must be made `internal` or a public property added (`EncounterData` getter).

---

## ATB Advantage — First Strike

### `ISceneTransitionService`

`StartCombatAsync` gains an optional parameter:
```
UniTask StartCombatAsync(string encounterId, ScriptableObject? encounterAsset = null, bool operatorsStartFull = false);
```

`PlayerAimController` passes `operatorsStartFull: true`. All other callers (`EnemyNavAgent`, `CombatTrigger`) use the default `false`. This eliminates any timing ambiguity — the flag arrives alongside the encounter data in a single call.

### `IEncounterContext` / `EncounterContext`

`IEncounterContext` gains:
```
bool OperatorsStartFull { get; }
```

`EncounterContext.Set()` gains the third parameter `bool operatorsStartFull = false` and stores it. `SceneTransitionService.StartCombatAsync` forwards the value when calling `encounterContext.Set(id, asset, operatorsStartFull)`. The flag is overwritten on every new encounter, preventing bleed-through.

### `ATBActorState`

Gains:
```
public void FillGauge() { this.Gauge = 1f; }
```

### `ATBSystem`

Gains:
```
public void FillOperatorGauges()
```
Iterates `actors` where `Config.Kind == ATBActorKind.Operator` and calls `FillGauge()` on each.

### `CombatOrchestrator.Initialize()`

After `atbSystem.Initialize(configs)`, if `encounterContext.OperatorsStartFull`:
```
atbSystem.FillOperatorGauges();
```

On the next `Update`, `NotifyReadyOperators` detects all operators with `Gauge >= 1f` and immediately undims them, making them available to act.

---

## Animator

The player `Animator` controller must have a `bool` parameter named `IsAiming`. The aiming animation blend is driven by this parameter. The value is set by `PlayerController.SetAiming`.

---

## Scope Boundaries

- `PlayerAimController` does **not** disable `EnemyNavAgent` scripts or modify enemy state.
- The dialogue pause system (`DialogueActiveChangedEvent`) is orthogonal — aiming is not blocked by dialogue since dialogue already blocks all gameplay input via `SwitchToDialogue()`.
- If `sceneTransitionService.IsInCombat` is true when Fire is pressed, the shoot is silently ignored (guard already exists in the transition service).

---

## Edge Cases

- **Lista vacía:** Si no hay enemigos activos al entrar en Aim, el modo igual entra pero Fire no hace nada.
- **Enemigo desactivado mientras se apunta:** Si `currentTarget.gameObject.activeSelf` se vuelve false (ej. enemigo derrotado mientras se apunta), `PlayerAimController` reconstruye la lista en el siguiente frame y selecciona el próximo candidato. Si no hay más, el target queda null y Fire no hace nada.
- **Combate ya activo:** Si `sceneTransitionService.IsInCombat` es true cuando se presiona Fire, el disparo se ignora silenciosamente.
- **Ciclo de targets:** El jugador puede ciclar a un enemigo sin LOS (detrás de una pared). El auto-aim rota hacia él igualmente. Solo el disparo falla (el raycast impacta la geometría primero).

---

## Files Affected

| File | Change |
|------|--------|
| `Navigation/Player/PlayerAimController.cs` | New |
| `Navigation/Player/PlayerController.cs` | `IsAiming` prop, movement block, animator param |
| `Navigation/NavigationScope.cs` | Register `PlayerAimController` |
| `Infrastructure/Input/IInputService.cs` | `AimFire` action |
| `Infrastructure/Input/InputService.cs` | Wire `AimFire` from Gameplay map |
| `Infrastructure/Scenes/ISceneTransitionService.cs` | `operatorsStartFull` param on `StartCombatAsync` |
| `Infrastructure/Scenes/SceneTransitionService.cs` | Forward param to `EncounterContext.Set()` |
| `Infrastructure/Scenes/IEncounterContext.cs` | `OperatorsStartFull` property |
| `Infrastructure/Scenes/EncounterContext.cs` | `operatorsStartFull` param on `Set()` |
| `Combat/ATBActorState.cs` | `FillGauge()` |
| `Combat/ATBSystem.cs` | `FillOperatorGauges()` |
| `Combat/CombatOrchestrator.cs` | Call `FillOperatorGauges` when advantage flag set |
| `Navigation/Enemy/EnemyNavAgent.cs` | Expose `EncounterData` as public getter |
| Input Action Asset (editor) | Bind `Aim` to X; add `AimFire` action bound to C |
