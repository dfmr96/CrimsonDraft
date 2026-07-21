# Enemy Poise — Design Spec

**Date:** 2026-07-20
**Status:** Approved
**Scope:** Combat — hidden Poise counter per enemy, weapon-driven Poise damage, and a direct knockdown/stagger consequence wired into the ATB turn queue. Ref: GDD §5.g, `Design/References/GD_RE2_Combate.md` §1.7/2.6.

---

## Overview

Every enemy currently has a fixed `MaxHp` (`EnemyData.maxHp`) and no notion of a "next action being interruptible." This spec adds a second, hidden resource — **Poise** — tracked per enemy slot alongside HP in `BattlefieldView`. Each bullet that lands drains Poise by an amount defined per weapon. If Poise reaches 0 while the enemy's HP is still above a per-enemy-type threshold, the counter silently resets (the enemy is "too healthy" to be staggered yet). If Poise reaches 0 **and** HP is below that threshold, the enemy is knocked down immediately: its ATB gauge is reset, any of its actions already queued are discarded when they reach the head of the queue, and it stays down for a fixed duration before recovering.

This is deliberately a **direct knockdown**, not RE2's two-step "wobble, needs a follow-up hit to complete" — see "Deviations from RE2" below.

Explicitly out of scope for this pass: Rip ammo's Poise bonus (no ammo-type system exists yet), and the knocked-down silhouette changing which zones a burst's remaining shots can reach (GDD §5.g's recoil-remap note). Both require systems that don't exist in code yet and are left as future work.

---

## Data additions

### `WeaponData`

```csharp
[SerializeField, Min(0)] private int poiseDamage = 10;
public int PoiseDamage => this.poiseDamage;
```

Set by hand per weapon, independent of HP damage — same principle as RE2's fixed per-weapon Poise values (knife 9, pistol 15, Colt 20, Burst 35).

### `IWeaponSlot` / `WeaponItem`

`AimingState` reads the active weapon through `OperatorRuntime.ActiveWeapon`, typed `IWeaponSlot?` — not `WeaponItem` directly — so the interface needs the new property too:

```csharp
// IWeaponSlot.cs
public interface IWeaponSlot
{
    Caliber Caliber    { get; }
    GunType GunType    { get; }
    int     BaseDamage { get; }
    int     CurrentAmmo { get; }
    int     MaxAmmo     { get; }
    int     PoiseDamage { get; } // new
    void    SetAmmo(int value);
}
```

`WeaponItem` implements it by delegating to `Data`:

```csharp
public int PoiseDamage => this.Data.PoiseDamage;
```

### `EnemyData`

```csharp
[SerializeField, Min(0)] private int   minPoise               = 15;
[SerializeField, Min(0)] private int   maxPoise               = 30;
[SerializeField, Range(0f, 100f)] private float staggerHpThresholdPct = 40f;
[SerializeField, Min(0f)] private float staggerDurationSec    = 2.5f;

public int   MinPoise               => this.minPoise;
public int   MaxPoise               => this.maxPoise;
public float StaggerHpThresholdPct  => this.staggerHpThresholdPct;
public float StaggerDurationSec     => this.staggerDurationSec;
```

`minPoise`/`maxPoise` mirror the random-pool shape GDD §5.f already describes for HP/speed (not yet implemented for those stats, but the same idea). `staggerHpThresholdPct` replaces RE2's hardcoded "83 HP" with a percentage, configurable per enemy type instead of a single magic number for all enemies.

### Legs multiplier

Not a data field — a fixed constant, since it applies uniformly for this pass:

```csharp
private const int LegsPoiseMultiplier = 2;
```

---

## `BattlefieldView` — Poise state and stagger transition

`EnemyRuntimeState` gains:

```csharp
public int   CurrentPoise;
public int   InitialPoise; // the roll this reset returns to — not EnemyData.MaxPoise
public bool  IsStaggered;
public float StaggerEndsAt;
```

### Initial roll (`Populate()`)

In the same loop that builds each `EnemyRuntimeState`, roll `CurrentPoise` (and store it as `InitialPoise`) from `[enemy.MinPoise, enemy.MaxPoise]` using a locally-owned `IRandomSource` (`new UnityRandomSource()`), matching the pattern `CombatOrchestrator` already uses rather than introducing new DI registration. `InitialPoise` is what a silent reset returns to — the specific value rolled for *this* enemy this encounter, not the pool's upper bound (`MaxPoise` is only the range boundary used for rolling, same distinction RE2 makes between its 15–31 roll range and the value actually assigned to a given zombie).

### `ApplyDamageToEnemy` signature change

```csharp
EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage);
```

Logic added after the existing HP-damage/death handling, only when the enemy survives the hit:

```csharp
state.CurrentPoise -= poiseDamage;
bool justStaggered = false;
if (state.CurrentPoise <= 0)
{
    float hpPct = state.MaxHp > 0 ? (float)state.CurrentHp / state.MaxHp * 100f : 0f;
    if (hpPct < enemy.StaggerHpThresholdPct)
    {
        state.IsStaggered   = true;
        state.StaggerEndsAt = Time.time + enemy.StaggerDurationSec;
        justStaggered       = true;
        if (this.enemyAnimatorBySlot.TryGetValue(slotIndex, out var anim) && anim != null)
            anim.SetBool(IsStaggeredHash, true);
    }
    else
    {
        state.CurrentPoise = state.InitialPoise; // silent reset — enemy too healthy to stagger yet
    }
}
```

`EnemyRuntimeState` needs a reference to its source `EnemyData` (or at least `MaxPoise`/thresholds) to reset/compare against — stored alongside `CurrentHp`/`MaxHp` at `Populate()` time, same lifetime as those fields.

### `EnemyDamageResult` gains a field

```csharp
public bool IsStaggered { get; } // true only on the hit that causes the stagger transition
```

This is what `AimingState` reads to know it must notify the orchestrator (below) — it is **not** the enemy's current staggered state (that's `IsEnemyStaggered(slotIndex)`), only a one-shot "this hit caused it" flag.

### Recovery (`Update()`)

`BattlefieldView` gains an `Update()` that, for every slot with `IsStaggered == true`, checks `Time.time >= state.StaggerEndsAt` and if so clears the flag and sets the Animator bool back to `false`. No trigger is used — per your note, the enemy spends real time on the ground and a bool driving stand-up/fall-down transitions is the correct shape, not a one-shot trigger.

### `IBattlefieldView` additions

```csharp
bool IsEnemyStaggered(int slotIndex);
```

Read-only query used by `CombatOrchestrator` (below). Returns `false` for unknown/dead slots.

---

## Poise damage per shot

New static helper next to `CombatMenuController.ComputeShotDamage`:

```csharp
internal static int ComputePoiseDamage(ShotZone zone, int weaponPoiseDamage) =>
    zone == ShotZone.Legs ? weaponPoiseDamage * LegsPoiseMultiplier : weaponPoiseDamage;
```

`AimingState.HandleShotsResolved` computes this alongside the existing `totalDamage` sum:

```csharp
int totalDamage = 0;
int totalPoiseDamage = 0;
int weaponPoise = this.roster.Count > this.context.SelectedOperator
    ? this.roster[this.context.SelectedOperator].ActiveWeapon?.PoiseDamage ?? 0
    : 0;

foreach (var shot in shots)
{
    totalDamage += Mathf.Max(0, shot.Damage);
    if (shot.Zone != ShotZone.Miss)
        totalPoiseDamage += CombatMenuController.ComputePoiseDamage(shot.Zone, weaponPoise);
}

if (this.context.CurrentTargetSlot >= 0)
{
    var result = this.battlefieldView.ApplyDamageToEnemy(
        this.context.CurrentTargetSlot, totalDamage, totalPoiseDamage);

    if (result.IsStaggered)
        this.context.Orchestrator.NotifyEnemyStaggered(this.context.CurrentTargetSlot);
    ...
}
```

`ActiveWeapon` is already read further down in the existing method (for `SetAmmo`), so this doesn't introduce a new dependency — just reads it earlier too.

---

## Wiring the stagger into the ATB turn queue (`CombatOrchestrator`)

`ICombatOrchestrator` gains:

```csharp
void NotifyEnemyStaggered(int enemySlot);
```

`CombatOrchestrator` implements it as the only place that touches `ATBSystem` directly (existing convention — `BattlefieldView` never reaches into `ATBSystem`):

```csharp
public void NotifyEnemyStaggered(int enemySlot) =>
    this.atbSystem.ResetActor(enemySlot, ATBActorKind.Enemy);
```

Two existing methods gain a staggered-enemy check:

**`EnqueueReadyEnemyAttacks()`** — don't let a staggered enemy queue a new attack even if its gauge happens to be full:

```csharp
for (int i = 0; i < this.encounter.EnemySlots.Length; i++)
{
    EnemyData? data = this.encounter.EnemySlots[i];
    if (data == null) continue;
    if (this.battlefieldView.IsEnemyStaggered(i)) continue; // new

    ATBActorState? actor = this.atbSystem.GetActor(i, ATBActorKind.Enemy);
    ...
```

**`ProcessQueueHead()`**, `EnemyAttack` branch — if a previously-queued attack reaches the head of the queue while its owner is now staggered, discard it instead of applying it (per your explicit call: "si una acción encolada entra en el cabezal de la cola, se ignora si está en stagger"):

```csharp
if (head.Type == PendingActionType.EnemyAttack)
{
    if (!this.enemyAttackInProgress)
    {
        if (IsActorDead(head)) { this.actionQueue.Dequeue(); return; }
        if (this.battlefieldView.IsEnemyStaggered(head.SlotIndex)) { this.actionQueue.Dequeue(); return; } // new
        if (Time.time < this.animationLockUntil) return;
        this.enemyAttackInProgress = true;
        ApplyEnemyAttack(head);
    }
    ...
```

No change to `IsActorDead`, `ApplyEnemyAttack`, or animation-lock timing beyond this.

---

## Animator

`Enemy_Combat_Controller.controller` gains a new **bool** parameter, `IsStaggered` (not a trigger — confirmed this needs to hold state while the enemy is down, driving its own fall/stand transitions). Code only sets/clears the parameter; wiring the actual states/transitions using your knockdown clip is done by you afterward in the editor, same as the existing `Hit1`/`Hit2` pattern where code and animator work were separate passes.

```csharp
private static readonly int IsStaggeredHash = Animator.StringToHash("IsStaggered");
```

---

## Test changes

- **New pure test**: `CombatMenuController.ComputePoiseDamage` — legs zone doubles, all other zones (including `Miss`, which the caller already filters out before calling it) pass through unchanged.
- **`FakeBattlefieldView`** (`CombatMenuControllerTests.cs`): `ApplyDamageToEnemy` signature updated to take `poiseDamage`; add `IsEnemyStaggered(int)` returning a settable per-slot bool so tests can force a staggered state; `ApplyDamageToEnemy` gains a settable "next result is staggered" flag so tests can simulate the transition without modeling the full Poise math in the fake.
- **`FakeOrchestrator`**: add `NotifyEnemyStaggeredCallCount` / `LastStaggeredSlot`, mirroring the existing `NotifyShootCompletedCallCount` pattern.
- **`FakeOperatorRoster.FakeWeaponSlot`** (same test file): gains `PoiseDamage` to satisfy the extended `IWeaponSlot`, defaulted to a value the new tests can assert against (e.g. `10`).
- **New test** in `CombatMenuControllerTests.cs`: firing a shot where `FakeBattlefieldView.ApplyDamageToEnemy` is set to return `IsStaggered = true` asserts `FakeOrchestrator.NotifyEnemyStaggeredCallCount == 1` and `LastStaggeredSlot` matches the target.
- `CombatOrchestrator` itself has no dedicated test file today (MonoBehaviour, `Update()`-coupled to `ATBSystem`/`BattlefieldView`) — consistent with the existing project pattern, no new tests are added directly against it. The `EnqueueReadyEnemyAttacks`/`ProcessQueueHead` staggered-skip logic is exercised only indirectly (manually, in Play Mode) for this pass.

---

## Deviations from RE2 (confirmed, not oversights)

- **No wobble/second-hit window.** RE2: reaching 0 Poise below the HP threshold puts the zombie in a stagger state that a *second* hit must land on within a window to complete the knockdown; if it doesn't land, Poise resets and the zombie "recovers its balance." Crimson Draft: reaching 0 Poise below the threshold knocks the enemy down immediately, no intermediate window. Confirmed as the intended simplification for this pass.
- **No "doesn't recover after first stagger" exception.** RE2 exempts pre-police-station zombies from Poise reset after a failed wobble. Not carried over — no equivalent enemy-tier distinction in Crimson Draft yet.
- **Legs multiplier and (deferred) Rip bonus** are Crimson Draft additions with no RE2 equivalent (RE2's zombies don't have a leg zone or ammo types).
- **Stagger duration as a tunable value** (`staggerDurationSec`) doesn't exist as a designed number in RE2 — there, "how long the zombie is down" falls out of its own animation/AI state, not a balance field. Here it's an explicit per-enemy-type value because Poise must interact with the ATB gauge instead of an action-based AI loop.

---

## Out of scope (deferred)

- Rip ammo's Poise bonus (GDD §5.g) — no ammo-type system exists in code yet.
- Knocked-down silhouette changing which zones a burst's remaining shots can land on (GDD §5.g) — depends on the aim/recoil pipeline in a way that needs its own design pass.
- Wiring `Enemy_Combat_Controller`'s actual knockdown state/transitions/clip — code only drives the `IsStaggered` bool; the Animator Controller work is done separately by you.
- Any change to the HP pool system (GDD §5.f) — `EnemyData.MaxHp` stays a single fixed value; only Poise gets the random-range treatment in this pass.
