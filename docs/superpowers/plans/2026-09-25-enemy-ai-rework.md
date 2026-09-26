# Enemy AI Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the navigation-phase zombie's guard-style `Patrol/Suspicious/Alert` AI and collision-based combat triggers with a genre-appropriate `Idle/Alerted/Attack` model, a turn-then-walk locomotion fix, and a hand-hitbox-driven combat start.

**Architecture:** `EnemyNavAgent` becomes the single owner of AI state and decisions (detection, movement gating, attack range); `EnemyAnimationReactor` is reduced to a pure reactor that plays animations off that state and reports Animator-specific edges back; a new `EnemyAttackHitbox` component turns a landed melee swing into the sole enemy-initiated combat trigger, replacing both the old distance check and the deleted `CombatTrigger` collider. No new dependencies — everything stays on Unity's built-in `NavMeshAgent`.

**Tech Stack:** Unity (C#), VContainer, MessagePipe, UniTask, NavMeshAgent, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-09-25-enemy-ai-rework-design.md`

## Global Constraints

- Stay on Unity's built-in `NavMeshAgent` — no third-party pathfinding package.
- No combat may start from a bare trigger volume — `CombatTrigger.cs` must be fully removed from code and from every scene/prefab that hosts it, with no replacement collider-only trigger introduced.
- AI decisions (what state the enemy is in, when it attacks) live only in `EnemyNavAgent`. `EnemyAnimationReactor` may only read that state and drive the `Animator`/`NavMeshAgent.isStopped` off it — it must not compute its own range/proximity decisions.
- Renamed types use the `Enemy` prefix, not `Guard` or `Infected` (`EnemyAlertState`, `EnemyAlertChangedEvent`, `EnemyId`).
- `EnemyNavAgent.NotifyCombatTriggered()` and its only caller, `PlayerAimController.HandleFire()`'s shoot-to-preempt path, are not modified by this work.
- `EnemyDetectionSensor` detects by sound (velocity-weighted) and vision only — no standalone distance-only proximity check.

## Review Focus

- **Two zombies alerted at once, only one reaches attack range:** the other must keep chasing independently — `TriggerCombat()`/`SetActive(false)` must only ever affect the zombie whose hitbox landed, never siblings. No automated test can cover this (untestable `MonoBehaviour`+`NavMeshAgent` combo); Task 9's manual QA explicitly drives two zombies into a chase together.
- **Player backs out of attack range mid-swing:** the attack animation must finish rather than cancel, and only then release back to `Alerted` — a reasonable player expects "I dodged, it doesn't hit me," not "it teleports back to chasing mid-swing" or "it still hits me anyway." Task 9's manual QA exercises this explicitly (item 3).
- **`EnemyAttackHitbox` re-enabled while the FSM already left `Attack`** (an Animation Event racing a state change, e.g. combat already started via a different path or `ResetToSpawn` fired mid-swing): must not double-trigger combat or throw. Covered by the `state != EnemyAlertState.Attack` guard in `NotifyAttackHit()` (Task 2) — flagged here since nothing exercises it automatically; Task 9's manual QA item 3 covers the ordinary case, but the race itself is a code-review point for Task 2, not a test.
- **A designer sets `attackRangeBuffer` to 0 or leaves `attackRange` larger than `turnInPlaceThreshold`'s effective approach distance:** produces flicker between `Alerted`/`Attack` or a zombie that never lines up before attacking. No code guard is added (matches the existing project convention of un-clamped designer-tuned floats on `NavigationEnemyData`); called out here so whoever tunes `NavigationEnemyData_Infected.asset` after this lands knows to sanity-check it in Play Mode, per Task 9.
- **Removing `CombatTrigger`/`EnemyPatrolPath` leaves a scene with a "Missing Script" GameObject** if a component instance is missed during scene cleanup: silently breaks that scene without a compile error. Task 7 explicitly re-checks every one of the 6 known locations plus a project-wide re-grep after the deletions, and Task 9's final `read_console` pass is the backstop.

---

## Task 1: Simplify `EnemyDetectionSensor` to sound + vision only

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyDetectionSensor.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs:262-278` (gizmo method only — do not touch anything else in this file; the rest is rewritten in Task 2)
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyDetectionSensorTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `EnemyDetectionSensor.Evaluate(NavigationEnemyData data, Transform player, Rigidbody playerRb, Transform? eyePoint)` keeps its exact signature and return type (`bool`) — Task 2's `EnemyNavAgent.Detect()` calls it unchanged. `NavigationEnemyData` loses `detectRadius`/`undetectRadius`; every other field Task 2/3 need (`chaseSpeed`, new fields) is untouched here.

- [ ] **Step 1: Update the test file to the target shape (proximity tests removed, `MakeData` simplified)**

Replace the full contents of `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyDetectionSensorTests.cs` with:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyDetectionSensorTests
    {
        private NavigationEnemyData MakeData(
            float walkRadius  = 0f,
            float runRadius   = 0f,
            float visualRange = 0f)
        {
            var data = ScriptableObject.CreateInstance<NavigationEnemyData>();
            data.walkSoundRadius    = walkRadius;
            data.runSoundRadius     = runRadius;
            data.playerDeadzone     = 0.1f;
            data.playerRunThreshold = 5.5f;
            data.visualRange        = visualRange;
            return data;
        }

        [Test]
        public void Sound_DetectsWalkingPlayerWithinWalkRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(3f, 0f, 0f); // inside walkRadius=5
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(4f, 0f, 0f); // walk speed (4 < runThreshold 5.5)

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsTrue(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_NoDetectionForIdlePlayer()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(3f, 0f, 0f);
            var playerRb = playerGO.AddComponent<Rigidbody>();
            // linearVelocity is Vector3.zero by default — player is idle

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_DetectsRunningPlayerWithinRunRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(7f, 0f, 0f); // inside runRadius=9, outside walkRadius=5
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(6f, 0f, 0f); // run speed (6 > runThreshold 5.5)

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsTrue(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_NoDetectionForRunningPlayerOutsideRunRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(10f, 0f, 0f); // outside runRadius=9
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(6f, 0f, 0f); // run speed (6 > runThreshold 5.5)

            var data = MakeData(walkRadius: 5f, runRadius: 9f);

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Visual_NoDetectionWhenOutsideVisualRange()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(5f, 0f, 0f); // outside visualRange=3
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData(visualRange: 3f);
            data.visualFov = 180f;

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Visual_NoDetectionWhenOutsideFOV()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            sensorGO.transform.forward = Vector3.forward; // facing +Z
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(2f, 0f, 0f); // directly to the side (+X), FOV=10° → 90° angle > 5°
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData(visualRange: 5f);
            data.visualFov = 10f;

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they still pass against today's sensor**

Run via Unity Test Runner (EditMode) or the MCP `run_tests` tool, filtered to `CrimsonDraft.Tests.EnemyDetectionSensorTests`.
Expected: all 6 tests PASS (the production sensor still has proximity code, but nothing in the test file exercises it anymore, and none of the remaining radii are large enough to accidentally trigger it at the test positions used).

- [ ] **Step 3: Remove the proximity block from `EnemyDetectionSensor.Evaluate()`**

In `EnemyDetectionSensor.cs`, delete the `proximityActive` field, the entire "1. Proximity with hysteresis" block, and the `ResetState()` method. The method becomes:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyDetectionSensor : MonoBehaviour
    {
        public bool Evaluate(NavigationEnemyData data, Transform player, Rigidbody playerRb, Transform? eyePoint)
        {
            var playerPos = player.position;
            var distance  = Vector3.Distance(transform.position, playerPos);

            // 1. Sound detection (distance from sensor origin to player)
            var speed = playerRb.linearVelocity.magnitude;
            if (speed > data.playerDeadzone)
            {
                var soundRadius = speed > data.playerRunThreshold
                    ? data.runSoundRadius
                    : data.walkSoundRadius;
                if (distance < soundRadius) return true;
            }

            // 2. Visual detection — 2-pass raycast from eye point
            if (distance < data.visualRange)
            {
                var origin      = eyePoint != null ? eyePoint.position : transform.position;
                var dirToPlayer = (playerPos - origin).normalized;
                var angle       = Vector3.Angle(transform.forward, dirToPlayer);

                if (angle < data.visualFov * 0.5f)
                {
                    var eyeDist = Vector3.Distance(origin, playerPos);
                    if (!Physics.Raycast(origin, dirToPlayer, eyeDist, data.obstructionMask))
                    {
                        if (Physics.Raycast(origin, dirToPlayer, eyeDist, data.targetMask))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Remove `detectRadius`/`undetectRadius` from `NavigationEnemyData`**

In `NavigationEnemyData.cs`, delete the entire `[Header("Proximity Detection")]` block (`detectRadius`, `undetectRadius` and their tooltips).

- [ ] **Step 5: Fix the now-broken gizmo code in `EnemyNavAgent.cs`**

In `EnemyNavAgent.cs`'s `OnDrawGizmosSelected()`, delete these two blocks (they reference the removed fields):

```csharp
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(pos, data.detectRadius);

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, data.undetectRadius);
```

Leave the `catchRadius` and sound/visual gizmo lines untouched for now — `catchRadius` is removed in Task 2.

- [ ] **Step 6: Run the tests again to confirm they still pass against the simplified sensor**

Run via Unity Test Runner (EditMode) or `run_tests`, filtered to `CrimsonDraft.Tests.EnemyDetectionSensorTests`.
Expected: all 6 tests PASS.

- [ ] **Step 7: Check the Unity console for compile errors**

Use the MCP `read_console` tool (or the Editor's Console window) filtered to Error.
Expected: no errors. (`EnemyNavAgent.cs` still references `path`/`patrolEnabled`/`catchRadius`/`suspiciousEnabled` etc. at this point — those are untouched by this task and still compile fine since Task 1 didn't remove them.)

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyDetectionSensor.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs Game/CrimsonDraft/Assets/Tests/EditMode/EnemyDetectionSensorTests.cs
git commit -m "refactor(enemy-ai): drop proximity detection, sound+vision only"
```

---

## Task 2: Rewrite `EnemyNavAgent` to the `Idle/Alerted/Attack` FSM

This is the load-bearing task: the enum rename, the data-asset field swap, and the full agent rewrite all have to land together to keep the project compiling, since they're mutually referential.

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Infrastructure/Events/GameEvents.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs` (one line)
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs` (full rewrite)

**Interfaces:**
- Consumes: `EnemyDetectionSensor.Evaluate(...)` (unchanged, from Task 1).
- Produces (used by Task 3 and Task 4):
  - `public enum EnemyAlertState { Idle, Alerted, Attack }` (in `GameEvents.cs`)
  - `EnemyNavAgent.State` — `public EnemyAlertState State => state;`
  - `EnemyNavAgent.NotifyAttackHit()` — `public void NotifyAttackHit()`
  - `EnemyNavAgent.NotifyAttackAnimationFinished()` — `public void NotifyAttackAnimationFinished()`
  - `NavigationEnemyData.chaseSpeed`, `.turnSpeed`, `.turnInPlaceThreshold`, `.attackRange`, `.attackRangeBuffer` (all `float`, all public fields)

- [ ] **Step 1: Rename the enum and event struct in `GameEvents.cs`**

Change:
```csharp
public enum GuardAlertState { Patrol, Suspicious, Alert }
```
to:
```csharp
public enum EnemyAlertState { Idle, Alerted, Attack }
```

Change:
```csharp
public readonly struct GuardAlertChangedEvent
{
    public string GuardId { get; init; }
    public GuardAlertState PreviousState { get; init; }
    public GuardAlertState NewState { get; init; }
}
```
to:
```csharp
public readonly struct EnemyAlertChangedEvent
{
    public string EnemyId { get; init; }
    public EnemyAlertState PreviousState { get; init; }
    public EnemyAlertState NewState { get; init; }
}
```

- [ ] **Step 2: Update the one reference in `NavigationScope.cs`**

Change:
```csharp
            builder.RegisterMessageBroker<GuardAlertChangedEvent>(msgOptions);
```
to:
```csharp
            builder.RegisterMessageBroker<EnemyAlertChangedEvent>(msgOptions);
```

- [ ] **Step 3: Swap the movement/attack fields on `NavigationEnemyData`**

Delete the entire `[Header("Movement")]` block (`patrolSpeed`, `chaseSpeed`, `waypointStopDistance`, `catchRadius`) and the entire `[Header("Suspicious State")]` block (`suspiciousEnabled`, `suspiciousDuration`). Replace them with:

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

(Leave `[Header("Sound Detection")]` and `[Header("Visual Detection")]` exactly as they are — Task 1 already handled `[Header("Proximity Detection")]`.)

- [ ] **Step 4: Rewrite `EnemyNavAgent.cs` in full**

Replace the entire file with:

```csharp
#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyNavAgent : MonoBehaviour
    {
        [SerializeField] private NavigationEnemyData  data          = null!;
        [SerializeField] private EncounterData        encounterData = null!;
        [SerializeField] private string               encounterId   = string.Empty;

        public string        EncounterId  => this.encounterId;
        public EncounterData? EncounterData => this.encounterData;
        [SerializeField] private EnemyDetectionSensor sensor        = null!;
        [SerializeField] private Transform?           eyePoint;

        private ISceneTransitionService?               sceneTransitionService;
        private ISubscriber<CombatEndedEvent>?         combatEndedSubscriber;
        private ISubscriber<DialogueActiveChangedEvent>? dialogueSubscriber;
        private IEncounterContext?                     encounterContext;
        private IPublisher<EnemyAlertChangedEvent>?    enemyAlertPublisher;
        private PlayerController?                      playerController;
        private EnemyStateRegistry?                    enemyStateRegistry;
        private string                                 enemyKey = string.Empty;

        private NavMeshAgent     navAgent        = null!;
        private Rigidbody        playerRb        = null!;
        private EnemyAlertState  state           = EnemyAlertState.Idle;
        private IDisposable?     combatEndedSub;
        private IDisposable?     dialogueSub;
        private bool             combatTriggered;
        private bool             dialoguePaused;

        public EnemyAlertState State => state;

        public void Construct(
            ISceneTransitionService                 sceneTransitionService,
            ISubscriber<CombatEndedEvent>           combatEndedSubscriber,
            ISubscriber<DialogueActiveChangedEvent> dialogueSubscriber,
            IEncounterContext                       encounterContext,
            IPublisher<EnemyAlertChangedEvent>      enemyAlertPublisher,
            PlayerController                        playerController,
            EnemyStateRegistry                      enemyStateRegistry,
            string                                  enemyKey)
        {
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.dialogueSubscriber     = dialogueSubscriber;
            this.encounterContext       = encounterContext;
            this.enemyAlertPublisher    = enemyAlertPublisher;
            this.playerController       = playerController;
            this.enemyStateRegistry     = enemyStateRegistry;
            this.enemyKey               = enemyKey;
        }

        private void Start()
        {
            navAgent = GetComponent<NavMeshAgent>();
            playerRb = playerController!.GetComponent<Rigidbody>();
            if (playerRb == null)
            {
                Debug.LogError($"[EnemyNavAgent] PlayerController on '{playerController.name}' has no Rigidbody. Sound detection will NullRef.", this);
                enabled = false;
                return;
            }

            combatEndedSub = combatEndedSubscriber?.Subscribe(OnCombatEnded);
            dialogueSub    = dialogueSubscriber?.Subscribe(OnDialogueActiveChanged);
        }

        private void OnDestroy()
        {
            combatEndedSub?.Dispose();
            dialogueSub?.Dispose();
        }

        private void Update()
        {
            if (playerController == null) return;
            if (dialoguePaused) return;

            switch (state)
            {
                case EnemyAlertState.Idle:    UpdateIdle();    break;
                case EnemyAlertState.Alerted: UpdateAlerted(); break;
                case EnemyAlertState.Attack:  UpdateAttack();  break;
            }
        }

        private void UpdateIdle()
        {
            if (Detect())
                TransitionTo(EnemyAlertState.Alerted);
        }

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

        private void UpdateAttack()
        {
            // Intentionally empty: the agent stays stopped (set on TransitionTo),
            // Animator plays the Attack clip, and EnemyAttackHitbox/EnemyAnimationReactor
            // drive the exit via NotifyAttackHit()/NotifyAttackAnimationFinished().
        }

        private bool Detect()
            => sensor.Evaluate(data, playerController!.transform, playerRb, eyePoint);

        private void TransitionTo(EnemyAlertState next)
        {
            var prev = state;
            state = next;

            enemyAlertPublisher?.Publish(new EnemyAlertChangedEvent
            {
                EnemyId       = gameObject.name,
                PreviousState = prev,
                NewState      = next,
            });

            switch (next)
            {
                case EnemyAlertState.Idle:
                    navAgent.isStopped = false;
                    navAgent.updateRotation = true;
                    navAgent.ResetPath();
                    break;

                case EnemyAlertState.Alerted:
                    navAgent.isStopped = false;
                    navAgent.updateRotation = true;
                    navAgent.speed = data.chaseSpeed;
                    break;

                case EnemyAlertState.Attack:
                    navAgent.isStopped = true;
                    navAgent.updateRotation = true;
                    break;
            }
        }

        public void NotifyCombatTriggered()
        {
            this.combatTriggered = true;
        }

        public void NotifyAttackHit()
        {
            if (state != EnemyAlertState.Attack) return;
            TriggerCombat();
        }

        public void NotifyAttackAnimationFinished()
        {
            if (state != EnemyAlertState.Attack) return;
            TransitionTo(EnemyAlertState.Alerted);
        }

        public void ResetToSpawn(Vector3 position, Quaternion rotation)
        {
            if (!isActiveAndEnabled) return;

            // A room activated for the first time this session cascades Awake()/OnEnable() on its
            // enemies synchronously, but Start() (where navAgent is normally assigned) is deferred
            // to the next frame — so navAgent can still be null here. GetComponent doesn't have that
            // restriction: the component already exists on the GameObject regardless of Start() timing.
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();

            navAgent.Warp(position);
            transform.rotation = rotation;

            combatTriggered = false;
            dialoguePaused  = false;

            TransitionTo(EnemyAlertState.Idle);
        }

        private void TriggerCombat()
        {
            if (sceneTransitionService == null) return;
            if (sceneTransitionService.IsInCombat) return;
            this.combatTriggered = true;
            sceneTransitionService.StartCombatAsync(this.encounterId, this.encounterData).Forget();
            gameObject.SetActive(false);
        }

        private void OnDialogueActiveChanged(DialogueActiveChangedEvent ev)
        {
            dialoguePaused = ev.IsActive;
            navAgent.isStopped = ev.IsActive || state is EnemyAlertState.Attack;
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            if (!combatTriggered) return;
            if (!ev.Victory) return;
            gameObject.SetActive(false);
            this.enemyStateRegistry?.SetDefeated(this.enemyKey);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            var pos       = transform.position;
            var eyeOrigin = eyePoint != null ? eyePoint.position : pos;

            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(pos, data.attackRange);

            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(pos, data.walkSoundRadius);

            Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
            Gizmos.DrawWireSphere(pos, data.runSoundRadius);

            DrawVisualFovGizmo(eyeOrigin, data.visualRange, data.visualFov);
        }

        private void DrawVisualFovGizmo(Vector3 origin, float range, float fov)
        {
            const int arcSegments = 24;

            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            var halfFov   = fov * 0.5f;
            var leftEdge  = Quaternion.Euler(0f, -halfFov, 0f) * transform.forward;
            var rightEdge = Quaternion.Euler(0f,  halfFov, 0f) * transform.forward;

            Gizmos.DrawLine(origin, origin + leftEdge  * range);
            Gizmos.DrawLine(origin, origin + rightEdge * range);

            var previous = origin + leftEdge * range;
            for (var i = 1; i <= arcSegments; i++)
            {
                var angle   = -halfFov + fov * (i / (float)arcSegments);
                var dir     = Quaternion.Euler(0f, angle, 0f) * transform.forward;
                var next    = origin + dir * range;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
```

Notes on what changed vs. the old file, for the implementer's own sanity-check: `path`/`patrolEnabled`/`suspiciousTurnSpeed`/`navPathCache`/`CanReachPlayer()`/`UpdatePatrol()`/`UpdateSuspicious()` are all gone; `sensor.ResetState()` is gone (Task 1 removed the method); the `[Tooltip]`-decorated `patrolEnabled`/`suspiciousTurnSpeed` serialized fields are gone from the Inspector — any scene/prefab data serialized against those fields is simply dropped by Unity on next save, which is harmless.

- [ ] **Step 5: Check the Unity console for compile errors**

Use `read_console` (or the Editor Console) filtered to Error.
Expected: no errors in `GameEvents.cs`, `NavigationScope.cs`, `NavigationEnemyData.cs`, `EnemyNavAgent.cs`. `EnemyAnimationReactor.cs` and `EnemyAttackHitbox.cs` don't exist/aren't updated yet — ignore any errors from those until Tasks 3–4 (there should be none yet, since Task 2 doesn't touch or reference them).

**Do not run `EnemyDetectionSensorTests` as a pass/fail gate for this task** — it isn't affected by this rewrite and already passed at the end of Task 1. This task has no automated test target of its own: `EnemyNavAgent` requires a real `NavMeshAgent` on a baked NavMesh plus DI-injected dependencies to run meaningfully, so it's verified manually in Play Mode at the very end (Task 9), matching this project's existing boundary for this kind of component (see the spec's Testing section).

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Infrastructure/Events/GameEvents.cs Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs
git commit -m "refactor(enemy-ai): rewrite EnemyNavAgent as Idle/Alerted/Attack FSM"
```

---

## Task 3: Reduce `EnemyAnimationReactor` to a pure reactor

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyAnimationReactor.cs`

**Interfaces:**
- Consumes: `EnemyNavAgent.State` (getter, from Task 2), `EnemyNavAgent.NotifyAttackAnimationFinished()` (from Task 2).
- Produces: nothing new consumed by later tasks.

- [ ] **Step 1: Rewrite `EnemyAnimationReactor.cs` in full**

Replace the entire file with:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.AI;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Traduce el estado de EnemyNavAgent y el movimiento del NavMeshAgent en los parámetros
    /// del Animator Controller de navegación del enemigo (Enemy_Nav_Controller):
    /// Idle / Walk01 / Walk02 (variedad al caminar), ZombieTwitch01 / 02 (interrupciones
    /// aleatorias del Idle), WalkEnemyClose (reacción cuando el jugador pasa cerca) y
    /// Attack (cuando EnemyNavAgent entra en el estado Attack).
    /// </summary>
    public sealed class EnemyAnimationReactor : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Transform del jugador. Si se deja vacío, se busca automáticamente por el tag \"Player\".")]
        [SerializeField] private Transform? player;
        [Tooltip("EnemyNavAgent dueño de este reactor. Si se deja vacío, se busca en un padre.")]
        [SerializeField] private EnemyNavAgent owner = null!;

        [Header("Proximidad — WalkEnemyClose")]
        [Tooltip("Distancia a la que el jugador es considerado \"cerca\" (dispara WalkEnemyClose).")]
        [SerializeField] private float closeRadius       = 4.5f;
        [Tooltip("Margen extra sobre closeRadius antes de desactivar IsClose, para evitar flickering en el borde.")]
        [SerializeField] private float closeRadiusBuffer = 0.5f;

        [Header("Locomoción")]
        [Tooltip("Velocidad mínima del NavMeshAgent para considerar que el enemigo está caminando.")]
        [SerializeField] private float movingSpeedThreshold = 0.05f;

        [Header("Twitch (Idle)")]
        [Tooltip("Rango de segundos entre interrupciones aleatorias del Idle con ZombieTwitch01/02.")]
        [SerializeField] private Vector2 twitchIntervalRange = new(4f, 9f);

        private static readonly int IsMovingHash    = Animator.StringToHash("IsMoving");
        private static readonly int WalkVariantHash = Animator.StringToHash("WalkVariant");
        private static readonly int Twitch1Hash     = Animator.StringToHash("Twitch1");
        private static readonly int Twitch2Hash     = Animator.StringToHash("Twitch2");
        private static readonly int IsCloseHash     = Animator.StringToHash("IsClose");
        private static readonly int AttackHash      = Animator.StringToHash("Attack");

        private Animator     animator = null!;
        private NavMeshAgent navAgent = null!;

        private bool  isCloseActive;
        private bool  wasMoving;
        private bool  wasPlayingAttack;
        private EnemyAlertState previousState;
        private float twitchTimer;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            navAgent = GetComponent<NavMeshAgent>();

            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (owner == null)
                owner = GetComponentInParent<EnemyNavAgent>();
        }

        private void Start()
        {
            twitchTimer = RandomTwitchInterval();
            previousState = owner.State;
        }

        private void Update()
        {
            if (player == null) return;

            UpdateProximity();
            UpdateLocomotion();
            UpdateTwitch();
            UpdateAttackTrigger();
            UpdateAttackFinishedEdge();
        }

        private void UpdateLocomotion()
        {
            var moving = navAgent.velocity.sqrMagnitude > movingSpeedThreshold * movingSpeedThreshold;

            if (moving && !wasMoving)
                animator.SetInteger(WalkVariantHash, Random.Range(0, 2)); // 0 = Walk01, 1 = Walk02

            wasMoving = moving;
            animator.SetBool(IsMovingHash, moving);
        }

        private void UpdateProximity()
        {
            var toPlayer = player!.position - transform.position;
            toPlayer.y = 0f;
            var distance = toPlayer.magnitude;

            // Hysteresis para no parpadear en el borde.
            if (!isCloseActive && distance < closeRadius)
                isCloseActive = true;
            else if (isCloseActive && distance > closeRadius + closeRadiusBuffer)
                isCloseActive = false;

            // Mientras ataca, no compite con Attack por la transición.
            var reportedClose = isCloseActive && owner.State != EnemyAlertState.Attack;

            animator.SetBool(IsCloseHash, reportedClose);
        }

        // Dispara el trigger Attack del Animator en el flanco de entrada al estado Attack
        // de EnemyNavAgent (única fuente de verdad sobre cuándo atacar).
        private void UpdateAttackTrigger()
        {
            if (owner.State == EnemyAlertState.Attack && previousState != EnemyAlertState.Attack)
                animator.SetTrigger(AttackHash);
            previousState = owner.State;
        }

        // "Current" se mantiene en Attack durante todo el crossfade de salida hacia Idle,
        // así que alcanza para cubrir la animación completa; el chequeo de transición cubre
        // además el instante justo en el que arranca el crossfade de entrada.
        private bool IsPlayingAttack()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack")) return true;
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Attack")) return true;
            return false;
        }

        // Notifica a EnemyNavAgent apenas la animación de ataque termina sin haber conectado
        // (si hubiera conectado, EnemyAttackHitbox ya sacó al enemigo del estado Attack).
        private void UpdateAttackFinishedEdge()
        {
            var playingAttack = IsPlayingAttack();
            if (wasPlayingAttack && !playingAttack && owner.State == EnemyAlertState.Attack)
                owner.NotifyAttackAnimationFinished();
            wasPlayingAttack = playingAttack;
        }

        private void UpdateTwitch()
        {
            // Solo dispara el twitch mientras el Animator está efectivamente en Idle:
            // evita que un trigger quede "en cola" y salte de golpe al volver a Idle
            // luego de caminar o de una reacción de proximidad.
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
            {
                twitchTimer = RandomTwitchInterval();
                return;
            }

            twitchTimer -= Time.deltaTime;
            if (twitchTimer > 0f) return;

            animator.SetTrigger(Random.value < 0.5f ? Twitch1Hash : Twitch2Hash);
            twitchTimer = RandomTwitchInterval();
        }

        private float RandomTwitchInterval()
            => Random.Range(twitchIntervalRange.x, twitchIntervalRange.y);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, closeRadius);
        }
#endif
    }
}
```

Notes: `attackRange`, `attackRangeBuffer`, `attackCooldown`, `isAttackRangeActive`, `attackTimer`, `stoppedForAttack`, `UpdateAttack()`, `UpdateAttackMovementLock()` are all gone — `EnemyNavAgent` now owns both the range decision and `navAgent.isStopped` during `Attack` (set in `TransitionTo`). The `[Header("Ataque")]` serialized fields disappear from the Inspector; any prefab data serialized against them is dropped harmlessly on next save. The purple `attackRange` gizmo circle is gone too — Task 2's `EnemyNavAgent.OnDrawGizmosSelected()` already draws `data.attackRange` in red.

- [ ] **Step 2: Check the Unity console for compile errors**

Use `read_console` (or the Editor Console) filtered to Error.
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyAnimationReactor.cs
git commit -m "refactor(enemy-ai): EnemyAnimationReactor reacts to EnemyNavAgent state instead of deciding"
```

---

## Task 4: Add the `EnemyAttackHitbox` component

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyAttackHitbox.cs`

**Interfaces:**
- Consumes: `EnemyNavAgent.NotifyAttackHit()` (from Task 2).
- Produces: `EnemyAttackHitbox.OnAttackHitboxOpen()`/`OnAttackHitboxClose()` — public parameterless methods, called by name from Animation Events in Task 8.

- [ ] **Step 1: Create the file**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyAttackHitbox : MonoBehaviour
    {
        [SerializeField] private EnemyNavAgent owner = null!;

        private Collider hitCollider = null!;

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
            hitCollider.enabled = false;
        }

        // Llamado por Animation Events en los frames activos del golpe.
        public void OnAttackHitboxOpen()  => hitCollider.enabled = true;
        public void OnAttackHitboxClose() => hitCollider.enabled = false;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            hitCollider.enabled = false; // un golpe por swing
            owner.NotifyAttackHit();
        }
    }
}
```

- [ ] **Step 2: Check the Unity console for compile errors**

Use `read_console` (or the Editor Console) filtered to Error.
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyAttackHitbox.cs
git commit -m "feat(enemy-ai): add EnemyAttackHitbox, the hand collider that starts combat on a landed hit"
```

---

## Task 5: Delete `EnemyPatrolPath.cs`

Now unreferenced — Task 2's rewrite of `EnemyNavAgent` dropped the `path` field, and nothing else in code references this type (confirmed by project-wide grep before writing this plan).

**Files:**
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs`
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs.meta`

**Interfaces:** none — nothing produced, nothing consumed.

- [ ] **Step 1: Delete both files**

```bash
git rm Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs.meta
```

- [ ] **Step 2: Check the Unity console for compile errors**

Use `read_console` filtered to Error.
Expected: no errors. If any appear referencing `EnemyPatrolPath`, stop — it means a reference was missed; re-grep the codebase for `EnemyPatrolPath` before continuing (this would indicate the pre-plan grep missed a file, e.g. an Editor-only script).

- [ ] **Step 3: Commit**

```bash
git commit -m "chore(enemy-ai): delete unused EnemyPatrolPath"
```

(Scene cleanup for the one scene that still has an `EnemyPatrolPath` component instance — `Navigation.unity` — happens in Task 7, since deleting the script here leaves that scene's GameObject with a "Missing Script" warning until then; that's expected and fixed in the very next scene-editing task.)

---

## Task 6: Delete `CombatTrigger.cs` and its DI wiring

**Files:**
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Combat/CombatTrigger.cs`
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Combat/CombatTrigger.cs.meta`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyBootstrap.cs`

**Interfaces:** none — nothing produced, nothing consumed.

- [ ] **Step 1: Delete the script and its meta file**

```bash
git rm Game/CrimsonDraft/Assets/Scripts/Navigation/Combat/CombatTrigger.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Combat/CombatTrigger.cs.meta
```

- [ ] **Step 2: Remove `CombatTrigger` references from `NavigationScope.cs`**

Delete the field:
```csharp
        [SerializeField] private CombatTrigger[]         cachedCombatTriggers = System.Array.Empty<CombatTrigger>();
```

Delete this line from `Configure(...)`:
```csharp
            builder.RegisterInstance(this.cachedCombatTriggers);
```

In the `[Button("Cache Scene Enemies")] CacheSceneEnemies()` method, delete:
```csharp
            this.cachedCombatTriggers = FindObjectsByType<CombatTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
```

`CombatTrigger` was the only type from `CrimsonDraft.Navigation.Combat` used in this file (confirmed: no other symbol from that namespace appears here), so also delete the now-dead `using CrimsonDraft.Navigation.Combat;` directive.

- [ ] **Step 3: Remove `CombatTrigger` references from `EnemyBootstrap.cs`**

Delete the constructor parameter `CombatTrigger[] triggers`, the field `private readonly CombatTrigger[] triggers;`, its assignment in the constructor, the `foreach (var trigger in this.triggers) { trigger.Construct(...); }` loop in `Initialize()`, and the `using CrimsonDraft.Navigation.Combat;` directive at the top (it has no other use in this file).

- [ ] **Step 4: Check the Unity console for compile errors**

Use `read_console` filtered to Error.
Expected: no errors. If anything references `CombatTrigger` or `cachedCombatTriggers`, stop and re-grep before continuing.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyBootstrap.cs
git commit -m "chore(enemy-ai): delete CombatTrigger, combat may no longer start from a bare collider"
```

(Scene/prefab cleanup for the 5 scenes + 1 prefab that still have `CombatTrigger` component instances happens in Task 7.)

---

## Task 7: Remove dangling `CombatTrigger`/`EnemyPatrolPath` components from scenes and prefabs

Deleting the scripts in Tasks 5–6 leaves every GameObject that had one of these components with a "Missing Script" (`{fileID: 0}`) reference in its scene/prefab YAML — Unity won't error on load, but the GameObject silently loses that behavior and the Inspector shows a broken component. This task removes those component entries directly. Do this through the Unity Editor (via UnityMCP tools), not by hand-editing the `.unity`/`.prefab` YAML — the file IDs and prefab-override bookkeeping are easy to corrupt by hand.

**Files (data, not code):**
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity` (has both `EnemyPatrolPath` and `CombatTrigger`)
- Modify: `Game/CrimsonDraft/Assets/Scenes/Test/FIX_Deck_B ShadersTest.unity` (`CombatTrigger`)
- Modify: `Game/CrimsonDraft/Assets/Scenes/Production/New Room.unity` (`CombatTrigger`)
- Modify: `Game/CrimsonDraft/Assets/Scenes/Deck B/New Room.unity` (`CombatTrigger`)
- Modify: `Game/CrimsonDraft/Assets/Scenes/Deck B/DeckB_Port_Stairs.unity` (`CombatTrigger`)
- Modify: `Game/CrimsonDraft/Assets/Prefabs/Core/__GAMEPLAYCORE.prefab` (`CombatTrigger`)

**Interfaces:** none.

- [ ] **Step 1: Re-confirm the current list of affected files**

Before editing anything, re-run a project-wide search for both component names to catch any scene/prefab created or changed since the spec was written:
```bash
grep -rl "CombatTrigger" "Game/CrimsonDraft/Assets/Scenes" "Game/CrimsonDraft/Assets/Prefabs"
grep -rl "EnemyPatrolPath" "Game/CrimsonDraft/Assets/Scenes" "Game/CrimsonDraft/Assets/Prefabs"
```
Expected: the same 6 files listed above (5 for `CombatTrigger`, 1 for `EnemyPatrolPath`). If the list differs, treat the new list as authoritative for the remaining steps.

- [ ] **Step 2: For each affected scene, open it and remove the component(s)**

For each of the 5 `CombatTrigger` scenes and the 1 `EnemyPatrolPath` scene:
1. Use `manage_scene` (open) to load the scene in the Editor.
2. Use `find_gameobjects` searching for the component type (`CombatTrigger` or `EnemyPatrolPath`) to locate the exact GameObject(s).
3. Use `manage_components` (remove) to remove that component instance from each GameObject found. For `EnemyPatrolPath`, also remove its waypoint child `Transform`s if they have no other purpose (check with `find_gameobjects`/hierarchy inspection first — don't delete a child that's reused for something else).
4. Use `manage_scene` (save) to save the scene.

- [ ] **Step 3: Remove the component from the prefab**

For `__GAMEPLAYCORE.prefab`:
1. Use `manage_prefabs` (open for edit, or the equivalent isolated-edit-mode entry point exposed by the tool) to open the prefab.
2. Use `find_gameobjects`/`manage_components` the same way as Step 2 to locate and remove the `CombatTrigger` component.
3. Save the prefab.

- [ ] **Step 4: Verify no missing-script warnings remain**

Use `read_console` filtered to Warning/Error after reloading each modified scene (and any scene that instantiates `__GAMEPLAYCORE.prefab`).
Expected: no "missing script"/"missing MonoBehaviour" messages.

- [ ] **Step 5: Re-run the project-wide search to confirm the components are gone**

```bash
grep -rl "CombatTrigger" "Game/CrimsonDraft/Assets/Scenes" "Game/CrimsonDraft/Assets/Prefabs"
grep -rl "EnemyPatrolPath" "Game/CrimsonDraft/Assets/Scenes" "Game/CrimsonDraft/Assets/Prefabs"
```
Expected: no output from either command.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scenes Game/CrimsonDraft/Assets/Prefabs/Core/__GAMEPLAYCORE.prefab
git commit -m "chore(enemy-ai): remove dangling CombatTrigger/EnemyPatrolPath component instances from scenes/prefab"
```

---

## Task 8: Wire `EnemyAttackHitbox` onto the zombie prefab and add Animation Events

**Files (data, not code):**
- Modify: `Game/CrimsonDraft/Assets/Prefabs/Enemies/EE_00.prefab` (the zombie prefab — has `EnemyNavAgent`, `EnemyDetectionSensor`, `EnemyAnimationReactor`, `Animator`, and bones including `Hand.L`/`Hand.R`)
- Modify: the Attack animation clip used by `Game/CrimsonDraft/Assets/Animations/Enemy_Nav_Controller.controller`'s `Attack` state (the clip is embedded in the zombie's source FBX — locate it via the Animator Controller's `Attack` state's Motion field in the Editor, or via `manage_animation`)

**Interfaces:** none new — this wires up `EnemyAttackHitbox.OnAttackHitboxOpen()`/`OnAttackHitboxClose()` (from Task 4) as Animation Event targets, and sets the `owner` field to the prefab's existing `EnemyNavAgent`.

- [ ] **Step 1: Determine which hand actually swings in the Attack clip**

Use the Unity Editor's Animation preview window (or `manage_animation`) to scrub the `Attack` clip on `EE_00`. Identify whether `Hand.L` or `Hand.R` (or both) is the striking limb — the plan can't determine this without watching the clip play.

- [ ] **Step 2: Add the hitbox GameObject**

Using `manage_gameobject`/`manage_components` on the opened `EE_00.prefab`:
1. Create a new child GameObject under the striking hand bone identified in Step 1 (e.g. `Hand.R`), named `AttackHitbox`.
2. Add a `Collider` sized to roughly cover the hand/claw (a `SphereCollider` with a small radius is simplest given an irregular hand mesh) and set `Is Trigger` to true.
3. Add the `EnemyAttackHitbox` component to this new GameObject.
4. Set its `owner` field to the prefab root's `EnemyNavAgent` component.
5. Make sure the GameObject's layer is one that can physically overlap the player's collider (match whatever layer `data.targetMask`/the player's own trigger volumes already use elsewhere in this prefab, so `OnTriggerEnter` actually fires — check the existing `EnemyDetectionSensor`/`PlayerAimController` layer setup if unsure).

- [ ] **Step 3: Add Animation Events to the Attack clip**

In the Attack clip's Inspector (Animation window, Events track), scrub to the frame where the swing's active/contact window starts and add an event calling `OnAttackHitboxOpen` (no parameters); scrub to the frame where the contact window ends (well before the recovery/idle-return frames) and add an event calling `OnAttackHitboxClose`.

- [ ] **Step 4: Save the prefab**

Use `manage_prefabs`/`manage_scene` to save changes back to `EE_00.prefab` (and the FBX's animation import settings, if the events were added there rather than on a standalone `.anim` asset).

- [ ] **Step 5: Verify in Play Mode**

Enter Play Mode with a scene containing an `EE_00` instance (e.g. `Navigation.unity`), let it reach `Attack`, and confirm via `read_console` or a temporary `Debug.Log` in `OnAttackHitboxOpen`/`OnAttackHitboxClose` that both fire once per swing at the expected moments. Remove any temporary logging before committing.

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Prefabs/Enemies/EE_00.prefab Game/CrimsonDraft/Assets/Art/Models/CharactersFBX/Zombie
git commit -m "feat(enemy-ai): wire EnemyAttackHitbox onto the zombie's attack swing"
```

---

## Task 9: Final verification

**Files:** none modified — this task only runs checks.

- [ ] **Step 1: Run the full EditMode test suite**

Use the MCP `run_tests` tool (or Unity Test Runner) with no filter.
Expected: all tests pass, including the 6 `EnemyDetectionSensorTests` from Task 1 and every other pre-existing EditMode test in the project (none of which this plan's tasks touch).

- [ ] **Step 2: Check the Unity console one last time**

Use `read_console` filtered to Error and Warning across a full domain reload.
Expected: no compile errors, no missing-script warnings.

- [ ] **Step 3: Manual Play Mode QA — locomotion and detection**

In a scene with a zombie (e.g. `Navigation.unity`):
1. Stand still near an `Idle` zombie without moving — confirm it does **not** detect you (no proximity-only trigger).
2. Approach from an angle so the zombie has to turn to face you once alerted — confirm it stops and turns in place first, then walks, with no sideways sliding.
3. Walk past a zombie's flank while it's `Alerted` and chasing — confirm it turns in place rather than sliding diagonally.

- [ ] **Step 4: Manual Play Mode QA — attack and combat start**

1. Let a zombie close to attack range — confirm it stops and plays the Attack animation, and that combat starts only when the hand hitbox actually lands (watch for the `OnAttackHitboxOpen`/`Close` window from Task 8).
2. Retreat out of range mid-swing — confirm the swing finishes, no combat starts, and the zombie resumes chasing (`Alerted`) afterward instead of the swing being cancelled or still landing.
3. Confirm none of the 5 former `CombatTrigger` locations start combat on touch anymore.
4. Confirm `PlayerAimController`'s shoot-to-preempt flow (aim at a spotted zombie and fire) still starts combat correctly and is unaffected by this rework.

- [ ] **Step 5: Manual Play Mode QA — multi-enemy and room reset**

1. With two zombies both alerted and chasing, let only one reach attack range and land a hit — confirm only that zombie's GameObject deactivates/starts combat, and the other keeps chasing independently right up until the scene transitions into combat.
2. Lure a zombie away from its spawn point, leave the room, and re-enter — confirm it resets to `Idle` at its original spawn transform (per `RoomController.Activate()` → `EnemyNavAgent.ResetToSpawn()`).

- [ ] **Step 6: Sanity-check the tuning values on the live data asset**

Open `NavigationEnemyData_Infected.asset` (and `NavigationEnemyData_Infected_Initial.asset` if it's a separate instance) in the Inspector and confirm `chaseSpeed`, `turnSpeed`, `turnInPlaceThreshold`, `attackRange`, and `attackRangeBuffer` all show sensible values (the field swap in Task 2 preserves values for fields that kept their name, like `chaseSpeed`, but any Inspector value previously set for now-removed fields like `catchRadius` is gone — the new fields start at their C# defaults until a designer retunes them). Adjust if the Step 3–5 QA revealed anything feels off (e.g. `turnInPlaceThreshold` too strict/loose).

No commit for this task — it's verification only. If Step 6 leads to a tuning change on the `.asset` file, commit that separately with a message describing the tuning change, not as part of this plan's task list.
