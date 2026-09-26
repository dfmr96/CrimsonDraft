# Enemy AI Rework — Design Spec

## Problem

The navigation-phase enemy (`NavigationEnemyData_Infected`, a shambling zombie) is driven by a state machine and combat trigger that were both modeled on stealth-guard AI (MGS-style) rather than a classic Resident Evil zombie:

- `EnemyNavAgent` has a `Patrol → Suspicious → Alert` FSM: it walks fixed waypoint routes, and on losing the player during `Suspicious` it gives up and resumes patrolling. Zombies don't patrol routes or lose interest.
- Combat starts in two blunt, collision-driven ways: a raw distance check (`catchRadius`) once `Alert` gets close enough, and a separate `CombatTrigger` component that starts combat on any collider touch with the tag `Player`, regardless of the enemy's AI state or facing.
- Movement looks wrong ("ice skating"): `NavMeshAgent` translates toward the new destination immediately while `updateRotation` slowly turns the body to match, so when the player passes to the side the zombie visibly slides sideways instead of turning to face them before advancing.
- `EnemyDetectionSensor` has a standalone omnidirectional proximity check (`detectRadius`/`undetectRadius`) that detects the player by raw distance regardless of whether they're moving — redundant with (and weaker than) sound detection, which already covers proximity but weighted by whether the player is walking, running, or standing still.

## Goal

Replace the FSM with a 3-state model appropriate to the genre — `Idle → Alerted → Attack` — with no patrol routes and no de-alerting. Combat starts only from a deliberate hit: a collider on the zombie's attack-hand, active only during its swing, touching the player. Fix the locomotion so the zombie turns in place to face its target before walking, which both matches the genre and eliminates the sliding artifact. Remove the scripted `CombatTrigger` collider entirely — no combat may start from a bare trigger volume anymore. Detection becomes sound + vision only: a perfectly still player is only found by sight, never by distance alone.

Out of scope: `CombatTrigger`'s combat-phase counterparts (`Enemy_Grunt`/`Enemy_Heavy`, which are `CrimsonDraft.Combat.EnemyData` battlefield stats, unrelated to navigation AI); the player-initiated ranged combat start in `PlayerAimController.HandleFire()` (aim + shoot at a spotted zombie to preempt melee — this already works correctly and doesn't trigger by collision); the cosmetic `WalkEnemyClose` reaction in `EnemyAnimationReactor` (stays as-is).

## Architecture

State ownership shifts fully into `EnemyNavAgent`, which already owns the FSM. Two changes to how neighboring components relate to it:

1. **`EnemyAnimationReactor` stops deciding, only reacts.** It currently computes its own `isAttackRangeActive` (with its own hysteresis) to decide when to stop the agent and fire the `Attack` animation trigger — duplicating what the FSM needs to know anyway. That decision moves to `EnemyNavAgent` (single source of truth for "what state is this enemy in"); the reactor is told the state and just plays animations / stops movement accordingly. It keeps owning direct `Animator` polling (attack-animation-finished detection, twitch timing, `WalkEnemyClose`), since that's Animator-specific bookkeping the FSM has no reason to know about — but it reports the "attack animation just finished" edge back to `EnemyNavAgent` via a new callback, since that edge is what drives the FSM's `Attack → Alerted` transition.
2. **A new `EnemyAttackHitbox` component** lives on the hand bone of the zombie's rig. It's a disabled-by-default trigger collider, toggled by Animation Events during the swing's active frames (same pattern already used by `FootstepController.OnWalkStep()` — public parameterless methods called by name from the animation clip). On `OnTriggerEnter` with tag `Player` while active, it calls back into the owning `EnemyNavAgent`, which runs the existing combat-start sequence (`IsInCombat` guard, `StartCombatAsync`, deactivate). This replaces `catchRadius` and the removed `CombatTrigger`.

`NotifyCombatTriggered()` on `EnemyNavAgent` is unchanged and keeps its current caller (`PlayerAimController`, for the shoot-to-preempt path) — that flow is untouched by this rework.

## Components

### `EnemyAlertState` (renamed from `GuardAlertState`, `Assets/Scripts/Infrastructure/Events/GameEvents.cs`)

```csharp
public enum EnemyAlertState { Idle, Alerted, Attack }

public readonly struct EnemyAlertChangedEvent
{
    public string EnemyId { get; init; }
    public EnemyAlertState PreviousState { get; init; }
    public EnemyAlertState NewState { get; init; }
}
```

Renames `GuardAlertState → EnemyAlertState`, `GuardAlertChangedEvent → EnemyAlertChangedEvent`, `GuardId → EnemyId`, dropping the "Guard"/MGS naming per the same reasoning as the state rework. Both types are pure declarations with no consumers beyond publish (`NavigationScope.Configure` registers the message broker; nothing subscribes yet), so the rename is mechanical — update the two reference sites in `NavigationScope.cs` and `EnemyNavAgent.cs`.

### `EnemyDetectionSensor` (`Assets/Scripts/Navigation/Enemy/EnemyDetectionSensor.cs`)

Drop the proximity block entirely (step 1 of `Evaluate()`, and the `proximityActive` field/hysteresis). `Evaluate()` becomes sound-then-visual only — same two blocks as today, renumbered, untouched internally. `ResetState()` has nothing left to reset, so it and its one caller (`EnemyNavAgent.TransitionTo`'s `Idle` case) are removed too.

### `NavigationEnemyData` (`Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs`)

Removed fields: `patrolSpeed`, `waypointStopDistance`, `catchRadius`, `suspiciousEnabled`, `suspiciousDuration` (all patrol/suspicious-specific, now dead), and `detectRadius`/`undetectRadius` (proximity detection, removed above).

Added fields, replacing what `EnemyAnimationReactor` and `EnemyNavAgent` used to hardcode locally, so all AI-tuning values live on the data-driven asset in one place:

```csharp
[Header("Alerted Movement")]
[Tooltip("Velocidad del NavMeshAgent mientras persigue en Alerted.")]
public float chaseSpeed = 3.5f;
[Tooltip("Velocidad de giro en grados/segundo al encarar al jugador antes de avanzar.")]
public float turnSpeed = 120f;
[Tooltip("Ángulo (grados) respecto al jugador por encima del cual el enemigo se detiene a girar en el lugar en vez de avanzar.")]
public float turnInPlaceThreshold = 35f;

[Header("Attack")]
[Tooltip("Distancia a la que el enemigo entra en el estado Attack.")]
public float attackRange = 1.3f;
[Tooltip("Margen extra sobre attackRange antes de volver a Alerted (evita flickering en el borde).")]
public float attackRangeBuffer = 0.3f;
```

Sound/visual-detection fields (`playerDeadzone`, `playerRunThreshold`, `walkSoundRadius`, `runSoundRadius`, `visualRange`, `visualFov`, `obstructionMask`, `targetMask`) are untouched.

### `EnemyNavAgent` (`Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs`)

- Drop `path` (`EnemyPatrolPath`), `patrolEnabled`, the local `suspiciousTurnSpeed` field (now `data.turnSpeed`), and `navPathCache`/`CanReachPlayer()` (the FSM no longer gates alerting on path reachability — it alerts immediately and lets `NavMeshAgent` handle an unreachable destination the way it already does today for `SetDestination`).
- `private EnemyAlertState state = EnemyAlertState.Idle;` (was `GuardAlertState.Patrol`), plus a new public getter `public EnemyAlertState State => state;` so `EnemyAnimationReactor` can poll it (see below).
- `Update()` switches on the new enum:

```csharp
switch (state)
{
    case EnemyAlertState.Idle:    UpdateIdle();    break;
    case EnemyAlertState.Alerted: UpdateAlerted(); break;
    case EnemyAlertState.Attack:  UpdateAttack();  break;
}
```

- `UpdateIdle()`: no movement, just `if (Detect()) TransitionTo(EnemyAlertState.Alerted);`.
- `UpdateAlerted()`:
  ```csharp
  private void UpdateAlerted()
  {
      var toPlayer = playerController!.transform.position - transform.position;
      toPlayer.y = 0f;

      if (toPlayer.magnitude < data.attackRange)
      {
          TransitionTo(EnemyAlertState.Attack);
          return;
      }

      var angle = Vector3.Angle(transform.forward, toPlayer.normalized);
      if (angle > data.turnInPlaceThreshold)
      {
          navAgent.isStopped = true;
          navAgent.updateRotation = false;
          var targetRotation = Quaternion.LookRotation(toPlayer.normalized);
          transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, data.turnSpeed * Time.deltaTime);
      }
      else
      {
          navAgent.isStopped = false;
          navAgent.updateRotation = true;
          navAgent.SetDestination(playerController.transform.position);
      }
  }
  ```
  This is the fix for the "ice skating" bug: large misalignment halts translation and turns in place; once roughly facing, `NavMeshAgent` resumes owning both movement and fine steering rotation.
- `UpdateAttack()`: no per-frame movement logic needed — the agent stays stopped (set on state entry) and waits for either `EnemyAttackHitbox`'s hit callback (→ combat start, handled the same as today's `TriggerCombat()`) or `EnemyAnimationReactor`'s "attack animation finished without a hit" callback (→ `TransitionTo(EnemyAlertState.Alerted)`, which immediately re-checks range and re-attacks if the player is still close, or resumes the chase if they backed off).
- `TransitionTo()` gains an `Attack` case (`navAgent.isStopped = true; navAgent.updateRotation = true;` — kept true so the agent's own steering keeps the body loosely facing the last movement direction; fine-tracking the player during the swing is cosmetic and not required for the hitbox, which is sized to the swing arc) and its `Idle` case replaces `Patrol`'s (drops the waypoint `SetDestination` and the now-removed `sensor.ResetState()` call).
- `OnDrawGizmosSelected()` drops the two wire spheres for `data.detectRadius`/`data.undetectRadius` (proximity removed); the sound and visual-cone gizmos are untouched.
- New method, called by `EnemyAttackHitbox`:
  ```csharp
  public void NotifyAttackHit()
  {
      if (state != EnemyAlertState.Attack) return; // stale/duplicate trigger from a re-enabled collider
      TriggerCombat();
  }
  ```
- New method, called by `EnemyAnimationReactor` on the attack-animation-finished edge:
  ```csharp
  public void NotifyAttackAnimationFinished()
  {
      if (state != EnemyAlertState.Attack) return; // combat already started via NotifyAttackHit
      TransitionTo(EnemyAlertState.Alerted);
  }
  ```
- `TriggerCombat()`'s body is unchanged (still `IsInCombat` guard → `StartCombatAsync` → `SetActive(false)`).
- `ResetToSpawn()` calls `TransitionTo(EnemyAlertState.Idle)` instead of `Patrol`, and drops the `path?.ResetIndex()` line (no `path` anymore).

### `EnemyAnimationReactor` (`Assets/Scripts/Navigation/Enemy/EnemyAnimationReactor.cs`)

- Drop `attackRange`, `attackRangeBuffer`, `attackCooldown`, `isAttackRangeActive`, `attackTimer`, `UpdateAttack()`, and the attack half of `UpdateAttackMovementLock()`/`UpdateProximity()` — `EnemyNavAgent` now owns the attack-range decision and its own `isStopped` control during `Attack`.
- Keeps `closeRadius`/`closeRadiusBuffer`/`isCloseActive`/`UpdateProximity()`'s `WalkEnemyClose` half as-is (approved to stay out of scope).
- New field: `[SerializeField] private EnemyNavAgent owner = null!;` (set the same way other same-GameObject/parent references are wired in this codebase — via `GetComponentInParent<EnemyNavAgent>()` in `Awake()`, matching the existing auto-find pattern already used here for `player`).
- `Update()` replaces `UpdateAttack()` with an edge-detect on the existing `IsPlayingAttack()` check:
  ```csharp
  private bool wasPlayingAttack;

  private void UpdateAttackFinishedEdge()
  {
      var playingAttack = IsPlayingAttack();
      if (wasPlayingAttack && !playingAttack)
          owner.NotifyAttackAnimationFinished();
      wasPlayingAttack = playingAttack;
  }
  ```
- `EnemyNavAgent` gains a public getter, `public EnemyAlertState State => state;`. `EnemyAnimationReactor` polls it every frame the same way `UpdateLocomotion()` already polls `navAgent.velocity`, and fires the trigger on the edge into `Attack`:
  ```csharp
  private EnemyAlertState previousState;

  private void UpdateAttackTrigger()
  {
      if (owner.State == EnemyAlertState.Attack && previousState != EnemyAlertState.Attack)
          animator.SetTrigger(AttackHash);
      previousState = owner.State;
  }
  ```
  This keeps `EnemyNavAgent` free of any direct `Animator` reference, preserving the existing separation (agent = decision + `NavMeshAgent`, reactor = all `Animator` I/O).

### `EnemyAttackHitbox` (new, `Assets/Scripts/Navigation/Enemy/EnemyAttackHitbox.cs`)

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyAttackHitbox : MonoBehaviour
    {
        [SerializeField] private EnemyNavAgent owner = null!;

        private void Awake() => GetComponent<Collider>().enabled = false;

        // Called by Animation Events at the swing's active frames, same pattern as
        // FootstepController.OnWalkStep()/OnRunStep().
        public void OnAttackHitboxOpen()  => GetComponent<Collider>().enabled = true;
        public void OnAttackHitboxClose() => GetComponent<Collider>().enabled = false;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            GetComponent<Collider>().enabled = false; // one hit per swing
            owner.NotifyAttackHit();
        }
    }
}
```

Placed on a child object of the hand bone with a small trigger `Collider` (sized to the model's hand/claw), `[SerializeField] owner` wired in the prefab inspector to the root `EnemyNavAgent` — a plain scene reference, not DI (matching how `EnemyPatrolPath`/`EnemyDetectionSensor` are wired to `EnemyNavAgent` today, i.e. no `Construct()` involvement).

### Removed: `EnemyPatrolPath.cs`, `CombatTrigger.cs`

- `EnemyPatrolPath.cs` is deleted. It's referenced in one scene (`Assets/Scenes/Production/Navigation.unity`) — the component and its waypoint child transforms need to be removed from the zombie GameObject(s) there.
- `CombatTrigger.cs` is deleted. Confirmed no encounter depends on it exclusively (every combat start goes through an enemy). It's referenced in:
  - Code: `NavigationScope.cs` (`cachedCombatTriggers` field, `RegisterInstance`, the `[Button] CacheSceneEnemies` populating it) and `EnemyBootstrap.cs` (`triggers` constructor param, injection loop) — both remove their `CombatTrigger`-related lines entirely.
  - Scenes/prefabs: `Navigation.unity`, `FIX_Deck_B ShadersTest.unity`, both `New Room.unity` (Production and Deck B), `DeckB_Port_Stairs.unity`, and `__GAMEPLAYCORE.prefab` — the `CombatTrigger` component instances need to be removed from whatever GameObjects host them in each.

## Data Flow

1. **Idle**: enemy stationary, `EnemyDetectionSensor.Evaluate()` runs every frame (sound + visual only). On a hit → `TransitionTo(Alerted)`.
2. **Alerted**: every frame, check distance to player first (→ `Attack` if within `attackRange`), otherwise check facing angle — turn in place if misaligned, else let `NavMeshAgent` walk + steer normally. Never reverts to `Idle` on its own (matches the "zombies don't lose interest" decision); only `ResetToSpawn()` (room re-entry) forces it back to `Idle`.
3. **Attack**: agent stopped, `Attack` animation plays, `EnemyAttackHitbox` opens/closes on Animation Events during the swing.
   - Hit lands → `EnemyAttackHitbox.OnTriggerEnter` → `EnemyNavAgent.NotifyAttackHit()` → `TriggerCombat()` (same as today: guard `IsInCombat`, `StartCombatAsync`, deactivate the enemy GameObject).
   - Animation ends without a hit → `EnemyAnimationReactor`'s edge-detect → `EnemyNavAgent.NotifyAttackAnimationFinished()` → `TransitionTo(Alerted)`, which re-evaluates range/facing next frame (re-attacks immediately if the player is still close, otherwise resumes the chase).
4. Room re-entry (`RoomController.Activate()` → `EnemyNavAgent.ResetToSpawn()`) always returns a still-alive enemy to `Idle` at its spawn transform, exactly as `Patrol` reset did before.
5. The player's proactive ranged trigger (`PlayerAimController.HandleFire()` → `NotifyCombatTriggered()` + `StartCombatAsync`) is unaffected — it doesn't go through `EnemyNavAgent`'s state machine at all.

## Edge Cases

- **Player circles a stationary `Attack`-state zombie**: the agent doesn't re-track rotation mid-swing (cosmetic simplification, see `TransitionTo`'s `Attack` case); worst case is one swing that misses cleanly, after which `Alerted` picks the facing back up. Acceptable for a slow zombie.
- **Player re-enters `attackRange` the instant `NotifyAttackAnimationFinished` fires**: `TransitionTo(Alerted)` still runs its own body next frame via `UpdateAlerted()`, immediately re-entering `Attack` — no stale state.
- **`EnemyAttackHitbox` overlapping the player when opened but the enemy is no longer in `Attack`** (e.g. a state transition raced the Animation Event): `NotifyAttackHit()` no-ops outside `Attack`, so a stray trigger can't start combat twice or from the wrong state.
- **Multiple zombies, one triggers combat**: unchanged from today — `TriggerCombat()` deactivates only the triggering enemy's GameObject; others keep running their own FSM independently until the scene reloads post-combat.
- **`turnInPlaceThreshold` set to 0 or 180 in the data asset**: degenerate but harmless — 0 means the agent almost never walks without perfect alignment (a very "stiff" zombie), 180 means it never stops to turn (reverts to today's sliding behavior for that enemy instance only). No code guard needed; it's a designer tuning knob on the ScriptableObject like the others.

## Testing

- No existing automated test covers `EnemyNavAgent` or `CombatTrigger`/the FSM. Consistent with this codebase's existing boundary (per `2026-07-21-room-enemy-reset-design.md`): components this coupled to `NavMeshAgent`/`Animator`/`Update()` are verified manually in Play Mode, not unit tested.
- `EnemyDetectionSensorTests.cs` does need updating for the proximity removal: delete `Proximity_DetectsWhenInsideDetectRadius`, `Proximity_NoDetectionOutsideUndetectRadius`, `Proximity_Hysteresis_StaysActiveInZoneBetweenRadii`, `Proximity_Hysteresis_DeactivatesOnceOutsideUndetectRadius`, and `ResetState_ClearsProximityHysteresis` (the method under test is gone). Update `MakeData()` to drop its `detectRadius`/`undetectRadius` parameters, and drop the now-meaningless `detectRadius`/`undetectRadius` arguments from the remaining `Sound_*`/`Visual_*` tests' `MakeData(...)` calls (they were only set to shrink proximity out of the way; with proximity gone there's nothing to shrink).
- Manual verification plan: (1) stand still until an idle zombie detects and confirm it turns to face before walking, no sideways sliding when approached from an angle; (2) walk past a zombie's flank and confirm it turns in place rather than sliding; (3) let a zombie close to attack range, confirm it stops and swings, and that combat starts only on a landed hit (retreat mid-swing and confirm it resumes chasing instead of starting combat); (4) confirm the removed `CombatTrigger` volumes' former locations no longer start combat on touch; (5) confirm `PlayerAimController`'s shoot-to-preempt flow still starts combat correctly; (6) re-enter a room after luring a zombie and confirm it resets to `Idle` at its spawn point.
