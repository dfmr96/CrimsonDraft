# Dead Operator Loot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A KIA operator's card is dimmed with "KIA" and their items are unreachable, except while the player stands inside their corpse's trigger in navigation, where items can be moved between them and the living operators.

**Architecture:** The rule lives in `InventoryService`: carried containers are only living operators', and a dead operator's container is *accessible* (pick up / drop) only when `ICorpseAccess.CanAccess(slot)`. A `CorpseProximityTracker` (navigation) implements `ICorpseAccess`, fed by an `OperatorCorpse` trigger component on the spawned corpse. UI reads `IInventoryService.IsCarried/IsAccessible`: presenters skip inaccessible grids when building navigation, the cursor treats non-carried grids as transfer-only, and the card shows the overlay when dead and not lootable.

**Tech Stack:** Unity 6000.3 (C# 9), VContainer, MessagePipe, NUnit EditMode, Unity CLI (`unity recompile`, `unity command run_tests`, `unity command eval_file`).

**Spec:** `docs/superpowers/specs/2026-10-05-dead-operator-loot-design.md`

## Global Constraints

- `#nullable enable` in every new file; serialized fields `= null!`; `[Preserve]` on DI constructors.
- No scene edits — prefabs only, edited through the Editor (CLI `eval`), source prefabs (`Card.prefab`, `OperatorNavCorpse.prefab`), never `UI_Canvas` overrides.
- Unity CLI for compile/tests/play mode. Delete `Assets/Temp` after any screenshot; restore any TMP font asset Play Mode dirties (`git checkout` it).
- **No commits until the last task** (user preference). Never stage the user's unrelated changes: `MapData_DeckB/C.asset`, `Deck_B_Development.unity`, `Art/UI/Boot*`, `Port_Stairs.prefab`, `Save_Room.prefab`.
- No `Co-Authored-By` trailer (CLAUDE.md).
- Known baseline failures: 5 × `CrimsonDraft.Tests.ItemSocketInteractableTests.*`, flaky `ShotResolutionStrategyTests.PelletSpreadStrategy_positions_fallWithinEllipseBounds`.
- **Compile + tests:** `bash .superpowers/sdd/2026-10-05-dead-operator-loot/ut.sh [Fully.Qualified.TestClass ...]` (no args = full suite), created in Task 1 Step 0.

## Review Focus

1. Absent roster slots (`IsPresent == false`) are now non-carried and inaccessible — pickups must still land in the living operators, and presenters must still skip those grids (they have no grid anyway).
2. Swap from a living grid into an accessible dead grid: the displaced item returns to the living origin; swap from the dead grid into a living one returns the displaced item into the dead grid (still accessible because the inventory is paused). Covered by a test in Task 2.
3. Initialization order: `DeadOperatorGearReleaser` must run after `OperatorRosterBootstrap` restored HP, otherwise a loaded dead operator keeps their weapon equipped. Verified in Task 6 (save/load probe); `Restore` also refuses to wire weapons to dead operators.
4. Player with several colliders tagged `Player` entering one corpse trigger: access must stay until the last one leaves, and `OnDisable` must clear it. Covered in Task 4 tests.
5. Opening the storage box while next to a corpse: the dead grid must be in the operator row (accessible) and transfers storage ↔ dead must work. Verified in Task 6.

---

### Task 1: `ICorpseAccess` + `CorpseProximityTracker`

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/ICorpseAccess.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/CorpseProximityTracker.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs` (corpse registrations, ~line 169)
- Test: `Game/CrimsonDraft/Assets/Tests/EditMode/CorpseProximityTrackerTests.cs`

**Interfaces:**
- Produces: `CrimsonDraft.Inventory.ICorpseAccess { bool CanAccess(int operatorSlot); }`; `CrimsonDraft.Navigation.CorpseProximityTracker : ICorpseAccess` with `void Enter(int operatorSlot)`, `void Exit(int operatorSlot)`; registered `Singleton` `.AsSelf().As<ICorpseAccess>()`.

- [ ] **Step 0: Create the verification script** `.superpowers/sdd/2026-10-05-dead-operator-loot/ut.sh` (repo-root relative, git-ignored):

```bash
#!/usr/bin/env bash
set -uo pipefail
proj="/d/Proyectos Unity/CrimsonDraft/CrimsonDraft/Game/CrimsonDraft"
ws="$(cd "$(dirname "$0")" && pwd)"
cd "$proj"
if ! unity recompile --no-banner > "$ws/last-compile.log" 2>&1; then
  echo "COMPILE FAILED:"; tail -n 40 "$ws/last-compile.log"; exit 1
fi
filters=("$@"); [ ${#filters[@]} -eq 0 ] && filters=("")
rc=0
for f in "${filters[@]}"; do
  args=(--mode EditMode --timeout 900 --result-only); [ -n "$f" ] && args+=(--filter "$f")
  unity command run_tests "${args[@]}" > "$ws/last-tests.json" 2>&1
  python - "$ws/last-tests.json" "${f:-ALL}" <<'PY' || rc=1
import json, sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
try: data = json.loads(raw[raw.index("{"):])
except Exception: print("NO JSON RESULT:\n" + raw[-2000:]); sys.exit(1)
s = data.get("Summary", {})
fails = [r for r in data.get("Results", []) if r.get("Status") == "Failed"]
known = [r for r in fails if r["FullName"].startswith("CrimsonDraft.Tests.ItemSocketInteractableTests.") or r["FullName"] == "CrimsonDraft.Tests.ShotResolutionStrategyTests.PelletSpreadStrategy_positions_fallWithinEllipseBounds"]
new = [r for r in fails if r not in known]
print(f"[{sys.argv[2]}] total={s.get('Total')} passed={s.get('Passed')} failed={s.get('Failed')} (known-baseline={len(known)}, new={len(new)})")
for r in new: print("  FAIL", r["FullName"], "::", (r.get("Message") or "").strip()[:300])
sys.exit(1 if new or s.get("Total", 0) == 0 else 0)
PY
done
exit $rc
```

Run with no args. Expected: `[ALL] total=462 … new=0`.

- [ ] **Step 1: Write the failing tests** — `CorpseProximityTrackerTests.cs`:

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Tests
{
    public sealed class CorpseProximityTrackerTests
    {
        [Test]
        public void CanAccess_initially_false()
        {
            Assert.IsFalse(new CorpseProximityTracker().CanAccess(1));
        }

        [Test]
        public void Enter_grantsAccessToThatSlotOnly()
        {
            var tracker = new CorpseProximityTracker();
            tracker.Enter(1);
            Assert.IsTrue(tracker.CanAccess(1));
            Assert.IsFalse(tracker.CanAccess(2));
        }

        [Test]
        public void Exit_revokesAccess()
        {
            var tracker = new CorpseProximityTracker();
            tracker.Enter(1);
            tracker.Exit(1);
            Assert.IsFalse(tracker.CanAccess(1));
        }

        [Test]
        public void TwoCorpses_areIndependent()
        {
            var tracker = new CorpseProximityTracker();
            tracker.Enter(1);
            tracker.Enter(2);
            tracker.Exit(1);
            Assert.IsFalse(tracker.CanAccess(1));
            Assert.IsTrue(tracker.CanAccess(2));
        }

        [Test]
        public void Exit_withoutEnter_isNoOp()
        {
            var tracker = new CorpseProximityTracker();
            Assert.DoesNotThrow(() => tracker.Exit(3));
            Assert.IsFalse(tracker.CanAccess(3));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails.** `ut.sh CrimsonDraft.Tests.CorpseProximityTrackerTests`. Expected: COMPILE FAILED (`CorpseProximityTracker` not found).

- [ ] **Step 3: Implement.**

`ICorpseAccess.cs`:
```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public interface ICorpseAccess
    {
        bool CanAccess(int operatorSlot);
    }
}
```

`CorpseProximityTracker.cs`:
```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class CorpseProximityTracker : ICorpseAccess
    {
        private readonly HashSet<int> slotsInRange = new HashSet<int>();

        [Preserve]
        public CorpseProximityTracker() { }

        public bool CanAccess(int operatorSlot) => this.slotsInRange.Contains(operatorSlot);

        public void Enter(int operatorSlot) => this.slotsInRange.Add(operatorSlot);

        public void Exit(int operatorSlot) => this.slotsInRange.Remove(operatorSlot);
    }
}
```

`NavigationScope.cs`, right after `builder.RegisterInstance(this.corpseSettings);`:
```csharp
            builder.Register<CorpseProximityTracker>(Lifetime.Singleton).AsSelf().As<ICorpseAccess>();
```
(add `using CrimsonDraft.Inventory;` if not present).

- [ ] **Step 4: Run.** `ut.sh CrimsonDraft.Tests.CorpseProximityTrackerTests`. Expected: `total=5 passed=5 new=0`.

- [ ] **Step 5: Task done** (no commit — commits are deferred to Task 6).

---

### Task 2: Domain — carried = alive, accessibility gate

**Files:**
- Modify: `Scripts/Inventory/IInventoryService.cs`, `Scripts/Inventory/InventoryService.cs`, `Scripts/Inventory/InventoryService.Actions.cs`, `Scripts/Inventory/InventoryService.Held.cs`
- Modify: `Tests/EditMode/Fakes/InventoryTestData.cs`, `Tests/EditMode/Fakes/FakeInventoryService.cs`
- Create: `Tests/EditMode/Fakes/FakeCorpseAccess.cs`
- Modify (constructor call sites): `Tests/EditMode/InventoryServiceTests.cs` (helpers, lines 14–18), `Tests/EditMode/InventorySerializerTests.cs` (line 19), `Tests/EditMode/InventoryBootstrapTests.cs` (line 27)
- Test: `Tests/EditMode/InventoryServiceTests.Dead.cs` (new partial)

**Interfaces:**
- Consumes: `ICorpseAccess` (Task 1).
- Produces: `InventoryService(IOperatorRoster roster, ICombineService combineService, ICorpseAccess corpseAccess)`; `IInventoryService.IsCarried(ContainerId)`, `IInventoryService.IsAccessible(ContainerId)`; test helpers `InventoryTestData.Dead(int slot)`, `FakeCorpseAccess` (`Slots` set, `static None`), `FakeInventoryService.Inaccessible` (`HashSet<ContainerId>`).

- [ ] **Step 1: Write the failing tests.**

`Fakes/FakeCorpseAccess.cs`:
```csharp
#nullable enable

using System.Collections.Generic;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class FakeCorpseAccess : ICorpseAccess
    {
        public readonly HashSet<int> Slots = new HashSet<int>();

        public static FakeCorpseAccess None => new FakeCorpseAccess();

        public bool CanAccess(int operatorSlot) => this.Slots.Contains(operatorSlot);
    }
}
```

`InventoryTestData.cs`, after `Alive`:
```csharp
        public static OperatorRuntime Dead(int slot)
        {
            var op = Alive(slot);
            op.ApplyDamage(op.MaxHp);
            op.ApplyDamage(1);
            return op;
        }
```

`InventoryServiceTests.cs` helpers become:
```csharp
        private static InventoryService Service(int operators = 2, ICombineService? combine = null) =>
            new InventoryService(FakeRoster.WithOperators(operators), combine ?? FakeCombineService.None, FakeCorpseAccess.None);

        private static InventoryService Service(FakeRoster roster, ICombineService? combine = null, ICorpseAccess? access = null) =>
            new InventoryService(roster, combine ?? FakeCombineService.None, access ?? FakeCorpseAccess.None);
```
`InventorySerializerTests.cs` line 19 and `InventoryBootstrapTests.cs` line 27: append `, FakeCorpseAccess.None` as the third constructor argument.

`InventoryServiceTests.Dead.cs`:
```csharp
#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed partial class InventoryServiceTests
    {
        private static FakeRoster DeadFirst() => new FakeRoster(Dead(0), Alive(1));

        private static ContainerId Dead0 => ContainerId.Operator(0);

        [Test]
        public void TryAdd_automatic_skipsDeadOperator()
        {
            var s = Service(DeadFirst());
            Assert.IsTrue(s.TryAdd(Consumable()));
            Assert.AreEqual(0, Op(s, 0).Placements.Count);
            Assert.AreEqual(1, Op(s, 1).Placements.Count);
        }

        [Test]
        public void HasItem_andTryUseKey_ignoreDeadOperator()
        {
            var s = Service(DeadFirst());
            s.TryAdd(Key(id: "key"), Dead0);
            Assert.IsFalse(s.HasItem("key"));
            Assert.AreEqual(KeyUseResult.NotFound, s.TryUseKey("key").Result);
        }

        [Test]
        public void TryUseConsumable_onDeadOperatorItem_rejectedWithoutChanged()
        {
            var s = Service(DeadFirst());
            s.TryAdd(Consumable(), Dead0);
            var med = Only(Op(s, 0));
            int changes = CountChanges(Op(s, 0), () => Assert.IsFalse(s.TryUseConsumable(med, 1)));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void TryCombine_withDeadOperatorItem_rejected()
        {
            var s    = Service(DeadFirst());
            var data = Consumable(stackable: true);
            s.TryAdd(data, Dead0, 2);
            s.TryAdd(data, ContainerId.Operator(1), 2);
            Assert.IsFalse(s.TryCombine(Only(Op(s, 0)), Only(Op(s, 1)), out _));
            Assert.IsFalse(s.TryCombine(Only(Op(s, 1)), Only(Op(s, 0)), out _));
        }

        [Test]
        public void CanReload_withAmmoInDeadOperator_false()
        {
            var s = Service(DeadFirst());
            s.TryAdd(Weapon(), ContainerId.Operator(1));
            var weapon = (WeaponItem)Only(Op(s, 1));
            weapon.SetAmmo(0);
            s.Equip(weapon, 1);
            s.TryAdd(Ammo(), Dead0, 10);
            Assert.IsFalse(s.CanReload(Only(Op(s, 0)), 1));
        }

        [Test]
        public void Equip_weaponInDeadOperator_isNoOp()
        {
            var s = Service(DeadFirst());
            s.TryAdd(Weapon(), Dead0);
            var weapon = (WeaponItem)Only(Op(s, 0));
            s.Equip(weapon, 1);
            Assert.IsFalse(weapon.IsEquipped);
        }

        [Test]
        public void CanSplit_stackInDeadOperator_false()
        {
            var s = Service(DeadFirst());
            s.TryAdd(Consumable(stackable: true), Dead0, 4);
            Assert.IsFalse(s.CanSplit(Only(Op(s, 0))));
        }

        [Test]
        public void IsCarried_onlyLivingOperators()
        {
            var s = Service(DeadFirst());
            Assert.IsFalse(s.IsCarried(Dead0));
            Assert.IsTrue(s.IsCarried(ContainerId.Operator(1)));
            Assert.IsFalse(s.IsCarried(ContainerId.Storage));
        }

        [Test]
        public void IsAccessible_deadOnlyWithCorpseAccess()
        {
            var access = FakeCorpseAccess.None;
            var s      = Service(DeadFirst(), access: access);
            Assert.IsTrue(s.IsAccessible(ContainerId.Storage));
            Assert.IsTrue(s.IsAccessible(ContainerId.Operator(1)));
            Assert.IsFalse(s.IsAccessible(Dead0));
            access.Slots.Add(0);
            Assert.IsTrue(s.IsAccessible(Dead0));
        }

        [Test]
        public void TryPickUp_fromDeadWithoutAccess_rejectedWithoutChanged()
        {
            var s = Service(DeadFirst());
            s.TryAdd(Consumable(), Dead0);
            var med = Only(Op(s, 0));
            int changes = CountChanges(Op(s, 0), () => Assert.IsFalse(s.TryPickUp(med)));
            Assert.AreEqual(0, changes);
            Assert.IsNull(s.Held);
        }

        [Test]
        public void TryPickUp_fromDeadWithAccess_succeeds()
        {
            var access = FakeCorpseAccess.None;
            access.Slots.Add(0);
            var s = Service(DeadFirst(), access: access);
            s.TryAdd(Consumable(), Dead0);
            var med = Only(Op(s, 0));
            Assert.IsTrue(s.TryPickUp(med));
            Assert.AreSame(med, s.Held);
        }

        [Test]
        public void TryDrop_intoDeadWithoutAccess_rejected()
        {
            var s = Service(DeadFirst());
            s.TryAdd(Consumable(), ContainerId.Operator(1));
            var med = Only(Op(s, 1));
            s.TryPickUp(med);
            Assert.AreEqual(DropResult.Rejected, s.TryDrop(Dead0, Vector2Int.zero));
            Assert.AreSame(med, s.Held);
        }

        [Test]
        public void TryDrop_intoDeadWithAccess_placed()
        {
            var access = FakeCorpseAccess.None;
            access.Slots.Add(0);
            var s = Service(DeadFirst(), access: access);
            s.TryAdd(Consumable(), ContainerId.Operator(1));
            s.TryPickUp(Only(Op(s, 1)));
            Assert.AreEqual(DropResult.Placed, s.TryDrop(Dead0, Vector2Int.zero));
            Assert.AreEqual(1, Op(s, 0).Placements.Count);
        }

        [Test]
        public void Swap_fromDeadIntoLiving_returnsDisplacedIntoDead()
        {
            var access = FakeCorpseAccess.None;
            access.Slots.Add(0);
            var s = Service(DeadFirst(), access: access);
            s.TryAdd(Consumable(id: "dead-med"), Dead0);
            s.TryAdd(Consumable(id: "live-med"), ContainerId.Operator(1));
            var deadMed = Only(Op(s, 0));
            var liveMed = Only(Op(s, 1));

            s.TryPickUp(deadMed);
            Assert.AreEqual(DropResult.Swapped, s.TryDrop(ContainerId.Operator(1), Vector2Int.zero));
            Assert.AreSame(liveMed, s.Held);
            s.CancelHeld();

            Assert.AreSame(deadMed, Only(Op(s, 1)));
            Assert.AreSame(liveMed, Only(Op(s, 0)));
        }

        [Test]
        public void CancelHeld_returnsToDeadOrigin_evenAfterAccessLost()
        {
            var access = FakeCorpseAccess.None;
            access.Slots.Add(0);
            var s = Service(DeadFirst(), access: access);
            s.TryAdd(Consumable(), Dead0);
            var med = Only(Op(s, 0));
            s.TryPickUp(med);
            access.Slots.Remove(0);
            s.CancelHeld();
            Assert.AreSame(med, Only(Op(s, 0)));
        }
    }
}
```

`FakeInventoryService.cs`: add field and members (keeps the interface compiling):
```csharp
        public readonly HashSet<ContainerId> Inaccessible = new HashSet<ContainerId>();

        public bool IsCarried(ContainerId id)    => id != ContainerId.Storage && !this.Inaccessible.Contains(id);
        public bool IsAccessible(ContainerId id) => !this.Inaccessible.Contains(id);
```

- [ ] **Step 2: Run to verify it fails.** `ut.sh CrimsonDraft.Tests.InventoryServiceTests`. Expected: COMPILE FAILED (constructor takes 2 arguments; `IsCarried`/`IsAccessible` not defined).

- [ ] **Step 3: Implement.**

`IInventoryService.cs`, after `bool HasItem(string itemId);`:
```csharp
        bool IsCarried(ContainerId id);
        bool IsAccessible(ContainerId id);
```

`InventoryService.cs`:
- Field `private readonly ICorpseAccess corpseAccess;`; constructor
  `public InventoryService(IOperatorRoster roster, ICombineService combineService, ICorpseAccess corpseAccess)` assigning it.
- Add public members after `HasItem`:
```csharp
        public bool IsCarried(ContainerId id) =>
            id.Kind == ContainerKind.Operator && id.Index >= 0 && id.Index < this.roster.Count && this.roster[id.Index].IsAlive;

        public bool IsAccessible(ContainerId id) =>
            id == ContainerId.Storage
            || IsCarried(id)
            || (id.Kind == ContainerKind.Operator && this.corpseAccess.CanAccess(id.Index));
```
- `TryAdd(ItemData data, int quantity = 0) => TryAddTo(CarriedContainers().ToArray(), data, quantity);`
- New helper `private IEnumerable<ItemContainer> CarriedContainers() => EnsureContainers().Where(c => IsCarried(c.Id));`
- `FindCarriedContainerOf` → `CarriedContainers().FirstOrDefault(c => c.Contains(item));`
- `AllPlacements` → `CarriedContainers().SelectMany(c => c.Placements);`

`InventoryService.Actions.cs`, `TryCombineRecipe`: `var candidates = CarriedContainers().OrderBy(c => c == containerA ? 0 : 1).ToList();`

`InventoryService.Held.cs`:
- `TryPickUp`: after `if (container == null || placement == null) return false;` add `if (!IsAccessible(container.Id)) return false;`
- `CanSplit`: replace `EnsureContainers().Any(` with `CarriedContainers().Any(`.
- `TryDrop`: after the held null check add `if (!IsAccessible(target)) return DropResult.Rejected;`

- [ ] **Step 4: Run.** `ut.sh CrimsonDraft.Tests.InventoryServiceTests CrimsonDraft.Tests.InventorySerializerTests CrimsonDraft.Tests.InventoryBootstrapTests`. Expected: all pass, `new=0`.

- [ ] **Step 5: Full suite.** `ut.sh`. Expected: `new=0`.

---

### Task 3: Weapon release on death

**Files:**
- Modify: `Scripts/Inventory/IInventoryService.cs`, `Scripts/Inventory/InventoryService.Actions.cs`, `Scripts/Inventory/InventoryService.Persistence.cs` (`WireEquipped`)
- Create: `Scripts/Navigation/DeadOperatorGearReleaser.cs`
- Modify: `Scripts/Navigation/NavigationScope.cs` (after `OperatorCorpseBootstrap` registration)
- Modify: `Tests/EditMode/Fakes/FakeInventoryService.cs`
- Test: `Tests/EditMode/InventoryServiceTests.Dead.cs` (append), `Tests/EditMode/InventorySerializerTests.cs` (append), `Tests/EditMode/DeadOperatorGearReleaserTests.cs` (new)

**Interfaces:**
- Consumes: Task 2 constructor and helpers.
- Produces: `IInventoryService.ReleaseDeadOperatorWeapons()`; `DeadOperatorGearReleaser(IInventoryService, ISubscriber<CombatEndedEvent>)` (`IInitializable`, `IDisposable`); `FakeInventoryService.ReleaseCalls`.

- [ ] **Step 1: Write the failing tests.**

Append to `InventoryServiceTests.Dead.cs`:
```csharp
        [Test]
        public void ReleaseDeadOperatorWeapons_unequipsAndKeepsItemInPlace()
        {
            var roster = FakeRoster.WithOperators(2);
            var s      = Service(roster);
            s.TryAdd(Weapon(), Dead0);
            var weapon = (WeaponItem)Only(Op(s, 0));
            s.Equip(weapon, 0);
            var origin = Op(s, 0).GetPlacement(weapon)!.Origin;
            roster[0].ApplyDamage(roster[0].MaxHp);
            roster[0].ApplyDamage(1);

            int changes = CountChanges(Op(s, 0), s.ReleaseDeadOperatorWeapons);

            Assert.IsFalse(weapon.IsEquipped);
            Assert.IsNull(roster[0].PrimaryWeapon);
            Assert.AreEqual(origin, Op(s, 0).GetPlacement(weapon)!.Origin);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void ReleaseDeadOperatorWeapons_keepsLivingOperatorWeapon()
        {
            var roster = FakeRoster.WithOperators(2);
            var s      = Service(roster);
            s.TryAdd(Weapon(), ContainerId.Operator(1));
            var weapon = (WeaponItem)Only(Op(s, 1));
            s.Equip(weapon, 1);

            s.ReleaseDeadOperatorWeapons();

            Assert.IsTrue(weapon.IsEquipped);
            Assert.AreSame(weapon, roster[1].PrimaryWeapon);
        }
```

Append to `InventorySerializerTests.cs`:
```csharp
        [Test]
        public void Restore_doesNotEquipWeaponToDeadOperator()
        {
            var weaponData = Weapon(id: "pistol");
            var source     = Service();
            source.TryAdd(weaponData, ContainerId.Operator(0));
            source.Equip((WeaponItem)Op(source, 0).Placements.Single().Item, 0);
            var entries = InventorySerializer.Capture(source);

            var deadRoster = new FakeRoster(Dead(0), Alive(1));
            var restored   = Service(deadRoster);
            restored.Restore(entries, Database(weaponData));

            var weapon = (WeaponItem)Op(restored, 0).Placements.Single().Item;
            Assert.IsFalse(weapon.IsEquipped);
            Assert.IsNull(deadRoster[0].PrimaryWeapon);
        }
```

`DeadOperatorGearReleaserTests.cs`:
```csharp
#nullable enable

using System;
using MessagePipe;
using NUnit.Framework;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Tests
{
    public sealed class DeadOperatorGearReleaserTests
    {
        private sealed class FakeSubscriber<T> : ISubscriber<T>
        {
            private IMessageHandler<T>? handler;

            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                this.handler = handler;
                return new Subscription(() => this.handler = null);
            }

            public void Publish(T value) => this.handler?.Handle(value);

            private sealed class Subscription : IDisposable
            {
                private readonly Action dispose;
                public Subscription(Action dispose) => this.dispose = dispose;
                public void Dispose() => this.dispose();
            }
        }

        [Test]
        public void Initialize_releasesOnce()
        {
            var inventory = new FakeInventoryService();
            var releaser  = new DeadOperatorGearReleaser(inventory, new FakeSubscriber<CombatEndedEvent>());
            ((IInitializable)releaser).Initialize();
            Assert.AreEqual(1, inventory.ReleaseCalls);
        }

        [Test]
        public void CombatEnded_releasesAgain()
        {
            var inventory  = new FakeInventoryService();
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var releaser   = new DeadOperatorGearReleaser(inventory, subscriber);
            ((IInitializable)releaser).Initialize();

            subscriber.Publish(new CombatEndedEvent { Victory = true });

            Assert.AreEqual(2, inventory.ReleaseCalls);
        }

        [Test]
        public void Dispose_stopsListening()
        {
            var inventory  = new FakeInventoryService();
            var subscriber = new FakeSubscriber<CombatEndedEvent>();
            var releaser   = new DeadOperatorGearReleaser(inventory, subscriber);
            ((IInitializable)releaser).Initialize();
            ((IDisposable)releaser).Dispose();

            subscriber.Publish(new CombatEndedEvent());

            Assert.AreEqual(1, inventory.ReleaseCalls);
        }
    }
}
```

`FakeInventoryService.cs`:
```csharp
        public int  ReleaseCalls;
        public void ReleaseDeadOperatorWeapons() => this.ReleaseCalls++;
```

- [ ] **Step 2: Run to verify it fails.** `ut.sh CrimsonDraft.Tests.InventoryServiceTests`. Expected: COMPILE FAILED (`ReleaseDeadOperatorWeapons`, `DeadOperatorGearReleaser` missing).

- [ ] **Step 3: Implement.**

`IInventoryService.cs`, after `KeyUseOutcome TryUseKey(string keyItemId);`:
```csharp
        void ReleaseDeadOperatorWeapons();
```

`InventoryService.Actions.cs`, after `HasEquippedWeapon`:
```csharp
        public void ReleaseDeadOperatorWeapons()
        {
            var released = AllContainers()
                .SelectMany(c => c.Placements.Select(p => p.Item))
                .OfType<WeaponItem>()
                .Where(w => w.IsEquipped && !this.roster[w.EquippedBySlot].IsAlive)
                .ToList();
            foreach (var weapon in released) UnequipInternal(weapon);
            NotifyChanged(released.Select(FindContainerObjectOf).ToArray());
        }
```

`InventoryService.Persistence.cs`, `WireEquipped` guard becomes:
```csharp
            if (item is not WeaponItem weapon || entry.equippedOperatorSlot < 0 || entry.equippedOperatorSlot >= this.roster.Count)
                return;
            if (!this.roster[entry.equippedOperatorSlot].IsAlive || FindCarriedContainerOf(weapon) == null) return;
```

`DeadOperatorGearReleaser.cs`:
```csharp
#nullable enable

using System;
using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class DeadOperatorGearReleaser : IInitializable, IDisposable
    {
        private readonly IInventoryService             inventory;
        private readonly ISubscriber<CombatEndedEvent> combatEndedSubscriber;
        private IDisposable?                           subscription;

        [Preserve]
        public DeadOperatorGearReleaser(IInventoryService inventory, ISubscriber<CombatEndedEvent> combatEndedSubscriber)
        {
            this.inventory             = inventory;
            this.combatEndedSubscriber = combatEndedSubscriber;
        }

        void IInitializable.Initialize()
        {
            this.inventory.ReleaseDeadOperatorWeapons();
            this.subscription = this.combatEndedSubscriber.Subscribe(_ => this.inventory.ReleaseDeadOperatorWeapons());
        }

        void IDisposable.Dispose() => this.subscription?.Dispose();
    }
}
```

`NavigationScope.cs`, after the `OperatorCorpseBootstrap` registration:
```csharp
            builder.Register<DeadOperatorGearReleaser>(Lifetime.Singleton).AsImplementedInterfaces();
```

- [ ] **Step 4: Run.** `ut.sh CrimsonDraft.Tests.InventoryServiceTests CrimsonDraft.Tests.InventorySerializerTests CrimsonDraft.Tests.DeadOperatorGearReleaserTests`. Expected: all pass, `new=0`.

---

### Task 4: Corpse identity + trigger

**Files:**
- Create: `Scripts/Navigation/OperatorCorpse.cs`
- Modify: `Scripts/Navigation/IOperatorCorpseSpawner.cs`, `Scripts/Navigation/OperatorCorpseSpawner.cs`, `Scripts/Navigation/OperatorCorpseBootstrap.cs`
- Modify: `Tests/EditMode/OperatorCorpseSpawnerTests.cs`, `Tests/EditMode/OperatorCorpseBootstrapTests.cs` (`FakeSpawner`, first test)
- Test: `Tests/EditMode/OperatorCorpseTests.cs` (new)
- Prefab: `Prefabs/Characters/OperatorNavCorpse.prefab` (root gets `BoxCollider` trigger + `OperatorCorpse`)

**Interfaces:**
- Consumes: `CorpseProximityTracker` (Task 1).
- Produces: `IOperatorCorpseSpawner.Spawn(int slot, RoomController room, Vector3 position, Quaternion rotation)`; `OperatorCorpseSpawner(OperatorCorpseSettings settings, CorpseProximityTracker tracker)`; `OperatorCorpse.Initialize(int slot, CorpseProximityTracker tracker)`, `int Slot`.

- [ ] **Step 1: Write the failing tests.**

`OperatorCorpseTests.cs`:
```csharp
#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseTests
    {
        private GameObject corpseGo = null!;
        private GameObject playerGo = null!;
        private GameObject otherGo  = null!;
        private OperatorCorpse corpse = null!;
        private CorpseProximityTracker tracker = null!;

        [SetUp]
        public void SetUp()
        {
            this.corpseGo = new GameObject("Corpse", typeof(BoxCollider));
            this.corpse   = this.corpseGo.AddComponent<OperatorCorpse>();
            this.tracker  = new CorpseProximityTracker();
            this.corpse.Initialize(2, this.tracker);

            this.playerGo = new GameObject("Player", typeof(BoxCollider)) { tag = "Player" };
            this.otherGo  = new GameObject("Other", typeof(BoxCollider));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(this.corpseGo);
            Object.DestroyImmediate(this.playerGo);
            Object.DestroyImmediate(this.otherGo);
        }

        private void Send(string message, GameObject who) =>
            typeof(OperatorCorpse)
                .GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this.corpse, who == null ? null : new object[] { who.GetComponent<Collider>() });

        private void Disable() =>
            typeof(OperatorCorpse)
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this.corpse, null);

        [Test]
        public void Initialize_storesSlot() => Assert.AreEqual(2, this.corpse.Slot);

        [Test]
        public void PlayerEnter_grantsAccess()
        {
            Send("OnTriggerEnter", this.playerGo);
            Assert.IsTrue(this.tracker.CanAccess(2));
        }

        [Test]
        public void NonPlayerEnter_ignored()
        {
            Send("OnTriggerEnter", this.otherGo);
            Assert.IsFalse(this.tracker.CanAccess(2));
        }

        [Test]
        public void PlayerExit_revokesAccess()
        {
            Send("OnTriggerEnter", this.playerGo);
            Send("OnTriggerExit", this.playerGo);
            Assert.IsFalse(this.tracker.CanAccess(2));
        }

        [Test]
        public void TwoPlayerColliders_accessUntilLastLeaves()
        {
            var second = new GameObject("PlayerPart", typeof(BoxCollider)) { tag = "Player" };
            try
            {
                Send("OnTriggerEnter", this.playerGo);
                Send("OnTriggerEnter", second);
                Send("OnTriggerExit", this.playerGo);
                Assert.IsTrue(this.tracker.CanAccess(2));
                Send("OnTriggerExit", second);
                Assert.IsFalse(this.tracker.CanAccess(2));
            }
            finally { Object.DestroyImmediate(second); }
        }

        [Test]
        public void Disable_whileInside_revokesAccess()
        {
            Send("OnTriggerEnter", this.playerGo);
            Disable();
            Assert.IsFalse(this.tracker.CanAccess(2));
        }
    }
}
```

`OperatorCorpseSpawnerTests.cs`: change the existing call to `new OperatorCorpseSpawner(settings, new CorpseProximityTracker())` and `spawner.Spawn(0, room, pos, rot)`, and add:
```csharp
        [Test]
        public void Spawn_initializesOperatorCorpseWithSlot()
        {
            var prefabSource = new GameObject("CorpseWithComponent", typeof(BoxCollider));
            prefabSource.AddComponent<OperatorCorpse>();
            var settings = ScriptableObject.CreateInstance<OperatorCorpseSettings>();
            var so = new SerializedObject(settings);
            so.FindProperty("corpsePrefab").objectReferenceValue = prefabSource;
            so.ApplyModifiedPropertiesWithoutUndo();
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            try
            {
                new OperatorCorpseSpawner(settings, new CorpseProximityTracker()).Spawn(2, room, Vector3.zero, Quaternion.identity);
                Assert.AreEqual(2, room.transform.GetChild(0).GetComponent<OperatorCorpse>().Slot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(prefabSource);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
```

`OperatorCorpseBootstrapTests.cs` `FakeSpawner`: add `public int LastSlot = -1;`, signature `public void Spawn(int slot, RoomController room, Vector3 position, Quaternion rotation)` setting `this.LastSlot = slot;`. In `OnCombatEnded_recordsAndSpawnsCorpseForNewlyDeadOperator` add `Assert.AreEqual(1, spawner.LastSlot);`.

- [ ] **Step 2: Run to verify it fails.** `ut.sh CrimsonDraft.Tests.OperatorCorpseTests`. Expected: COMPILE FAILED (`OperatorCorpse` missing, spawner signature).

- [ ] **Step 3: Implement.**

`OperatorCorpse.cs`:
```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation
{
    [RequireComponent(typeof(Collider))]
    public sealed class OperatorCorpse : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        private CorpseProximityTracker? tracker;
        private int playerCollidersInside;

        public int Slot { get; private set; } = -1;

        public void Initialize(int slot, CorpseProximityTracker proximityTracker)
        {
            this.Slot    = slot;
            this.tracker = proximityTracker;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(PlayerTag)) return;
            if (this.playerCollidersInside++ == 0) this.tracker?.Enter(this.Slot);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(PlayerTag) || this.playerCollidersInside == 0) return;
            if (--this.playerCollidersInside == 0) this.tracker?.Exit(this.Slot);
        }

        // Unity sends no OnTriggerExit when the room holding this corpse is deactivated.
        private void OnDisable()
        {
            if (this.playerCollidersInside == 0) return;
            this.playerCollidersInside = 0;
            this.tracker?.Exit(this.Slot);
        }
    }
}
```

`IOperatorCorpseSpawner.cs`: `void Spawn(int slot, RoomController room, Vector3 position, Quaternion rotation);`

`OperatorCorpseSpawner.cs`:
```csharp
    public sealed class OperatorCorpseSpawner : IOperatorCorpseSpawner
    {
        private readonly OperatorCorpseSettings settings;
        private readonly CorpseProximityTracker tracker;

        [Preserve]
        public OperatorCorpseSpawner(OperatorCorpseSettings settings, CorpseProximityTracker tracker)
        {
            this.settings = settings;
            this.tracker  = tracker;
        }

        public void Spawn(int slot, RoomController room, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(this.settings.CorpsePrefab, position, rotation, room.transform);
            if (instance.TryGetComponent(out OperatorCorpse corpse)) corpse.Initialize(slot, this.tracker);
        }
    }
```

`OperatorCorpseBootstrap.cs`: `this.spawner.Spawn(i, room, pos, rot);` and `this.spawner.Spawn(entry.SlotIndex, room, entry.Position, entry.Rotation);`

- [ ] **Step 4: Run.** `ut.sh CrimsonDraft.Tests.OperatorCorpseTests CrimsonDraft.Tests.OperatorCorpseSpawnerTests CrimsonDraft.Tests.OperatorCorpseBootstrapTests`. Expected: all pass, `new=0`.

- [ ] **Step 5: Prefab.** With the CLI (`unity command eval_file`), open `Assets/Prefabs/Characters/OperatorNavCorpse.prefab` with `PrefabUtility.LoadPrefabContents`, on the root add `BoxCollider` (`isTrigger = true`, `center = (0, 1, 0)`, `size = (2.5, 2, 2.5)` in world units — divide by the root's `lossyScale` if it is not 1) and `OperatorCorpse`, `SaveAsPrefabAsset`, `UnloadPrefabContents`. If the root already has a non-trigger collider, put the trigger + `OperatorCorpse` on a new child `LootTrigger` instead and ledger the ruling. Re-load the prefab and print its components to confirm. Expected: `BoxCollider(isTrigger=True)` and `OperatorCorpse` present.

---

### Task 5: UI — navigation, cursor, card

**Files:**
- Modify: `Scripts/UI/Inventory/InventoryPresenters.cs`, `Scripts/UI/Inventory/GridCursor.cs`, `Scripts/UI/Inventory/OperatorWidgetView.cs`, `Scripts/UI/Inventory/PartyPanelView.cs`
- Test: `Tests/EditMode/InventoryPresentersTests.cs` (new), `Tests/EditMode/OperatorWidgetViewTests.cs` (new)
- Prefab: `Prefabs/UI/Inventory/Card.prefab` (`deadOverlay` image enabled + "KIA" label)

**Interfaces:**
- Consumes: `IInventoryService.IsCarried/IsAccessible` (Task 2), `InventoryTestData.Dead`, `FakeCorpseAccess`.
- Produces: `OperatorWidgetView.Bind(OperatorRuntime op, bool canLoot)`.

- [ ] **Step 1: Write the failing tests.**

`InventoryPresentersTests.cs`:
```csharp
#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.UI;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryPresentersTests
    {
        private GameObject root = null!;
        private InventoryGrid[] grids = null!;

        [SetUp]
        public void SetUp()
        {
            this.root  = new GameObject("Group");
            this.grids = Enumerable.Range(0, 3).Select(i =>
            {
                var go = new GameObject($"Grid{i}", typeof(RectTransform));
                go.transform.SetParent(this.root.transform);
                var grid = go.AddComponent<InventoryGrid>();
                var so   = new SerializedObject(grid);
                so.FindProperty("containerIndex").intValue = i;
                so.ApplyModifiedPropertiesWithoutUndo();
                return grid;
            }).ToArray();

            for (int i = 0; i < 3; i++)
            {
                var so = new SerializedObject(this.grids[i]);
                so.FindProperty("left").objectReferenceValue  = i > 0 ? this.grids[i - 1] : null;
                so.FindProperty("right").objectReferenceValue = i < 2 ? this.grids[i + 1] : null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.root);

        private InventoryPresenters Build(int deadSlot)
        {
            var group = this.root.AddComponent<InventoryGridGroup>();
            var so    = new SerializedObject(group);
            var array = so.FindProperty("grids");
            array.arraySize = 3;
            for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = this.grids[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var roster    = new FakeRoster(Enumerable.Range(0, 3).Select(i => i == deadSlot ? Dead(i) : Alive(i)).ToArray());
            var inventory = new InventoryService(roster, FakeCombineService.None, FakeCorpseAccess.None);
            var presenters = new InventoryPresenters(inventory, group);
            presenters.Initialize();
            return presenters;
        }

        [Test]
        public void BuildLinks_skipsInaccessibleDeadGrid()
        {
            var links = Build(deadSlot: 1).BuildLinks();
            Assert.AreEqual(ContainerId.Operator(2), links[ContainerId.Operator(0)].Right);
            Assert.AreEqual(ContainerId.Operator(0), links[ContainerId.Operator(2)].Left);
        }

        [Test]
        public void FirstGrid_skipsInaccessibleDeadGrid()
        {
            Assert.AreEqual(ContainerId.Operator(1), Build(deadSlot: 0).FirstGrid);
        }

        [Test]
        public void BuildStorageRows_operatorRowExcludesInaccessibleGrid()
        {
            var rows = Build(deadSlot: 0).BuildStorageRows();
            CollectionAssert.AreEqual(new[] { ContainerId.Operator(1), ContainerId.Operator(2) }, rows[1]);
        }
    }
}
```

`OperatorWidgetViewTests.cs`:
```csharp
#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.UI;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorWidgetViewTests
    {
        private GameObject card = null!;
        private GameObject overlay = null!;
        private OperatorWidgetView view = null!;

        [SetUp]
        public void SetUp()
        {
            this.card    = new GameObject("Card");
            this.overlay = new GameObject("deadOverlay");
            this.overlay.transform.SetParent(this.card.transform);
            this.view    = this.card.AddComponent<OperatorWidgetView>();
            var so = new SerializedObject(this.view);
            so.FindProperty("deadOverlay").objectReferenceValue = this.overlay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.card);

        [Test]
        public void Dead_notLootable_showsOverlay()
        {
            this.view.Bind(Dead(0), canLoot: false);
            Assert.IsTrue(this.overlay.activeSelf);
        }

        [Test]
        public void Dead_lootable_hidesOverlay()
        {
            this.view.Bind(Dead(0), canLoot: true);
            Assert.IsFalse(this.overlay.activeSelf);
        }

        [Test]
        public void Alive_hidesOverlay()
        {
            this.view.Bind(Alive(0), canLoot: false);
            Assert.IsFalse(this.overlay.activeSelf);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails.** `ut.sh CrimsonDraft.Tests.InventoryPresentersTests CrimsonDraft.Tests.OperatorWidgetViewTests`. Expected: COMPILE FAILED (`Bind` has no `canLoot` parameter). If the presenters tests compile separately, they fail on the link/first-grid assertions.

- [ ] **Step 3: Implement.**

`InventoryPresenters.cs`:
```csharp
        public ContainerId FirstGrid => this.order.First(this.inventory.IsAccessible);
```
`BuildStorageRows` second row: `this.order.Where(this.inventory.IsAccessible).ToArray(),`
`NextPresented` loop condition: `while (next != null && !IsNavigable(next.ContainerId))` plus
```csharp
        private bool IsNavigable(ContainerId id) => this.presenters.ContainsKey(id) && this.inventory.IsAccessible(id);
```

`OperatorWidgetView.cs`: `public void Bind(OperatorRuntime op, bool canLoot)`; overlay line becomes
`if (this.deadOverlay != null) this.deadOverlay.SetActive(!op.IsAlive && !canLoot);`

`PartyPanelView.cs`: add `using CrimsonDraft.Inventory;`, field `[Inject] private IInventoryService? inventory;`, helper
```csharp
        private bool CanLoot(int slot) => this.inventory != null && this.inventory.IsAccessible(ContainerId.Operator(slot));
```
and both bind calls become `this.widgets[i].Bind(source[i], CanLoot(i));` / `this.widgets[i].Bind(this.roster[i], CanLoot(i));`.

`GridCursor.cs`:
- `ResetCursorToOrigin()`: first line `if (!this.transferMode) this.navigator = null;` (links are rebuilt with the current access every time the inventory opens).
- `OnConfirm`: right after `var view = CurrentPresenter.ViewAt(Navigator.Cell);` insert
```csharp
            if (!this.inventory.IsCarried(Navigator.Grid))
            {
                if (this.isCombineMode) this.sfx.PlayInvalidAction(this.gameObject);
                else                    PickUpAtCursor();
                return;
            }
```
- `TryEnterMeleeSlot()`: first line `if (!this.inventory.IsCarried(Navigator.Grid)) return false;`
- `SelectItemById`: `if (!this.presenters.Has(container.Id) || !this.inventory.IsAccessible(container.Id)) continue;`

- [ ] **Step 4: Run.** `ut.sh CrimsonDraft.Tests.InventoryPresentersTests CrimsonDraft.Tests.OperatorWidgetViewTests`. Expected: all pass. Then `ut.sh` (full). Expected: `new=0`.

- [ ] **Step 5: Card prefab.** With the CLI, `LoadPrefabContents("Assets/Prefabs/UI/Inventory/Card.prefab")`; find the `OperatorWidgetView` `deadOverlay` object; enable its `Image`; add child `KIA` (`RectTransform` stretched to the overlay, `TextMeshProUGUI` text `KIA`, font size 56, bold, color `(0.8, 0.1, 0.1, 1)`, centred, `raycastTarget = false`, the project's default TMP font); save and unload. Re-load and print overlay `Image.enabled` and the `KIA` text. Expected: `True`, `KIA`.

---

### Task 6: Play Mode verification, review, commit

**Files:** none new (probe scripts live in the scratchpad).

- [ ] **Step 1: Play Mode probe (CLI).** In `Deck_B_Development` (the scene the user has open), `editor_play`, then `eval_file` probes that resolve services from the `NavigationScope` container:
  1. Kill operator 1 (`roster[1].ApplyDamage(999)` twice) and publish `CombatEndedEvent`. Expected: a corpse spawns at the player with `OperatorCorpse.Slot == 1`; operator 1's equipped weapon (if any) is no longer equipped and stays in their grid; after the next physics step `CorpseProximityTracker.CanAccess(1) == true`.
  2. Move the player 10 m away, step physics. Expected: `CanAccess(1) == false`; open the inventory: card 1 shows overlay + KIA, `presenters.BuildLinks()[Operator(0)].Right == Operator(2)`.
  3. Move the player back onto the corpse, open the inventory. Expected: no overlay; pick up an item from operator 1's grid and drop it into operator 0 (and back) via `IInventoryService` succeeds.
  4. Publish `StorageOpenRequestedEvent` while on the corpse. Expected: storage rows include `Operator(1)`; a storage → operator 1 transfer succeeds.
  5. Capture the game view (`capture_game_view --source screen`) for steps 2 and 3, inspect, then delete `Assets/Temp`.
  6. `editor_stop`; `git checkout` the TMP fallback font asset if Play Mode modified it.
  If a probe fails, use superpowers:systematic-debugging before changing code.

- [ ] **Step 2: Full suite.** `ut.sh`. Expected: `new=0`.

- [ ] **Step 3: Final whole-branch review** (fresh reviewer, most capable model) per superpowers:executing-plans, with this plan's Review Focus.

- [ ] **Step 4: Commit** (only the files of this feature + spec + plan; never the user's unrelated changes):

```bash
git add docs/superpowers/specs/2026-10-05-dead-operator-loot-design.md docs/superpowers/plans/2026-10-05-dead-operator-loot.md \
  Game/CrimsonDraft/Assets/Scripts/Inventory Game/CrimsonDraft/Assets/Scripts/Navigation/CorpseProximityTracker.cs* \
  Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpse.cs* Game/CrimsonDraft/Assets/Scripts/Navigation/DeadOperatorGearReleaser.cs* \
  Game/CrimsonDraft/Assets/Scripts/Navigation/IOperatorCorpseSpawner.cs Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseSpawner.cs \
  Game/CrimsonDraft/Assets/Scripts/Navigation/OperatorCorpseBootstrap.cs Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs \
  Game/CrimsonDraft/Assets/Scripts/UI/Inventory Game/CrimsonDraft/Assets/Tests/EditMode \
  Game/CrimsonDraft/Assets/Prefabs/Characters/OperatorNavCorpse.prefab Game/CrimsonDraft/Assets/Prefabs/UI/Inventory/Card.prefab
git status --short   # confirm nothing unrelated is staged
git commit -m "feat(inventory): dead operator loot gated by corpse proximity"
```
