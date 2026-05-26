# Enemy Navigation AI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** Implements [[Sistema de IA de Navegacion]] — see `Design/GDD/Sistema de IA de Navegacion.md`

**Goal:** Enemies patrol rooms, detect the player by proximity/sound/sight, and trigger combat when they catch them — faithful to classic RE1-RE3 room-scoped AI.

**Architecture:** Four new scripts under `Scripts/Navigation/Enemy/`: a data ScriptableObject, a patrol path helper, a detection sensor, and the main state machine agent. NavigationScope gets two small additions. No new assembly definitions needed — `CrimsonDraft.Navigation.asmdef` already covers NavMesh (built-in Unity module) and all required DI/event packages.

**Tech Stack:** Unity NavMeshAgent (built-in AI module), VContainer `[Inject]`, MessagePipe `IPublisher`/`ISubscriber`, UniTask `Forget()`, NUnit for EditMode tests.

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs` | ScriptableObject: all detection + movement parameters |
| Create | `Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs` | Waypoint list + cursor for patrol |
| Create | `Assets/Scripts/Navigation/Enemy/EnemyDetectionSensor.cs` | Proximity/sound/sight evaluation; stateful hysteresis |
| Create | `Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs` | State machine (Patrol/Suspicious/Alert) + DI + combat trigger |
| Create | `Assets/Tests/EditMode/EnemyDetectionSensorTests.cs` | EditMode unit tests for sensor logic |
| Modify | `Assets/Scripts/Navigation/NavigationScope.cs` | Register `GuardAlertChangedEvent` broker + all `EnemyNavAgent` instances |

All paths are relative to `Game/CrimsonDraft/`.

---

## Task 1: NavigationEnemyData

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs`

- [ ] **Step 1.1 — Create the ScriptableObject**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    [CreateAssetMenu(fileName = "NavigationEnemyData", menuName = "CrimsonDraft/Navigation Enemy Data")]
    public sealed class NavigationEnemyData : ScriptableObject
    {
        [Header("Combat")]
        public string encounterId = string.Empty;

        [Header("Movement")]
        public float patrolSpeed          = 2.0f;
        public float chaseSpeed           = 3.5f;
        public float waypointStopDistance = 0.3f;
        public float catchRadius          = 0.8f;

        [Header("Proximity Detection")]
        public float detectRadius   = 1.8f;
        public float undetectRadius = 2.4f;

        [Header("Sound Detection")]
        public float playerDeadzone     = 0.1f;
        public float playerRunThreshold = 5.5f;
        public float walkSoundRadius    = 3.5f;
        public float runSoundRadius     = 9.0f;

        [Header("Visual Detection")]
        public float     visualRange     = 7.0f;
        public float     visualFov       = 110f;
        public LayerMask obstructionMask;
        public LayerMask targetMask;

        [Header("Suspicious State")]
        public bool  suspiciousEnabled  = false;
        public float suspiciousDuration = 2.0f;
    }
}
```

- [ ] **Step 1.2 — Verify compilation**

In Unity MCP: `read_console` — expect no errors. If you see "The type or namespace name 'NavigationEnemyData' could not be found", check that the file is inside `Assets/Scripts/Navigation/Enemy/` (which is within the `CrimsonDraft.Navigation` assembly root).

- [ ] **Step 1.3 — Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/NavigationEnemyData.cs"
git commit -m "feat(navigation): add NavigationEnemyData ScriptableObject"
```

---

## Task 2: EnemyPatrolPath

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs`

- [ ] **Step 2.1 — Create the component**

```csharp
#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyPatrolPath : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();

        private int index = 0;

        public bool HasWaypoints => waypoints.Length > 0;
        public Transform Current  => waypoints[index];

        public void Advance() => index = (index + 1) % waypoints.Length;
    }
}
```

- [ ] **Step 2.2 — Verify compilation**

`read_console` — no errors expected.

- [ ] **Step 2.3 — Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs"
git commit -m "feat(navigation): add EnemyPatrolPath waypoint component"
```

---

## Task 3: EnemyDetectionSensor + Tests

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyDetectionSensor.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyDetectionSensorTests.cs`

- [ ] **Step 3.1 — Write the failing tests first**

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
            float detectRadius   = 2.0f,
            float undetectRadius = 3.0f,
            float walkRadius     = 0f,
            float runRadius      = 0f,
            float visualRange    = 0f)
        {
            var data = ScriptableObject.CreateInstance<NavigationEnemyData>();
            data.detectRadius   = detectRadius;
            data.undetectRadius = undetectRadius;
            // Disable sound and visual by default to isolate proximity tests
            data.walkSoundRadius    = walkRadius;
            data.runSoundRadius     = runRadius;
            data.playerDeadzone     = 0.1f;
            data.playerRunThreshold = 5.5f;
            data.visualRange        = visualRange;
            return data;
        }

        [Test]
        public void Proximity_DetectsWhenInsideDetectRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(1f, 0f, 0f); // inside detectRadius=2
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData();

            Assert.IsTrue(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Proximity_NoDetectionOutsideUndetectRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(5f, 0f, 0f); // outside undetectRadius=3
            var playerRb = playerGO.AddComponent<Rigidbody>();

            var data = MakeData();

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Proximity_Hysteresis_StaysActiveInZoneBetweenRadii()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            var playerRb = playerGO.AddComponent<Rigidbody>();
            var data = MakeData();

            // Enter detect zone
            playerGO.transform.position = new Vector3(1f, 0f, 0f);
            sensor.Evaluate(data, playerGO.transform, playerRb, null); // activates

            // Move to hysteresis zone (between 2 and 3)
            playerGO.transform.position = new Vector3(2.5f, 0f, 0f);
            bool inHysteresis = sensor.Evaluate(data, playerGO.transform, playerRb, null);

            Assert.IsTrue(inHysteresis, "Should remain detected in hysteresis zone");

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Proximity_Hysteresis_DeactivatesOnceOutsideUndetectRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            var playerRb = playerGO.AddComponent<Rigidbody>();
            var data = MakeData();

            // Enter and exit fully
            playerGO.transform.position = new Vector3(1f, 0f, 0f);
            sensor.Evaluate(data, playerGO.transform, playerRb, null);

            playerGO.transform.position = new Vector3(5f, 0f, 0f); // outside undetect=3
            bool afterExit = sensor.Evaluate(data, playerGO.transform, playerRb, null);

            Assert.IsFalse(afterExit, "Should lose detection after exiting undetect radius");

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Sound_DetectsWalkingPlayerWithinWalkRadius()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            playerGO.transform.position = new Vector3(3f, 0f, 0f); // inside walkRadius=5, outside proximity
            var playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.linearVelocity = new Vector3(4f, 0f, 0f); // walk speed (4 < runThreshold 5.5)

            var data = MakeData(detectRadius: 1f, undetectRadius: 1.5f, walkRadius: 5f, runRadius: 9f);

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

            var data = MakeData(detectRadius: 1f, undetectRadius: 1.5f, walkRadius: 5f, runRadius: 9f);

            Assert.IsFalse(sensor.Evaluate(data, playerGO.transform, playerRb, null));

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void ResetState_ClearsProximityHysteresis()
        {
            var sensorGO = new GameObject();
            sensorGO.transform.position = Vector3.zero;
            var sensor = sensorGO.AddComponent<EnemyDetectionSensor>();

            var playerGO = new GameObject();
            var playerRb = playerGO.AddComponent<Rigidbody>();
            var data = MakeData();

            // Activate proximity
            playerGO.transform.position = new Vector3(1f, 0f, 0f);
            sensor.Evaluate(data, playerGO.transform, playerRb, null);

            // Reset, then move to hysteresis zone
            sensor.ResetState();
            playerGO.transform.position = new Vector3(2.5f, 0f, 0f);
            bool afterReset = sensor.Evaluate(data, playerGO.transform, playerRb, null);

            Assert.IsFalse(afterReset, "After ResetState, hysteresis zone should not detect");

            Object.DestroyImmediate(sensorGO);
            Object.DestroyImmediate(playerGO);
            Object.DestroyImmediate(data);
        }
    }
}
```

- [ ] **Step 3.2 — Run tests to verify they fail**

In Unity: Window → General → Test Runner → EditMode → Run All
Expected: 6 failures with "EnemyDetectionSensor" not found.

- [ ] **Step 3.3 — Implement EnemyDetectionSensor**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyDetectionSensor : MonoBehaviour
    {
        private bool proximityActive = false;

        public bool Evaluate(NavigationEnemyData data, Transform player, Rigidbody playerRb, Transform? eyePoint)
        {
            var playerPos = player.position;
            var distance  = Vector3.Distance(transform.position, playerPos);

            // 1. Proximity with hysteresis (omnidirectional, from sensor origin)
            if (!proximityActive && distance < data.detectRadius)
                proximityActive = true;
            else if (proximityActive && distance > data.undetectRadius)
                proximityActive = false;

            if (proximityActive) return true;

            // 2. Sound detection (distance from sensor origin to player)
            var speed = playerRb.linearVelocity.magnitude;
            if (speed > data.playerDeadzone)
            {
                var soundRadius = speed > data.playerRunThreshold
                    ? data.runSoundRadius
                    : data.walkSoundRadius;
                if (distance < soundRadius) return true;
            }

            // 3. Visual detection — 2-pass raycast from eye point
            if (distance < data.visualRange)
            {
                var origin      = eyePoint != null ? eyePoint.position : transform.position;
                var dirToPlayer = (playerPos - origin).normalized;
                var angle       = Vector3.Angle(transform.forward, dirToPlayer);

                if (angle < data.visualFov * 0.5f)
                {
                    var eyeDist = Vector3.Distance(origin, playerPos);
                    // Pass 1: is there an obstruction between eye and player?
                    if (!Physics.Raycast(origin, dirToPlayer, eyeDist, data.obstructionMask))
                    {
                        // Pass 2: is the player's collider on the target layer?
                        if (Physics.Raycast(origin, dirToPlayer, eyeDist, data.targetMask))
                            return true;
                    }
                }
            }

            return false;
        }

        public void ResetState()
        {
            proximityActive = false;
        }
    }
}
```

- [ ] **Step 3.4 — Run tests to verify they pass**

Window → General → Test Runner → EditMode → Run All
Expected: 6 tests pass. `read_console` — no compilation errors.

- [ ] **Step 3.5 — Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyDetectionSensor.cs" \
        "Game/CrimsonDraft/Assets/Tests/EditMode/EnemyDetectionSensorTests.cs"
git commit -m "feat(navigation): add EnemyDetectionSensor with proximity/sound/sight"
```

---

## Task 4: EnemyNavAgent

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs`

This is the main state machine. Requires `NavMeshAgent` component on the same GameObject.

- [ ] **Step 4.1 — Create EnemyNavAgent**

```csharp
#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyNavAgent : MonoBehaviour
    {
        [SerializeField] private NavigationEnemyData  data      = null!;
        [SerializeField] private EnemyPatrolPath      path      = null!;
        [SerializeField] private EnemyDetectionSensor sensor    = null!;
        [SerializeField] private Transform?           eyePoint;

        private ISceneTransitionService?               sceneTransitionService;
        private ISubscriber<CombatEndedEvent>?         combatEndedSubscriber;
        private IEncounterContext?                     encounterContext;
        private IPublisher<GuardAlertChangedEvent>?    guardAlertPublisher;
        private PlayerController?                      playerController;

        private NavMeshAgent     navAgent        = null!;
        private Rigidbody        playerRb        = null!;
        private GuardAlertState  state           = GuardAlertState.Patrol;
        private float            suspiciousTimer;
        private IDisposable?     combatEndedSub;

        [Inject]
        public void Construct(
            ISceneTransitionService            sceneTransitionService,
            ISubscriber<CombatEndedEvent>      combatEndedSubscriber,
            IEncounterContext                  encounterContext,
            IPublisher<GuardAlertChangedEvent> guardAlertPublisher,
            PlayerController                  playerController)
        {
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.encounterContext       = encounterContext;
            this.guardAlertPublisher    = guardAlertPublisher;
            this.playerController       = playerController;
        }

        private void Start()
        {
            navAgent = GetComponent<NavMeshAgent>();
            playerRb = playerController!.GetComponent<Rigidbody>();
            navAgent.speed = data.patrolSpeed;

            if (path.HasWaypoints)
                navAgent.SetDestination(path.Current.position);

            combatEndedSub = combatEndedSubscriber?.Subscribe(OnCombatEnded);
        }

        private void OnDestroy()
        {
            combatEndedSub?.Dispose();
        }

        private void Update()
        {
            if (playerController == null) return;

            switch (state)
            {
                case GuardAlertState.Patrol:     UpdatePatrol();     break;
                case GuardAlertState.Suspicious: UpdateSuspicious(); break;
                case GuardAlertState.Alert:      UpdateAlert();      break;
            }
        }

        private void UpdatePatrol()
        {
            if (path.HasWaypoints
                && !navAgent.pathPending
                && navAgent.hasPath
                && navAgent.remainingDistance < data.waypointStopDistance)
            {
                path.Advance();
                navAgent.SetDestination(path.Current.position);
            }

            if (!Detect()) return;

            if (data.suspiciousEnabled)
                TransitionTo(GuardAlertState.Suspicious);
            else if (CanReachPlayer())
                TransitionTo(GuardAlertState.Alert);
        }

        private void UpdateSuspicious()
        {
            var dir = (playerController!.transform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);

            suspiciousTimer -= Time.deltaTime;

            if (Detect() && CanReachPlayer())
            {
                TransitionTo(GuardAlertState.Alert);
                return;
            }

            if (suspiciousTimer <= 0f)
                TransitionTo(GuardAlertState.Patrol);
        }

        private void UpdateAlert()
        {
            navAgent.SetDestination(playerController!.transform.position);

            var distToPlayer = Vector3.Distance(transform.position, playerController.transform.position);
            if (distToPlayer < data.catchRadius)
                TriggerCombat();
        }

        private bool Detect()
            => sensor.Evaluate(data, playerController!.transform, playerRb, eyePoint);

        private bool CanReachPlayer()
        {
            var navPath = new NavMeshPath();
            NavMesh.CalculatePath(
                transform.position,
                playerController!.transform.position,
                NavMesh.AllAreas,
                navPath);
            return navPath.status == NavMeshPathStatus.PathComplete;
        }

        private void TransitionTo(GuardAlertState next)
        {
            var prev = state;
            state = next;

            guardAlertPublisher?.Publish(new GuardAlertChangedEvent
            {
                GuardId       = gameObject.name,
                PreviousState = prev,
                NewState      = next,
            });

            switch (next)
            {
                case GuardAlertState.Patrol:
                    navAgent.speed = data.patrolSpeed;
                    sensor.ResetState();
                    if (path.HasWaypoints)
                        navAgent.SetDestination(path.Current.position);
                    break;

                case GuardAlertState.Suspicious:
                    navAgent.ResetPath();
                    suspiciousTimer = data.suspiciousDuration;
                    break;

                case GuardAlertState.Alert:
                    navAgent.speed = data.chaseSpeed;
                    break;
            }
        }

        private void TriggerCombat()
        {
            if (sceneTransitionService == null) return;
            if (sceneTransitionService.IsInCombat) return;
            sceneTransitionService.StartCombatAsync(data.encounterId).Forget();
            gameObject.SetActive(false);
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            if (!ev.Victory) return;
            if (encounterContext?.CurrentEncounterId != data.encounterId) return;
            gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 4.2 — Verify compilation**

`read_console` — no errors. If `UnityEngine.AI` namespace is not found: this is unexpected since `noEngineReferences: false` in the asmdef. Check that the AI module is enabled in Project Settings → Player → Other Settings → Script Compilation.

- [ ] **Step 4.3 — Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs"
git commit -m "feat(navigation): add EnemyNavAgent state machine (Patrol/Suspicious/Alert)"
```

---

## Task 5: NavigationScope Registration

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

Two additions: register the `GuardAlertChangedEvent` MessagePipe broker (reusing parent `msgOptions`), and register all `EnemyNavAgent` instances with the same foreach pattern as `CombatTrigger`.

- [ ] **Step 5.1 — Add using**

At the top of `NavigationScope.cs`, add inside the existing usings block:

```csharp
using CrimsonDraft.Navigation.Enemy;
```

- [ ] **Step 5.2 — Add broker + agent registration**

Find this existing block (lines 64–70 in the current file):

```csharp
            var msgOptions = Parent!.Container.Resolve<MessagePipeOptions>();
            builder.RegisterMessageBroker<RoomTransitionStartedEvent>(msgOptions);
            builder.RegisterMessageBroker<RoomTransitionedEvent>(msgOptions);
```

Add the `GuardAlertChangedEvent` broker immediately after `RoomTransitionedEvent`:

```csharp
            var msgOptions = Parent!.Container.Resolve<MessagePipeOptions>();
            builder.RegisterMessageBroker<RoomTransitionStartedEvent>(msgOptions);
            builder.RegisterMessageBroker<RoomTransitionedEvent>(msgOptions);
            builder.RegisterMessageBroker<GuardAlertChangedEvent>(msgOptions);
```

Then find the foreach block for `CombatTrigger` (around line 41):

```csharp
            foreach (var trigger in FindObjectsByType<CombatTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(trigger);
```

Add an identical foreach for `EnemyNavAgent` immediately after:

```csharp
            foreach (var trigger in FindObjectsByType<CombatTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(trigger);
            foreach (var agent in FindObjectsByType<EnemyNavAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(agent);
```

- [ ] **Step 5.3 — Verify compilation**

`read_console` — no errors. Check specifically that `GuardAlertChangedEvent` resolves (it lives in `CrimsonDraft.Infrastructure.Events`, already imported via `CrimsonDraft.Infrastructure` asmdef reference).

- [ ] **Step 5.4 — Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git commit -m "feat(navigation): register EnemyNavAgent and GuardAlertChangedEvent in NavigationScope"
```

---

## Task 6: Scene Setup + Manual Playtest

**Files:**
- Asset (create via Unity Inspector): `Assets/Data/Enemies/NavigationEnemyData_Infected.asset`
- Scene: `Assets/Scenes/Production/Navigation.unity`

- [ ] **Step 6.1 — Bake NavMesh**

In Unity: open Navigation scene → Window → AI → Navigation → Agents tab, confirm a Default agent (radius ~0.35, height ~1.8).

Select all floor geometry in the scene → Inspector → Static dropdown → check **Navigation Static**.

Bake tab → click **Bake**. The blue NavMesh overlay should appear over all walkable floors.

- [ ] **Step 6.2 — Create NavigationEnemyData asset**

Right-click in Project window → Create → CrimsonDraft → Navigation Enemy Data → name it `NavigationEnemyData_Infected`.

Assign an existing `encounterId` from `EncounterDatabase` (check `Assets/Data/Encounters/` for a valid ID).

Configure:
- `obstructionMask`: select **Wall** layer (or whichever layer room walls are on)
- `targetMask`: select **Player** layer (or whichever layer the player collider is on)
- Leave all other values at their defaults

- [ ] **Step 6.3 — Create enemy GameObject in a room**

In the Navigation scene Hierarchy, expand a `RoomController` GameObject.

Create a child empty GameObject → name it `Enemy_Infected_01`.

Add components:
1. `NavMeshAgent` — set Radius=0.35, Height=1.8, Speed=2 (will be overridden at runtime), Stopping Distance=0
2. `Rigidbody` — Is Kinematic = **true**
3. `EnemyDetectionSensor`
4. `EnemyNavAgent` — assign `data = NavigationEnemyData_Infected`, assign `sensor` = the EnemyDetectionSensor on this GameObject

Create a sibling empty → name it `PatrolPoints`. Add `EnemyPatrolPath` component. Create 3 child empty Transforms named `WP_0`, `WP_1`, `WP_2`. Position them around the room floor. Assign them in the `EnemyPatrolPath.waypoints` array.

Assign `path = PatrolPoints` on the `EnemyNavAgent`.

- [ ] **Step 6.4 — Enter Play mode and test detection**

Expected behaviors:

| Scenario | Expected result |
|---|---|
| Player idle far away (> 9u) | Enemy patrols waypoints, no reaction |
| Player walks within 3.5u | Enemy transitions to ALERT, pursues |
| Player runs within 9u | Enemy transitions to ALERT, pursues |
| Player enters visual cone with clear LOS | Enemy transitions to ALERT, pursues |
| Player steps behind cover breaking LOS | Visual detection blocked (enemy stays PATROL if no sound) |
| Enemy reaches player (< 0.8u) | Combat scene loads |
| Win combat | Return to Navigation scene — enemy gone |

- [ ] **Step 6.5 — Verify console**

`read_console` — no errors during play mode. Common issues:

- `VContainer: EnemyNavAgent could not be resolved` → means the agent wasn't in the scene when NavigationScope configured. Check that the enemy is inside an **active** (not deactivated) room at scene start, or that `FindObjectsInactive.Include` picks it up.
- `NavMesh.CalculatePath: start/end not on NavMesh` → the enemy or player is not on the baked NavMesh. Move them onto a NavMesh-static floor.
- `NullReferenceException in EnemyNavAgent.Start` → navAgent, playerController, or data is null. Check that `NavMeshAgent` component exists and that VContainer injected `PlayerController`.

- [ ] **Step 6.6 — Commit scene**

```bash
git add "Game/CrimsonDraft/Assets/Scenes/Production/Navigation.unity" \
        "Game/CrimsonDraft/Assets/Data/Enemies/"
git commit -m "feat(navigation): place first enemy in Navigation scene with NavMesh"
```

---

## Self-Review

**Spec coverage check:**

| GDD requirement | Task |
|---|---|
| Patrol state (waypoints, NavMeshAgent) | Task 4 (UpdatePatrol) |
| Suspicious state (optional, suspiciousEnabled flag) | Task 4 (UpdateSuspicious) |
| Alert state (follow current position, no de-aggro) | Task 4 (UpdateAlert) |
| Proximity detection with hysteresis | Task 3 (EnemyDetectionSensor) |
| Sound detection (walk vs run radii) | Task 3 (EnemyDetectionSensor) |
| Visual detection (2-pass raycast) | Task 3 (EnemyDetectionSensor) |
| Reachability check before ALERT | Task 4 (CanReachPlayer) |
| NavigationEnemyData SO with all params | Task 1 |
| EnemyPatrolPath waypoints | Task 2 |
| NavigationScope registration | Task 5 |
| GuardAlertChangedEvent broker | Task 5 |
| Enemy deactivates with room | Task 6 (room hierarchy) |
| Combat trigger pattern | Task 4 (TriggerCombat) |
| CombatEnded → deactivate enemy | Task 4 (OnCombatEnded) |

No gaps found.

**Placeholder scan:** No TBD, TODO, or "similar to Task N" — all code is written out completely.

**Type consistency:**
- `EnemyDetectionSensor.Evaluate(NavigationEnemyData, Transform, Rigidbody, Transform?)` — called identically in Task 3 (tests) and Task 4 (EnemyNavAgent)
- `EnemyDetectionSensor.ResetState()` — called in tests (Task 3) and TransitionTo (Task 4)
- `EnemyPatrolPath.HasWaypoints`, `.Current`, `.Advance()` — defined in Task 2, used in Task 4
- `GuardAlertState.Patrol/Suspicious/Alert` — enum from `CrimsonDraft.Infrastructure.Events`, used consistently throughout
