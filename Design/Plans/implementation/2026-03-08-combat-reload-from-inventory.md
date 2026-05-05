# Combat Reload from Inventory — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Wire `IInventoryService.ReloadOperator` into the combat state machine so selecting Reload in the CommandPanel opens a SubPanel showing compatible ammo boxes, and confirming one actually reloads the weapon and consumes inventory items.

**Architecture:** Add `CrimsonDraft.Inventory` reference to `CrimsonDraft.Combat.asmdef` (no cycle). Inject `IInventoryService` into `CombatMenuController` and thread it down to `CommandPanelState` (builds the ammo list) and `SubPanelState` (executes the reload). A new `int[] ReloadAmmoBoxIndices` property on the controller bridges the index mapping between the two states.

**Tech Stack:** Unity EditMode NUnit tests, VContainer DI, `IInventoryService`/`InventoryService`, state machine pattern (CommandPanelState → SubPanelState).

---

### Task 1: Add `CrimsonDraft.Inventory` reference to Combat asmdef

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/CrimsonDraft.Combat.asmdef`

**Step 1: Add the reference**

In `CrimsonDraft.Combat.asmdef`, add `"CrimsonDraft.Inventory"` to the `references` array:

```json
"references": [
    "CrimsonDraft.Infrastructure",
    "CrimsonDraft.Operators",
    "CrimsonDraft.Inventory",
    "VContainer",
    "VContainer.Unity",
    "UniTask",
    "MessagePipe",
    "Unity.InputSystem",
    "DOTween.Modules",
    "Unity.TextMeshPro"
],
```

**Step 2: Verify project compiles**

Open Unity or run tests — confirm no compile errors. No tests change.

**Step 3: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/CrimsonDraft.Combat.asmdef
git commit -m "feat(combat): add CrimsonDraft.Inventory reference to Combat assembly"
```

---

### Task 2: Wire `IInventoryService` into `CombatMenuController`

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Step 1: Write the failing test**

In `CombatMenuControllerTests`, add `FakeInventoryService` (inner class, end of file before closing `}`) and update `BuildAndInit` to accept an optional inventory:

```csharp
private FakeInventoryService inventory = null!;

// In SetUp(), add:
this.inventory = new FakeInventoryService();

// Replace BuildAndInit():
private CombatMenuController BuildAndInit(FakeInventoryService? inv = null)
{
    var controller = new CombatMenuController(
        this.menuView, this.commandPanel, this.subPanel, this.shotCountView,
        this.publisher, this.aimView, this.battlefieldView, this.roster,
        inv ?? this.inventory);
    ((IInitializable)controller).Initialize();
    return controller;
}

// New fake at bottom of test class:
private sealed class FakeInventoryService : IInventoryService
{
    private readonly List<InventoryItem> items = new();
    public IReadOnlyList<InventoryItem> Items => this.items;

    public void AddItem(ItemData data, int quantity = 0) { }
    public void EquipWeapon(int itemIndex, int operatorSlot) { }
    public void UnequipWeapon(int itemIndex) { }
    public int  GetEquippedWeaponIndex(int operatorSlot) => -1;
    public bool CanReload(int ammoBoxIndex, int operatorSlot) => false;
    public void ReloadOperator(int ammoBoxIndex, int operatorSlot) { }

    // Test helper: inject a pre-built AmmoBoxItem as a compatible item
    public void AddCompatibleBox(AmmoBoxItem box) => this.items.Add(box);
    public int  ReloadCallCount  { get; private set; }
    public int  LastAmmoBoxIndex { get; private set; } = -1;
    public int  LastOperatorSlot { get; private set; } = -1;

    // Override CanReload/ReloadOperator to track calls
    // (replace the no-op implementations above with these):
}
```

> **Note:** The `FakeInventoryService` needs to be configurable: by default `CanReload` returns false, but tests can add boxes and override behavior. Use a flag-based approach:

Full `FakeInventoryService` to add as inner class inside `CombatMenuControllerTests`:

```csharp
private sealed class FakeInventoryService : IInventoryService
{
    private readonly List<InventoryItem>      items       = new();
    private readonly Dictionary<int, bool>    canReloadBy = new();

    public IReadOnlyList<InventoryItem> Items => this.items;
    public int  ReloadCallCount  { get; private set; }
    public int  LastAmmoBoxIndex { get; private set; } = -1;
    public int  LastOperatorSlot { get; private set; } = -1;

    public void AddItem(ItemData data, int quantity = 0) { }
    public void EquipWeapon(int itemIndex, int operatorSlot)  { }
    public void UnequipWeapon(int itemIndex)                  { }
    public int  GetEquippedWeaponIndex(int operatorSlot)      => -1;

    public bool CanReload(int ammoBoxIndex, int operatorSlot)
        => this.canReloadBy.TryGetValue(ammoBoxIndex, out bool v) && v;

    public void ReloadOperator(int ammoBoxIndex, int operatorSlot)
    {
        this.ReloadCallCount++;
        this.LastAmmoBoxIndex = ammoBoxIndex;
        this.LastOperatorSlot = operatorSlot;
    }

    /// <summary>Registers an ammo box item at the next index. canReload controls CanReload result.</summary>
    public void RegisterBox(AmmoBoxItem box, bool canReload)
    {
        int idx = this.items.Count;
        this.items.Add(box);
        this.canReloadBy[idx] = canReload;
    }
}
```

`FakeInventoryService` needs `using CrimsonDraft.Inventory;` — the test asmdef already references `CrimsonDraft.Inventory`, so this is fine.

**Step 2: Run tests — they will fail to compile** because `CombatMenuController`'s internal constructor doesn't accept `IInventoryService` yet.

**Step 3: Update `CombatMenuController`**

Add `using CrimsonDraft.Inventory;` at top.

Add field and property:
```csharp
private readonly IInventoryService inventory;

internal int[] ReloadAmmoBoxIndices { get; set; } = Array.Empty<int>();
```

Update the public constructor (add `IInventoryService inventory` as last param before `IInputService`):
```csharp
[Preserve]
public CombatMenuController(
    ICombatActionMenuView        menuView,
    ICommandPanelView            commandPanel,
    ISubPanelView                subPanel,
    IShotCountView               shotCountView,
    IPublisher<CombatEndedEvent> combatEndedPublisher,
    IAimView                     aimView,
    IBattlefieldView             battlefieldView,
    IOperatorRoster              roster,
    IInventoryService            inventory,
    IInputService                inputService)
{
    // ... existing assignments ...
    this.inventory    = inventory;
    this.inputService = inputService;
}
```

Update the internal test constructor (add `IInventoryService inventory`):
```csharp
internal CombatMenuController(
    ICombatActionMenuView        menuView,
    ICommandPanelView            commandPanel,
    ISubPanelView                subPanel,
    IShotCountView               shotCountView,
    IPublisher<CombatEndedEvent> combatEndedPublisher,
    IAimView                     aimView,
    IBattlefieldView             battlefieldView,
    IOperatorRoster              roster,
    IInventoryService            inventory)
{
    // ... existing assignments ...
    this.inventory = inventory;
}
```

In `Initialize()`, update the state constructors to pass `inventory` (Tasks 3 and 4 will use them, so add the parameter now even if the state constructors don't accept it yet — do this after Tasks 3 and 4 update the state classes):

> **Important:** Do NOT update the `Initialize()` state constructor calls yet — wait until Tasks 3 and 4 update the state classes. For now just add the field and property.

**Step 4: Run tests — all existing tests should pass**

Run: Unity Test Runner → EditMode → All

Expected: all previously passing tests still pass. The new `BuildAndInit` signature change is backwards compatible (uses default parameter).

**Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat): inject IInventoryService into CombatMenuController"
```

---

### Task 3: Update `CommandPanelState` — Reload opens SubPanel with inventory ammo

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs` (Initialize call)
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Step 1: Write failing tests**

In `CombatMenuControllerTests`, update the existing Reload test and add new ones:

```csharp
// REPLACE existing test (it will break — Reload now DOES show SubPanel):
// Old: CommandSelected_Reload_doesNotShowSubPanel
// New:
[Test]
public void CommandSelected_Reload_noCompatibleAmmo_showsSubPanelWithNoAmmoItem()
{
    // inventory has no compatible ammo (default FakeInventoryService)
    BuildAndInit();
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);

    Assert.IsTrue(this.subPanel.IsVisible);
    Assert.AreEqual(1, this.subPanel.LastShownItems.Length);
    Assert.AreEqual("NO AMMO", this.subPanel.LastShownItems[0].Label);
}

[Test]
public void CommandSelected_Reload_withCompatibleAmmo_showsSubPanelWithAmmoLabels()
{
    var inv = new FakeInventoryService();
    // Create a compatible ammo box
    var boxData = ScriptableObject.CreateInstance<AmmoBoxData>();
    // We need displayName — use SerializedObject like InventoryServiceTests does,
    // but that requires UnityEditor. Alternatively use reflection or accept "".
    // For simplicity, set via reflection:
    SetDisplayName(boxData, "9MM FMJ");
    var box = new AmmoBoxItem(boxData, 45);
    inv.RegisterBox(box, canReload: true);

    BuildAndInit(inv);
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);

    Assert.IsTrue(this.subPanel.IsVisible);
    Assert.AreEqual(1, this.subPanel.LastShownItems.Length);
    Assert.AreEqual("9MM FMJ ×45", this.subPanel.LastShownItems[0].Label);
}

[Test]
public void CommandSelected_Reload_withCompatibleAmmo_storesInventoryIndexMapping()
{
    var inv = new FakeInventoryService();
    var boxData = ScriptableObject.CreateInstance<AmmoBoxData>();
    SetDisplayName(boxData, "9MM FMJ");
    inv.RegisterBox(new AmmoBoxItem(boxData, 10), canReload: false); // index 0 — not compatible
    inv.RegisterBox(new AmmoBoxItem(boxData, 20), canReload: true);  // index 1 — compatible

    var c = BuildAndInit(inv);
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);

    // ReloadAmmoBoxIndices should map SubPanel[0] → inventory index 1
    Assert.AreEqual(1, c.ReloadAmmoBoxIndices.Length);
    Assert.AreEqual(1, c.ReloadAmmoBoxIndices[0]);
}
```

Add the `SetDisplayName` helper and update `FakeSubPanelView` to capture shown items:

```csharp
// Add to FakeSubPanelView:
public SubPanelItem[] LastShownItems { get; private set; } = Array.Empty<SubPanelItem>();
public void Show(SubPanelItem[] items, RectTransform __)
{
    this.LastShownItems = items;
    this.IsVisible = true;
}

// Add helper method to test class:
private static void SetDisplayName(ItemData data, string name)
{
    var so = new UnityEditor.SerializedObject(data);
    so.FindProperty("displayName").stringValue = name;
    so.ApplyModifiedPropertiesWithoutUndo();
}
```

**Step 2: Run tests — they fail** (`LastShownItems` doesn't exist yet, Reload still goes back to OperatorSelState).

**Step 3: Update `CommandPanelState`**

Add `using CrimsonDraft.Inventory;` at top.

Add field:
```csharp
private readonly IInventoryService inventory;
```

Update constructor signature (add `IInventoryService inventory` after `IBattlefieldView battlefieldView`):
```csharp
internal CommandPanelState(
    CombatMenuController  context,
    ICombatActionMenuView menuView,
    ICommandPanelView     commandPanel,
    ISubPanelView         subPanel,
    IBattlefieldView      battlefieldView,
    IOperatorRoster       roster,
    IInventoryService     inventory)
{
    // ... existing assignments ...
    this.inventory = inventory;
}
```

Replace the `CombatCommand.Reload` branch entirely:

```csharp
if (command == CombatCommand.Reload)
{
    int op = this.context.SelectedOperator;

    // Build list of compatible ammo boxes from inventory
    var compatibleIndices = new System.Collections.Generic.List<int>();
    var items             = new System.Collections.Generic.List<SubPanelItem>();

    for (int i = 0; i < this.inventory.Items.Count; i++)
    {
        if (this.inventory.CanReload(i, op) && this.inventory.Items[i] is AmmoBoxItem box)
        {
            compatibleIndices.Add(i);
            items.Add(new SubPanelItem($"{box.Data.DisplayName} \u00d7{box.Quantity}"));
        }
    }

    if (items.Count == 0)
        items.Add(new SubPanelItem("NO AMMO"));

    this.context.ReloadAmmoBoxIndices = compatibleIndices.ToArray();
    this.commandPanel.SetDimmed(true);
    this.subPanel.Show(items.ToArray(), this.commandPanel.PanelRect);
    this.context.TransitionTo(this.context.SubPanelState);
    return;
}
```

Also remove the now-unreachable `CombatCommand.Reload` entry from `GetItemsFor`:
```csharp
private static SubPanelItem[] GetItemsFor(CombatCommand command) => command switch
{
    CombatCommand.Items  => new[] { new SubPanelItem("MORPHINE"), new SubPanelItem("BANDAGE") },
    CombatCommand.Defend => new[] { new SubPanelItem("SHIELD") },
    _                    => Array.Empty<SubPanelItem>()
};
```

**Step 4: Update `CombatMenuController.Initialize()` to pass `inventory` to `CommandPanelState`**

```csharp
this.CommandPanelState = new CommandPanelState(
    this, this.menuView, this.commandPanel, this.subPanel,
    this.battlefieldView, this.roster, this.inventory);
```

**Step 5: Delete the old `CommandSelected_Reload_doesNotShowSubPanel` test** (it is replaced by the new tests in Step 1).

Also update `Reload_doesNotRefillAmmo_andShootRemainsUnavailable` — the test currently triggers Reload without completing SubPanel selection. After our change, Reload transitions to SubPanelState. The cancel input will close SubPanel and return to CommandPanel. Update the test:

```csharp
[Test]
public void Reload_noCompatibleAmmo_doesNotRefillAmmo_andShootRemainsUnavailable()
{
    // inventory has no compatible ammo (default)
    var c = BuildAndInit();
    this.menuView.RaiseOnOperatorSelected(0);

    // Empty the weapon via 6 shots
    for (int i = 0; i < 6; i++)
    {
        this.commandPanel.RaiseOnCommandSelected(CombatCommand.Shoot);
        InvokeConfirm(c);
        this.aimView.FireResolvedShots(new[] { new ResolvedShot(0, Vector2.zero, ShotZone.Miss, 0) });
        InvokeConfirm(c);
        this.menuView.RaiseOnOperatorSelected(0);
    }

    Assert.IsFalse(this.commandPanel.IsCommandEnabled(CombatCommand.Shoot));

    // Select Reload → SubPanel opens with NO AMMO → cancel back
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
    c.HandleCancelPressed(); // back to CommandPanel
    Assert.IsFalse(this.commandPanel.IsCommandEnabled(CombatCommand.Shoot));
}
```

**Step 6: Run tests — all should pass**

Run: Unity Test Runner → EditMode → All

**Step 7: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat-ui): Reload command opens SubPanel with compatible ammo from inventory"
```

---

### Task 4: Update `SubPanelState` — selecting ammo box executes reload

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/SubPanelState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs` (Initialize call)
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs`

**Step 1: Write failing tests**

```csharp
[Test]
public void SubPanel_selectAmmoBox_callsReloadOperator_andHidesSubPanel()
{
    var inv     = new FakeInventoryService();
    var boxData = ScriptableObject.CreateInstance<AmmoBoxData>();
    SetDisplayName(boxData, "9MM FMJ");
    inv.RegisterBox(new AmmoBoxItem(boxData, 30), canReload: true); // inventory index 0

    BuildAndInit(inv);
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);

    // SubPanel is visible with 1 compatible box
    Assert.IsTrue(this.subPanel.IsVisible);

    this.subPanel.RaiseOnItemSelected(0); // select the first (and only) box

    Assert.AreEqual(1,  inv.ReloadCallCount,  "ReloadOperator called once");
    Assert.AreEqual(0,  inv.LastAmmoBoxIndex, "correct inventory index");
    Assert.AreEqual(0,  inv.LastOperatorSlot, "correct operator slot");
    Assert.IsFalse(this.subPanel.IsVisible,   "SubPanel hidden after reload");
}

[Test]
public void SubPanel_selectNoAmmo_doesNotCallReloadOperator()
{
    // inventory has no compatible ammo
    var inv = new FakeInventoryService();

    BuildAndInit(inv);
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);

    // SubPanel shows "NO AMMO" at index 0
    this.subPanel.RaiseOnItemSelected(0);

    Assert.AreEqual(0, inv.ReloadCallCount, "ReloadOperator not called");
}

[Test]
public void SubPanel_reload_updatesAmmoHud()
{
    var inv     = new FakeInventoryService();
    var boxData = ScriptableObject.CreateInstance<AmmoBoxData>();
    SetDisplayName(boxData, "9MM FMJ");
    inv.RegisterBox(new AmmoBoxItem(boxData, 30), canReload: true);

    BuildAndInit(inv);
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
    this.subPanel.RaiseOnItemSelected(0);

    // menuView.SetOperatorAmmo should have been called for operator 0
    Assert.IsTrue(this.menuView.TryGetAmmo(0, out _));
}

[Test]
public void SubPanel_reload_transitionsBackToOperatorSelection()
{
    var inv     = new FakeInventoryService();
    var boxData = ScriptableObject.CreateInstance<AmmoBoxData>();
    SetDisplayName(boxData, "9MM FMJ");
    inv.RegisterBox(new AmmoBoxItem(boxData, 30), canReload: true);

    BuildAndInit(inv);
    this.menuView.RaiseOnOperatorSelected(0);
    this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
    this.subPanel.RaiseOnItemSelected(0);

    // CommandPanel should now be hidden (back in OperatorSelState)
    Assert.IsFalse(this.commandPanel.IsVisible);
}
```

Also add `RaiseOnItemSelected` to `FakeSubPanelView`:

```csharp
public void RaiseOnItemSelected(int index) => this.OnItemSelected?.Invoke(index);
```

**Step 2: Run tests — they fail** (`SubPanelState.OnItemSelected` is still a no-op).

**Step 3: Update `SubPanelState`**

Add `using CrimsonDraft.Inventory;` at top.

Add fields:
```csharp
private readonly IInventoryService     inventory;
private readonly IOperatorRoster       roster;
private readonly ICombatActionMenuView menuView;
```

Update constructor:
```csharp
internal SubPanelState(
    CombatMenuController  context,
    ISubPanelView         subPanel,
    IInventoryService     inventory,
    IOperatorRoster       roster,
    ICombatActionMenuView menuView)
{
    this.context   = context;
    this.subPanel  = subPanel;
    this.inventory = inventory;
    this.roster    = roster;
    this.menuView  = menuView;
}
```

Implement `OnItemSelected`:
```csharp
public void OnItemSelected(int index)
{
    int[] indices = this.context.ReloadAmmoBoxIndices;
    if (index >= indices.Length) return; // "NO AMMO" selected — do nothing

    int op = this.context.SelectedOperator;
    this.inventory.ReloadOperator(indices[index], op);

    var weapon = this.roster.Count > op ? this.roster[op].EquippedWeapon : null;
    this.menuView.SetOperatorAmmo(op, weapon?.CurrentAmmo ?? 0, weapon?.MaxAmmo ?? 0);

    this.subPanel.Hide();
    this.context.TransitionTo(this.context.OperatorSelState);
}
```

**Step 4: Update `CombatMenuController.Initialize()` to pass new params to `SubPanelState`**

```csharp
this.SubPanelState = new SubPanelState(
    this, this.subPanel, this.inventory, this.roster, this.menuView);
```

**Step 5: Run tests — all should pass**

Run: Unity Test Runner → EditMode → All

**Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/SubPanelState.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/UI/CombatMenuController.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/CombatMenuControllerTests.cs
git commit -m "feat(combat-ui): SubPanelState executes reload and updates ammo HUD on ammo box selection"
```

---

## Summary of all changed files

| File | Change |
|------|--------|
| `Scripts/Combat/CrimsonDraft.Combat.asmdef` | Add `CrimsonDraft.Inventory` reference |
| `Scripts/Combat/UI/CombatMenuController.cs` | Add `IInventoryService` + `ReloadAmmoBoxIndices`, update both constructors + `Initialize()` |
| `Scripts/Combat/States/CommandPanelState.cs` | Replace Reload stub with SubPanel-building logic |
| `Scripts/Combat/States/SubPanelState.cs` | Implement `OnItemSelected` for reload execution |
| `Tests/EditMode/CombatMenuControllerTests.cs` | Add `FakeInventoryService`, update `FakeSubPanelView`, add/update tests |
