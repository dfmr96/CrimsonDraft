# Item Socket System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Item Socket system as defined in `Sistema de Item Socket.md` and `docs/superpowers/specs/2026-04-24-item-socket-design.md`.

**Architecture:** New `ItemType.SocketItem` + `SocketItemData` + `SocketItem` for the inventory layer. `IInteractionCaster` interface extracted from `PlayerInteractionCaster` with a `TryUseItem(ItemData)` method that reuses the existing raycast. `ItemSocketInteractable` MonoBehaviour accepts items via `TryInsert` and fires a `UnityEvent` when satisfied. `InventoryController` dispatches the `Use` context menu action to `IInteractionCaster` for SocketItems.

**Tech Stack:** Unity 6 / C# 9, VContainer DI, NUnit EditMode tests (UnityEditor.SerializedObject pattern).

---

## File Map

### Create
- `Game/CrimsonDraft/Assets/Scripts/Inventory/SocketItemData.cs`
- `Game/CrimsonDraft/Assets/Scripts/Inventory/SocketItem.cs`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IInteractionCaster.cs`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/ItemSocketInteractable.cs`
- `Game/CrimsonDraft/Assets/Tests/EditMode/ItemSocketInteractableTests.cs`

### Modify
- `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemType.cs` — add `SocketItem`
- `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs:67-73` — add `SocketItemData` factory case
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs` — implement `IInteractionCaster`, add `TryUseItem`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs:180-190` — add `SocketItem` to context menu
- `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryController.cs` — inject `IInteractionCaster`, dispatch Use
- `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs:49` — register as `IInteractionCaster`
- `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs` — add `MakeSocketItemData` helper + 1 test

---

## Task 1: SocketItemData, SocketItem, ItemType

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/SocketItemData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/SocketItem.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemType.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs`

- [ ] **Step 1: Write failing test in `InventoryServiceTests.cs`**

Add `MakeSocketItemData` helper after `MakeKeyItemData` (line ~114), then add the test at the bottom of the file before the closing `}`:

```csharp
private static SocketItemData MakeSocketItemData(string? id = null)
{
    var d  = ScriptableObject.CreateInstance<SocketItemData>();
    var so = new UnityEditor.SerializedObject(d);
    so.FindProperty("itemId").stringValue      = id ?? System.Guid.NewGuid().ToString();
    so.FindProperty("itemType").enumValueIndex = (int)ItemType.SocketItem;
    so.FindProperty("displayName").stringValue = "Test Socket Item";
    so.ApplyModifiedPropertiesWithoutUndo();
    return d;
}

[Test]
public void AddItem_socketItem_placesSocketItemInSlot()
{
    var data    = MakeSocketItemData(id: "socket-a");
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    bool result = service.AddItem(data, operatorSlot: 0);

    Assert.IsTrue(result);
    var item = service.Slots[0].Item as SocketItem;
    Assert.IsNotNull(item);
    Assert.AreEqual("socket-a", item!.Data.ItemId);
}
```

- [ ] **Step 2: Run test — expect compile error (SocketItemData doesn't exist yet)**

Open Unity Test Runner (Window > General > Test Runner > EditMode) and attempt to run `AddItem_socketItem_placesSocketItemInSlot`. Expected: compile error "type or namespace 'SocketItemData' could not be found".

- [ ] **Step 3: Create `SocketItemData.cs`**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "SocketItemData", menuName = "CrimsonDraft/Inventory/Socket Item Data")]
    public sealed class SocketItemData : ItemData { }
}
```

- [ ] **Step 4: Create `SocketItem.cs`**

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class SocketItem : InventoryItem
    {
        public new SocketItemData Data => (SocketItemData)base.Data;

        public SocketItem(SocketItemData data) : base(data) { }
    }
}
```

- [ ] **Step 5: Update `ItemType.cs`**

Replace the entire file:

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public enum ItemType { Weapon, AmmoBox, Consumable, KeyItem, SocketItem }
}
```

- [ ] **Step 6: Update `InventoryService.cs` — add SocketItemData factory case**

In `AddItem`, locate the `data switch` expression (lines ~67-73). Add `SocketItemData` before the discard arm:

```csharp
InventoryItem item = data switch
{
    WeaponData     wd => new WeaponItem(wd),
    AmmoBoxData    ad => new AmmoBoxItem(ad, quantity),
    ConsumableData cd => new ConsumableItem(cd),
    KeyItemData    kd => new KeyItem(kd),
    SocketItemData sd => new SocketItem(sd),
    _ => throw new ArgumentException($"Unknown ItemData subtype: {data.GetType().Name}")
};
```

- [ ] **Step 7: Run test — expect PASS**

Run `AddItem_socketItem_placesSocketItemInSlot` in Unity Test Runner. Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/SocketItemData.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/SocketItem.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/ItemType.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs
git commit -m "feat(inventory): add SocketItemData, SocketItem, ItemType.SocketItem"
```

---

## Task 2: IInteractionCaster + PlayerInteractionCaster.TryUseItem

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IInteractionCaster.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs`

No automated tests for this task — it requires `Physics.Raycast` and a running scene. Verified by compilation and manual play in Task 5.

- [ ] **Step 1: Create `IInteractionCaster.cs`**

```csharp
#nullable enable

using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public interface IInteractionCaster
    {
        bool TryUseItem(ItemData item);
    }
}
```

- [ ] **Step 2: Update `PlayerInteractionCaster.cs`**

Replace the entire file:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PlayerInteractionCaster : MonoBehaviour, IInteractionCaster
    {
        [SerializeField] private float     rayDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;

        private IInputService       inputService        = null!;
        private IInventoryService   inventoryService    = null!;
        private PoiController       poiController       = null!;
        private DocumentController  documentController  = null!;
        private ContainerController containerController = null!;

        [Inject]
        public void Construct(
            IInputService       inputService,
            IInventoryService   inventoryService,
            PoiController       poiController,
            DocumentController  documentController,
            ContainerController containerController)
        {
            this.inputService        = inputService;
            this.inventoryService    = inventoryService;
            this.poiController       = poiController;
            this.documentController  = documentController;
            this.containerController = containerController;
            this.inputService.Interact.performed += OnInteract;
        }

        private void OnDestroy()
        {
            if (this.inputService != null)
                this.inputService.Interact.performed -= OnInteract;
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return;

            if (!hit.collider.TryGetComponent<IInteractable>(out var interactable))
                return;

            var context = new InteractionContext(
                this.inventoryService,
                this.inputService,
                this.poiController,
                this.documentController,
                this.containerController);
            interactable.Interact(context);
        }

        public bool TryUseItem(ItemData item)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return false;

            if (!hit.collider.TryGetComponent<ItemSocketInteractable>(out var socket))
                return false;

            return socket.TryInsert(item, this.poiController);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var origin = transform.position;
            var tip    = origin + transform.forward * this.rayDistance;

            bool hit = Physics.Raycast(origin, transform.forward, out var hitInfo, this.rayDistance, this.interactableLayer);

            Gizmos.color = hit ? Color.green : Color.cyan;
            Gizmos.DrawRay(origin, transform.forward * this.rayDistance);
            Gizmos.DrawWireSphere(tip, 0.08f);

            if (hit)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(hitInfo.point, 0.12f);

                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(hitInfo.point + Vector3.up * 0.3f, hitInfo.collider.name);
            }
        }
#endif
    }
}
```

- [ ] **Step 3: Verify compilation in Unity**

Save all files. Check the Unity console (Window > General > Console). Expected: no compile errors. The `IInteractionCaster` reference in `PlayerInteractionCaster` resolves because both are in the same assembly (`CrimsonDraft.Navigation`). `ItemSocketInteractable` is forward-referenced — it will compile once Task 3 creates it.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IInteractionCaster.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs
git commit -m "feat(navigation): IInteractionCaster interface + TryUseItem on PlayerInteractionCaster"
```

---

## Task 3: ItemSocketInteractable

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/ItemSocketInteractable.cs`
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/ItemSocketInteractableTests.cs`

- [ ] **Step 1: Write failing tests in `ItemSocketInteractableTests.cs`**

```csharp
#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class ItemSocketInteractableTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static SocketItemData MakeSocketItemData(string id)
        {
            var d  = ScriptableObject.CreateInstance<SocketItemData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.SocketItem;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ConsumableData MakeConsumableData(string id)
        {
            var d  = ScriptableObject.CreateInstance<ConsumableData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Consumable;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ItemSocketInteractable MakeSocket(params SocketItemData[] required)
        {
            var go     = new GameObject();
            var socket = go.AddComponent<ItemSocketInteractable>();
            var so     = new UnityEditor.SerializedObject(socket);
            var arr    = so.FindProperty("requiredItems");
            arr.arraySize = required.Length;
            for (int i = 0; i < required.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = required[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return socket;
        }

        // ── TryInsert ─────────────────────────────────────────────────────────

        [Test]
        public void TryInsert_returnsTrue_whenItemIdMatches()
        {
            var data   = MakeSocketItemData("panel-a");
            var socket = MakeSocket(data);

            bool result = socket.TryInsert(data, poi: null);

            Assert.IsTrue(result);
        }

        [Test]
        public void TryInsert_returnsFalse_whenItemIdDoesNotMatch()
        {
            var required = MakeSocketItemData("panel-a");
            var wrong    = MakeSocketItemData("panel-b");
            var socket   = MakeSocket(required);

            bool result = socket.TryInsert(wrong, poi: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryInsert_returnsFalse_forNonSocketItemData()
        {
            var socketData   = MakeSocketItemData("panel-a");
            var consumable   = MakeConsumableData("consumable-x");
            var socket       = MakeSocket(socketData);

            bool result = socket.TryInsert(consumable, poi: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryInsert_returnsFalse_whenAlreadyActivated()
        {
            var data   = MakeSocketItemData("panel-a");
            var socket = MakeSocket(data);
            socket.TryInsert(data, poi: null); // activates (single-slot socket)

            bool result = socket.TryInsert(data, poi: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryInsert_activatesSocket_whenAllSlotsSatisfied()
        {
            var dataA  = MakeSocketItemData("panel-a");
            var dataB  = MakeSocketItemData("panel-b");
            var socket = MakeSocket(dataA, dataB);

            socket.TryInsert(dataA, poi: null);
            Assert.IsFalse(socket.IsActivated, "still one slot remaining");

            socket.TryInsert(dataB, poi: null);
            Assert.IsTrue(socket.IsActivated, "all slots satisfied");
        }

        [Test]
        public void TryInsert_canSatisfySameItemTwice_whenRequiredTwice()
        {
            var data   = MakeSocketItemData("battery");
            var socket = MakeSocket(data, data);

            bool first  = socket.TryInsert(data, poi: null);
            bool second = socket.TryInsert(data, poi: null);

            Assert.IsTrue(first,  "first insert accepted");
            Assert.IsTrue(second, "second insert accepted");
            Assert.IsTrue(socket.IsActivated);
        }

        [Test]
        public void TryInsert_doesNotSatisfyAlreadySatisfiedSlot_whenInsertingAgain()
        {
            var dataA  = MakeSocketItemData("panel-a");
            var dataB  = MakeSocketItemData("panel-b");
            var socket = MakeSocket(dataA, dataB);

            socket.TryInsert(dataA, poi: null);           // satisfies slot 0
            bool duplicate = socket.TryInsert(dataA, poi: null); // no unsatisfied slot for panel-a

            Assert.IsFalse(duplicate, "panel-a slot already satisfied, no second slot for it");
            Assert.IsFalse(socket.IsActivated, "panel-b still missing");
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile error (ItemSocketInteractable doesn't exist)**

Run tests in Unity Test Runner. Expected: compile error "type or namespace 'ItemSocketInteractable' could not be found".

- [ ] **Step 3: Create `ItemSocketInteractable.cs`**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ItemSocketInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private SocketItemData[] requiredItems = System.Array.Empty<SocketItemData>();
        [SerializeField] private UnityEvent       onActivated   = new();

        private bool[] inserted = System.Array.Empty<bool>();

        public bool IsActivated { get; private set; }

        public bool TryInsert(ItemData item, PoiController? poi)
        {
            if (this.IsActivated) return false;
            if (item is not SocketItemData) return false;

            var ins = EnsureInserted();
            for (int i = 0; i < this.requiredItems.Length; i++)
            {
                if (ins[i]) continue;
                if (this.requiredItems[i].ItemId != item.ItemId) continue;

                ins[i] = true;
                poi?.Open(new[] { $"Inserted: {item.DisplayName}." });

                if (IsComplete())
                {
                    this.IsActivated = true;
                    this.onActivated.Invoke();
                }

                return true;
            }

            poi?.Open(new[] { $"Can't use {item.DisplayName} here." });
            return false;
        }

        public void Interact(InteractionContext context)
        {
            if (this.IsActivated)
            {
                context.PoiController.Open(new[] { "Already activated." });
                return;
            }

            var ins   = EnsureInserted();
            var lines = new string[this.requiredItems.Length];
            for (int i = 0; i < this.requiredItems.Length; i++)
                lines[i] = $"{(ins[i] ? "[✓]" : "[ ]")} {this.requiredItems[i].DisplayName}";
            context.PoiController.Open(lines);
        }

        private bool[] EnsureInserted()
        {
            if (this.inserted.Length != this.requiredItems.Length)
                this.inserted = new bool[this.requiredItems.Length];
            return this.inserted;
        }

        private bool IsComplete()
        {
            var ins = EnsureInserted();
            for (int i = 0; i < ins.Length; i++)
                if (!ins[i]) return false;
            return ins.Length > 0;
        }
    }
}
```

- [ ] **Step 4: Run all EditMode tests — expect all PASS**

Run all EditMode tests in Unity Test Runner. Expected output:

```
ItemSocketInteractableTests.TryInsert_returnsTrue_whenItemIdMatches — PASS
ItemSocketInteractableTests.TryInsert_returnsFalse_whenItemIdDoesNotMatch — PASS
ItemSocketInteractableTests.TryInsert_returnsFalse_forNonSocketItemData — PASS
ItemSocketInteractableTests.TryInsert_returnsFalse_whenAlreadyActivated — PASS
ItemSocketInteractableTests.TryInsert_activatesSocket_whenAllSlotsSatisfied — PASS
ItemSocketInteractableTests.TryInsert_canSatisfySameItemTwice_whenRequiredTwice — PASS
ItemSocketInteractableTests.TryInsert_doesNotSatisfyAlreadySatisfiedSlot_whenInsertingAgain — PASS
InventoryServiceTests.AddItem_socketItem_placesSocketItemInSlot — PASS
```

All pre-existing tests must still pass.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/ItemSocketInteractable.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/ItemSocketInteractableTests.cs
git commit -m "feat(navigation): ItemSocketInteractable with TryInsert and tests"
```

---

## Task 4: InventoryView + InventoryController — wire Use command

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryController.cs`

- [ ] **Step 1: Update `InventoryView.cs` — add SocketItem to context menu**

In `GetActionsForItem` (line ~180), add the `SocketItem` case before the discard arm:

```csharp
private static List<ContextMenuAction> GetActionsForItem(InventoryItem item) =>
    item.Data.ItemType switch
    {
        ItemType.Weapon     => item.IsEquipped
                                ? new List<ContextMenuAction> { ContextMenuAction.Unequip, ContextMenuAction.Combine, ContextMenuAction.Examine }
                                : new List<ContextMenuAction> { ContextMenuAction.Equip,   ContextMenuAction.Combine, ContextMenuAction.Examine },
        ItemType.AmmoBox    => new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine },
        ItemType.Consumable => new List<ContextMenuAction> { ContextMenuAction.Use, ContextMenuAction.Combine, ContextMenuAction.Examine },
        ItemType.KeyItem    => new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine },
        ItemType.SocketItem => new List<ContextMenuAction> { ContextMenuAction.Use, ContextMenuAction.Combine, ContextMenuAction.Examine },
        _                   => new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine }
    };
```

- [ ] **Step 2: Update `InventoryController.cs` — add IInteractionCaster field and constructor parameter**

Add the field declaration alongside the other readonly fields (after `InventoryView view`):

```csharp
private readonly IInteractionCaster interactionCaster;
```

Add the using directive at the top of the file:

```csharp
using CrimsonDraft.Navigation.Interactables;
```

Replace the constructor signature and body assignment:

```csharp
[Preserve]
public InventoryController(
    IInputService      inputService,
    IInventoryService  inventoryService,
    IOperatorRoster    roster,
    InventoryView      view,
    IInteractionCaster interactionCaster)
{
    this.inputService       = inputService;
    this.inventoryService   = inventoryService;
    this.roster             = roster;
    this.view               = view;
    this.interactionCaster  = interactionCaster;
}
```

- [ ] **Step 3: Update `InventoryController.cs` — dispatch Use by ItemType**

Replace the `case ContextMenuAction.Use:` stub (currently just `break;`):

```csharp
case ContextMenuAction.Use:
{
    var item = this.inventoryService.Slots[this.cursorSlotIndex].Item;
    if (item?.Data.ItemType == ItemType.SocketItem)
    {
        if (this.interactionCaster.TryUseItem(item.Data))
            this.inventoryService.RemoveItem(this.cursorSlotIndex);
    }
    break;
}
```

- [ ] **Step 4: Verify compilation in Unity**

Check the Unity console. Expected: no compile errors. InventoryController now has 5 constructor parameters; VContainer will resolve `IInteractionCaster` after Task 5 registers it.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryController.cs
git commit -m "feat(inventory): wire Use command to ItemSocketInteractable via IInteractionCaster"
```

---

## Task 5: NavigationScope registration + end-to-end verification

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

- [ ] **Step 1: Update `NavigationScope.cs` — register PlayerInteractionCaster as IInteractionCaster**

Replace the existing `RegisterComponentInHierarchy<PlayerInteractionCaster>()` line with:

```csharp
builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
```

The full updated `Configure` method should have this line where `PlayerInteractionCaster` was previously registered (after `ContainerController`):

```csharp
builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
builder.RegisterComponentInHierarchy<PoiDialogView>();
builder.Register<PoiController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

- [ ] **Step 2: Verify compilation in Unity**

Check the Unity console. Expected: no compile errors. `InventoryController` will now receive `IInteractionCaster` from VContainer since `PlayerInteractionCaster` implements it.

- [ ] **Step 3: Run all EditMode tests — expect all PASS**

Run all EditMode tests in Unity Test Runner. All previously passing tests must still pass.

- [ ] **Step 4: Create a SocketItemData asset for manual testing**

In the Unity Project panel: right-click → Create → CrimsonDraft → Inventory → Socket Item Data. Name it `Panel_A_Data`. Set `itemId = "panel-a"`, `displayName = "Panel A"`.

- [ ] **Step 5: Place an ItemSocketInteractable in the test scene**

In the scene (Navigation.unity or FIX_Deck_B.unity):
1. Create a new empty GameObject. Name it `ItemSocket_Test`.
2. Add component `ItemSocketInteractable`.
3. Set `requiredItems` array size to 1. Assign `Panel_A_Data` to slot 0.
4. Add a `UnityEvent` listener to `onActivated` — connect it to any visible feedback (e.g., disable a door, log to console).
5. Place it in the Interactable layer at a position reachable by the player.

- [ ] **Step 6: Create a SocketItem pickup for manual testing**

Create a SocketItemData asset named `Panel_A_Data` (reuse from Step 4). Place a `PickupInteractable` GameObject in the scene referencing `Panel_A_Data`.

- [ ] **Step 7: Manual end-to-end test**

1. Enter Play mode.
2. Walk to the pickup → press Interact → `Panel_A_Data` added to inventory.
3. Open inventory (Tab/Start). Select `Panel_A_Data`. Press A (context menu). Select `Use`.
4. Expected with socket NOT in range: nothing happens, item stays in inventory.
5. Face the `ItemSocket_Test` from close range. Open inventory. Select `Panel_A_Data` → Use.
6. Expected: PoiController shows "Inserted: Panel A." — item removed from inventory — `onActivated` fires.
7. Open inventory again. Select any other SocketItem → Use while facing the socket.
8. Expected: PoiController shows "Already activated." ... wait, the `Use` from inventory won't trigger Interact. The "Already activated" message is only via the normal Interact button. Verify this: press Interact while facing the activated socket → "Already activated." ✓

- [ ] **Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(navigation): register PlayerInteractionCaster as IInteractionCaster in NavigationScope"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Task |
|---|---|
| `ItemType.SocketItem` enum value | Task 1 |
| `SocketItemData` ScriptableObject | Task 1 |
| `SocketItem` runtime wrapper | Task 1 |
| `IInteractionCaster.TryUseItem` | Task 2 |
| `PlayerInteractionCaster` implements it, reuses raycast | Task 2 |
| `ItemSocketInteractable` with `SocketItemData[] requiredItems` | Task 3 |
| `bool[] inserted` runtime state, `IsActivated` | Task 3 |
| `TryInsert` — match by itemId, consume on success, fire event | Task 3 |
| `Interact` — show checklist / "Already activated." | Task 3 |
| Context menu shows Use for SocketItem | Task 4 |
| `InventoryController` dispatches Use → `TryUseItem` → `RemoveItem` | Task 4 |
| `NavigationScope` registration | Task 5 |

**No placeholders.** All code blocks are complete.

**Type consistency:** `TryInsert(ItemData item, PoiController? poi)` is defined in Task 3 and called in Task 2. `IsActivated` is defined in Task 3. `IInteractionCaster.TryUseItem(ItemData)` is defined in Task 2 and called in Task 4. All consistent.
