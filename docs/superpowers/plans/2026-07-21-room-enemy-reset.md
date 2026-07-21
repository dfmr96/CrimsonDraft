# Room Enemy Position Reset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every enemy in a room snaps back to a designer-authored spawn position/rotation whenever the room becomes active (first entry or door re-entry), so luring an enemy to the door and leaving-then-returning can no longer leave it parked somewhere that immediately triggers combat.

**Architecture:** `RoomController.Activate()` is the single choke point already called on both initial room selection and door transitions. It gains a serialized `(EnemyNavAgent, Transform)` pair array, populated via an editor-only "Cache Room Enemies" button that snapshots each child enemy's current transform. On `Activate()`, every still-alive cached enemy is told to reset itself via a new `EnemyNavAgent.ResetToSpawn(position, rotation)` method, which owns all the actual reset logic (NavMeshAgent warp, patrol index, transient AI flags, state transition back to Patrol).

**Tech Stack:** Unity C#, NavMeshAgent, NaughtyAttributes (`[Button]`), NUnit EditMode tests.

## Global Constraints

- `#nullable enable` at the top of every touched file (already present in all three).
- Match the project's existing `[Button]`-based "cache children into a serialized array" convention (see `NavigationScope.CacheSceneEnemies`), including `#if UNITY_EDITOR` wrapping and `UnityEditor.EditorUtility.SetDirty(this)`.
- No `Co-Authored-By` trailers in commit messages (project convention, `CLAUDE.md`).
- Tests run via Unity Test Runner / UnityMCP `run_tests` — there is no CLI test command in this project.
- Per the approved spec, the full `RoomController.Activate() -> EnemyNavAgent.ResetToSpawn()` integration has no automated test (matches the existing boundary for `MonoBehaviour`s this coupled to `NavMeshAgent`/`Update()`, e.g. `CombatOrchestrator`) — verified manually in Play Mode instead.

---

## Task 1: `EnemyPatrolPath.ResetIndex()`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyPatrolPathTests.cs` (new file)

**Interfaces:**
- Produces: `EnemyPatrolPath.ResetIndex() : void` — resets the internal waypoint index back to 0, so the next `Current` read returns the first waypoint.

- [ ] **Step 1: Write the failing test**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/EnemyPatrolPathTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyPatrolPathTests
    {
        private static EnemyPatrolPath BuildPathWithWaypoints(int count)
        {
            var go   = new GameObject();
            var path = go.AddComponent<EnemyPatrolPath>();

            var waypoints = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var wpGo = new GameObject($"Waypoint{i}");
                wpGo.transform.position = new Vector3(i, 0f, 0f);
                waypoints[i] = wpGo.transform;
            }

            var so = new UnityEditor.SerializedObject(path);
            var arrayProp = so.FindProperty("waypoints");
            arrayProp.arraySize = count;
            for (int i = 0; i < count; i++)
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return path;
        }

        [Test]
        public void ResetIndex_afterAdvancing_returnsCurrentToFirstWaypoint()
        {
            var path = BuildPathWithWaypoints(3);
            path.Advance();
            path.Advance();
            Assert.AreEqual(2f, path.Current.position.x); // sanity: advanced to waypoint 2

            path.ResetIndex();

            Assert.AreEqual(0f, path.Current.position.x);

            Object.DestroyImmediate(path.gameObject);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run via UnityMCP `run_tests` (EditMode), filtered to `CrimsonDraft.Tests.EnemyPatrolPathTests`.
Expected: FAIL to compile — `EnemyPatrolPath.ResetIndex` does not exist.

- [ ] **Step 3: Implement `ResetIndex`**

`EnemyPatrolPath.cs` — add right after `Advance()`:

```csharp
        public void Advance()
        {
            if (waypoints.Length == 0) return;
            index = (index + 1) % waypoints.Length;
        }

        public void ResetIndex() => index = 0;
```

- [ ] **Step 4: Run test to verify it passes**

Run via UnityMCP `run_tests` (EditMode), filtered to `CrimsonDraft.Tests.EnemyPatrolPathTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyPatrolPath.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/EnemyPatrolPathTests.cs
git commit -m "feat(navigation): add EnemyPatrolPath.ResetIndex"
```

---

## Task 2: `EnemyNavAgent.ResetToSpawn(...)`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs`

**Interfaces:**
- Consumes: `EnemyPatrolPath.ResetIndex()` (Task 1).
- Produces: `EnemyNavAgent.ResetToSpawn(Vector3 position, Quaternion rotation) : void`.

No automated test for this task — per the approved spec, `EnemyNavAgent` needs a real `NavMeshAgent` on a baked NavMesh plus several DI-injected dependencies (`ISceneTransitionService`, `IEncounterContext`, `PlayerController`, `EnemyStateRegistry`, etc.) to run meaningfully; this matches the project's existing boundary for deeply Unity-systems-coupled `MonoBehaviour`s (see `CombatOrchestrator`, which also has no dedicated test file). Verified by compiling clean and by the Task 3/manual Play Mode check at the end of this plan.

- [ ] **Step 1: Add `ResetToSpawn`**

`EnemyNavAgent.cs` — add right after `NotifyCombatTriggered()` (a public method, currently at line ~214-217), before the private `TriggerCombat()`:

```csharp
        public void NotifyCombatTriggered()
        {
            this.combatTriggered = true;
        }

        public void ResetToSpawn(Vector3 position, Quaternion rotation)
        {
            if (!isActiveAndEnabled) return;

            navAgent.Warp(position);
            transform.rotation = rotation;

            combatTriggered = false;
            dialoguePaused  = false;

            path?.ResetIndex();
            TransitionTo(GuardAlertState.Patrol);
        }

        private void TriggerCombat()
        {
```

Notes for the implementer:
- `navAgent.Warp(...)` (not a direct `transform.position` assignment) is required so the `NavMeshAgent`'s internal path-planning state stays in sync with the mesh.
- `TransitionTo(GuardAlertState.Patrol)` already resets `EnemyDetectionSensor` (via `sensor.ResetState()`) and — since the patrol index was just reset to 0 — re-targets the agent at the first waypoint (via `navAgent.SetDestination(path.Current.position)`), so no duplicate logic is needed here.
- If `patrolEnabled` is `false` or the agent has no waypoints, the existing guards inside `TransitionTo`/`UpdatePatrol` already make the patrol-specific part of this a safe no-op — the enemy is still repositioned and returned to `Patrol` (idle) state.

- [ ] **Step 2: Confirm the project compiles**

Run `mcp__UnityMCP__refresh_unity` (force recompile) → `mcp__UnityMCP__read_console` filtered for `CS` errors.
Expected: no compile errors.

- [ ] **Step 3: Run the full EditMode suite to confirm no regressions**

Run via UnityMCP `run_tests` (EditMode), no filter (or at minimum `CrimsonDraft.Tests.EnemyPatrolPathTests` + any existing Navigation/Combat suites).
Expected: same pass/fail counts as before this task (no new failures introduced).

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Enemy/EnemyNavAgent.cs
git commit -m "feat(navigation): add EnemyNavAgent.ResetToSpawn for room re-entry"
```

---

## Task 3: `RoomController` spawn caching and reset-on-activate

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomController.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/RoomControllerTests.cs`

**Interfaces:**
- Consumes: `EnemyNavAgent.ResetToSpawn(Vector3, Quaternion)` (Task 2).
- Produces: `RoomController.Activate()` now also resets cached enemies (behavior change, same signature).

- [ ] **Step 1: Write the failing test**

Add to `Game/CrimsonDraft/Assets/Tests/EditMode/RoomControllerTests.cs`, after `Deactivate_makesGameObjectInactive`:

```csharp
        [Test]
        public void Activate_withMissingSpawnPointReference_doesNotThrow()
        {
            var go   = new GameObject();
            go.SetActive(false);
            var room = go.AddComponent<RoomController>();

            var so        = new SerializedObject(room);
            var arrayProp = so.FindProperty("enemySpawns");
            arrayProp.arraySize = 1;
            var entryProp = arrayProp.GetArrayElementAtIndex(0);
            entryProp.FindPropertyRelative("enemy").objectReferenceValue      = null;
            entryProp.FindPropertyRelative("spawnPoint").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.DoesNotThrow(() => room.Activate());
            Assert.IsTrue(go.activeSelf);

            Object.DestroyImmediate(go);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run via UnityMCP `run_tests` (EditMode), filtered to `CrimsonDraft.Tests.RoomControllerTests`.
Expected: FAIL to compile — `enemySpawns` serialized property does not exist yet (or the test simply has nothing to find, since the field doesn't exist).

- [ ] **Step 3: Add `EnemySpawnEntry`, the `enemySpawns` field, and the `Activate()` reset loop**

`RoomController.cs` — full replacement:

```csharp
#nullable enable

using System;
using UnityEngine;
using NaughtyAttributes;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomController : MonoBehaviour
    {
        [Serializable]
        private sealed class EnemySpawnEntry
        {
            public EnemyNavAgent enemy = null!;
            public Transform     spawnPoint = null!;
        }

        [SerializeField] private string roomId = "";
        [SerializeField] private EnemySpawnEntry[] enemySpawns = Array.Empty<EnemySpawnEntry>();

        public string RoomId => this.roomId;

        public void Activate()
        {
            gameObject.SetActive(true);

            for (int i = 0; i < this.enemySpawns.Length; i++)
            {
                var entry = this.enemySpawns[i];
                if (entry?.enemy == null || entry.spawnPoint == null) continue;
                if (!entry.enemy.gameObject.activeSelf) continue; // defeated or otherwise hidden
                entry.enemy.ResetToSpawn(entry.spawnPoint.position, entry.spawnPoint.rotation);
            }
        }

        public void Deactivate() => gameObject.SetActive(false);

#if UNITY_EDITOR
        [Button("Cache Room Enemies")]
        private void CacheRoomEnemies()
        {
            for (int i = 0; i < this.enemySpawns.Length; i++)
            {
                if (this.enemySpawns[i]?.spawnPoint != null)
                    DestroyImmediate(this.enemySpawns[i].spawnPoint.gameObject);
            }

            var enemies = GetComponentsInChildren<EnemyNavAgent>(includeInactive: true);
            this.enemySpawns = new EnemySpawnEntry[enemies.Length];

            for (int i = 0; i < enemies.Length; i++)
            {
                var spawnGo = new GameObject($"EnemySpawn_{enemies[i].name}");
                spawnGo.transform.SetParent(transform);
                spawnGo.transform.SetPositionAndRotation(
                    enemies[i].transform.position, enemies[i].transform.rotation);

                this.enemySpawns[i] = new EnemySpawnEntry
                {
                    enemy      = enemies[i],
                    spawnPoint = spawnGo.transform,
                };
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
```

Notes for the implementer:
- `entry?.enemy == null || entry.spawnPoint == null` guards both a null array element and null references inside it — either way the entry is skipped rather than throwing.
- `entry.enemy.gameObject.activeSelf` is `false` for enemies `EnemyStateRegistry`/`OnCombatEnded` already deactivated on defeat — skipping them means defeated enemies stay hidden after a room re-entry, matching existing behavior.
- Re-pressing **Cache Room Enemies** destroys and regenerates every spawn point from scratch (including previously hand-tuned ones) — this is the approved behavior, not a bug.

- [ ] **Step 4: Run test to verify it passes**

Run via UnityMCP `run_tests` (EditMode), filtered to `CrimsonDraft.Tests.RoomControllerTests`.
Expected: all 3 tests (`Activate_makesGameObjectActive`, `Deactivate_makesGameObjectInactive`, `Activate_withMissingSpawnPointReference_doesNotThrow`) PASS.

- [ ] **Step 5: Run the full EditMode suite to confirm no regressions**

Run via UnityMCP `run_tests` (EditMode), no filter.
Expected: same pass/fail counts as before this task (no new failures introduced beyond any pre-existing known failures).

- [ ] **Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Rooms/RoomController.cs \
        Game/CrimsonDraft/Assets/Tests/EditMode/RoomControllerTests.cs
git commit -m "feat(navigation): reset cached enemies to their spawn point on room activate"
```

---

## Manual verification (Play Mode)

Not covered by the automated suite: the full `RoomController.Activate() -> EnemyNavAgent.ResetToSpawn()` integration, and the `[Button]` editor tooling. After all 3 tasks:

1. Open a room with at least one patrolling enemy in the Editor. Select its `RoomController` and press **Cache Room Enemies** — confirm a new child `Transform` named `EnemySpawn_<enemy name>` appears under it for each enemy, positioned where that enemy currently stands.
2. Enter Play Mode, enter that room, and lure the enemy toward the door (get it into `Suspicious`/`Alert` and let it follow partway, or just let it patrol away from its spawn).
3. Leave the room, then re-enter through the same door.
4. Confirm the enemy is back at its cached spawn position and facing, in `Patrol` state, walking toward its first waypoint — and that re-entering does not immediately trigger combat.
5. Repeat after defeating an enemy in that room (win the resulting combat), then leave and re-enter — confirm the defeated enemy does **not** reappear (it's skipped since its `GameObject` is inactive).
6. Confirm rooms with zero cached enemies still activate normally (no errors in the console).
