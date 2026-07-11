# Enemy Flinch Animation — Design Spec

**Date:** 2026-07-11
**Status:** Approved
**Scope:** Combat — cycling the enemy's two flinch clips in sync with each bullet of the operator's shoot burst

---

## Overview

`Enemy_Combat_Controller.controller` already has two flinch clips, `Armature|Hit_1` and `Armature|Hit_2`, now fully wired: `Hit1`/`Hit2` triggers, `AnyState → Hit_1` / `AnyState → Hit_2` transitions, and `Hit_1 → Idle_1_twitch` / `Hit_2 → Idle_1_twitch` unconditioned exit-time transitions back to the default state. Nothing in code drives these triggers yet.

This spec wires the target enemy's flinch into the same per-bullet loop that already drives the operator's shoot animation (`BattlefieldView.PlayOperatorShootBurstAsync`, built in the [operator shoot burst animation feature](2026-07-08-operator-shoot-burst-animation-design.md)). On each bullet that actually hits (not a `Miss`), the enemy plays the next flinch clip in strict alternation (`Hit_1`, `Hit_2`, `Hit_1`, `Hit_2`, ...), fired at the same moment the operator's shot trigger fires. A `Miss` bullet does not flinch the enemy.

---

## Where the per-shot hit/miss data comes from

`AimingState.HandleShotsResolved(ResolvedShot[] shots)` already receives the full per-bullet results but doesn't retain them past that call — it only aggregates total damage. It gains a field:

```csharp
private ResolvedShot[] pendingShots = Array.Empty<ResolvedShot>();
```

set at the top of `HandleShotsResolved`, so the array is available later when the burst plays (after the player dismisses the aim window).

---

## `BattlefieldView` / `IBattlefieldView`

### Signature change

`PlayOperatorShootBurstAsync`'s job grows from "play the operator's shoot animation `shotCount` times" to "play the operator's shoot animation once per bullet, syncing the target enemy's flinch to each hit." Its signature changes accordingly — `shotCount` is replaced by the full per-bullet data, since the method now needs to know which bullets hit:

```csharp
UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots);
```

`shots.Length` replaces the old `shotCount` parameter for the loop bound. `enemySlotIndex` is `-1` when there was no enemy target (the existing no-enemies path already used by `ShotCountSelectionState`); the method must skip all enemy-flinch logic in that case, exactly like `AimingState.HandleShotsResolved` already skips `ApplyDamageToEnemy` when `CurrentTargetSlot < 0`.

### New per-slot tracking

Mirroring `operatorAnimatorBySlot`:

```csharp
private readonly Dictionary<int, Animator> enemyAnimatorBySlot = new();
private readonly Dictionary<int, bool> enemyHitToggleBySlot = new(); // false = Hit1 next, true = Hit2 next
private static readonly int Hit1Hash = Animator.StringToHash("Hit1");
private static readonly int Hit2Hash = Animator.StringToHash("Hit2");
```

Both populated/cleared in `Populate()`: `enemyAnimatorBySlot` cached via `GetComponentInChildren<Animator>()` in the existing enemy spawn loop (same pattern as the operator loop), `enemyHitToggleBySlot` cleared alongside the other per-slot dictionaries so every new encounter starts each enemy's alternation at `Hit1`.

### Implementation

```csharp
public async UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)
{
    if (!this.operatorAnimatorBySlot.TryGetValue(operatorSlotIndex, out var animator) || animator == null)
        return;

    animator.SetBool(AimHash, true);
    while (!animator.GetCurrentAnimatorStateInfo(0).IsName("AimingIdlePistol"))
        await UniTask.NextFrame();

    int count = Mathf.Max(1, shots.Length);
    for (int i = 0; i < count; i++)
    {
        animator.SetTrigger(ShootHash);

        if (i < shots.Length && shots[i].Zone != ShotZone.Miss)
            this.TriggerEnemyFlinch(enemySlotIndex);

        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("ShootPistolFlexed2"))
            await UniTask.NextFrame();

        var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
        float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
        if (duration > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(duration));
    }

    animator.SetBool(AimHash, false);
}

private void TriggerEnemyFlinch(int enemySlotIndex)
{
    if (enemySlotIndex < 0) return;
    if (!this.enemyAnimatorBySlot.TryGetValue(enemySlotIndex, out var animator) || animator == null) return;

    bool useHit2 = this.enemyHitToggleBySlot.TryGetValue(enemySlotIndex, out var toggle) && toggle;
    animator.SetTrigger(useHit2 ? Hit2Hash : Hit1Hash);
    this.enemyHitToggleBySlot[enemySlotIndex] = !useHit2;
}
```

The enemy trigger fires immediately alongside the operator's `Shoot` trigger for that same bullet — it does not gate the loop's timing. Only the operator's own clip length paces the burst, exactly as before; the enemy's flinch plays independently on its own animator, guaranteed to return to `Idle_1_twitch` on its own via the exit-time transition regardless of how long the operator's burst continues.

If the target enemy has no cached `Animator` (defensive — mirrors the operator's own no-Animator fallback), the flinch is silently skipped; nothing else in the burst is affected.

---

## `AimingState`

`CloseAimAndReturnToOperatorSelectionAsync` (added by the shoot-burst feature) changes its call site to pass the new arguments:

```csharp
await this.battlefieldView.PlayOperatorShootBurstAsync(
    this.context.SelectedOperator,
    this.context.CurrentTargetSlot,
    this.pendingShots);
```

`this.context.CurrentTargetSlot` is still valid at this point — it isn't reset to `-1` until after this `await` returns, exactly matching today's ordering (the same reason the existing code already reads `SelectedOperator`/`SelectedShotCount` before resetting them).

---

## Test fake migration

`FakeBattlefieldView.PlayOperatorShootBurstAsync`'s signature changes to match. The existing `LastBurstShotCount` tracking becomes `LastBurstShots` (the full `ResolvedShot[]` passed in), and a new `LastBurstEnemySlotIndex` property is added. The existing test `ShotFired_extraConfirm_playsOperatorShootBurst_withSelectedOperatorAndShotCount` updates its assertion from `LastBurstShotCount == 3` to `LastBurstShots.Length == 3`, and gains an assertion on `LastBurstEnemySlotIndex` matching the occupied enemy slot used in that test. No other existing test asserts on the old `shotCount`-shaped signature.

---

## Out of scope

- Varying which clip plays based on hit zone (head/torso/etc.) or damage amount — always strict `Hit1`/`Hit2` alternation regardless of where the bullet landed.
- Flinch on `Miss` — explicitly excluded per this spec.
- Any change to `Enemy_Combat_Controller`'s `Attack`/`Attack_2` wiring (already incomplete — no return transition — but unrelated to this feature and not touched here).
- Flinch during the enemy's own attack action (`PlayEnemyAttackFeedback`'s DOTween shake) — untouched, remains a separate visual feedback mechanism for when the enemy attacks the player, not when the enemy is hit.
