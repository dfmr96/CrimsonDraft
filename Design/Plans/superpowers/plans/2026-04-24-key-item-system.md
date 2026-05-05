# Key Item System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `KeyItem` type to the inventory with finite, tracked uses; expose `TryUseKey` on `IInventoryService`; wire `DoorInteractable` to use it and show a discard prompt on last use.

**Architecture:** `KeyItemData` (ScriptableObject, static data) + `KeyItem` (runtime wrapper, carries `UsesRemaining`) mirror the existing `ConsumableData`/`ConsumableItem` pattern. `IInventoryService.TryUseKey` finds the key by `itemId`, decrements uses, and returns a `KeyUseOutcome` struct so callers know the slot index for an optional `RemoveItem` call. `DoorInteractable` is updated to call `TryUseKey` and show a two-step PoiController dialog on depletion (use → discard prompt).

**Tech Stack:** C# · Unity ScriptableObjects · VContainer · NUnit (EditMode tests) · `PoiController` for discard prompt UI

**Spec:** [docs/superpowers/specs/2026-04-24-key-item-design.md](../specs/2026-04-24-key-item-design.md)

---

### Task 1: Add `KeyItem` to `ItemType` and create `KeyItemData`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemType.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyItemData.cs`

- [ ] **Step 1: Add `KeyItem` to the enum**

Open `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemType.cs`. Replace the file content with:

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public enum ItemType { Weapon, AmmoBox, Consumable, KeyItem }
}
```

- [ ] **Step 2: Create `KeyItemData`**

Create `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyItemData.cs`:

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "KeyItemData", menuName = "CrimsonDraft/Inventory/Key Item Data")]
    public sealed class KeyItemData : ItemData
    {
        [SerializeField] [Min(1)] private int maxUses = 1;

        public int MaxUses => this.maxUses;
    }
}
```

- [ ] **Step 3: Check compilation**

In Unity, open Window → Console. Confirm no errors appear after Unity recompiles.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/ItemType.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/KeyItemData.cs
git commit -m "feat(inventory): add KeyItem type and KeyItemData ScriptableObject"
```

---

### Task 2: Create `KeyItem` runtime class and unit tests

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyItem.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Open `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs`.

Add a `MakeKeyItemData` helper alongside the existing `MakeConsumableData`:

```csharp
private static KeyItemData MakeKeyItemData(string? id = null, int maxUses = 1)
{
    var d  = ScriptableObject.CreateInstance<KeyItemData>();
    var so = new UnityEditor.SerializedObject(d);
    so.FindProperty("itemId").stringValue      = id ?? System.Guid.NewGuid().ToString();
    so.FindProperty("itemType").enumValueIndex = (int)ItemType.KeyItem;
    so.FindProperty("displayName").stringValue = "Test Key";
    so.FindProperty("maxUses").intValue        = maxUses;
    so.ApplyModifiedPropertiesWithoutUndo();
    return d;
}
```

Add a new test class section at the bottom of the file, before the closing `}` of the outer class:

```csharp
// ── KeyItem.Consume ────────────────────────────────────────────────────

[Test]
public void KeyItem_Consume_decrementsUsesRemaining_andReturnsFalse_whenNotLastUse()
{
    var key      = new KeyItem(MakeKeyItemData(maxUses: 2));
    bool result  = key.Consume();
    Assert.AreEqual(1, key.UsesRemaining);
    Assert.IsFalse(result);
}

[Test]
public void KeyItem_Consume_returnsTrueAndZero_onLastUse()
{
    var key     = new KeyItem(MakeKeyItemData(maxUses: 1));
    bool result = key.Consume();
    Assert.IsTrue(result);
    Assert.AreEqual(0, key.UsesRemaining);
}

[Test]
public void KeyItem_Consume_returnsTrueWithoutDecrement_whenAlreadyZero()
{
    var key = new KeyItem(MakeKeyItemData(maxUses: 1));
    key.Consume();
    bool result = key.Consume();
    Assert.IsTrue(result);
    Assert.AreEqual(0, key.UsesRemaining, "must not go below 0");
}
```

- [ ] **Step 2: Run tests to confirm they fail**

In Unity: Window → General → Test Runner → EditMode → Run All.
Expected: 3 new tests fail with "KeyItem does not exist in namespace".

- [ ] **Step 3: Create `KeyItem`**

Create `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyItem.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class KeyItem : InventoryItem
    {
        public new KeyItemData Data     => (KeyItemData)base.Data;
        public int             UsesRemaining { get; private set; }

        public KeyItem(KeyItemData data) : base(data)
        {
            this.UsesRemaining = data.MaxUses;
        }

        /// <summary>
        /// Decrements UsesRemaining. Returns true if it reached 0 (including if already 0).
        /// </summary>
        public bool Consume()
        {
            if (this.UsesRemaining == 0) return true;
            this.UsesRemaining--;
            return this.UsesRemaining == 0;
        }
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

Window → Test Runner → EditMode → Run All.
Expected: 3 new `KeyItem_Consume_*` tests PASS. All previously passing tests still pass.

- [ ] **Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/KeyItem.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs
git commit -m "feat(inventory): add KeyItem runtime class with Consume() and unit tests"
```

---

### Task 3: Add `KeyUseResult` enum and `KeyUseOutcome` struct

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyUseResult.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyUseOutcome.cs`

- [ ] **Step 1: Create `KeyUseResult`**

Create `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyUseResult.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public enum KeyUseResult
    {
        Success,          // use registered; key has remaining uses
        DepletedAfterUse, // use registered; key reached 0 uses — caller shows discard prompt
        AlreadyDepleted,  // key found but already at 0 uses; use not registered
        NotFound          // no slot contains a KeyItem with the given itemId
    }
}
```

- [ ] **Step 2: Create `KeyUseOutcome`**

Create `Game/CrimsonDraft/Assets/Scripts/Inventory/KeyUseOutcome.cs`:

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public readonly struct KeyUseOutcome
    {
        public KeyUseResult Result    { get; }
        public int          SlotIndex { get; } // -1 when Result is NotFound

        public KeyUseOutcome(KeyUseResult result, int slotIndex)
        {
            this.Result    = result;
            this.SlotIndex = slotIndex;
        }
    }
}
```

- [ ] **Step 3: Confirm compilation**

Unity console — no errors.

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/KeyUseResult.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/KeyUseOutcome.cs
git commit -m "feat(inventory): add KeyUseResult enum and KeyUseOutcome struct"
```

---

### Task 4: Add `TryUseKey` to `IInventoryService` and implement in `InventoryService`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add the following section to `InventoryServiceTests.cs`, before the final closing `}`:

```csharp
// ── TryUseKey ─────────────────────────────────────────────────────────

[Test]
public void TryUseKey_returnsNotFound_whenKeyNotInInventory()
{
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    var outcome = service.TryUseKey("missing-key");
    Assert.AreEqual(KeyUseResult.NotFound, outcome.Result);
    Assert.AreEqual(-1, outcome.SlotIndex);
}

[Test]
public void TryUseKey_returnsSuccess_whenKeyHasMultipleUsesRemaining()
{
    var data    = MakeKeyItemData(id: "key-b", maxUses: 3);
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    service.AddItem(data, operatorSlot: 0);

    var outcome = service.TryUseKey("key-b");

    Assert.AreEqual(KeyUseResult.Success, outcome.Result);
    Assert.AreEqual(0, outcome.SlotIndex);
    Assert.IsFalse(service.Slots[0].IsEmpty, "key stays in slot");
    Assert.AreEqual(2, ((KeyItem)service.Slots[0].Item!).UsesRemaining);
}

[Test]
public void TryUseKey_returnsDepletedAfterUse_onLastUse_andKeyRemainsInSlot()
{
    var data    = MakeKeyItemData(id: "key-c", maxUses: 1);
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    service.AddItem(data, operatorSlot: 0);

    var outcome = service.TryUseKey("key-c");

    Assert.AreEqual(KeyUseResult.DepletedAfterUse, outcome.Result);
    Assert.AreEqual(0, outcome.SlotIndex);
    Assert.IsFalse(service.Slots[0].IsEmpty, "key not auto-removed");
    Assert.AreEqual(0, ((KeyItem)service.Slots[0].Item!).UsesRemaining);
}

[Test]
public void TryUseKey_returnsAlreadyDepleted_whenKeyIsAtZeroUses()
{
    var data    = MakeKeyItemData(id: "key-d", maxUses: 1);
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    service.AddItem(data, operatorSlot: 0);
    service.TryUseKey("key-d"); // depletes it

    var outcome = service.TryUseKey("key-d"); // second call

    Assert.AreEqual(KeyUseResult.AlreadyDepleted, outcome.Result);
    Assert.AreEqual(0, outcome.SlotIndex);
}

[Test]
public void AddItem_keyItem_placesKeyItemInSlot()
{
    var data    = MakeKeyItemData(id: "key-e", maxUses: 2);
    var service = MakeService(new FakeRoster(MakeAlive(0)));
    bool result = service.AddItem(data, operatorSlot: 0);

    Assert.IsTrue(result);
    var key = service.Slots[0].Item as KeyItem;
    Assert.IsNotNull(key);
    Assert.AreEqual(2, key!.UsesRemaining);
}
```

- [ ] **Step 2: Run tests to confirm they fail**

Test Runner → EditMode → Run All.
Expected: 5 new tests fail with "`TryUseKey` does not exist" or `KeyItem` cast errors.

- [ ] **Step 3: Add `TryUseKey` to `IInventoryService`**

Open `Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs`. Add after the `TryCombine` summary:

```csharp
/// <summary>
/// Finds the first KeyItem with the given itemId, decrements its uses, and returns the outcome.
/// The key is never auto-removed — caller must call RemoveItem(outcome.SlotIndex) to discard it.
/// </summary>
KeyUseOutcome TryUseKey(string keyItemId);
```

- [ ] **Step 4: Implement `TryUseKey` in `InventoryService`**

Open `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs`. Add after `TryCombine`:

```csharp
public KeyUseOutcome TryUseKey(string keyItemId)
{
    var s = EnsureSlots();
    for (int i = 0; i < s.Length; i++)
    {
        if (s[i].Item is not KeyItem key) continue;
        if (key.Data.ItemId != keyItemId) continue;

        if (key.UsesRemaining == 0)
            return new KeyUseOutcome(KeyUseResult.AlreadyDepleted, i);

        bool depleted = key.Consume();
        return new KeyUseOutcome(
            depleted ? KeyUseResult.DepletedAfterUse : KeyUseResult.Success,
            i);
    }
    return new KeyUseOutcome(KeyUseResult.NotFound, -1);
}
```

- [ ] **Step 5: Add `KeyItemData` case to `AddItem` switch in `InventoryService`**

In `InventoryService.AddItem`, find the switch expression that creates item instances. Add the `KeyItemData` case before the `_` throw:

```csharp
InventoryItem item = data switch
{
    WeaponData     wd => new WeaponItem(wd),
    AmmoBoxData    ad => new AmmoBoxItem(ad, quantity),
    ConsumableData cd => new ConsumableItem(cd),
    KeyItemData    kd => new KeyItem(kd),
    _ => throw new ArgumentException($"Unknown ItemData subtype: {data.GetType().Name}")
};
```

- [ ] **Step 6: Run tests to confirm they pass**

Test Runner → EditMode → Run All.
Expected: All 5 new `TryUseKey_*` and `AddItem_keyItem_*` tests PASS. All previous tests still pass.

- [ ] **Step 7: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs
git commit -m "feat(inventory): implement TryUseKey on InventoryService with KeyItemData support"
```

---

### Task 5: Add `onCancel` support to `PoiController`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PoiController.cs`

The door needs to show a Yes/No discard prompt after the use message. `PoiController` currently only supports one path (confirm → close). This task adds an optional `onCancel` callback triggered by UICancel, and fixes a re-entrancy bug (Close was nulling `onClose` after invoking it).

- [ ] **Step 1: Update `PoiController`**

Replace the full file content of `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PoiController.cs`:

```csharp
#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PoiController : IInitializable, IDisposable
    {
        private readonly IInputService inputService;
        private readonly PoiDialogView view;

        private string[] lines = Array.Empty<string>();
        private int      lineIndex;
        private bool     isOpen;
        private Action?  onClose;
        private Action?  onCancel;

        [Preserve]
        public PoiController(IInputService inputService, PoiDialogView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UIConfirm.performed += OnConfirm;
            this.inputService.UICancel.performed  += OnCancel;
        }

        public void Open(string[] poiLines, Action? onClose = null, Action? onCancel = null)
        {
            this.lines     = poiLines;
            this.lineIndex = 0;
            this.isOpen    = true;
            this.onClose   = onClose;
            this.onCancel  = onCancel;
            Time.timeScale  = 0f;
            this.inputService.SwitchToUI();
            this.view.Show(this.lines[0]);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;

            this.lineIndex++;

            if (this.lineIndex >= this.lines.Length)
            {
                Close();
                return;
            }

            this.view.Show(this.lines[this.lineIndex]);
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!this.isOpen || this.onCancel == null) return;
            var action    = this.onCancel;
            this.onClose  = null;
            this.onCancel = null;
            this.isOpen   = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            action.Invoke();
        }

        private void Close()
        {
            this.isOpen   = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            var action    = this.onClose;
            this.onClose  = null;
            this.onCancel = null;
            action?.Invoke();
        }

        void IDisposable.Dispose()
        {
            this.inputService.UIConfirm.performed -= OnConfirm;
            this.inputService.UICancel.performed  -= OnCancel;
        }
    }
}
```

- [ ] **Step 2: Confirm compilation**

Unity console — no errors.

- [ ] **Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PoiController.cs
git commit -m "feat(navigation): add onCancel callback to PoiController and fix Close re-entrancy"
```

---

### Task 6: Update `DoorData` and `DoorInteractable` to use `TryUseKey`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/DoorData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorInteractable.cs`

> **Note:** If any existing `DoorData` ScriptableObject assets in the project have a `keyItem` field pointing to a non-`KeyItemData` asset, that reference will be lost after changing the field type. Re-assign it in the Unity Inspector after this task.

- [ ] **Step 1: Change `DoorData.KeyItem` type to `KeyItemData?`**

Replace the full file content of `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/DoorData.cs`:

```csharp
#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DoorData")]
    public sealed class DoorData : ScriptableObject
    {
        [SerializeField] private bool          locked  = false;
        [SerializeField] private KeyItemData?  keyItem = null;

        public bool          Locked  => this.locked;
        public KeyItemData?  KeyItem => this.keyItem;
    }
}
```

- [ ] **Step 2: Rewrite `DoorInteractable.Interact` to use `TryUseKey`**

Replace the full file content of `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorInteractable.cs`:

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.Events;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorData   data   = null!;
        [SerializeField] private UnityEvent onOpen = new();

        private bool unlocked;

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked || this.unlocked)
            {
                this.onOpen.Invoke();
                return;
            }

            if (this.data.KeyItem == null)
            {
                context.PoiController.Open(new[] { "Locked." });
                return;
            }

            var keyItemData = this.data.KeyItem;
            var outcome     = context.InventoryService.TryUseKey(keyItemData.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                    context.PoiController.Open(new[] { $"You need: {keyItemData.DisplayName}." });
                    break;

                case KeyUseResult.AlreadyDepleted:
                    context.PoiController.Open(new[] { "Locked." });
                    break;

                case KeyUseResult.Success:
                    context.PoiController.Open(
                        new[] { $"Used {keyItemData.DisplayName}." },
                        onClose: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                        });
                    break;

                case KeyUseResult.DepletedAfterUse:
                    context.PoiController.Open(
                        new[] { $"Used {keyItemData.DisplayName}." },
                        onClose: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                            context.PoiController.Open(
                                new[] { $"Ya no necesitas {keyItemData.DisplayName}. ¿Deseas descartarla?" },
                                onClose: () => context.InventoryService.RemoveItem(outcome.SlotIndex),
                                onCancel: () => { });
                        });
                    break;
            }
        }
    }
}
```

- [ ] **Step 3: Confirm compilation**

Unity console — no errors. The `using System.Linq;` import was removed (it was only used by the deleted `FindKeyIndex` method).

- [ ] **Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/DoorData.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorInteractable.cs
git commit -m "feat(navigation): update DoorInteractable to use TryUseKey with discard prompt"
```

---

### Task 7: Update `InventoryView` context menu for `KeyItem`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs`

The existing `_` default case in `GetActionsForItem` already returns `{ Combine, Examine }` — which is correct for keys. This task adds an explicit `KeyItem` arm so future changes to the default don't silently affect keys.

- [ ] **Step 1: Add explicit `KeyItem` case**

In `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs`, find `GetActionsForItem` and update it:

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
        _                   => new List<ContextMenuAction> { ContextMenuAction.Combine, ContextMenuAction.Examine }
    };
```

- [ ] **Step 2: Confirm compilation**

Unity console — no errors.

- [ ] **Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs
git commit -m "feat(inventory): add explicit KeyItem context menu case in InventoryView"
```

---

### Task 8: Update GDD doc `Sistema de Inventario.md`

**Files:**
- Modify: `Sistema de Inventario.md` (vault root)
- Modify: `Game/CrimsonDraft/Assets/_Design/Sistema de Inventario.md` (Unity asset copy)

- [ ] **Step 1: Update the item types table**

In `Sistema de Inventario.md`, find the item types table:

```
| Tipo | Stackable por defecto | Acciones disponibles |
|---|---|---|
| Arma | No | Equipar / Desequipar, Combinar, Examinar |
| Caja de balas | Sí | Recargar, Combinar, Examinar |
| Consumible | No | Usar, Combinar, Examinar |
```

Replace it with:

```
| Tipo | Stackable por defecto | Acciones disponibles |
|---|---|---|
| Arma | No | Equipar / Desequipar, Combinar, Examinar |
| Caja de balas | Sí | Recargar, Combinar, Examinar |
| Consumible | No | Usar, Combinar, Examinar |
| Llave | No | Combinar, Examinar |
```

- [ ] **Step 2: Update the rules table**

In the same file, find the "Usar consumible" row in the rules table and ensure the table is consistent. The table should reflect that Usar is NOT available for llaves:

```
| Usar consumible | Solo desde slots del operador dueño |
```

No change needed here — keys do not have Usar, and the table only lists Consumible actions.

- [ ] **Step 3: Add a wikilink to the key system doc**

In the Pendiente section at the bottom of `Sistema de Inventario.md`, add:

```
- [x] Sistema de llaves con usos múltiples — ver [[Sistema de Llaves]] (spec: `docs/superpowers/specs/2026-04-24-key-item-design.md`)
```

- [ ] **Step 4: Copy to Assets/_Design/**

Copy the updated file content to `Game/CrimsonDraft/Assets/_Design/Sistema de Inventario.md` (keep both in sync).

- [ ] **Step 5: Commit**

```bash
git add "Sistema de Inventario.md"
git add "Game/CrimsonDraft/Assets/_Design/Sistema de Inventario.md"
git commit -m "docs(gdd): add Llave type to Sistema de Inventario item types table"
```

---

### Task 9: Run full test suite and verify

- [ ] **Step 1: Run all EditMode tests**

Unity Test Runner → EditMode → Run All.

Expected output (all pass):
```
KeyItem_Consume_decrementsUsesRemaining_andReturnsFalse_whenNotLastUse   PASS
KeyItem_Consume_returnsTrueAndZero_onLastUse                             PASS
KeyItem_Consume_returnsTrueWithoutDecrement_whenAlreadyZero              PASS
TryUseKey_returnsNotFound_whenKeyNotInInventory                          PASS
TryUseKey_returnsSuccess_whenKeyHasMultipleUsesRemaining                 PASS
TryUseKey_returnsDepletedAfterUse_onLastUse_andKeyRemainsInSlot          PASS
TryUseKey_returnsAlreadyDepleted_whenKeyIsAtZeroUses                     PASS
AddItem_keyItem_placesKeyItemInSlot                                      PASS
[... all pre-existing tests also pass ...]
```

- [ ] **Step 2: Manual smoke test in editor**

1. Create a `KeyItemData` asset: right-click in Project → Create → CrimsonDraft → Inventory → Key Item Data. Set `maxUses = 2`, assign an `itemId`.
2. In a test scene with a `DoorInteractable`, assign a `DoorData` with `Locked = true` and the new key asset as `KeyItem`.
3. Add the key to the player inventory via `StartingLoadout` or directly in the scene.
4. Enter play mode. Walk to the door and interact. Confirm the "Used X" message appears and the door opens.
5. Interact again with the same door (if not yet locked to another instance) — or set up a second door requiring the same key. On the last use, confirm the discard prompt appears. Press A (confirm) → key removed from inventory. Test again with B (cancel) → key stays with 0 uses.
6. With a depleted key (0 uses), interact with a door requiring that key → confirm the door stays locked.
7. Open inventory with the depleted key → confirm context menu shows only Combinar and Examinar.

- [ ] **Step 3: Final commit (if any fixups needed)**

```bash
git add -u
git commit -m "fix(inventory): key item system smoke test fixups"
```
