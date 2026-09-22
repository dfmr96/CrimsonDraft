# Hotspot Item-Use Prompt Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an `ItemExamineHotspots.Hotspot` optionally require an inventory item; when that item is present, examining the hotspot runs a Yes/No Yarn dialogue instead of static text, and choosing "Sí" consumes the item and fires a per-hotspot `UnityEvent`.

**Architecture:** `ItemExamineHotspots` gains a pure `Resolve(Collider?, IInventoryService)` method returning a new `ExamineResolution` (either text to type, or a prompt to run). `PickupPreviewView.TryGetExamineDialogue()` forwards to it. `InspectPanel` gets `IInventoryService`/`IDialogueService` injected and branches: type text as today, or hand off to the project's existing `IDialogueService`/`DialogueRunner` (the same one `pickup_prompt` and door dialogue already use) for the prompt, registering one fixed Yarn command (`use_required_item`) that consumes the item and fires the hotspot's `onUsed` hook.

**Tech Stack:** Unity 6000.3, C# (`#nullable enable`), VContainer, Yarn Spinner (`YarnSpinner.Unity`), NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-09-21-hotspot-item-prompt-design.md`

## Global Constraints

- No post-use state tracking — re-examining after consuming the item just re-resolves normally (item gone → falls back to `Text`).
- No new UI — the Sí/No prompt runs through the existing `IDialogueService`/`DialogueRunner`.
- No new Yarn tag — prompt nodes use the existing `examine`-tag-filtered picker.
- No automatic re-typing after the dialogue closes — control returns to `InspectPanel` as-is.
- The Yarn command name for the "Sí" branch is fixed project-wide: `use_required_item`.
- All new/modified C# files use `#nullable enable` (project convention).

---

### Task 1: `IInventoryService.HasItem` / `TryRemoveItem`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs`
- Modify (compile fix only, no behavior change): `Game/CrimsonDraft/Assets/Tests/EditMode/SceneDoorInteractableTests.cs`, `SaveGameLoaderTests.cs`, `SaveControllerTests.cs`, `RoomDoorInteractableTests.cs`, `MapPickupInteractableTests.cs`, `DoorInteractableTests.cs`, `CombatOrchestratorTests.cs`, `CombatMenuControllerTests.cs`

**Interfaces:**
- Produces: `IInventoryService.HasItem(string itemId) : bool` — true if any slot holds an item with that `ItemId`. `IInventoryService.TryRemoveItem(string itemId) : bool` — finds and clears the first slot holding that item, returns false (no mutation) if not found. Task 2 and Task 4 both call these.

Adding two members to `IInventoryService` breaks every class implementing it until each gets a stub — there are 8 test-only fakes across the files listed above, found via: every class in the project declaring `: IInventoryService`. This task fixes all of them so the whole project keeps compiling.

- [ ] **Step 1: Write the failing tests**

Open `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs`. It already has `MakeService`, `FakeRoster`, `MakeAlive`, and `MakeKeyItemData` helpers (used by the existing `TryUseKey_*` tests just above where you'll add these). Add these tests near the other `TryUseKey_*` tests (e.g. right after `TryUseKey_returnsAlreadyDepleted_whenKeyIsAtZeroUses`, around line 573):

```csharp
[Test]
public void HasItem_returnsTrue_whenItemPresent()
{
    var data    = MakeKeyItemData(id: "key-f");
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    service.AddItem(data, operatorSlot: 0);

    Assert.IsTrue(service.HasItem("key-f"));
}

[Test]
public void HasItem_returnsFalse_whenItemAbsent()
{
    var service = MakeService(new FakeRoster(MakeAlive(0)));

    Assert.IsFalse(service.HasItem("nonexistent"));
}

[Test]
public void TryRemoveItem_removesFirstMatch_andReturnsTrue()
{
    var data    = MakeKeyItemData(id: "key-g");
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    service.AddItem(data, operatorSlot: 0);

    bool result = service.TryRemoveItem("key-g");

    Assert.IsTrue(result);
    Assert.IsTrue(service.Slots[0].IsEmpty);
}

[Test]
public void TryRemoveItem_returnsFalse_withoutMutation_whenItemAbsent()
{
    var data    = MakeKeyItemData(id: "key-h");
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    service.AddItem(data, operatorSlot: 0);

    bool result = service.TryRemoveItem("nonexistent");

    Assert.IsFalse(result);
    Assert.IsFalse(service.Slots[0].IsEmpty, "unrelated slot must be untouched");
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via the UnityMCP `run_tests` tool with `filter: "InventoryServiceTests"`.
Expected: compile error — `HasItem`/`TryRemoveItem` don't exist yet on `IInventoryService`/`InventoryService`. This is expected; the project already has 17 pre-existing failures in this same test class from unrelated caliber/ammo tests (confirmed on `Development` before this branch existed) — ignore those, only the 4 new tests above and any other compile-blocking error matter here.

- [ ] **Step 3: Add the two members to the interface**

In `Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs`, add right after `TryUseKey`'s declaration (currently the line `KeyUseOutcome TryUseKey(string keyItemId);`):

```csharp
        /// <summary>Returns true if any slot holds an item with the given itemId.</summary>
        bool HasItem(string itemId);

        /// <summary>Finds and clears the first slot holding an item with the given
        /// itemId. Returns false (no mutation) if not found.</summary>
        bool TryRemoveItem(string itemId);
```

- [ ] **Step 4: Implement both in `InventoryService`**

In `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs`, add right after the `TryUseKey` method (currently ends around line 230 with `return new KeyUseOutcome(KeyUseResult.NotFound, -1); }`):

```csharp
        public bool HasItem(string itemId)
        {
            var s = EnsureSlots();
            for (int i = 0; i < s.Length; i++)
                if (!s[i].IsEmpty && s[i].Item!.Data.ItemId == itemId) return true;
            return false;
        }

        public bool TryRemoveItem(string itemId)
        {
            var s = EnsureSlots();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i].IsEmpty || s[i].Item!.Data.ItemId != itemId) continue;
                RemoveItem(i);
                return true;
            }
            return false;
        }
```

- [ ] **Step 5: Fix the 8 test-only fakes so the project compiles**

Each of these files has a private nested class implementing `IInventoryService` (find it by searching the file for `: IInventoryService`). Add these two lines to the body of each one (anywhere inside the class — member order doesn't matter):

```csharp
            public bool HasItem(string itemId) => false;
            public bool TryRemoveItem(string itemId) => false;
```

Files and their fake class names:
- `Game/CrimsonDraft/Assets/Tests/EditMode/SceneDoorInteractableTests.cs` — class `FakeInventory`
- `Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs` — class `FakeInventoryService`
- `Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs` — class `FakeInventoryService`
- `Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs` — class `FakeInventory`
- `Game/CrimsonDraft/Assets/Tests/EditMode/MapPickupInteractableTests.cs` — class `FakeInventory`
- `Game/CrimsonDraft/Assets/Tests/EditMode/DoorInteractableTests.cs` — class `FakeDoorInventoryService` (around line 170-200)
- `Game/CrimsonDraft/Assets/Tests/EditMode/CombatOrchestratorTests.cs` — class `FakeInventoryService`
- `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs` — class `FakeInventoryService`

None of these fakes' host tests exercise `HasItem`/`TryRemoveItem` — plain `false`-returning stubs are correct and match how these fakes already stub out every other member they don't specifically test (e.g. `FakeDoorInventoryService`'s `AddItem`/`AddExistingItem`/etc. all just return `false`).

- [ ] **Step 6: Run the tests to verify they pass, and confirm the project compiles**

Use the UnityMCP `refresh_unity` tool with `compile: "request"`, `scope: "scripts"`, then `read_console` filtered to `"error CS"` — expect zero entries (this confirms all 8 fakes compile).

Run via the UnityMCP `run_tests` tool with `filter: "InventoryServiceTests"`.
Expected: the 4 new tests pass. The pre-existing 17 unrelated failures in this class (caliber/ammo/weapon-slot tests) are expected and not caused by this change — do not attempt to fix them, they are out of scope (confirmed pre-existing on `Development`, unrelated to inventory item presence/removal).

- [ ] **Step 7: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs" "Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/SceneDoorInteractableTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/SaveGameLoaderTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/SaveControllerTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/RoomDoorInteractableTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/MapPickupInteractableTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/DoorInteractableTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/CombatOrchestratorTests.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs"
git commit -m "feat(inventory): add IInventoryService.HasItem/TryRemoveItem"
```

---

### Task 2: `ItemExamineHotspots.Resolve`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemExamineHotspots.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/ItemExamineHotspotsTests.cs`

**Interfaces:**
- Consumes: `IInventoryService.HasItem(string) : bool` (Task 1).
- Produces: `CrimsonDraft.Inventory.ExaminePrompt` (readonly struct: `Dialogue : DialogueReference`, `RequiredItem : ItemData`, `OnUsed : UnityEvent?`), `CrimsonDraft.Inventory.ExamineResolution` (readonly struct: `Text : DialogueReference?`, `Prompt : ExaminePrompt?`, static factories `ForText`/`ForPrompt`), and `ItemExamineHotspots.Resolve(Collider? hitCollider, IInventoryService inventory) : ExamineResolution?`. Task 3 calls `Resolve`.
- `Hotspot` gains three new public fields: `requiredItem : ItemData?`, `promptDialogue : DialogueReference`, `onUsed : UnityEvent?`. `GetDialogue(Collider?)` keeps its exact existing signature/behavior (still used by its own 4 existing tests) but its match logic moves into a new private `FindHotspot`.

- [ ] **Step 1: Write the failing tests**

Read the current test file first — `Game/CrimsonDraft/Assets/Tests/EditMode/ItemExamineHotspotsTests.cs` already has a `MakeHotspots` helper and 4 tests for `GetDialogue`. Leave those 4 tests untouched. Add a `FakeInventoryService`, an `ItemData` factory, and 4 new `Resolve` tests to the same file (add the fake as a private nested class, add the tests as new `[Test]` methods):

```csharp
private sealed class FakeInventoryService : IInventoryService
{
    private readonly HashSet<string> itemIds;
    public FakeInventoryService(params string[] presentItemIds) => this.itemIds = new HashSet<string>(presentItemIds);

    public bool HasItem(string itemId) => this.itemIds.Contains(itemId);
    public bool TryRemoveItem(string itemId) => this.itemIds.Remove(itemId);

    public IReadOnlyList<InventorySlot> Slots => Array.Empty<InventorySlot>();
    public int  SlotCount                                           => 0;
    public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
    public bool AddExistingItem(InventoryItem item, int operatorSlot)      => false;
    public bool AddItemAuto(ItemData data, int quantity = 0)               => false;
    public void RemoveItem(int slotIndex) { }
    public void PruneEmptyStacks() { }
    public void MoveItem(int fromSlot, int toSlot)         { }
    public void EquipWeapon(int slotIndex, int operatorSlot) { }
    public void UnequipWeapon(int slotIndex)               { }
    public int  GetEquippedWeaponIndex(int operatorSlot)   => -1;
    public bool CanReload(int slotIndex, int operatorSlot) => false;
    public void ReloadOperator(int slotIndex, int operatorSlot) { }
    public bool TryCombine(int slotA, int slotB)                       => false;
    public bool TryCombine(int slotA, int slotB, int resultSlot, out InventoryItem? combinedItem) { combinedItem = null; return false; }
    public KeyUseOutcome TryUseKey(string keyItemId)                   => new KeyUseOutcome(KeyUseResult.NotFound, -1);
    public void          SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
    public void          LoadState(InventorySlot[] slots)               { }
    public InventorySlot[] GetRawSlots()                               => Array.Empty<InventorySlot>();
}

private static ItemData MakeItemData(string itemId)
{
    var d  = ScriptableObject.CreateInstance<ItemData>();
    var so = new UnityEditor.SerializedObject(d);
    so.FindProperty("itemId").stringValue = itemId;
    so.ApplyModifiedPropertiesWithoutUndo();
    return d;
}

[Test]
public void Resolve_noRequiredItem_returnsText()
{
    var collider = new GameObject().AddComponent<BoxCollider>();
    var dialogue = new DialogueReference { nodeName = "hit_node" };
    var comp = MakeHotspots(
        new[] { new ItemExamineHotspots.Hotspot { collider = collider, dialogue = dialogue } },
        new DialogueReference { nodeName = "default_node" });

    var result = comp.Resolve(collider, new FakeInventoryService());

    Assert.IsNotNull(result);
    Assert.IsNull(result!.Value.Prompt);
    Assert.AreSame(dialogue, result.Value.Text);
}

[Test]
public void Resolve_requiredItemPresent_validPrompt_returnsPrompt()
{
    var collider     = new GameObject().AddComponent<BoxCollider>();
    var requiredItem = MakeItemData("small_key");
    // "examine_placeholder" is a real node already in the project's YarnProject,
    // already tagged `examine` -- used here only so DialogueReference.IsValid is
    // true; its actual text is irrelevant to this test.
    var prompt       = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" };
    var hotspot = new ItemExamineHotspots.Hotspot
    {
        collider       = collider,
        dialogue       = new DialogueReference { nodeName = "locked_node" },
        requiredItem   = requiredItem,
        promptDialogue = prompt,
    };
    var comp = MakeHotspots(new[] { hotspot }, new DialogueReference { nodeName = "default_node" });

    var result = comp.Resolve(collider, new FakeInventoryService("small_key"));

    Assert.IsNotNull(result);
    Assert.IsNotNull(result!.Value.Prompt);
    Assert.AreSame(prompt, result.Value.Prompt!.Value.Dialogue);
    Assert.AreSame(requiredItem, result.Value.Prompt.Value.RequiredItem);
}

[Test]
public void Resolve_requiredItemAbsent_fallsBackToText()
{
    var collider     = new GameObject().AddComponent<BoxCollider>();
    var requiredItem = MakeItemData("small_key");
    var lockedText   = new DialogueReference { nodeName = "locked_node" };
    var hotspot = new ItemExamineHotspots.Hotspot
    {
        collider       = collider,
        dialogue       = lockedText,
        requiredItem   = requiredItem,
        // promptDialogue.IsValid is never checked here -- HasItem returns false first --
        // so its content doesn't matter for this test.
        promptDialogue = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" },
    };
    var comp = MakeHotspots(new[] { hotspot }, new DialogueReference { nodeName = "default_node" });

    var result = comp.Resolve(collider, new FakeInventoryService()); // key not present

    Assert.IsNotNull(result);
    Assert.IsNull(result!.Value.Prompt);
    Assert.AreSame(lockedText, result.Value.Text);
}

[Test]
public void Resolve_requiredItemPresent_invalidPromptDialogue_fallsBackToText()
{
    var collider     = new GameObject().AddComponent<BoxCollider>();
    var requiredItem = MakeItemData("small_key");
    var lockedText   = new DialogueReference { nodeName = "locked_node" };
    var hotspot = new ItemExamineHotspots.Hotspot
    {
        collider       = collider,
        dialogue       = lockedText,
        requiredItem   = requiredItem,
        promptDialogue = new DialogueReference(), // unconfigured -- IsValid == false
    };
    var comp = MakeHotspots(new[] { hotspot }, new DialogueReference { nodeName = "default_node" });

    var result = comp.Resolve(collider, new FakeInventoryService("small_key"));

    Assert.IsNotNull(result);
    Assert.IsNull(result!.Value.Prompt);
    Assert.AreSame(lockedText, result.Value.Text);
}
```

`DialogueReference.IsValid` requires a non-null `project` with a compiled `Program` containing the node — `MakeYarnProjectStandIn()` doesn't exist yet; add it next to `MakeItemData`:

```csharp
private static Yarn.Unity.YarnProject MakeYarnProjectStandIn()
{
    // DialogueReference.IsValid checks project.Program.Nodes.ContainsKey(nodeName)
    // against a real compiled Program -- a bare ScriptableObject.CreateInstance()
    // has no compiled data, so tests that need IsValid == true load the project's
    // actual, already-imported YarnProject asset and reference one of its real
    // node names (e.g. "examine_placeholder") instead of a synthetic one.
    var project = UnityEditor.AssetDatabase.LoadAssetAtPath<Yarn.Unity.YarnProject>(
        "Assets/Dialogues/CrimsonDraft.yarnproject");
    Assert.IsNotNull(project, "Test requires Assets/Dialogues/CrimsonDraft.yarnproject to exist.");
    return project!;
}
```

`MakeYarnProjectStandIn` returns the real, already-imported project asset, so `"examine_placeholder"` (used above) resolves as a genuinely valid node — `DialogueReference.IsValid` checks `project.Program.Nodes.ContainsKey(nodeName)` against real compiled data, not a mock. `Resolve_requiredItemPresent_invalidPromptDialogue_fallsBackToText` deliberately uses an empty `DialogueReference()` instead, since that test wants an *invalid* reference.

The existing file's usings are `System.Reflection`, `NUnit.Framework`, `UnityEngine`, `Yarn.Unity`, `CrimsonDraft.Inventory` — add `System` (for `Array.Empty<T>()`) and `System.Collections.Generic` (for `HashSet<string>` and `IReadOnlyList<T>`) to the top of the test file.

- [ ] **Step 2: Run the tests to verify they fail**

Run via the UnityMCP `run_tests` tool with `filter: "ItemExamineHotspotsTests"`.
Expected: compile error — `Resolve`, `requiredItem`, `promptDialogue`, `ExaminePrompt`, `ExamineResolution` don't exist yet.

- [ ] **Step 3: Implement the new fields, types, and `Resolve`**

In `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemExamineHotspots.cs`, replace the whole file with:

```csharp
#nullable enable

using System;
using UnityEngine;
using UnityEngine.Events;
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

            // Optional item-use prompt. If requiredItem is null, this hotspot behaves
            // exactly as if these three fields didn't exist.
            public ItemData?         requiredItem;
            public DialogueReference promptDialogue;
            public UnityEvent?       onUsed;
        }

        [SerializeField] private Hotspot[] hotspots = Array.Empty<Hotspot>();
        [SerializeField] private DialogueReference defaultDialogue = new();

        private Hotspot? FindHotspot(Collider? hitCollider)
        {
            if (hitCollider == null) return null;
            foreach (var h in this.hotspots)
                if (h.collider == hitCollider) return h;
            return null;
        }

        public DialogueReference GetDialogue(Collider? hitCollider) =>
            FindHotspot(hitCollider)?.dialogue ?? this.defaultDialogue;

        // Takes IInventoryService as a parameter, not injected -- this component lives on
        // a prefab instantiated at runtime via Instantiate(), outside VContainer's build
        // graph (same reasoning as PickupPreviewView's own lack of injection).
        //
        // Returns null when nothing usable resolves at all (no matched hotspot and no
        // valid defaultDialogue, or a matched hotspot with an invalid dialogue and no
        // valid defaultDialogue either) -- callers fall back to ItemData.ExamineDialogue
        // in that case, same contract PickupPreviewView.TryGetExamineDialogue() already
        // documented before this method existed.
        public ExamineResolution? Resolve(Collider? hitCollider, IInventoryService inventory)
        {
            var hotspot = FindHotspot(hitCollider);

            if (hotspot is { } h
                && h.requiredItem != null
                && inventory.HasItem(h.requiredItem.ItemId)
                && h.promptDialogue.IsValid)
            {
                return ExamineResolution.ForPrompt(
                    new ExaminePrompt(h.promptDialogue, h.requiredItem, h.onUsed));
            }

            var candidate = hotspot?.dialogue;
            var text = (candidate != null && candidate.IsValid) ? candidate : this.defaultDialogue;
            return text.IsValid ? ExamineResolution.ForText(text) : null;
        }

        void OnDrawGizmosSelected() => DrawGizmos();

        // Exposed so other systems (PickupPreviewView's raycast debug gizmo) can draw
        // these same wireframes against a live runtime instance without needing to
        // separately select it in the hierarchy.
        public void DrawGizmos()
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

    public readonly struct ExaminePrompt
    {
        public readonly DialogueReference Dialogue;
        public readonly ItemData          RequiredItem;
        public readonly UnityEvent?       OnUsed;

        public ExaminePrompt(DialogueReference dialogue, ItemData requiredItem, UnityEvent? onUsed)
        {
            this.Dialogue     = dialogue;
            this.RequiredItem = requiredItem;
            this.OnUsed       = onUsed;
        }
    }

    public readonly struct ExamineResolution
    {
        public readonly DialogueReference? Text;
        public readonly ExaminePrompt?     Prompt;

        public static ExamineResolution ForText(DialogueReference text) => new(text, null);
        public static ExamineResolution ForPrompt(ExaminePrompt prompt) => new(null, prompt);

        private ExamineResolution(DialogueReference? text, ExaminePrompt? prompt)
        {
            this.Text   = text;
            this.Prompt = prompt;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run via the UnityMCP `run_tests` tool with `filter: "ItemExamineHotspotsTests"`.
Expected: PASS, 8/8 (the original 4 `GetDialogue` tests plus the 4 new `Resolve` tests).

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Inventory/ItemExamineHotspots.cs" "Game/CrimsonDraft/Assets/Tests/EditMode/ItemExamineHotspotsTests.cs"
git commit -m "feat(inventory): add ItemExamineHotspots.Resolve for item-required prompt hotspots"
```

---

### Task 3: `PickupPreviewView.TryGetExamineDialogue` → `ExamineResolution?`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PickupPreviewView.cs`

**Interfaces:**
- Consumes: `ItemExamineHotspots.Resolve(Collider?, IInventoryService) : ExamineResolution?` (Task 2).
- Produces: `PickupPreviewView.TryGetExamineDialogue(IInventoryService inventory) : ExamineResolution?` — replaces the old `TryGetExamineDialogue() : DialogueReference?`. Task 4 calls this with the new signature.

No automated test, same reasoning as before (raycast/physics not testable in this project's EditMode fakes pattern).

- [ ] **Step 1: Change the method**

In `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PickupPreviewView.cs`, replace (currently around lines 165-197):

```csharp
        // Returns null when the currently-shown item has no ItemExamineHotspots at all -- callers
        // should fall back to that item's own default examine text in that case. A non-null result
        // is always a valid, fully-resolved DialogueReference (hotspot-specific, or the component's
        // own defaultDialogue when the raycast didn't hit a registered hotspot).
        public DialogueReference? TryGetExamineDialogue()
        {
            if (this.currentInstance == null) return null;

            var hotspots = this.currentInstance.GetComponentInChildren<ItemExamineHotspots>();
            if (hotspots == null) return null;

            Collider? hitCollider = null;
            if (this.previewCamera != null)
            {
                // Player rotates the model via mountPoint.Rotate (SetRotationInput), a plain
                // Transform op -- with autoSyncTransforms disabled project-wide and Time.timeScale
                // at 0 while inspect is open (no physics step to pick it up naturally), PhysX
                // would otherwise see a stale collider pose here.
                Physics.SyncTransforms();

                int mask = this.previewLayer >= 0 ? 1 << this.previewLayer : ~0;
                if (Physics.Raycast(this.previewCamera.transform.position, this.previewCamera.transform.forward,
                        out var hit, Mathf.Infinity, mask, QueryTriggerInteraction.Collide))
                {
                    hitCollider = hit.collider;
                }
            }

            var dialogue = hotspots.GetDialogue(hitCollider);
            // An unconfigured hotspot/default reference means "no answer" -- let the caller
            // fall back to ItemData.ExamineDialogue rather than typing out an empty string.
            return dialogue.IsValid ? dialogue : null;
        }
```

with:

```csharp
        // Returns null when the currently-shown item has no ItemExamineHotspots at all, or
        // when nothing usable resolves -- callers should fall back to that item's own
        // default examine text in that case. A non-null result is either text to type or a
        // prompt to run -- see ExamineResolution.
        public ExamineResolution? TryGetExamineDialogue(IInventoryService inventory)
        {
            if (this.currentInstance == null) return null;

            var hotspots = this.currentInstance.GetComponentInChildren<ItemExamineHotspots>();
            if (hotspots == null) return null;

            Collider? hitCollider = null;
            if (this.previewCamera != null)
            {
                // Player rotates the model via mountPoint.Rotate (SetRotationInput), a plain
                // Transform op -- with autoSyncTransforms disabled project-wide and Time.timeScale
                // at 0 while inspect is open (no physics step to pick it up naturally), PhysX
                // would otherwise see a stale collider pose here.
                Physics.SyncTransforms();

                int mask = this.previewLayer >= 0 ? 1 << this.previewLayer : ~0;
                if (Physics.Raycast(this.previewCamera.transform.position, this.previewCamera.transform.forward,
                        out var hit, Mathf.Infinity, mask, QueryTriggerInteraction.Collide))
                {
                    hitCollider = hit.collider;
                }
            }

            return hotspots.Resolve(hitCollider, inventory);
        }
```

- [ ] **Step 2: Compile and check the console**

Use the UnityMCP `refresh_unity` tool with `compile: "request"`, `scope: "scripts"`, then `read_console` filtered to `"error CS"`.
Expected: **errors** — `InspectPanel.cs`'s call site (`this.modelPreview?.TryGetExamineDialogue()`) doesn't match the new signature yet. This is expected; Task 4 fixes it. Confirm the errors are only in `InspectPanel.cs` and only about the `TryGetExamineDialogue` call, nothing else.

- [ ] **Step 3: Commit**

Do NOT commit yet — `InspectPanel.cs` won't compile until Task 4 lands. Leave this uncommitted; Task 4's commit will include both files.

---

### Task 4: `InspectPanel` orchestration

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/UI/Inventory/InspectPanel.cs`

**Interfaces:**
- Consumes: `PickupPreviewView.TryGetExamineDialogue(IInventoryService) : ExamineResolution?` (Task 3), `IInventoryService.HasItem`/`TryRemoveItem` (Task 1), `IDialogueService.StartDialogue(string, IReadOnlyDictionary<string,object>?, Action?, IReadOnlyDictionary<string,Action>?)` and `IDialogueService.IsRunning : bool` (already exist, `CrimsonDraft.Navigation.Dialogue` namespace).
- Both `IInventoryService` and `IDialogueService` are already registered in `NavigationScope` (`Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs:78,107`) — no scope changes needed.

No automated test, same reasoning as Task 3 (this only wires resolution output into the existing typewriter/dialogue systems).

- [ ] **Step 1: Add the two injected dependencies**

In `Game/CrimsonDraft/Assets/Scripts/UI/Inventory/InspectPanel.cs`, add `using CrimsonDraft.Navigation.Dialogue;` to the usings (near the existing `using CrimsonDraft.Navigation.Interactables.UI;`), and add two fields right after the existing injected fields:

```csharp
        [Inject] private InventorySfxData sfx   = null!;
        [Inject] private IInputService    input = null!;
        [Inject] private IInventoryService inventoryService = null!;
        [Inject] private IDialogueService  dialogueService  = null!;
```

- [ ] **Step 2: Extend the rotation lock to also hold during a prompt dialogue**

Replace:

```csharp
        void Update()
        {
            // Lock rotation while the examine text is being typed out -- otherwise the
            // player could rotate onto/off a hotspot mid-reveal for text that was already
            // resolved for a different aim.
            if (!IsOpen || this.modelPreview == null || this.isTyping) return;
            this.modelPreview.SetRotationInput(this.input.InventoryNavigate.ReadValue<Vector2>());
        }
```

with:

```csharp
        void Update()
        {
            // Lock rotation while the examine text is being typed out, or while a hotspot's
            // item-use prompt dialogue is running -- otherwise the player could rotate
            // onto/off a hotspot mid-interaction.
            if (!IsOpen || this.modelPreview == null || this.isTyping || this.dialogueService.IsRunning) return;
            this.modelPreview.SetRotationInput(this.input.InventoryNavigate.ReadValue<Vector2>());
        }
```

- [ ] **Step 3: Branch `OnConfirmPressed` on the resolution, and add `UseRequiredItem`**

Replace:

```csharp
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

with:

```csharp
            // Hotspot items resolve their text fresh on every press (the player may have
            // rotated the model between attempts); items without hotspots keep the text
            // cached at Open() time.
            var resolution = this.modelPreview?.TryGetExamineDialogue(this.inventoryService);

            if (resolution?.Prompt is { } prompt)
            {
                this.dialogueService.StartDialogue(
                    prompt.Dialogue.nodeName ?? string.Empty,
                    commands: new Dictionary<string, Action>
                    {
                        ["use_required_item"] = () => UseRequiredItem(prompt)
                    });
                return;
            }

            string text = resolution?.Text != null
                ? ExtractExamineText(resolution.Value.Text)
                : this.pendingExamineText;

            TypewriterRoutine(text).Forget();
        }

        // Registered as the "use_required_item" Yarn command for the duration of a
        // hotspot's prompt dialogue (see OnConfirmPressed) -- every prompt node's "Sí"
        // branch calls this same fixed command name.
        void UseRequiredItem(ExaminePrompt prompt)
        {
            if (this.inventoryService.TryRemoveItem(prompt.RequiredItem.ItemId))
                prompt.OnUsed?.Invoke();
        }
```

Add `using System;` and `using System.Collections.Generic;` to the top of the file if not already present (needed for `Action` and `Dictionary<,>`).

- [ ] **Step 4: Compile and check the console**

Use the UnityMCP `refresh_unity` tool with `compile: "request"`, `scope: "scripts"`, then `read_console` filtered to `"error CS"`.
Expected: no compile errors (this also resolves Task 3's expected errors from its Step 2).

- [ ] **Step 5: Run the full EditMode suite once**

Run via the UnityMCP `run_tests` tool with no filter (whole `EditMode` suite).
Expected: the same 17 pre-existing `InventoryServiceTests` failures as before this plan (unrelated caliber/ammo tests), plus all `ItemExamineHotspotsTests` (8/8) and everything else green. If any *new* class of failure appears beyond those 17, stop and investigate before proceeding — do not assume it's unrelated.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PickupPreviewView.cs" "Game/CrimsonDraft/Assets/Scripts/UI/Inventory/InspectPanel.cs"
git commit -m "feat(inventory): InspectPanel runs a Yarn prompt for item-required hotspots"
```

---

### Task 5: KI_05 lock content — author the prompt node and wire the prefab

**Files:**
- Create: `Game/CrimsonDraft/Assets/Dialogues/inventory/examine_KI_05_lock_prompt.yarn`
- Modify: `Game/CrimsonDraft/Assets/Prefabs/Items/Inspect/KI_05_Preview.prefab` (via UnityMCP, not hand-edited YAML)

**Interfaces:**
- None produced — this is content wiring that exercises Tasks 1-4 through real data. Task 6 verifies it.

`examine_KI_05_lock.yarn` (already exists, already tagged `examine`) is the hotspot's normal `dialogue` — the "you don't have the key yet" line. This task adds the prompt node the same hotspot switches to once the player has the key.

- [ ] **Step 1: Author the prompt node**

Create `Game/CrimsonDraft/Assets/Dialogues/inventory/examine_KI_05_lock_prompt.yarn`:

```
title: examine_KI_05_lock_prompt
tags: examine
---
¿Quieres usar {$item_name}?
-> Sí
    <<use_required_item>>
-> No
===
```

`{$item_name}` needs a `$item_name` variable set before this node runs, or it'll render literally. `IDialogueService.StartDialogue` (called from `InspectPanel.OnConfirmPressed`, Task 4 Step 3) does not currently pass a `variables` dictionary for this call — leave it that way for this task (the plan's scope is "the item can be used," not polished prompt copy). If `{$item_name}` renders as an unset-variable error or empty string in the Editor's dialogue UI during Task 6, replace it with a hardcoded line instead (e.g. `¿Quieres usar la llave pequeña?`) and note that in the Task 6 report — don't add variable-passing plumbing to `InspectPanel` for this; that's out of this plan's scope.

- [ ] **Step 2: Reimport the Yarn project and confirm the node compiled**

Via UnityMCP `execute_code`:

```csharp
UnityEditor.AssetDatabase.ImportAsset("Assets/Dialogues/CrimsonDraft.yarnproject", UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.AssetDatabase.Refresh();

var project = UnityEditor.AssetDatabase.LoadAssetAtPath<Yarn.Unity.YarnProject>("Assets/Dialogues/CrimsonDraft.yarnproject");
Yarn.Node node;
bool found = project.Program.Nodes.TryGetValue("examine_KI_05_lock_prompt", out node);
return "found=" + found + " tags=" + (found ? string.Join(",", node.Tags) : "");
```

Expected: `found=True tags=examine`.

- [ ] **Step 3: Find the lock hotspot's collider and the small-key ItemData**

The suitcase's lock hotspot collider and its `ItemExamineHotspots` component live inside `Assets/Prefabs/Items/Inspect/KI_05_Preview.prefab`. Open it in isolation to inspect its structure (via UnityMCP `manage_prefabs` `open_prefab_stage`, or `get_hierarchy`) and confirm which child collider is the lock hotspot (it should already be registered in the `ItemExamineHotspots` component's `Hotspots` list, pointing at `examine_KI_05_lock` — that's the hotspot this task adds `requiredItem`/`promptDialogue` to).

The required item is the small key — `KI_10` or `KI_12` (`Game/CrimsonDraft/Assets/Data/Interactables/Key Items/KI_10.asset` / `KI_12.asset`), both currently "A small key, just the right size for a padlock or a small mechanism." Confirm which one is intended for this specific lock (check with the person running this plan if ambiguous — both exist and are generic small-key text, so this is a content decision, not something inferable from the code).

- [ ] **Step 4: Set `requiredItem` and `promptDialogue` on the lock hotspot**

Via UnityMCP `manage_prefabs` with `action: "modify_contents"` (or `open_prefab_stage` + `manage_components`/`manage_gameobject` + `save_prefab_stage`), set the matched `Hotspots` array element's:
- `requiredItem` → the chosen `KI_10.asset` or `KI_12.asset`
- `promptDialogue.project` → `Assets/Dialogues/CrimsonDraft.yarnproject`
- `promptDialogue.nodeName` → `examine_KI_05_lock_prompt`

Leave `onUsed` empty for this task — no gameplay effect is wired yet (matches the spec's non-goals; a follow-up task decides what "unlocking" actually does).

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Dialogues/inventory/examine_KI_05_lock_prompt.yarn" "Game/CrimsonDraft/Assets/Dialogues/inventory/examine_KI_05_lock_prompt.yarn.meta" "Game/CrimsonDraft/Assets/Prefabs/Items/Inspect/KI_05_Preview.prefab"
git commit -m "content(inventory): wire KI_05 lock hotspot to the small key prompt"
```

---

### Task 6: Manual end-to-end verification

**Files:**
- None committed — this task only exercises Tasks 1-5 through the real Editor/game.

**Interfaces:**
- None — this task produces nothing later tasks depend on.

This is the only path with no automated coverage (raycast + physics + a live `DialogueRunner` session, same constraint as the original hotspots feature). Verification technique note, learned the hard way while building the original hotspot feature and directly applicable here:

- `PickupPreviewView`'s `Awake()` (which resolves `previewCamera`/`previewLayer`) only runs once its GameObject becomes active in the hierarchy — if `InspectPanel`'s GameObject chain starts inactive in the scene (it does, by default, until the player opens the inventory through the real UI), calling `PickupPreviewView.Show()` directly without going through the real "open inventory" flow leaves `previewCamera`/`previewLayer` at their C# defaults (null / -1), breaking the raycast. Either drive this through the *real* inventory-open UI flow (which activates that GameObject chain normally), or if scripting it directly for speed, invoke `PickupPreviewView`'s private `Awake()` via reflection first (see `CombatMenuControllerTests`' `InvokeConfirm` for the project's established reflection-bypass pattern).
- After any script-driven `Transform` change to a hotspot collider (e.g. repositioning it for a controlled test), call `Physics.SyncTransforms()` before raycasting — the project has `autoSyncTransforms` disabled and this doesn't happen automatically within a single frame.
- `PickupPreviewView`'s `autoRotate` (default on) will slowly rotate the model between separate tool calls in an automated test session — disable it via reflection for the duration of a scripted verification, or account for the drift.

- [ ] **Step 1: Verify the "don't have the key" path**

Enter Play Mode. Without the small key in inventory, get KI_05 into inventory, open Inspect, rotate to aim at the lock, press Confirm.
Expected: types `examine_KI_05_lock`'s text ("It's locked tight. It has a lock for a small key.") — unchanged from before this plan.

- [ ] **Step 2: Verify the "have the key" prompt path**

With the small key (KI_10 or KI_12, whichever Task 5 wired) also in inventory, aim at the lock again and press Confirm.
Expected: instead of typing text, the project's normal Yarn dialogue UI opens showing the prompt (or its hardcoded fallback if `{$item_name}` didn't render — see Task 5 Step 1's note). Model rotation input is locked while this dialogue is open.

- [ ] **Step 3: Verify "Sí" consumes the item**

Choose "Sí". Expected: the dialogue closes, control returns to `InspectPanel`. Check the inventory grid — the small key is gone. Aim at the lock and press Confirm again: expected to now show `examine_KI_05_lock`'s normal text again (since the key is gone, `HasItem` is false, falls back to `Text` — per this plan's Global Constraints, no different post-use text is expected yet).

- [ ] **Step 4: Verify "No" does nothing**

Repeat with a fresh key in inventory (or via a save/undo if easier), aim at the lock, press Confirm, choose "No" this time.
Expected: dialogue closes, key is still in inventory, no other state changed.

- [ ] **Step 5: Verify an unrelated hotspot-less item is unaffected**

Open any item with a `PreviewModel` but no `ItemExamineHotspots` component (e.g. `12ga_Box.asset`, used for this same check in the original feature's Task 4). Expected: still shows its normal `ExamineDialogue` text, completely unaffected by this plan's changes.

- [ ] **Step 6: Report result**

If all four branches behaved as expected, the feature is complete. If something didn't behave as expected, that's a bug in Tasks 1-5, not this task — go back and fix it there, re-run Task 6 from Step 1.
