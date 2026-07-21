# Focus Fire — Design Spec

**Date:** 2026-07-21
**Status:** Approved
**Scope:** Combat — a new command that lets the player mark several ready operators to fire together against one shared QTE, replacing GDD §5.s's "Synced Shoot" with a grounded name and a concrete mechanism.

---

## Overview

The GDD (§5.s) describes a Chrono-Trigger-style combo: mark several operators while they wait their turn; when any marked operator fires, they all fire together off one shared QTE, letting the player cash in several ATB turns for one concentrated volley. It leaves the exact mechanism as open TODOs (ammo cost, participant limit, how the shared QTE resolves damage per weapon).

This spec resolves those TODOs and renames the feature **Focus Fire** — a real tactical term (concentrating several shooters' fire on one point), consistent with the squad's military framing, and a better fit than the generic "Synced Shoot."

**Core loop:** an operator can be *marked* for Focus Fire instead of acting normally. Marking spends their turn (ATB resets and freezes) but doesn't fire anything yet. The moment an *unmarked* operator picks the normal Shoot command while ≥1 operator is marked, all of them — the shooter plus every marked operator — resolve as one group: each picks their own shot count in turn, then **one shared aim QTE** locks a single position, and each participant's shots land from that same position using their own weapon's recoil/dispersion pattern. To guarantee the group can always actually fire, marking is disabled for whichever operator would otherwise be the last one left able to trigger it.

---

## Data model changes

### `CombatCommand`

```csharp
public enum CombatCommand { Shoot, Items, FocusFire }
```

### `PendingAction` / `PendingActionType`

```csharp
public enum PendingActionType { Shoot, UseItem, EnemyAttack, EnemyRecover, FocusFire }
```

`PendingAction` gains a `int[] FocusFireParticipants` field (empty for every other type), populated by a new factory:

```csharp
public static PendingAction FocusFire(int triggerOperatorSlot, int[] participants) =>
    new PendingAction(PendingActionType.FocusFire, triggerOperatorSlot, focusFireParticipants: participants);
```

`SlotIndex` stays the triggering operator (consistent with how every other action type uses `SlotIndex` as "whose turn is this"); `FocusFireParticipants` is the *full* group including the trigger, in the order each participant will choose their shot count.

### New event

Mirrors `ShootConfigurationRequestedEvent`, but for the whole group:

```csharp
public readonly struct FocusFireConfigurationRequestedEvent
{
    public int[] ParticipantSlots { get; }
    public FocusFireConfigurationRequestedEvent(int[] participantSlots) => this.ParticipantSlots = participantSlots;
}
```

Published from `CombatOrchestrator.ProcessQueueHead()`'s new `FocusFire` branch, the same way `Shoot` publishes `ShootConfigurationRequestedEvent`. `CombatMenuController` subscribes to it alongside the existing `shootSubscriber`.

### `CombatMenuController` shared state

```csharp
internal List<int> FocusFireMarked { get; } = new(); // slots currently marked, in mark order
```

Lives next to `SelectedOperator`/`CurrentTargetSlot` — the other combat-menu states read/mutate it the same way they already read/mutate those.

### `ICombatActionMenuView`

New method so the concrete view can show a distinct "marked" visual (separate from the existing dimmed-because-it's-not-your-turn look):

```csharp
void SetOperatorFocusFireMarked(int index, bool marked);
```

---

## Marking

`CommandPanelState.OnCommandSelected(CombatCommand.FocusFire)`:

1. Add `this.context.SelectedOperator` to `context.FocusFireMarked`.
2. Reset **and freeze** that operator's ATB gauge — a new `ICombatOrchestrator.MarkOperatorForFocusFire(int slot)` method that calls `atbSystem.ResetActor` + `atbSystem.FreezeActor` for that operator (mirrors the existing enemy-stagger freeze in `NotifyEnemyStaggered`). Freezing (not just resetting) is what actually prevents `NotifyReadyOperators()` from ever re-offering them a command panel while marked — `IsReady` requires `Gauge >= 1f`, which a frozen gauge can never reach.
3. `menuView.SetOperatorFocusFireMarked(slot, true)`.
4. Hide the command panel and return to `OperatorSelState`, exactly like a normal command does today.

### The "last available" rule

`CommandPanelState.Enter()` (where the panel is shown for whichever operator's turn it is) computes whether offering Focus Fire would leave nobody able to trigger it, and disables the command via the existing `ICommandPanelView.SetCommandEnabled`:

```csharp
bool wouldExhaustGroup = this.context.FocusFireMarked.Count >= this.roster.GetAliveSlots().Count - 1;
this.commandPanel.SetCommandEnabled(CombatCommand.FocusFire, !wouldExhaustGroup);
```

`GetAliveSlots().Count - 1` is "everyone except the operator currently being offered commands." If marking them would mark literally everyone else too (`markedCount == aliveCount - 1`), Focus Fire is disabled — their only real options become Shoot (which, since others are marked, becomes the trigger) or Items. This check re-runs every time the command panel opens, so it stays correct as operators die mid-encounter.

---

## Triggering

`CommandPanelState.OnCommandSelected(CombatCommand.Shoot)` changes: if `context.FocusFireMarked.Count > 0`, this is a group trigger, not a solo shot.

```csharp
if (command == CombatCommand.Shoot)
{
    if (GetMaxAvailableShotCount() <= 0) return;
    this.sfx?.PlayDecide(this.commandPanel.PanelRect.gameObject);

    if (this.context.FocusFireMarked.Count > 0)
    {
        int[] participants = new int[this.context.FocusFireMarked.Count + 1];
        this.context.FocusFireMarked.CopyTo(participants, 0);
        participants[^1] = this.context.SelectedOperator; // trigger fires last in the sequence
        foreach (int slot in this.context.FocusFireMarked)
            this.menuView.SetOperatorFocusFireMarked(slot, false);
        this.context.FocusFireMarked.Clear();

        this.context.Orchestrator.EnqueueAction(PendingAction.FocusFire(this.context.SelectedOperator, participants));
    }
    else
    {
        this.context.Orchestrator.EnqueueAction(PendingAction.Shoot(this.context.SelectedOperator));
    }

    this.commandPanel.Hide();
    this.menuView.SetDimmed(false);
    this.context.TransitionTo(this.context.OperatorSelState);
    return;
}
```

`EnqueueAction` already resets (and optionally freezes) the *trigger* operator's ATB the same way it does for a solo Shoot — no special-casing needed there. The already-marked participants' gauges were reset+frozen back when they were marked; `CombatOrchestrator`'s `FocusFire` branch (below) unfreezes all of them once the group action is actually dequeued.

---

## Orchestrator: queue handling

`CombatOrchestrator.IsActorDead` and `ProcessQueueHead` both need a `FocusFire` case. Dead-check: a `FocusFire` action is an *operator* action (unlike `EnemyAttack`/`EnemyRecover`), but with multiple participants — it should be discarded if **any** participant died before their turn came up (consistent with the existing single-operator dead-check, extended to a list):

```csharp
if (action.Type == PendingActionType.FocusFire)
{
    for (int i = 0; i < action.FocusFireParticipants.Length; i++)
    {
        int s = action.FocusFireParticipants[i];
        if (s >= this.roster.Count || !this.roster[s].IsAlive) return true;
    }
    return false;
}
```

(A plain loop, not LINQ — the codebase doesn't use `System.Linq` in `Combat/`.)

`ProcessQueueHead` gains a branch before the generic operator-action fallback, mirroring the `Shoot` branch but publishing the group event and unfreezing every participant once configuration is requested:

```csharp
if (head.Type == PendingActionType.FocusFire)
{
    if (!this.shootConfigurationInProgress)
    {
        if (IsActorDead(head)) { this.DequeueAction(); return; }
        this.shootConfigurationInProgress = true;
        foreach (int slot in head.FocusFireParticipants)
            this.atbSystem.UnfreezeActor(slot, ATBActorKind.Operator);
        this.focusFirePublisher.Publish(new FocusFireConfigurationRequestedEvent(head.FocusFireParticipants));
    }
    return;
}
```

Completion mirrors `NotifyShootCompleted()` — a new `NotifyFocusFireCompleted()` dequeues the head (which must still be the `FocusFire` action) and clears `shootConfigurationInProgress`, same shape as the existing method.

`CombatOrchestrator` needs a new constructor-injected `IPublisher<FocusFireConfigurationRequestedEvent> focusFirePublisher`. `ShootConfigurationRequestedEvent` (the event this mirrors) is defined in `Infrastructure/Events/GameEvents.cs` and registered in `GameLifetimeScope.cs` via `builder.RegisterMessageBroker<ShootConfigurationRequestedEvent>(options);` — `FocusFireConfigurationRequestedEvent` follows the exact same two spots.

---

## Resolution: the combat-menu state machine

This is the biggest new piece. Reuses every existing per-shot building block (`ShotCountSelectionState`'s UI, `AimViewController`'s QTE and burst-pattern math, `PlayOperatorShootBurstAsync`) but needs a new coordinating layer to loop them across a participant list instead of a single operator.

### `CombatMenuController` gains

```csharp
internal int[] FocusFireParticipants     { get; set; } = Array.Empty<int>();
internal int   FocusFireParticipantIndex { get; set; }
internal readonly Dictionary<int, int> FocusFireShotCounts = new(); // slot -> chosen count, built up as each participant confirms
```

`shootSubscriber`'s handler (`BeginShootConfiguration`) gets a `FocusFire` counterpart: the new `focusFireSubscriber.Subscribe(e => BeginFocusFireConfiguration(e.ParticipantSlots))`, which seeds `FocusFireParticipants`, resets `FocusFireParticipantIndex = 0` and `FocusFireShotCounts.Clear()`, sets `SelectedOperator = participants[0]`, and transitions into `ShotCountState` exactly like `BeginShootConfiguration` does today.

### `ShotCountSelectionState` becomes group-aware

On confirm, instead of always moving to `TargetSelState`/`AimingState`, it checks whether it's mid-group:

- If `context.FocusFireParticipants.Length > 0`: record `FocusFireShotCounts[SelectedOperator] = chosenCount`. If `FocusFireParticipantIndex + 1 < FocusFireParticipants.Length`, advance the index, set `SelectedOperator = FocusFireParticipants[nextIndex]`, and **re-enter itself** (`context.TransitionTo(this)` or an explicit re-`Enter()`) so the next participant picks their count with the same UI. Once every participant has a count, proceed to `TargetSelState` once (shared target — nobody's targeted anything yet).
- If `FocusFireParticipants.Length == 0` (the existing solo-Shoot path): unchanged.

### `TargetSelectionState` — unchanged

Already operates on `context.SelectedOperator` only to pull that operator's weapon for the hit-mask/dispersion sprite in `aimView.ConfigureWeapon(...)`. For the group flow, `SelectedOperator` at this point is the trigger (index `Length - 1`, the last one to confirm a shot count) — which is exactly who should drive the one interactive QTE (see below). Target selection itself (which enemy) is shared across the whole group.

### `AimingState` — group resolution

This is where per-participant weapon application happens. Since the trigger operator is last in `FocusFireParticipants` (see Triggering), they're also whoever `TargetSelectionState` left `context.SelectedOperator` as — so the real interactive QTE (`aimView.ConfigureWeapon` + the vertical/horizontal oscillation + `Confirm()`) resolves using **the trigger's own weapon**. `HandleShotsResolved` — if `context.FocusFireParticipants.Length > 0` — additionally loops the other participants (indices `0` through `Length - 2`, the ones who were marked) using a new `IAimView` method that reuses the already-locked aim position instead of re-running the interactive oscillation:

```csharp
// IAimView
ResolvedShot[] ResolveShotsForWeapon(WeaponData? weaponData, int shotCount);
```

```csharp
// AimViewController
public ResolvedShot[] ResolveShotsForWeapon(WeaponData? weaponData, int shotCount)
{
    this.ConfigureWeapon(weaponData);
    this.shotCount = Mathf.Max(1, shotCount);
    var firstShotLocal = this.ComputeRandomShotLocal(); // reuses the already-locked confirmedLocalPos
    return this.BuildResolvedShots(firstShotLocal, this.shotCount);
}
```

No new interactive phase, no new marker/feedback visuals for the marked participants (the single shared QTE — the trigger's — is the only interactive/visual aim moment: "un único QTE compartido" per the GDD, not one silhouette pass per participant). Each participant's `ResolvedShot[]` is resolved against `context.FocusFireShotCounts[slot]` and their own `WeaponData` (via `roster[slot].ActiveWeapon`); the trigger's own shots are simply the ones the real QTE already produced, no `ResolveShotsForWeapon` call needed for them.

`AimingState.HandleShotsResolved`/`CloseAimAndReturnToOperatorSelectionAsync` extend to, for the group case:
1. For each participant in order, compute `totalDamage`/`totalPoiseDamage` from their `ResolvedShot[]` (the trigger, last in the list, uses the shots already resolved by the real QTE; the marked participants before it via `ResolveShotsForWeapon`) and call `battlefieldView.ApplyDamageToEnemy` **sequentially per participant** — not one aggregate sum. This keeps stagger/death handling exactly as it already works today (`ShouldStagger`/`IsDead` computed against the enemy's *current* HP/Poise at the time each participant's shots land), and naturally handles the enemy dying partway through the group (later participants' `ApplyDamageToEnemy` calls hit an already-dead enemy and no-op, same as the existing dead-enemy guard).
2. Play each participant's burst **sequentially** via the existing `PlayOperatorShootBurstAsync(participantSlot, targetSlot, participantShots)` — one operator's animation finishes before the next one's starts. No new choreography needed; this is the existing method called once per participant instead of once.
3. Apply the existing stagger/death finalize logic (`TriggerEnemyStagger`/`FinalizeEnemyDeath`) once, using whichever participant's hit actually caused it — if the enemy dies or staggers partway through the sequence, later participants still finish their bursts (visually "wasting" shots on an already-falling enemy is acceptable and arguably reads as appropriately excessive for a focus-fire volley) but their `ApplyDamageToEnemy` calls no-op per the existing dead-enemy guard.
4. Deduct each participant's own ammo (`weapon.SetAmmo(weapon.CurrentAmmo - theirShotCount)`) — confirmed per-operator consumption.
5. Call `context.Orchestrator.NotifyFocusFireCompleted()` instead of `NotifyShootCompleted()`, then transition back to `OperatorSelState` as today.

---

## Out of scope

- Per-weapon Animation Lock duration — not a real gap (burst duration already scales with each weapon's actual clip length via the existing `await` in `PlayOperatorShootBurstAsync`), and Focus Fire doesn't depend on it.
- Un-marking an operator once marked, or marking cancel/undo.
- Any visual choreography beyond sequential per-participant bursts (e.g. simultaneous multi-rig animation).
- Extending Focus Fire to `UseItem`/non-Shoot commands.
