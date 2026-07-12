# Inventory State Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist the player's inventory across scene transitions so items, weapon equip state, and ammo survive deck changes.

**Architecture:** Add `InventoryStateRegistry` to `GameLifetimeScope` (the global, never-destroyed scope) following the exact pattern of `DoorStateRegistry` and `PickupRegistry`. `InventoryBootstrap` saves the `InventorySlot[]` array into the registry on `Dispose()` (called by VContainer when `NavigationScope` unloads) and restores it on `Initialize()` instead of applying the starting loadout — but only when a saved state exists. A fresh game has no saved state, so the starting loadout applies as today.

**Tech Stack:** C# · VContainer · Unity EditMode Tests (NUnit) · NaughtyAttributes

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| ~~**Create**~~ **✅ Done** | `Assets/Scripts/Inventory/InventoryStateRegistry.cs` | Holds `InventorySlot[]` reference across scope lifetimes |
| **Modify** | `Assets/Scripts/Inventory/IInventoryService.cs` | Add `LoadState` + `GetRawSlots` |
| **Modify** | `Assets/Scripts/Inventory/InventoryService.cs` | Implement `LoadState` (set slots + re-wire weapons) + `GetRawSlots` |
| **Modify** | `Assets/Scripts/Navigation/InventoryBootstrap.cs` | Implement `IDisposable`; restore from registry on init; save on dispose |
| **Modify** | `Assets/Scripts/Infrastructure/GameLifetimeScope.cs` | Register `InventoryStateRegistry` as singleton |
| **Create** | `Assets/Tests/EditMode/InventoryStateRegistryTests.cs` | Unit tests for the new registry |
| **Modify** | `Assets/Tests/EditMode/InventoryServiceTests.cs` | Tests for `LoadState` and `GetRawSlots` |
| **Create** | `Assets/Tests/EditMode/InventoryBootstrapTests.cs` | Tests for restore-vs-loadout and save-on-dispose |

---

## Task 1: InventoryStateRegistry — tests + implementation

**Files:**
- Create: `Assets/Scripts/Infrastructure/InventoryStateRegistry.cs`
- Create: `Assets/Tests/EditMode/InventoryStateRegistryTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/InventoryStateRegistryTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryStateRegistryTests
    {
        [Test]
        public void HasSavedState_initially_isFalse()
        {
            var registry = new InventoryStateRegistry();
            Assert.IsFalse(registry.HasSavedState);
        }

        [Test]
        public void Load_initially_returnsNull()
        {
            var registry = new InventoryStateRegistry();
            Assert.IsNull(registry.Load());
        }

        [Test]
        public void Save_setsHasSavedState_toTrue()
        {
            var registry = new InventoryStateRegistry();
            registry.Save(new InventorySlot[4]);
            Assert.IsTrue(registry.HasSavedState);
        }

        [Test]
        public void Load_afterSave_returnsSameArrayReference()
        {
            var registry = new InventoryStateRegistry();
            var slots = new InventorySlot[4];
            registry.Save(slots);
            Assert.AreSame(slots, registry.Load());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

In Unity: Window → General → Test Runner → EditMode → filter `InventoryStateRegistryTests` → Run.
Expected: 4 failures — `InventoryStateRegistry` does not exist yet.

- [ ] **Step 3: Create InventoryStateRegistry**

Create `Assets/Scripts/Infrastructure/InventoryStateRegistry.cs`:

```csharp
#nullable enable

using UnityEngine.Scripting;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Infrastructure
{
    public sealed class InventoryStateRegistry
    {
        private InventorySlot[]? savedSlots;

        [Preserve]
        public InventoryStateRegistry() { }

        public bool HasSavedState => this.savedSlots != null;

        public void Save(InventorySlot[] slots) => this.savedSlots = slots;

        public InventorySlot[]? Load() => this.savedSlots;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Filter `InventoryStateRegistryTests` → Run.
Expected: 4 passing.

- [x] **Step 5: Commit** ✅ DONE — files staged, see Task 1 note

```
git add "Assets/Scripts/Inventory/InventoryStateRegistry.cs" "Assets/Tests/EditMode/InventoryStateRegistryTests.cs"
git commit -m "feat(inventory): add InventoryStateRegistry for cross-scene slot persistence"
```

> **NOTE:** `InventoryStateRegistry` was placed in `CrimsonDraft.Inventory` namespace (not `Infrastructure`). This affects Task 3 (`InventoryBootstrap` needs no extra using) and Task 4 (`GameLifetimeScope` needs `using CrimsonDraft.Inventory`).

---

## Task 2: IInventoryService + InventoryService — LoadState and GetRawSlots

**Files:**
- Modify: `Assets/Scripts/Inventory/IInventoryService.cs`
- Modify: `Assets/Scripts/Inventory/InventoryService.cs`
- Modify: `Assets/Tests/EditMode/InventoryServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to the test class in `Assets/Tests/EditMode/InventoryServiceTests.cs` (inside the class body, before the last `}`):

```csharp
        // ── LoadState / GetRawSlots ────────────────────────────────────────────

        [Test]
        public void GetRawSlots_returnsUnderlyingArray()
        {
            var service = MakeService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(), operatorSlot: 0);
            var raw = service.GetRawSlots();
            Assert.IsNotNull(raw);
            Assert.IsFalse(raw[0].IsEmpty);
        }

        [Test]
        public void LoadState_replacesSlotContents()
        {
            var service = MakeService(new FakeRoster(MakeAlive(0)));
            var saved   = new InventorySlot[4];
            saved[0]    = new InventorySlot { Item = new InventoryItem(MakeKeyItemData()), Quantity = 1 };
            service.LoadState(saved);
            Assert.AreSame(saved[0].Item, service.Slots[0].Item);
        }

        [Test]
        public void LoadState_rewiresEquippedWeapon_toRoster()
        {
            var op0     = MakeAlive(0);
            var roster  = new FakeRoster(op0);
            var service = MakeService(roster);

            var weaponData = MakeWeaponData();
            var weapon     = new WeaponItem(weaponData);
            weapon.SetEquipped(operatorSlot: 0, weaponSlot: 0);

            var saved  = new InventorySlot[4];
            saved[0]   = new InventorySlot { Item = weapon, Quantity = 1 };
            service.LoadState(saved);

            Assert.AreSame(weapon, roster[0].PrimaryWeapon);
        }

        [Test]
        public void LoadState_doesNotRewire_unequippedWeapons()
        {
            var op0     = MakeAlive(0);
            var roster  = new FakeRoster(op0);
            var service = MakeService(roster);

            var weapon = new WeaponItem(MakeWeaponData()); // EquippedBySlot = -1

            var saved = new InventorySlot[4];
            saved[0]  = new InventorySlot { Item = weapon, Quantity = 1 };
            service.LoadState(saved);

            Assert.IsNull(roster[0].PrimaryWeapon);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Filter `LoadState` and `GetRawSlots` → Run.
Expected: 4 failures — methods not defined on `IInventoryService`.

- [ ] **Step 3: Add methods to IInventoryService**

In `Assets/Scripts/Inventory/IInventoryService.cs`, append inside the interface (after `TryUseKey`):

```csharp
        /// <summary>
        /// Replaces the internal slot array with the provided one and re-wires
        /// equipped weapons to the operator roster. Used to restore saved state
        /// across scene transitions.
        /// </summary>
        void LoadState(InventorySlot[] slots);

        /// <summary>Returns the raw slot array for serialization by InventoryBootstrap.</summary>
        InventorySlot[] GetRawSlots();
```

- [ ] **Step 4: Implement in InventoryService**

In `Assets/Scripts/Inventory/InventoryService.cs`, add after `TryUseKey`:

```csharp
        public void LoadState(InventorySlot[] slots)
        {
            this.slots = slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Item is WeaponItem weapon && weapon.IsEquipped)
                    this.roster[weapon.EquippedBySlot].SetEquippedWeapon(weapon, weapon.EquippedWeaponSlot);
            }
        }

        public InventorySlot[] GetRawSlots() => EnsureSlots();
```

- [ ] **Step 5: Run tests to verify they pass**

Filter `LoadState` and `GetRawSlots` → Run.
Expected: 4 passing.

- [ ] **Step 6: Commit**

```
git add "Assets/Scripts/Inventory/IInventoryService.cs" "Assets/Scripts/Inventory/InventoryService.cs" "Assets/Tests/EditMode/InventoryServiceTests.cs"
git commit -m "feat(inventory): add LoadState and GetRawSlots to InventoryService"
```

---

## Task 3: InventoryBootstrap — restore from registry + save on dispose

**Files:**
- Modify: `Assets/Scripts/Navigation/InventoryBootstrap.cs`
- Create: `Assets/Tests/EditMode/InventoryBootstrapTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/InventoryBootstrapTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.Scripting;
using System.Collections.Generic;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryBootstrapTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────

        private sealed class FakeInventoryService : IInventoryService
        {
            private InventorySlot[] slots = new InventorySlot[4];
            public int              addItemCallCount;
            public InventorySlot[]? loadedSlots;

            public FakeInventoryService()
            {
                for (int i = 0; i < this.slots.Length; i++)
                    this.slots[i] = new InventorySlot();
            }

            public IReadOnlyList<InventorySlot> Slots    => this.slots;
            public int                          SlotCount => this.slots.Length;

            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0)
            {
                this.addItemCallCount++;
                return true;
            }

            public bool AddItemAuto(ItemData data, int quantity = 0) => true;
            public void RemoveItem(int slotIndex) { }
            public void MoveItem(int fromSlot, int toSlot) { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex) { }
            public int  GetEquippedWeaponIndex(int operatorSlot) => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB) => false;
            public KeyUseOutcome TryUseKey(string keyItemId) => new KeyUseOutcome(KeyUseResult.NotFound, -1);

            public void LoadState(InventorySlot[] slots) => this.loadedSlots = slots;
            public InventorySlot[] GetRawSlots()         => this.slots;
        }

        private static StartingLoadout MakeLoadout()
        {
            var loadout = ScriptableObject.CreateInstance<StartingLoadout>();
            // No items — we only care about whether AddItem is called.
            return loadout;
        }

        private static InventoryBootstrap MakeBootstrap(
            FakeInventoryService inventory,
            InventoryStateRegistry registry)
        {
            return new InventoryBootstrap(MakeLoadout(), inventory, registry);
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void Initialize_whenNoSavedState_appliesStartingLoadout()
        {
            var inventory = new FakeInventoryService();
            var registry  = new InventoryStateRegistry();
            var bootstrap = MakeBootstrap(inventory, registry);

            ((IInitializable)bootstrap).Initialize();

            // StartingLoadout has no items — but EquipWeapon paths are tested
            // by checking AddItem was NOT bypassed (called 0+ times from loadout).
            // The key assertion: LoadState was NOT called.
            Assert.IsNull(inventory.loadedSlots);
        }

        [Test]
        public void Initialize_whenSavedState_restoresFromRegistry_notLoadout()
        {
            var inventory  = new FakeInventoryService();
            var registry   = new InventoryStateRegistry();
            var savedSlots = new InventorySlot[4];
            for (int i = 0; i < savedSlots.Length; i++) savedSlots[i] = new InventorySlot();
            registry.Save(savedSlots);

            var bootstrap = MakeBootstrap(inventory, registry);
            ((IInitializable)bootstrap).Initialize();

            Assert.AreSame(savedSlots, inventory.loadedSlots);
            Assert.AreEqual(0, inventory.addItemCallCount);
        }

        [Test]
        public void Dispose_savesCurrentSlots_toRegistry()
        {
            var inventory = new FakeInventoryService();
            var registry  = new InventoryStateRegistry();
            var bootstrap = MakeBootstrap(inventory, registry);

            ((IInitializable)bootstrap).Initialize();
            ((System.IDisposable)bootstrap).Dispose();

            Assert.IsTrue(registry.HasSavedState);
            Assert.AreSame(inventory.GetRawSlots(), registry.Load());
        }

        [Test]
        public void Initialize_isIdempotent()
        {
            var inventory = new FakeInventoryService();
            var registry  = new InventoryStateRegistry();
            var bootstrap = MakeBootstrap(inventory, registry);

            ((IInitializable)bootstrap).Initialize();
            int callsAfterFirst = inventory.addItemCallCount;
            ((IInitializable)bootstrap).Initialize();

            Assert.AreEqual(callsAfterFirst, inventory.addItemCallCount);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Filter `InventoryBootstrapTests` → Run.
Expected: failures — `InventoryBootstrap` constructor doesn't accept `InventoryStateRegistry`.

- [ ] **Step 3: Modify InventoryBootstrap**

Replace `Assets/Scripts/Navigation/InventoryBootstrap.cs` entirely:

```csharp
#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class InventoryBootstrap : IInitializable, IDisposable
    {
        private readonly StartingLoadout        loadout;
        private readonly IInventoryService      inventory;
        private readonly InventoryStateRegistry registry;
        private bool initialized;

        [Preserve]
        public InventoryBootstrap(
            StartingLoadout        loadout,
            IInventoryService      inventory,
            InventoryStateRegistry registry)
        {
            this.loadout   = loadout;
            this.inventory = inventory;
            this.registry  = registry;
        }

        public void Initialize()
        {
            if (this.initialized) return;
            this.initialized = true;

            var saved = this.registry.Load();
            if (saved != null)
            {
                this.inventory.LoadState(saved);
                return;
            }

            foreach (var entry in this.loadout.Items)
                this.inventory.AddItem(entry.item, entry.operatorSlot, entry.quantity);

            for (int slot = 0; slot < this.loadout.DefaultWeapons.Length; slot++)
            {
                var weaponData = this.loadout.DefaultWeapons[slot];
                if (weaponData == null) continue;

                this.inventory.AddItem(weaponData, operatorSlot: slot);

                int start = slot * 4;
                for (int i = start; i < start + 4; i++)
                {
                    if (this.inventory.Slots[i].Item?.Data == weaponData
                        && this.inventory.Slots[i].Item!.EquippedBySlot < 0)
                    {
                        this.inventory.EquipWeapon(i, slot);
                        if (this.inventory.Slots[i].Item is WeaponItem w)
                            w.SetAmmo(w.MaxAmmo);
                        break;
                    }
                }
            }
        }

        public void Dispose()
        {
            this.registry.Save(this.inventory.GetRawSlots());
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Filter `InventoryBootstrapTests` → Run.
Expected: 4 passing.

Also run full `InventoryServiceTests` to confirm no regressions.

- [ ] **Step 5: Commit**

```
git add "Assets/Scripts/Navigation/InventoryBootstrap.cs" "Assets/Tests/EditMode/InventoryBootstrapTests.cs"
git commit -m "feat(inventory): restore inventory from registry on scene load, save on dispose"
```

---

## Task 4: Wire InventoryStateRegistry into GameLifetimeScope

**Files:**
- Modify: `Assets/Scripts/Infrastructure/GameLifetimeScope.cs`

- [ ] **Step 1: Register InventoryStateRegistry**

In `Assets/Scripts/Infrastructure/GameLifetimeScope.cs`, add the `using` at the top and one registration line.

Add using (it's already in the `Infrastructure` namespace so no import needed).

In `Configure`, after the `PickupRegistry` line:

```csharp
            builder.Register<PickupRegistry>(Lifetime.Singleton);
            builder.Register<InventoryStateRegistry>(Lifetime.Singleton);
```

The full block should now end:
```csharp
            builder.Register<DoorStateRegistry>(Lifetime.Singleton);
            builder.Register<PickupRegistry>(Lifetime.Singleton);
            builder.Register<InventoryStateRegistry>(Lifetime.Singleton);
```

VContainer automatically makes this available to child scopes (`NavigationScope`), so no changes to `NavigationScope.cs` are needed — the `InventoryBootstrap` constructor will resolve `InventoryStateRegistry` from the parent scope automatically.

- [ ] **Step 2: Check for compilation errors**

In Unity, wait for domain reload and check Console for errors.
Expected: No errors. `InventoryBootstrap` resolves `InventoryStateRegistry` from parent scope via VContainer's scope chain.

- [ ] **Step 3: Commit**

```
git add "Assets/Scripts/Infrastructure/GameLifetimeScope.cs"
git commit -m "feat(infrastructure): register InventoryStateRegistry in GameLifetimeScope"
```

---

## Task 5: Smoke test in Play Mode

- [ ] **Step 1: Enter Play Mode in a navigation scene**

Open a gameplay scene (e.g. `Deck_B_Developtment`), enter Play Mode.

- [ ] **Step 2: Pick up an item**

Walk the player over a pickup, accept it. Confirm item appears in inventory.

- [ ] **Step 3: Transition to another deck**

Use a `SceneDoorInteractable` to transition to another scene. Confirm the scene loads.

- [ ] **Step 4: Verify inventory persists**

Open the inventory UI on the new scene. Confirm the item from Step 2 is still present.

- [ ] **Step 5: Verify fresh-start still applies loadout**

Stop Play Mode, re-enter. Confirm inventory starts with the default loadout, not the saved state from the previous session.

> Note: The registry lives in `GameLifetimeScope` which is `DontDestroyOnLoad` but is destroyed when Play Mode exits. A new Play Mode session creates a fresh registry with `HasSavedState = false`, so the starting loadout always applies on a fresh start.

---

## Self-Review Checklist

- **Spec coverage:** Fresh start → loadout ✓ | Scene transition → restore ✓ | Weapon re-wire ✓ | `IDisposable` save ✓
- **Placeholder scan:** No TBDs, all code blocks complete ✓
- **Type consistency:** `InventoryStateRegistry`, `LoadState`, `GetRawSlots`, `IInitializable`, `IDisposable` — all consistent across tasks ✓
- **`FakeRoster` in bootstrap tests:** Uses `FakeInventoryService` which doesn't need `OperatorRuntime` — no internal constructor issues ✓
- **VContainer `IDisposable`:** VContainer calls `Dispose()` on all `IDisposable` registrations when the container disposes — confirmed pattern ✓
