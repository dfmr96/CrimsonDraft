# Head Look at Points of Interest — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Characters turn their head smoothly toward nearby items that have a `Lookable` marker component, using Unity's built-in Animator IK.

**Architecture:** A `Lookable` marker component opts any item in — no coupling to `IInteractable`. `PlayerHeadLookController` (on the Animator child) detects nearby Lookables via `OverlapSphere` + cone filter, selects the highest-priority one, and drives the head via `SetLookAtWeight`/`SetLookAtPosition` inside `OnAnimatorIK`. Selection logic is extracted as a `public static` method for EditMode unit testing.

**Tech Stack:** Unity 6, C# 10, NUnit (EditMode tests), Unity Humanoid Animator IK

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Navigation/Interactables/Lookable.cs` | Marker component — offset, priority, world look position |
| Create | `Assets/Scripts/Navigation/Player/PlayerHeadLookController.cs` | Detection, IK weight blending, `SelectBest` static helper |
| Create | `Assets/Scripts/Editor/AnimatorIKPassEnabler.cs` | One-shot Editor menu to enable IK Pass on an animator layer |
| Modify | `Assets/Animations/Player/PlayerAnimator.controller` | Enable IK Pass on Base Layer |
| Modify | `Assets/Prefabs/Characters/Player.prefab` | Add `PlayerHeadLookController` to `HumanoidBase_Overlapping_TPose` child |
| Modify | `Assets/Prefabs/Items/Key_Pickup_Demo.prefab` | Add `Lookable` as example |
| Create | `Assets/Tests/EditMode/LookableTests.cs` | Unit tests for `Lookable.LookPosition` |
| Create | `Assets/Tests/EditMode/LookableSelectorTests.cs` | Unit tests for `PlayerHeadLookController.SelectBest` |

---

## Task 1: `Lookable` component (TDD)

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/Lookable.cs`
- Create: `Assets/Tests/EditMode/LookableTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/LookableTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class LookableTests
    {
        [Test]
        public void LookPosition_withNoOffset_returnsObjectWorldPosition()
        {
            var go       = new GameObject();
            var lookable = go.AddComponent<Lookable>();
            go.transform.position = new Vector3(1f, 2f, 3f);

            Assert.AreEqual(go.transform.position, lookable.LookPosition);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LookPosition_withLocalOffset_returnsWorldPositionWithOffset()
        {
            var go       = new GameObject();
            var lookable = go.AddComponent<Lookable>();
            go.transform.position = new Vector3(1f, 0f, 0f);

            var so = new SerializedObject(lookable);
            so.FindProperty("offset").vector3Value = new Vector3(0f, 1f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(new Vector3(1f, 1f, 0f), lookable.LookPosition);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LookPosition_withRotatedParent_returnsCorrectWorldPosition()
        {
            var go       = new GameObject();
            var lookable = go.AddComponent<Lookable>();
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var so = new SerializedObject(lookable);
            so.FindProperty("offset").vector3Value = new Vector3(1f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // After 90° Y rotation, local X becomes world -Z
            var expected = go.transform.TransformPoint(new Vector3(1f, 0f, 0f));
            Assert.AreEqual(expected, lookable.LookPosition);

            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they FAIL**

Via MCP `run_tests` with filter `LookableTests`. Expected: compile error ("Lookable not found").

- [ ] **Step 3: Implement `Lookable.cs`**

Create `Assets/Scripts/Navigation/Interactables/Lookable.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class Lookable : MonoBehaviour
    {
        [SerializeField] private Vector3 offset;
        [SerializeField] private int priority;

        public int Priority => priority;
        public Vector3 LookPosition => transform.TransformPoint(offset);
    }
}
```

- [ ] **Step 4: Check compilation via MCP `read_console`**

Expected: no errors. If errors appear, fix before continuing.

- [ ] **Step 5: Run tests — verify they PASS**

Via MCP `run_tests` with filter `LookableTests`. Expected: 3 passed, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Lookable.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/LookableTests.cs"
git commit -m "feat(navigation): add Lookable marker component with offset and priority"
```

---

## Task 2: `PlayerHeadLookController` — selection logic (TDD)

**Files:**
- Create: `Assets/Scripts/Navigation/Player/PlayerHeadLookController.cs` (SelectBest only first)
- Create: `Assets/Tests/EditMode/LookableSelectorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/LookableSelectorTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Tests
{
    public sealed class LookableSelectorTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static (GameObject go, Collider col, Lookable lookable) MakeLookable(
            Vector3 position, int priority = 0)
        {
            var go       = new GameObject();
            go.transform.position = position;
            var col      = go.AddComponent<BoxCollider>();
            var lookable = go.AddComponent<Lookable>();
            var so       = new SerializedObject(lookable);
            so.FindProperty("priority").intValue = priority;
            so.ApplyModifiedPropertiesWithoutUndo();
            return (go, col, lookable);
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void SelectBest_singleCandidateInCone_returnsIt()
        {
            var (go, col, lookable) = MakeLookable(Vector3.forward * 2f);
            var colliders = new Collider[] { col };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 1,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.AreEqual(lookable, result);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SelectBest_candidateOutsideCone_returnsNull()
        {
            var (go, col, _) = MakeLookable(Vector3.right * 2f); // 90° from forward
            var colliders = new Collider[] { col };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 1,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.IsNull(result);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SelectBest_noCandidates_returnsNull()
        {
            var result = PlayerHeadLookController.SelectBest(
                new Collider[4], count: 0,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.IsNull(result);
        }

        [Test]
        public void SelectBest_higherPriorityWins()
        {
            var (goA, colA, _)        = MakeLookable(Vector3.forward * 2f, priority: 1);
            var (goB, colB, lookableB) = MakeLookable(Vector3.forward * 1f, priority: 5);
            var colliders = new Collider[] { colA, colB };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 2,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.AreEqual(lookableB, result);
            Object.DestroyImmediate(goA);
            Object.DestroyImmediate(goB);
        }

        [Test]
        public void SelectBest_samePriorityNearestWins()
        {
            var (goA, colA, lookableA) = MakeLookable(Vector3.forward * 1f, priority: 0);
            var (goB, colB, _)         = MakeLookable(Vector3.forward * 3f, priority: 0);
            var colliders = new Collider[] { colA, colB };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 2,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.AreEqual(lookableA, result);
            Object.DestroyImmediate(goA);
            Object.DestroyImmediate(goB);
        }

        [Test]
        public void SelectBest_colliderWithNoLookable_isIgnored()
        {
            var go  = new GameObject();
            var col = go.AddComponent<BoxCollider>();
            go.transform.position = Vector3.forward * 2f;
            var colliders = new Collider[] { col };

            var result = PlayerHeadLookController.SelectBest(
                colliders, count: 1,
                origin: Vector3.zero, forward: Vector3.forward, maxAngle: 60f);

            Assert.IsNull(result);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they FAIL**

Via MCP `run_tests` with filter `LookableSelectorTests`. Expected: compile error ("PlayerHeadLookController not found").

- [ ] **Step 3: Create `PlayerHeadLookController.cs` with `SelectBest` only**

Create `Assets/Scripts/Navigation/Player/PlayerHeadLookController.cs`:

```csharp
#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerHeadLookController : MonoBehaviour
    {
        [SerializeField] private float detectionRadius   = 3f;
        [SerializeField] private float maxAngle          = 60f;
        [SerializeField] private float weightSpeed       = 3f;
        [SerializeField] private float detectionInterval = 0.3f;
        [SerializeField] private LayerMask lookableLayer;

        private Animator   m_Animator        = null!;
        private Lookable?  m_CurrentTarget;
        private Vector3    m_LastLookPosition;
        private float      m_Weight;
        private float      m_DetectionTimer;

        private readonly Collider[] m_OverlapResults = new Collider[16];

        private void Awake()  => m_Animator = GetComponent<Animator>();

        private void Update()
        {
            m_DetectionTimer -= Time.deltaTime;
            if (m_DetectionTimer > 0f) return;

            m_DetectionTimer = detectionInterval;
            int count = Physics.OverlapSphereNonAlloc(
                transform.parent.position, detectionRadius, m_OverlapResults, lookableLayer);
            m_CurrentTarget = SelectBest(
                m_OverlapResults, count, transform.parent.position, transform.forward, maxAngle);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (m_CurrentTarget != null)
            {
                m_Weight           = Mathf.MoveTowards(m_Weight, 1f, weightSpeed * Time.deltaTime);
                m_LastLookPosition = m_CurrentTarget.LookPosition;
            }
            else
            {
                m_Weight = Mathf.MoveTowards(m_Weight, 0f, weightSpeed * Time.deltaTime);
            }

            m_Animator.SetLookAtWeight(m_Weight);
            if (m_Weight > 0f)
                m_Animator.SetLookAtPosition(m_LastLookPosition);
        }

        public static Lookable? SelectBest(
            Collider[] colliders, int count,
            Vector3 origin, Vector3 forward, float maxAngle)
        {
            Lookable? best     = null;
            float     bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (!colliders[i].TryGetComponent<Lookable>(out var lookable))
                    continue;

                Vector3 dir = lookable.transform.position - origin;
                if (Vector3.Angle(forward, dir) > maxAngle)
                    continue;

                float dist = dir.sqrMagnitude;
                if (best == null
                    || lookable.Priority > best.Priority
                    || (lookable.Priority == best.Priority && dist < bestDist))
                {
                    best     = lookable;
                    bestDist = dist;
                }
            }

            return best;
        }
    }
}
```

- [ ] **Step 4: Run tests — verify they PASS**

Via MCP `run_tests` with filter `LookableSelectorTests`. Expected: 6 passed, 0 failed.

- [ ] **Step 5: Check compilation via MCP `read_console`**

Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Player/PlayerHeadLookController.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/LookableSelectorTests.cs"
git commit -m "feat(navigation): add PlayerHeadLookController with cone detection and IK blending"
```

---

## Task 3: Enable IK Pass on PlayerAnimator.controller

**Files:**
- Create: `Assets/Scripts/Editor/AnimatorIKPassEnabler.cs`
- Modify: `Assets/Animations/Player/PlayerAnimator.controller`

- [ ] **Step 1: Create the Editor utility script**

Create `Assets/Scripts/Editor/AnimatorIKPassEnabler.cs`:

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    public static class AnimatorIKPassEnabler
    {
        [MenuItem("Tools/CrimsonDraft/Enable IK Pass – PlayerAnimator Base Layer")]
        public static void EnablePlayerAnimatorIKPass()
        {
            const string path = "Assets/Animations/Player/PlayerAnimator.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Debug.LogError($"[AnimatorIKPassEnabler] Controller not found at {path}");
                return;
            }

            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[AnimatorIKPassEnabler] IK Pass enabled on Base Layer.");
        }
    }
}
#endif
```

- [ ] **Step 2: Compile check via MCP `read_console`**

Expected: no errors.

- [ ] **Step 3: Execute the menu item via MCP `execute_menu_item`**

Tool: `mcp__UnityMCP__execute_menu_item`
Path: `Tools/CrimsonDraft/Enable IK Pass – PlayerAnimator Base Layer`

Expected console output: `[AnimatorIKPassEnabler] IK Pass enabled on Base Layer.`

- [ ] **Step 4: Verify the controller was modified**

Via MCP `read_console`, check for the success log. The `.controller` file on disk will now have `m_IKPass: 1` in its Base Layer.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Editor/AnimatorIKPassEnabler.cs"
git add "Game/CrimsonDraft/Assets/Animations/Player/PlayerAnimator.controller"
git commit -m "feat(navigation): enable IK Pass on PlayerAnimator Base Layer"
```

---

## Task 4: Wire up Player prefab

**Files:**
- Modify: `Assets/Prefabs/Characters/Player.prefab`

- [ ] **Step 1: Add `PlayerHeadLookController` to `HumanoidBase_Overlapping_TPose`**

Via MCP `manage_components`:
- Prefab path: `Assets/Prefabs/Characters/Player.prefab`
- Target child object: `HumanoidBase_Overlapping_TPose`
- Action: add component `PlayerHeadLookController`

- [ ] **Step 2: Configure serialized fields**

Via MCP or `manage_prefabs`, set on `PlayerHeadLookController`:
- `detectionRadius`: `3`
- `maxAngle`: `60`
- `weightSpeed`: `3`
- `detectionInterval`: `0.3`
- `lookableLayer`: Layer `Interactable` (layer 8)

- [ ] **Step 3: Verify prefab saved**

Via MCP `find_gameobjects` or `read_console` — no errors.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab"
git commit -m "feat(navigation): add PlayerHeadLookController to Player prefab"
```

---

## Task 5: Add `Lookable` to Key_Pickup_Demo prefab

**Files:**
- Modify: `Assets/Prefabs/Items/Key_Pickup_Demo.prefab`

- [ ] **Step 1: Add `Lookable` component to the prefab**

Via MCP `manage_components`:
- Prefab path: `Assets/Prefabs/Items/Key_Pickup_Demo.prefab`
- Action: add component `Lookable`

- [ ] **Step 2: Set offset and priority**

Via MCP or `manage_prefabs`, set on `Lookable`:
- `offset`: `(0, 0.15, 0)` — slightly above the key's pivot so the head aims at the item, not its base
- `priority`: `1`

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Prefabs/Items/Key_Pickup_Demo.prefab"
git commit -m "feat(content): add Lookable to Key_Pickup_Demo prefab"
```

---

## Task 6: Smoke test in Play Mode

- [ ] **Step 1: Open a scene that contains the Player and a Key_Pickup_Demo instance**

Via MCP `manage_scene`, load the demo scene (e.g. `Assets/Scenes/DemoRoom.unity` or equivalent).

- [ ] **Step 2: Enter Play Mode**

Via MCP `manage_editor` with action `play`.

- [ ] **Step 3: Verify behavior**

Walk the player toward the key. Expected:
- When the key enters the 3m radius AND is within ~60° of the player's forward direction, the character's head turns toward it smoothly
- When the player moves away or turns so the key is behind them, the head returns to forward smoothly

Check MCP `read_console` for any errors (null refs, missing components, IK warnings).

- [ ] **Step 4: Exit Play Mode**

Via MCP `manage_editor` with action `stop`.

- [ ] **Step 5: Commit any adjustments**

If you tuned `detectionRadius`, `maxAngle`, or `offset` values during the smoke test, commit the prefab changes:

```bash
git add "Game/CrimsonDraft/Assets/Prefabs/Characters/Player.prefab"
git add "Game/CrimsonDraft/Assets/Prefabs/Items/Key_Pickup_Demo.prefab"
git commit -m "fix(navigation): tune head-look detection radius and key offset after smoke test"
```
