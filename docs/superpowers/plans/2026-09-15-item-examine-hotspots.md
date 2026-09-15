# Item Examine Hotspots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an item's `PreviewModel` prefab define raycast-hittable hotspots that resolve to different Yarn examine text depending on where the player aims while rotating the model in `InspectPanel`, falling back to a default when no hotspot is hit.

**Architecture:** A new `ItemExamineHotspots` MonoBehaviour (pure hit→dialogue lookup, no Unity APIs beyond `Collider` equality) sits on the preview model prefab. `PickupPreviewView` gets a `TryGetExamineDialogue()` method that raycasts from the preview camera and asks that component for the right `DialogueReference` (or returns `null` if the item has no hotspots at all). `InspectPanel.OnConfirmPressed` calls this every time it starts a fresh typewriter run, instead of always using the text cached at `Open()`.

**Tech Stack:** Unity 6000.3, C# (`#nullable enable`), VContainer, Yarn Spinner (`YarnSpinner.Unity`), NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-09-15-item-examine-hotspots-design.md`

## Global Constraints

- No face/side detection — a hotspot fires on any hit against its collider, regardless of which side the ray entered from (spec's explicit non-goal).
- Hotspot colliders are primitives only (Box/Sphere/Capsule) — no MeshCollider.
- No dedicated editor tool beyond `OnDrawGizmosSelected` — hotspots are placed by hand in Scene view.
- No aim reticle/crosshair UI.
- All new code files use `#nullable enable` (project convention, CLAUDE.md).

---

### Task 1: `ItemExamineHotspots` component

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemExamineHotspots.cs`
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/ItemExamineHotspotsTests.cs`

**Interfaces:**
- Produces: `CrimsonDraft.Inventory.ItemExamineHotspots` (MonoBehaviour) with nested `public struct Hotspot { public Collider collider; public DialogueReference dialogue; }` and `public DialogueReference GetDialogue(Collider? hitCollider)`. Task 2 calls `GetComponentInChildren<ItemExamineHotspots>()` and `.GetDialogue(...)` on the result.

- [ ] **Step 1: Write the failing tests**

Create `Game/CrimsonDraft/Assets/Tests/EditMode/ItemExamineHotspotsTests.cs`:

```csharp
#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ItemExamineHotspotsTests
    {
        private static ItemExamineHotspots MakeHotspots(
            ItemExamineHotspots.Hotspot[] hotspots,
            DialogueReference defaultDialogue)
        {
            var go   = new GameObject();
            var comp = go.AddComponent<ItemExamineHotspots>();

            var hotspotsField = typeof(ItemExamineHotspots).GetField("hotspots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var defaultField = typeof(ItemExamineHotspots).GetField("defaultDialogue",
                BindingFlags.Instance | BindingFlags.NonPublic);

            hotspotsField!.SetValue(comp, hotspots);
            defaultField!.SetValue(comp, defaultDialogue);

            return comp;
        }

        [Test]
        public void GetDialogue_hitMatchingCollider_returnsHotspotDialogue()
        {
            var collider = new GameObject().AddComponent<BoxCollider>();
            var hotspotDialogue = new DialogueReference { nodeName = "hit_node" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[] { new ItemExamineHotspots.Hotspot { collider = collider, dialogue = hotspotDialogue } },
                defaultDialogue);

            var result = comp.GetDialogue(collider);

            Assert.AreSame(hotspotDialogue, result);
        }

        [Test]
        public void GetDialogue_nullCollider_returnsDefaultDialogue()
        {
            var collider = new GameObject().AddComponent<BoxCollider>();
            var hotspotDialogue = new DialogueReference { nodeName = "hit_node" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[] { new ItemExamineHotspots.Hotspot { collider = collider, dialogue = hotspotDialogue } },
                defaultDialogue);

            var result = comp.GetDialogue(null);

            Assert.AreSame(defaultDialogue, result);
        }

        [Test]
        public void GetDialogue_hitUnregisteredCollider_returnsDefaultDialogue()
        {
            var registered = new GameObject().AddComponent<BoxCollider>();
            var other       = new GameObject().AddComponent<BoxCollider>();

            var hotspotDialogue = new DialogueReference { nodeName = "hit_node" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[] { new ItemExamineHotspots.Hotspot { collider = registered, dialogue = hotspotDialogue } },
                defaultDialogue);

            var result = comp.GetDialogue(other);

            Assert.AreSame(defaultDialogue, result);
        }

        [Test]
        public void GetDialogue_multipleHotspots_returnsTheOneThatMatches()
        {
            var colliderA = new GameObject().AddComponent<BoxCollider>();
            var colliderB = new GameObject().AddComponent<BoxCollider>();
            var dialogueA = new DialogueReference { nodeName = "node_a" };
            var dialogueB = new DialogueReference { nodeName = "node_b" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[]
                {
                    new ItemExamineHotspots.Hotspot { collider = colliderA, dialogue = dialogueA },
                    new ItemExamineHotspots.Hotspot { collider = colliderB, dialogue = dialogueB },
                },
                defaultDialogue);

            Assert.AreSame(dialogueB, comp.GetDialogue(colliderB));
            Assert.AreSame(dialogueA, comp.GetDialogue(colliderA));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via the UnityMCP `run_tests` tool with `filter: "ItemExamineHotspotsTests"` (or Window → General → Test Runner → EditMode → run the class).
Expected: compile error — `ItemExamineHotspots` does not exist yet. This is the "red" step; Unity test runs fail as a compile error rather than a runtime assertion failure when the type under test doesn't exist yet, which is expected here.

- [ ] **Step 3: Implement `ItemExamineHotspots`**

Create `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemExamineHotspots.cs`:

```csharp
#nullable enable

using System;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Inventory
{
    // Placed on an ItemData.PreviewModel prefab to make specific colliders on the model
    // resolve to different examine dialogue when hit by PickupPreviewView's inspect raycast.
    // Items without this component keep using ItemData.ExamineDialogue unchanged.
    public sealed class ItemExamineHotspots : MonoBehaviour
    {
        [Serializable]
        public struct Hotspot
        {
            public Collider collider;
            public DialogueReference dialogue;
        }

        [SerializeField] private Hotspot[] hotspots = Array.Empty<Hotspot>();
        [SerializeField] private DialogueReference defaultDialogue = new();

        public DialogueReference GetDialogue(Collider? hitCollider)
        {
            if (hitCollider != null)
            {
                foreach (var h in this.hotspots)
                    if (h.collider == hitCollider) return h.dialogue;
            }
            return this.defaultDialogue;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (var h in this.hotspots)
            {
                if (h.collider == null) continue;
                Gizmos.matrix = h.collider.transform.localToWorldMatrix;
                switch (h.collider)
                {
                    case BoxCollider box:
                        Gizmos.DrawWireCube(box.center, box.size);
                        break;
                    case SphereCollider sphere:
                        Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                        break;
                    default:
                        Gizmos.DrawWireCube(Vector3.zero, h.collider.bounds.size);
                        break;
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run via the UnityMCP `run_tests` tool with `filter: "ItemExamineHotspotsTests"`.
Expected: PASS, 4/4.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Inventory/ItemExamineHotspots.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/ItemExamineHotspotsTests.cs"
git commit -m "feat(inventory): add ItemExamineHotspots component for hit-dependent examine text"
```

---

### Task 2: `PickupPreviewView.TryGetExamineDialogue`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PickupPreviewView.cs`

**Interfaces:**
- Consumes: `ItemExamineHotspots.GetDialogue(Collider? hitCollider) : DialogueReference` (Task 1). Existing private fields `currentInstance : GameObject?`, `previewCamera : Camera?`, `previewLayer : int`.
- Produces: `public DialogueReference? TryGetExamineDialogue()` — `null` means "this item has no hotspot component, caller should use its own default text"; non-null is always a resolved `DialogueReference` (hotspot-specific or the component's own default).

This method depends on live Physics raycasts and real GameObjects with colliders — not something the project's EditMode plain-C#-fakes pattern covers (no scene, no real camera/model hierarchy in those tests). Per the spec, this task has no automated test; Task 4 covers manual verification of the full flow.

- [ ] **Step 1: Add the method**

In `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PickupPreviewView.cs`, add `using CrimsonDraft.Inventory;` to the usings if not already present (check first — it's already imported for the `ItemData` parameter on `Show`), then add this method right after `SetRotationInput`:

```csharp
// Returns null when the currently-shown item has no ItemExamineHotspots at all -- callers
// should fall back to that item's own default examine text in that case. A non-null result
// is always a fully-resolved DialogueReference (hotspot-specific, or the component's own
// defaultDialogue when the raycast didn't hit a registered hotspot).
public DialogueReference? TryGetExamineDialogue()
{
    if (this.currentInstance == null) return null;

    var hotspots = this.currentInstance.GetComponentInChildren<ItemExamineHotspots>();
    if (hotspots == null) return null;

    Collider? hitCollider = null;
    if (this.previewCamera != null)
    {
        int mask = this.previewLayer >= 0 ? 1 << this.previewLayer : ~0;
        if (Physics.Raycast(this.previewCamera.transform.position, this.previewCamera.transform.forward,
                out var hit, Mathf.Infinity, mask, QueryTriggerInteraction.Collide))
        {
            hitCollider = hit.collider;
        }
    }

    return hotspots.GetDialogue(hitCollider);
}
```

- [ ] **Step 2: Compile and check the console**

Use the UnityMCP `refresh_unity` tool with `compile: "request"`, `scope: "scripts"`, then `read_console` filtered to `"error CS"`.
Expected: no compile errors.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PickupPreviewView.cs"
git commit -m "feat(inventory): add hotspot raycast resolution to PickupPreviewView"
```

---

### Task 3: `InspectPanel` integration

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/Inventory/InspectPanel.cs`

**Interfaces:**
- Consumes: `PickupPreviewView.TryGetExamineDialogue() : DialogueReference?` (Task 2). Existing private method `ExtractExamineText(DialogueReference) : string`, existing field `pendingExamineText : string`.

No automated test for the same reason as Task 2 — this only changes which `DialogueReference` feeds an already-existing, already-manually-verified typewriter pipeline. Task 4 covers manual verification.

- [ ] **Step 1: Change `OnConfirmPressed` to resolve hotspot dialogue first**

In `Game/CrimsonDraft/Assets/Scripts/UI/Inventory/InspectPanel.cs`, replace:

```csharp
            // While typing, Confirm completes the text instantly. Only once finished does
            // Confirm replay it from the start.
            if (this.isTyping) { this.skipRequested = true; return; }
            TypewriterRoutine(this.pendingExamineText).Forget();
        }
```

with:

```csharp
            // While typing, Confirm completes the text instantly. Only once finished does
            // Confirm replay it from the start.
            if (this.isTyping) { this.skipRequested = true; return; }

            // Hotspot items resolve their text fresh on every press (the player may have
            // rotated the model between attempts); items without hotspots keep the text
            // cached at Open() time.
            var hotspotDialogue = this.modelPreview?.TryGetExamineDialogue();
            string text = hotspotDialogue != null
                ? ExtractExamineText(hotspotDialogue)
                : this.pendingExamineText;

            TypewriterRoutine(text).Forget();
        }
```

- [ ] **Step 2: Compile and check the console**

Use the UnityMCP `refresh_unity` tool with `compile: "request"`, `scope: "scripts"`, then `read_console` filtered to `"error CS"`.
Expected: no compile errors.

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/UI/Inventory/InspectPanel.cs"
git commit -m "feat(inventory): InspectPanel resolves hotspot-specific examine text on Confirm"
```

---

### Task 4: Manual end-to-end verification with a throwaway test fixture

**Files:**
- None committed — everything created in this task is temporary scaffolding to prove the feature works, then deleted. (If useful as a starting point for real content later, ask before keeping any of it — don't leave throwaway test assets in the project without confirming.)

**Interfaces:**
- None — this task only exercises Tasks 1-3 through the real Editor/game, it doesn't produce anything later tasks depend on.

There is no real item with a hotspot-bearing `PreviewModel` yet (confirmed during design — no "sheep key" asset exists). This task builds the smallest possible fixture to prove the raycast → hotspot → dialogue → typewriter chain actually works end to end, since Tasks 2 and 3 have no automated coverage.

- [ ] **Step 1: Create two temporary Yarn nodes**

In the YarnProject already used by `FilesTabController`/other examine dialogue (find it via the `yarnProject` field on an existing `FilesTabController` instance in the inventory scene, or via `KeyA_Data.asset`'s `examineDialogue.project` GUID — both point at the same project asset), add two throwaway nodes to a scratch `.yarn` source file, e.g.:

```
title: temp_hotspot_hit_test
---
Es una llave con una oveja incrustada.
===

title: temp_hotspot_miss_test
---
Una llave vieja, me pregunto qué abrirá.
===
```

Re-import so the YarnProject compiles them into its string table.

- [ ] **Step 2: Build a temp preview model with a hotspot**

Using UnityMCP (`manage_gameobject`, `manage_components`, `manage_prefabs`) or the Editor directly:
1. Create a GameObject with a simple primitive mesh (e.g. a Cube) as visual stand-in for "the key".
2. Add a child GameObject positioned at one corner of the cube (standing in for "the head"), with a `BoxCollider` sized small relative to the cube, `Is Trigger` checked.
3. Add an `ItemExamineHotspots` component to the root GameObject. In the Inspector, set `Hotspots` to size 1: element 0's `Collider` = the child BoxCollider, `Dialogue` → project = the YarnProject from Step 1, nodeName = `temp_hotspot_hit_test`. Set `Default Dialogue` → same project, nodeName = `temp_hotspot_miss_test`.
4. Save as a prefab, e.g. `Assets/_Scratch/TempHotspotKey.prefab` (outside `Assets/` tracked content the team cares about, or wherever the project keeps scratch assets — check for a `_Scratch`/similar gitignored folder first; if none exists, ask before creating files that will need cleanup).

- [ ] **Step 3: Wire the temp prefab onto an existing item for testing**

`KeyA_Data.asset` (`Game/CrimsonDraft/Assets/Data/Inventory/KeyA_Data.asset`) has no `PreviewModel` set and is clearly a placeholder/demo item already — temporarily set its `Preview Model` field to the prefab from Step 2 in the Inspector (or via UnityMCP `manage_asset`). Note the original value (empty) so it can be restored after this task.

- [ ] **Step 4: Run the game and verify both branches**

Enter Play Mode, get `KeyA_Data`'s item into the inventory (however the project's test/debug spawn flow works — check `TestPickupItem.cs` or the debug inventory tools already in the project), open the inventory, select it, choose Inspect.
- Rotate the model (existing rotation feature) so the hotspot collider faces the preview camera, press Confirm. Expected: typed text reads "Es una llave con una oveja incrustada."
- Close and reopen the inspect (or rotate away) so the hotspot is no longer facing the camera, press Confirm. Expected: typed text reads "Una llave vieja, me pregunto qué abrirá."
- Confirm items *without* a hotspot component still show their normal `ExamineDialogue` text unaffected (pick any other existing inventory item with `PreviewModel` set).

- [ ] **Step 5: Clean up**

Revert `KeyA_Data.asset`'s `Preview Model` field to empty (its original state). Delete the temp prefab and temp GameObjects created in Step 2. Remove the two scratch Yarn nodes from Step 1 (or leave the `.yarn` file untouched if they were added to a genuinely scratch/throwaway source file that's not part of tracked content — check before deleting real content). Confirm `git status` shows no leftover changes to `KeyA_Data.asset` or the YarnProject before moving on.

- [ ] **Step 6: Report result**

If both branches worked as expected in Step 4, the feature is complete — no commit needed for this task (everything was cleaned up). If something didn't behave as expected, that's a bug in Task 1-3's code, not this task — go back and fix it there, re-run Task 4 from Step 4.
