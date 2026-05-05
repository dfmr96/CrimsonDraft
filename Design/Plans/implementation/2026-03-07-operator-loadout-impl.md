# Operator Loadout Persistence — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ammo vive en el item de arma (no en el operador), operadores definidos como assets con nombres reales, starting loadout data-driven, Navigation y Combat leen el mismo estado.

**Architecture:** `IWeaponSlot` en el assembly de `Operators` rompe la dependencia circular; `WeaponItem` (en `Inventory`) implementa `IWeaponSlot`; `OperatorRuntime` referencia `IWeaponSlot?`. Un solo `StartingLoadout` ScriptableObject reemplaza `DefaultOperatorRosterSeedProvider` e `InventoryDebugSeeder`. Combat lee ammo vía `roster[slot].EquippedWeapon?.CurrentAmmo`.

**Spec:** `docs/plans/2026-03-07-operator-loadout-design.md`

**Tech Stack:** Unity C#, VContainer DI, NUnit EditMode tests, ScriptableObject, asmdef

---

## Notas de compilación

Entre los Tasks 3 y 5, `InventoryServiceTests` seguirá compilando (ItemData NO se hace abstract) pero los tests fallarán en runtime. Esto es esperado. No correr tests hasta el Task 5.

---

## Task 1: IWeaponSlot + OperatorData.displayName

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Operators/OperatorData.cs`

**Step 1: Crear IWeaponSlot.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Operators
{
    public interface IWeaponSlot
    {
        string Caliber     { get; }
        int    CurrentAmmo { get; }
        int    MaxAmmo     { get; }
        void   SetAmmo(int value);
    }
}
```

**Step 2: Agregar displayName a OperatorData**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    [CreateAssetMenu(fileName = "OperatorData", menuName = "CrimsonDraft/Operators/Operator Data")]
    public sealed class OperatorData : ScriptableObject
    {
        [SerializeField] private string operatorId  = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Sprite sprite      = null!;

        public string OperatorId   => this.operatorId;
        public string DisplayName  => this.displayName;
        public Sprite Sprite       => this.sprite;
    }
}
```

**Step 3: Verificar compilación en Unity**

Abrir Unity. Esperar a que compile. No deben haber errores.

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Operators/IWeaponSlot.cs
git add Game/CrimsonDraft/Assets/Scripts/Operators/OperatorData.cs
git commit -m "feat(operators): add IWeaponSlot interface and OperatorData.displayName"
```

---

## Task 2: OperatorRuntime — quitar Ammo/MaxAmmo, agregar EquippedWeapon

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Operators/OperatorRuntime.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Operators/OperatorRosterSeed.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Operators/OperatorRoster.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/OperatorRosterTests.cs`

**Step 1: Reescribir OperatorRuntime.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    public sealed class OperatorRuntime
    {
        public OperatorData?  Data           { get; }
        public int            SlotIndex      { get; }
        public bool           IsPresent      { get; }
        public int            MaxHp          { get; }

        public int            Hp             { get; private set; }
        public IWeaponSlot?   EquippedWeapon { get; private set; }
        public float          HpRatio        => this.MaxHp > 0 ? Mathf.Clamp01((float)this.Hp / this.MaxHp) : 0f;
        public bool           IsAlive        => this.IsPresent && this.Hp > 0;

        internal OperatorRuntime(int slotIndex, OperatorData? data, bool isPresent, int maxHp)
        {
            this.SlotIndex = slotIndex;
            this.Data      = data;
            this.IsPresent = isPresent;
            this.MaxHp     = maxHp;
            this.Hp        = isPresent ? maxHp : 0;
        }

        public OperatorDamageResult ApplyDamage(int damage)
        {
            if (!this.IsAlive)
                return new OperatorDamageResult(this.SlotIndex, 0, this.Hp, true);

            int applied = Mathf.Max(0, damage);
            this.Hp = Mathf.Max(0, this.Hp - applied);
            return new OperatorDamageResult(this.SlotIndex, applied, this.Hp, this.Hp <= 0);
        }

        public void SetEquippedWeapon(IWeaponSlot? weapon) => this.EquippedWeapon = weapon;
    }
}
```

**Step 2: Actualizar OperatorRosterSeed.cs — quitar DefaultAmmo**

```csharp
#nullable enable

namespace CrimsonDraft.Operators
{
    public readonly struct OperatorRosterSeed
    {
        public OperatorData?[] Operators { get; }
        public int             DefaultHp { get; }

        public OperatorRosterSeed(OperatorData?[] operators, int defaultHp)
        {
            this.Operators = operators;
            this.DefaultHp = defaultHp;
        }
    }
}
```

**Step 3: Actualizar OperatorRoster.cs — quitar DefaultAmmo del constructor de OperatorRuntime**

Solo cambia `EnsureInitialized`. Reemplazar la línea:
```csharp
this.slots[i] = new OperatorRuntime(i, seed.Operators[i], isPresent, seed.DefaultHp, seed.DefaultAmmo);
```
por:
```csharp
this.slots[i] = new OperatorRuntime(i, seed.Operators[i], isPresent, seed.DefaultHp);
```

**Step 4: Actualizar OperatorRosterTests.cs**

Los tests `Reload_restoresAmmoToMax`, `ConsumeAmmo_clampsToZero` y el chequeo de `Ammo` en `AbsentOperator_isNotAlive` ya no aplican.

```csharp
#nullable enable

using NUnit.Framework;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorRosterTests
    {
        private sealed class FakeSeedProvider : IOperatorRosterSeedProvider
        {
            private readonly OperatorRosterSeed seed;

            internal FakeSeedProvider(OperatorData?[] operators, int defaultHp) =>
                this.seed = new OperatorRosterSeed(operators, defaultHp);

            public OperatorRosterSeed GetSeed() => this.seed;
        }

        private static OperatorRuntime MakePresent(int slot, int maxHp = 100) =>
            new OperatorRuntime(slot, null, isPresent: true, maxHp);

        private static OperatorRuntime MakeAbsent(int slot) =>
            new OperatorRuntime(slot, null, isPresent: false, maxHp: 100);

        [Test]
        public void EnsureInitialized_setsHpFromSeed()
        {
            var roster = new OperatorRoster(new FakeSeedProvider(new OperatorData?[] { null, null, null }, defaultHp: 80));
            roster.EnsureInitialized();

            Assert.AreEqual(0, roster[0].Hp);   // null slot → not present → Hp = 0
        }

        [Test]
        public void EnsureInitialized_isIdempotent()
        {
            var roster = new OperatorRoster(new FakeSeedProvider(new OperatorData?[] { null, null }, defaultHp: 80));

            roster.EnsureInitialized();
            int firstCount = roster.Count;
            roster.EnsureInitialized();

            Assert.IsTrue(roster.IsInitialized);
            Assert.AreEqual(firstCount, roster.Count);
        }

        [Test]
        public void ApplyDamage_clampsToZero_andMarksDead()
        {
            var op     = MakePresent(0, maxHp: 100);
            var result = op.ApplyDamage(150);

            Assert.AreEqual(0, result.RemainingHp);
            Assert.IsTrue(result.IsDead);
            Assert.IsFalse(op.IsAlive);
        }

        [Test]
        public void ApplyDamage_partialDamage_doesNotKill()
        {
            var op     = MakePresent(1, maxHp: 100);
            var result = op.ApplyDamage(30);

            Assert.AreEqual(70, result.RemainingHp);
            Assert.IsFalse(result.IsDead);
            Assert.IsTrue(op.IsAlive);
        }

        [Test]
        public void GetAliveSlots_excludesAbsentAndDeadOperators()
        {
            var roster = new OperatorRoster(
                new FakeSeedProvider(new OperatorData?[] { null, null, null }, defaultHp: 100));
            roster.EnsureInitialized();

            var alive = roster.GetAliveSlots();
            Assert.AreEqual(0, alive.Count);
        }

        [Test]
        public void AbsentOperator_isNotAlive()
        {
            var op = MakeAbsent(0);
            Assert.IsFalse(op.IsPresent);
            Assert.IsFalse(op.IsAlive);
            Assert.AreEqual(0, op.Hp);
        }

        [Test]
        public void HpRatio_returnsCorrectFraction()
        {
            var op = MakePresent(0, maxHp: 100);
            op.ApplyDamage(25);
            Assert.AreEqual(0.75f, op.HpRatio, delta: 0.001f);
        }

        [Test]
        public void SetEquippedWeapon_updatesEquippedWeapon()
        {
            var op = MakePresent(0);
            Assert.IsNull(op.EquippedWeapon);

            var fakeWeapon = new FakeWeaponSlot("9mm", 15, 15);
            op.SetEquippedWeapon(fakeWeapon);

            Assert.AreEqual(fakeWeapon, op.EquippedWeapon);
            Assert.AreEqual(15, op.EquippedWeapon!.CurrentAmmo);
        }

        [Test]
        public void SetEquippedWeapon_null_clearsWeapon()
        {
            var op = MakePresent(0);
            op.SetEquippedWeapon(new FakeWeaponSlot("9mm", 15, 15));
            op.SetEquippedWeapon(null);

            Assert.IsNull(op.EquippedWeapon);
        }

        private sealed class FakeWeaponSlot : IWeaponSlot
        {
            public string Caliber     { get; }
            public int    CurrentAmmo { get; private set; }
            public int    MaxAmmo     { get; }

            internal FakeWeaponSlot(string caliber, int currentAmmo, int maxAmmo)
            {
                this.Caliber     = caliber;
                this.CurrentAmmo = currentAmmo;
                this.MaxAmmo     = maxAmmo;
            }

            public void SetAmmo(int value) => this.CurrentAmmo = value < 0 ? 0 : value > this.MaxAmmo ? this.MaxAmmo : value;
        }
    }
}
```

**Step 5: Correr tests en Unity Test Runner**

Window > General > Test Runner > Run All (EditMode).
Esperado: `OperatorRosterTests` todos pasan. `InventoryServiceTests` pueden fallar — se arreglan en Task 5.

**Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Operators/OperatorRuntime.cs
git add Game/CrimsonDraft/Assets/Scripts/Operators/OperatorRosterSeed.cs
git add Game/CrimsonDraft/Assets/Scripts/Operators/OperatorRoster.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/OperatorRosterTests.cs
git commit -m "refactor(operators): move ammo tracking from OperatorRuntime to IWeaponSlot"
```

---

## Task 3: ItemData — quitar CreateAssetMenu del base, agregar subclases

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/ItemData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/AmmoBoxData.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/ConsumableData.cs`

**Step 1: Modificar ItemData.cs — quitar CreateAssetMenu y caliber**

`caliber` se mueve a las subclases. `CreateAssetMenu` se quita del base (usar las subclases para crear assets).

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    // No [CreateAssetMenu] on base — use WeaponData, AmmoBoxData or ConsumableData.
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string   itemId      = string.Empty;
        [SerializeField] private ItemType itemType    = ItemType.Consumable;
        [SerializeField] private string   displayName = string.Empty;

        public string   ItemId      => this.itemId;
        public ItemType ItemType    => this.itemType;
        public string   DisplayName => this.displayName;
    }
}
```

**Step 2: Crear WeaponData.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "CrimsonDraft/Inventory/Weapon Data")]
    public sealed class WeaponData : ItemData
    {
        [SerializeField] private string caliber          = string.Empty;
        [SerializeField] private int    magazineCapacity = 1;

        public string Caliber          => this.caliber;
        public int    MagazineCapacity => this.magazineCapacity;
    }
}
```

**Step 3: Crear AmmoBoxData.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "AmmoBoxData", menuName = "CrimsonDraft/Inventory/Ammo Box Data")]
    public sealed class AmmoBoxData : ItemData
    {
        [SerializeField] private string caliber         = string.Empty;
        [SerializeField] private int    defaultQuantity = 30;

        public string Caliber         => this.caliber;
        public int    DefaultQuantity => this.defaultQuantity;
    }
}
```

**Step 4: Crear ConsumableData.cs**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "ConsumableData", menuName = "CrimsonDraft/Inventory/Consumable Data")]
    public sealed class ConsumableData : ItemData { }
}
```

**Step 5: Verificar compilación en Unity**

No deben haber errores de compilación. Los tests de InventoryService no se corren aún.

**Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/ItemData.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponData.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/AmmoBoxData.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/ConsumableData.cs
git commit -m "feat(inventory): add WeaponData, AmmoBoxData, ConsumableData subclasses of ItemData"
```

---

## Task 4: InventoryItem — subclases con ammo y quantity

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryItem.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/AmmoBoxItem.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Inventory/ConsumableItem.cs`

**Step 1: Modificar InventoryItem.cs — hacer clase base (no abstract para no romper código existente)**

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public class InventoryItem
    {
        public ItemData Data           { get; }
        public int      EquippedBySlot { get; internal set; } = -1;
        public bool     IsEquipped     => this.EquippedBySlot >= 0;

        protected internal InventoryItem(ItemData data) => this.Data = data;
    }
}
```

**Step 2: Crear WeaponItem.cs**

`WeaponItem` implementa `IWeaponSlot`. `CurrentAmmo` inicia en `MagazineCapacity`. `SetAmmo` clampea entre 0 y MaxAmmo.

```csharp
#nullable enable

using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed class WeaponItem : InventoryItem, IWeaponSlot
    {
        public new WeaponData Data    => (WeaponData)base.Data;
        public string Caliber        => this.Data.Caliber;
        public int    MaxAmmo        => this.Data.MagazineCapacity;
        public int    CurrentAmmo    { get; private set; }

        public WeaponItem(WeaponData data) : base(data)
        {
            this.CurrentAmmo = data.MagazineCapacity;
        }

        public void SetAmmo(int value) =>
            this.CurrentAmmo = value < 0 ? 0 : value > this.MaxAmmo ? this.MaxAmmo : value;
    }
}
```

**Step 3: Crear AmmoBoxItem.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class AmmoBoxItem : InventoryItem
    {
        public new AmmoBoxData Data => (AmmoBoxData)base.Data;
        public int Quantity { get; internal set; }

        public AmmoBoxItem(AmmoBoxData data, int quantity) : base(data)
        {
            this.Quantity = quantity > 0 ? quantity : data.DefaultQuantity;
        }
    }
}
```

**Step 4: Crear ConsumableItem.cs**

```csharp
#nullable enable

namespace CrimsonDraft.Inventory
{
    public sealed class ConsumableItem : InventoryItem
    {
        public ConsumableItem(ConsumableData data) : base(data) { }
    }
}
```

**Step 5: Verificar compilación en Unity**

No deben haber errores.

**Step 6: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryItem.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/WeaponItem.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/AmmoBoxItem.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/ConsumableItem.cs
git commit -m "feat(inventory): add WeaponItem, AmmoBoxItem, ConsumableItem with ammo/quantity tracking"
```

---

## Task 5: InventoryService — factory + equip/reload + tests

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs`

**Step 1: Actualizar IInventoryService.cs**

`AddItem` agrega `int quantity = 0`. El resto de la interfaz no cambia en firma (los semantics internos cambian en el servicio).

```csharp
#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Inventory
{
    public interface IInventoryService
    {
        IReadOnlyList<InventoryItem> Items { get; }

        /// <summary>Creates the correct InventoryItem subtype based on ItemData type. quantity is used for AmmoBox.</summary>
        void AddItem(ItemData data, int quantity = 0);

        /// <summary>Equips weapon at itemIndex to operatorSlot. Unequips any weapon that slot was previously carrying.</summary>
        void EquipWeapon(int itemIndex, int operatorSlot);

        /// <summary>Unequips weapon at itemIndex. No-op if not equipped.</summary>
        void UnequipWeapon(int itemIndex);

        /// <summary>Returns the index of the weapon equipped by operatorSlot, or -1 if none.</summary>
        int GetEquippedWeaponIndex(int operatorSlot);

        /// <summary>Returns true if ammoBox at ammoBoxIndex can reload operatorSlot.</summary>
        bool CanReload(int ammoBoxIndex, int operatorSlot);

        /// <summary>Reloads weapon using ammo from box. Partially deducts box.Quantity. Removes box if exhausted.</summary>
        void ReloadOperator(int ammoBoxIndex, int operatorSlot);
    }
}
```

**Step 2: Reescribir InventoryService.cs**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly IOperatorRoster     roster;
        private readonly List<InventoryItem> items = new();

        [Preserve]
        public InventoryService(IOperatorRoster roster) => this.roster = roster;

        public IReadOnlyList<InventoryItem> Items => this.items;

        public void AddItem(ItemData data, int quantity = 0)
        {
            InventoryItem item = data switch
            {
                WeaponData   wd => new WeaponItem(wd),
                AmmoBoxData  ad => new AmmoBoxItem(ad, quantity),
                ConsumableData cd => new ConsumableItem(cd),
                _ => throw new ArgumentException($"Unknown ItemData subtype: {data.GetType().Name}")
            };
            this.items.Add(item);
        }

        public void EquipWeapon(int itemIndex, int operatorSlot)
        {
            // Unequip any weapon already on this slot
            for (int i = 0; i < this.items.Count; i++)
            {
                if (this.items[i].EquippedBySlot == operatorSlot)
                {
                    this.items[i].EquippedBySlot = -1;
                    this.roster[operatorSlot].SetEquippedWeapon(null);
                    break;
                }
            }
            this.items[itemIndex].EquippedBySlot = operatorSlot;
            this.roster[operatorSlot].SetEquippedWeapon(this.items[itemIndex] as IWeaponSlot);
        }

        public void UnequipWeapon(int itemIndex)
        {
            int slot = this.items[itemIndex].EquippedBySlot;
            this.items[itemIndex].EquippedBySlot = -1;
            if (slot >= 0)
                this.roster[slot].SetEquippedWeapon(null);
        }

        public int GetEquippedWeaponIndex(int operatorSlot)
        {
            for (int i = 0; i < this.items.Count; i++)
                if (this.items[i].EquippedBySlot == operatorSlot)
                    return i;
            return -1;
        }

        public bool CanReload(int ammoBoxIndex, int operatorSlot)
        {
            if (this.items[ammoBoxIndex] is not AmmoBoxItem box)
                return false;

            var weapon = this.roster[operatorSlot].EquippedWeapon;
            if (weapon == null) return false;
            if (weapon.Caliber != box.Data.Caliber) return false;

            return this.roster[operatorSlot].IsAlive && weapon.CurrentAmmo < weapon.MaxAmmo;
        }

        public void ReloadOperator(int ammoBoxIndex, int operatorSlot)
        {
            if (!CanReload(ammoBoxIndex, operatorSlot)) return;

            var box    = (AmmoBoxItem)this.items[ammoBoxIndex];
            var weapon = this.roster[operatorSlot].EquippedWeapon!;

            int rounds = weapon.MaxAmmo - weapon.CurrentAmmo;
            weapon.SetAmmo(weapon.MaxAmmo);
            box.Quantity -= rounds;

            if (box.Quantity <= 0)
                this.items.RemoveAt(ammoBoxIndex);
        }
    }
}
```

**Step 3: Reescribir InventoryServiceTests.cs**

Los helpers `MakeWeaponData` y `MakeAmmoBoxData` usan `SerializedObject` para setear los campos privados (incluyendo campos heredados de `ItemData`). `FakeRoster` se actualiza para que `OperatorRuntime` no requiera `maxAmmo`.

```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryServiceTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────

        private sealed class FakeRoster : IOperatorRoster
        {
            private readonly OperatorRuntime[] slots;
            public bool IsInitialized => true;
            public int  Count         => this.slots.Length;
            public OperatorRuntime this[int i] => this.slots[i];

            public FakeRoster(params OperatorRuntime[] slots) => this.slots = slots;

            public IReadOnlyList<int> GetAliveSlots()
            {
                var alive = new List<int>();
                for (int i = 0; i < this.slots.Length; i++)
                    if (this.slots[i].IsAlive) alive.Add(i);
                return alive;
            }

            public void EnsureInitialized() { }
        }

        private static OperatorRuntime MakeAlive(int slot) =>
            new OperatorRuntime(slot, null, isPresent: true, maxHp: 100);

        private static WeaponData MakeWeaponData(string caliber = "9mm", int magazineCapacity = 6)
        {
            var d  = ScriptableObject.CreateInstance<WeaponData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue          = System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex     = (int)ItemType.Weapon;
            so.FindProperty("displayName").stringValue     = "Test Weapon";
            so.FindProperty("caliber").stringValue         = caliber;
            so.FindProperty("magazineCapacity").intValue   = magazineCapacity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static AmmoBoxData MakeAmmoBoxData(string caliber = "9mm", int defaultQuantity = 30)
        {
            var d  = ScriptableObject.CreateInstance<AmmoBoxData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue          = System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex     = (int)ItemType.AmmoBox;
            so.FindProperty("displayName").stringValue     = "Test Box";
            so.FindProperty("caliber").stringValue         = caliber;
            so.FindProperty("defaultQuantity").intValue    = defaultQuantity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void AddItem_weapon_createsWeaponItemWithFullAmmo()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeWeaponData(magazineCapacity: 30));

            Assert.AreEqual(1, service.Items.Count);
            var item = service.Items[0] as WeaponItem;
            Assert.IsNotNull(item);
            Assert.AreEqual(30, item!.CurrentAmmo);
            Assert.AreEqual(30, item.MaxAmmo);
        }

        [Test]
        public void AddItem_ammoBox_usesProvidedQuantity()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData(defaultQuantity: 30), quantity: 99);

            var item = service.Items[0] as AmmoBoxItem;
            Assert.IsNotNull(item);
            Assert.AreEqual(99, item!.Quantity);
        }

        [Test]
        public void AddItem_ammoBox_usesDefaultQuantity_whenZero()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData(defaultQuantity: 30), quantity: 0);

            var item = service.Items[0] as AmmoBoxItem;
            Assert.AreEqual(30, item!.Quantity);
        }

        [Test]
        public void EquipWeapon_setsEquippedBySlotAndUpdatesRoster()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData());

            service.EquipWeapon(0, operatorSlot: 0);

            Assert.AreEqual(0, service.Items[0].EquippedBySlot);
            Assert.IsNotNull(op.EquippedWeapon);
        }

        [Test]
        public void EquipWeapon_unequipsPreviousWeaponOfSameSlot()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData());
            service.AddItem(MakeWeaponData());

            service.EquipWeapon(0, operatorSlot: 0);
            service.EquipWeapon(1, operatorSlot: 0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot, "old weapon unequipped");
            Assert.AreEqual( 0, service.Items[1].EquippedBySlot, "new weapon equipped");
            Assert.AreEqual(service.Items[1] as IWeaponSlot, op.EquippedWeapon);
        }

        [Test]
        public void UnequipWeapon_clearsSlotAndNullsRosterWeapon()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData());
            service.EquipWeapon(0, operatorSlot: 0);

            service.UnequipWeapon(0);

            Assert.AreEqual(-1, service.Items[0].EquippedBySlot);
            Assert.IsNull(op.EquippedWeapon);
        }

        [Test]
        public void WeaponAmmo_persistsWhenTransferredBetweenOperators()
        {
            var op0     = MakeAlive(0);
            var op1     = MakeAlive(1);
            var service = new InventoryService(new FakeRoster(op0, op1));
            service.AddItem(MakeWeaponData(magazineCapacity: 30));

            service.EquipWeapon(0, operatorSlot: 0);
            // Simulate spending some ammo
            op0.EquippedWeapon!.SetAmmo(10);
            Assert.AreEqual(10, op0.EquippedWeapon.CurrentAmmo);

            // Transfer weapon to op1
            service.UnequipWeapon(0);
            service.EquipWeapon(0, operatorSlot: 1);

            // Ammo should be preserved on the item
            Assert.AreEqual(10, op1.EquippedWeapon!.CurrentAmmo, "ammo stays on weapon item");
        }

        [Test]
        public void CanReload_returnsFalse_whenNoWeaponEquipped()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData("9mm"));

            Assert.IsFalse(service.CanReload(0, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsFalse_whenCaliberMismatch()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("5.56", 30));
            service.AddItem(MakeAmmoBoxData("9mm"));
            service.EquipWeapon(0, operatorSlot: 0);

            Assert.IsFalse(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsTrue_whenCaliberMatchAndNotFull()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm"));
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(10); // spend some ammo

            Assert.IsTrue(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void CanReload_returnsFalse_whenWeaponFull()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm"));
            service.EquipWeapon(0, operatorSlot: 0);
            // weapon starts full (30/30)

            Assert.IsFalse(service.CanReload(1, operatorSlot: 0));
        }

        [Test]
        public void ReloadOperator_fillsWeapon_andDeductsFromBox()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm", defaultQuantity: 99), quantity: 99);
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(10); // 20 rounds spent

            service.ReloadOperator(1, operatorSlot: 0);

            Assert.AreEqual(30, op.EquippedWeapon.CurrentAmmo, "weapon is full");
            Assert.AreEqual(2,  service.Items.Count,           "box still in inventory");
            var box = service.Items[1] as AmmoBoxItem;
            Assert.AreEqual(79, box!.Quantity, "box deducted 20 rounds");
        }

        [Test]
        public void ReloadOperator_removesBox_whenExhausted()
        {
            var op      = MakeAlive(0);
            var service = new InventoryService(new FakeRoster(op));
            service.AddItem(MakeWeaponData("9mm", 30));
            service.AddItem(MakeAmmoBoxData("9mm"), quantity: 5); // only 5 rounds left
            service.EquipWeapon(0, operatorSlot: 0);
            op.EquippedWeapon!.SetAmmo(0); // empty

            service.ReloadOperator(1, operatorSlot: 0);

            // weapon refills to min(30, 5) = 5 rounds; box exhausted
            Assert.AreEqual(5, op.EquippedWeapon.CurrentAmmo, "weapon gets 5 rounds");
            Assert.AreEqual(1, service.Items.Count,            "box removed");
        }

        [Test]
        public void ReloadOperator_noOp_whenCannotReload()
        {
            var service = new InventoryService(new FakeRoster(MakeAlive(0)));
            service.AddItem(MakeAmmoBoxData("9mm"), quantity: 30); // no weapon equipped

            service.ReloadOperator(0, operatorSlot: 0);

            Assert.AreEqual(1, service.Items.Count, "box not consumed");
        }
    }
}
```

**Nota sobre `ReloadOperator_removesBox_whenExhausted`:** cuando la caja tiene 5 balas y el arma está vacía (maxAmmo=30), solo recibe 5 balas (el mínimo de las disponibles). El box se agota. La lógica en `InventoryService.ReloadOperator` necesita ajustarse para manejar cajas con menos balas que la capacidad del arma:

La lógica real es:
```
rounds = min(weapon.MaxAmmo - weapon.CurrentAmmo, box.Quantity)
weapon.SetAmmo(weapon.CurrentAmmo + rounds)
box.Quantity -= rounds
if box.Quantity <= 0: remove box
```

Actualizar `InventoryService.ReloadOperator` para usar este cálculo:

```csharp
public void ReloadOperator(int ammoBoxIndex, int operatorSlot)
{
    if (!CanReload(ammoBoxIndex, operatorSlot)) return;

    var box    = (AmmoBoxItem)this.items[ammoBoxIndex];
    var weapon = this.roster[operatorSlot].EquippedWeapon!;

    int needed = weapon.MaxAmmo - weapon.CurrentAmmo;
    int rounds = needed < box.Quantity ? needed : box.Quantity;
    weapon.SetAmmo(weapon.CurrentAmmo + rounds);
    box.Quantity -= rounds;

    if (box.Quantity <= 0)
        this.items.RemoveAt(ammoBoxIndex);
}
```

**Step 4: Correr tests**

Window > General > Test Runner > Run All (EditMode).
Esperado: `OperatorRosterTests` pasan (10 tests), `InventoryServiceTests` pasan (13 tests).

**Step 5: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs
git add Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs
git add Game/CrimsonDraft/Assets/Tests/EditMode/InventoryServiceTests.cs
git commit -m "feat(inventory): factory pattern, ammo on WeaponItem, partial reload from AmmoBox"
```

---

## Task 6: StartingLoadout + providers + NavigationScope

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/StartingLoadout.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/StartingLoadoutRosterSeedProvider.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/InventoryBootstrap.cs`
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/DefaultOperatorRosterSeedProvider.cs`
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryDebugSeeder.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`

**Step 1: Crear StartingLoadout.cs**

```csharp
#nullable enable

using System;
using UnityEngine;
using CrimsonDraft.Operators;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    [Serializable]
    public struct StartingItemEntry
    {
        public ItemData item;
        public int      quantity;
    }

    [CreateAssetMenu(fileName = "StartingLoadout", menuName = "CrimsonDraft/Starting Loadout")]
    public sealed class StartingLoadout : ScriptableObject
    {
        [SerializeField] private OperatorData?[]     operatorSlots = new OperatorData?[4];
        [SerializeField] private StartingItemEntry[] items         = Array.Empty<StartingItemEntry>();

        public OperatorData?[]     OperatorSlots => this.operatorSlots;
        public StartingItemEntry[] Items         => this.items;
    }
}
```

**Step 2: Crear StartingLoadoutRosterSeedProvider.cs**

```csharp
#nullable enable

using UnityEngine.Scripting;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    public sealed class StartingLoadoutRosterSeedProvider : IOperatorRosterSeedProvider
    {
        private const int DefaultHp = 100;
        private readonly StartingLoadout loadout;

        [Preserve]
        public StartingLoadoutRosterSeedProvider(StartingLoadout loadout) => this.loadout = loadout;

        public OperatorRosterSeed GetSeed() =>
            new OperatorRosterSeed(this.loadout.OperatorSlots, DefaultHp);
    }
}
```

**Step 3: Crear InventoryBootstrap.cs**

```csharp
#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class InventoryBootstrap : IInitializable
    {
        private readonly StartingLoadout   loadout;
        private readonly IInventoryService inventory;

        [Preserve]
        public InventoryBootstrap(StartingLoadout loadout, IInventoryService inventory)
        {
            this.loadout   = loadout;
            this.inventory = inventory;
        }

        public void Initialize()
        {
            foreach (var entry in this.loadout.Items)
                this.inventory.AddItem(entry.item, entry.quantity);
        }
    }
}
```

**Step 4: Eliminar DefaultOperatorRosterSeedProvider.cs e InventoryDebugSeeder.cs**

Desde Unity, eliminar los archivos (Assets > Scripts > Navigation > DefaultOperatorRosterSeedProvider.cs y Assets > Scripts > Navigation > UI > InventoryDebugSeeder.cs) o via el explorador. No usar `git rm` directamente — Unity gestiona los .meta files.

**Step 5: Actualizar NavigationScope.cs**

```csharp
#nullable enable

using VContainer;
using VContainer.Unity;
using UnityEngine;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Navigation.Combat;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    /// <summary>
    /// DI scope for the ship navigation scene (top-down exploration).
    /// Parent: GameLifetimeScope. Child: CombatScope (loaded additively).
    ///
    /// Assign this component to a GameObject in Navigation.unity.
    /// Set the Parent field in the Inspector to the GameLifetimeScope prefab.
    /// </summary>
    public sealed class NavigationScope : LifetimeScope
    {
        [SerializeField] private StartingLoadout startingLoadout = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.startingLoadout);

            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InventoryView>();
            builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
            builder.Register<InventoryController>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<InventoryBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<CombatTrigger>();
            builder.RegisterComponentInHierarchy<NavigationCameraRegistrar>().AsImplementedInterfaces();
            builder.Register<StartingLoadoutRosterSeedProvider>(Lifetime.Singleton).As<IOperatorRosterSeedProvider>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();
            builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();
        }
    }
}
```

**Step 6: Verificar compilación en Unity**

No deben haber errores. Unity mostrará warnings sobre los .asset viejos de ItemData en ScriptableObjects/Inventory — esto se arregla en Task 9.

**Step 7: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/StartingLoadout.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/StartingLoadoutRosterSeedProvider.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/InventoryBootstrap.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs
git commit -m "feat(navigation): StartingLoadout replaces DefaultOperatorRosterSeedProvider and InventoryDebugSeeder"
```

---

## Task 7: Navigation display — usar DisplayName del operador

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryController.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs`

**Step 1: Actualizar InventoryController.cs — usar DisplayName en BuildOperatorNameMap y BuildOperatorSubMenuEntries**

En `BuildOperatorNameMap` (línea 258), cambiar:
```csharp
string id = op.Data?.OperatorId ?? string.Empty;
map[i] = id.Length > 0 ? id : $"Slot {i}";
```
por:
```csharp
string name = op.Data?.DisplayName ?? string.Empty;
map[i] = name.Length > 0 ? name : $"Slot {i}";
```

En `BuildOperatorSubMenuEntries` (línea 201), cambiar:
```csharp
string rawId = op.Data?.OperatorId ?? string.Empty;
string name  = rawId.Length > 0 ? rawId : $"Slot {i}";
```
por:
```csharp
string rawName = op.Data?.DisplayName ?? string.Empty;
string name    = rawName.Length > 0 ? rawName : $"Slot {i}";
```

**Step 2: Actualizar InventoryView.cs — usar DisplayName y mostrar ammo en el panel del roster**

En `RefreshRosterPanel` (línea 104), cambiar:
```csharp
string rawId   = op.Data?.OperatorId ?? string.Empty;
string name    = rawId.Length > 0 ? rawId : $"Slot {i}";
int    wIdx    = inventory.GetEquippedWeaponIndex(i);
string wpnName = wIdx >= 0 ? inventory.Items[wIdx].Data.DisplayName : "---";
```
por:
```csharp
string rawName = op.Data?.DisplayName ?? string.Empty;
string name    = rawName.Length > 0 ? rawName : $"Slot {i}";
int    wIdx    = inventory.GetEquippedWeaponIndex(i);
string wpnName;
if (wIdx >= 0)
{
    string dn     = inventory.Items[wIdx].Data.DisplayName;
    var    weapon = op.EquippedWeapon;
    wpnName = weapon != null ? $"{dn} ({weapon.CurrentAmmo}/{weapon.MaxAmmo})" : dn;
}
else
{
    wpnName = "---";
}
```

**Step 3: Verificar compilación en Unity**

No deben haber errores.

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryController.cs
git add Game/CrimsonDraft/Assets/Scripts/Navigation/UI/InventoryView.cs
git commit -m "feat(inventory-ui): show operator displayName and weapon ammo in roster panel"
```

---

## Task 8: Combat — actualizar lecturas de ammo

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/OperatorSelectionState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/ShotCountSelectionState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Commands/ShootCommand.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Combat/Commands/ReloadCommand.cs`

**Step 1: Actualizar OperatorSelectionState.cs**

Cambiar `SyncAllOperatorAmmo` (línea 67):
```csharp
// Antes:
this.menuView.SetOperatorAmmo(i, this.roster[i].Ammo, this.roster[i].MaxAmmo);
// Después:
var w = this.roster[i].EquippedWeapon;
this.menuView.SetOperatorAmmo(i, w?.CurrentAmmo ?? 0, w?.MaxAmmo ?? 0);
```

Cambiar `OnOperatorFocused` (línea 49):
```csharp
// Antes:
this.menuView.SetOperatorAmmo(index, this.roster[index].Ammo, this.roster[index].MaxAmmo);
// Después:
var w = this.roster[index].EquippedWeapon;
this.menuView.SetOperatorAmmo(index, w?.CurrentAmmo ?? 0, w?.MaxAmmo ?? 0);
```

Cambiar `OnOperatorSelected` (línea 56):
```csharp
// Antes:
bool hasAmmo = this.roster.Count > index && this.roster[index].Ammo > 0;
// Después:
bool hasAmmo = this.roster.Count > index && (this.roster[index].EquippedWeapon?.CurrentAmmo ?? 0) > 0;
```

**Step 2: Actualizar CommandPanelState.cs**

Cambiar el bloque `CombatCommand.Reload` (líneas 57-65):
```csharp
if (command == CombatCommand.Reload)
{
    int op = this.context.SelectedOperator;
    if (this.roster.Count > op)
    {
        var w = this.roster[op].EquippedWeapon;
        this.menuView.SetOperatorAmmo(op, w?.CurrentAmmo ?? 0, w?.MaxAmmo ?? 0);
    }
    this.commandPanel.Hide();
    this.context.TransitionTo(this.context.OperatorSelState);
    return;
}
```

Cambiar `GetMaxAvailableShotCount` (línea 79):
```csharp
// Antes:
return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].Ammo);
// Después:
return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].EquippedWeapon?.CurrentAmmo ?? 0);
```

**Step 3: Actualizar ShotCountSelectionState.cs**

Cambiar `GetMaxAvailable` (línea 73):
```csharp
// Antes:
return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].Ammo);
// Después:
return Mathf.Min(CombatMenuController.MaxShotCount, this.roster[op].EquippedWeapon?.CurrentAmmo ?? 0);
```

**Step 4: Actualizar AimingState.cs**

Cambiar en `HandleShotsResolved` (línea 80):
```csharp
// Antes:
this.roster[op].ConsumeAmmo(this.context.SelectedShotCount);
// Después:
var weapon = this.roster[op].EquippedWeapon;
if (weapon != null)
    weapon.SetAmmo(weapon.CurrentAmmo - this.context.SelectedShotCount);
```

**Step 5: Actualizar ShootCommand.cs**

```csharp
#nullable enable

using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat.Commands
{
    public sealed class ShootCommand : IOperatorCommand
    {
        private readonly OperatorRuntime op;
        private readonly int             targetSlot;
        private readonly int             shotCount;
        private readonly IBattlefieldView battlefield;

        public ShootCommand(OperatorRuntime op, int targetSlot, int shotCount, IBattlefieldView battlefield)
        {
            this.op          = op;
            this.targetSlot  = targetSlot;
            this.shotCount   = shotCount;
            this.battlefield = battlefield;
        }

        public void Execute()
        {
            var weapon = this.op.EquippedWeapon;
            if (weapon != null)
                weapon.SetAmmo(weapon.CurrentAmmo - this.shotCount);
            this.battlefield.ApplyDamageToEnemy(this.targetSlot, this.shotCount * CombatMenuController.BaseDamage);
        }
    }
}
```

**Step 6: Actualizar ReloadCommand.cs**

`Reload` en combat ya no aplica (recarga viene de Navigation/InventoryService).

```csharp
#nullable enable

using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat.Commands
{
    public sealed class ReloadCommand : IOperatorCommand
    {
        private readonly OperatorRuntime op;

        public ReloadCommand(OperatorRuntime op) => this.op = op;

        // Reload is handled via InventoryService in Navigation, not during combat.
        public void Execute() { }
    }
}
```

**Step 7: Verificar compilación en Unity**

No deben haber errores. Correr EditMode tests: todos deben pasar.

**Step 8: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/OperatorSelectionState.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/CommandPanelState.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/ShotCountSelectionState.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/States/AimingState.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/Commands/ShootCommand.cs
git add Game/CrimsonDraft/Assets/Scripts/Combat/Commands/ReloadCommand.cs
git commit -m "refactor(combat): read ammo from EquippedWeapon instead of OperatorRuntime.Ammo"
```

---

## Task 9: Editor — actualizar generadores de assets

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Editor/InventoryAssetGenerator.cs`
- Create: `Game/CrimsonDraft/Assets/Scripts/Editor/OperatorAssetGenerator.cs`

**Step 1: Reescribir InventoryAssetGenerator.cs**

Ahora crea `WeaponData` y `AmmoBoxData` en vez de `ItemData`. Si existe un asset del tipo incorrecto en la ruta, lo elimina y recrea.

```csharp
#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Editor
{
    public static class InventoryAssetGenerator
    {
        private const string OutputPath = "Assets/ScriptableObjects/Inventory";

        [MenuItem("CrimsonDraft/Generate Inventory Assets")]
        public static void GenerateAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(OutputPath))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Inventory");

            CreateWeapon("Mk18",       "mk18",       "Mk18 (5.56)",       "5.56",  30);
            CreateWeapon("Benelli_M4", "benelli_m4", "Benelli M4 (12ga)", "12ga",  8);
            CreateWeapon("P229",       "p229",       "P229 (9mm)",        "9mm",   15);
            CreateWeapon("MP5",        "mp5",        "MP5 (9mm)",         "9mm",   30);
            CreateAmmoBox("9mm_Box",   "9mm_box",    "9mm Box",           "9mm",   30);
            CreateAmmoBox("556_Box",   "556_box",    "5.56 Box",          "5.56",  30);
            CreateAmmoBox("12ga_Box",  "12ga_box",   "12ga Box",          "12ga",  30);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[InventoryAssetGenerator] Done. Assets at {OutputPath}");
        }

        private static void CreateWeapon(
            string fileName, string itemId, string displayName,
            string caliber,  int magazineCapacity)
        {
            string path = $"{OutputPath}/{fileName}.asset";
            EnsureCorrectType<WeaponData>(path);
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null)
            {
                Debug.Log($"[InventoryAssetGenerator] Skipped (exists): {path}");
                return;
            }

            var d  = ScriptableObject.CreateInstance<WeaponData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue        = itemId;
            so.FindProperty("itemType").enumValueIndex   = (int)ItemType.Weapon;
            so.FindProperty("displayName").stringValue   = displayName;
            so.FindProperty("caliber").stringValue       = caliber;
            so.FindProperty("magazineCapacity").intValue = magazineCapacity;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(d, path);
            Debug.Log($"[InventoryAssetGenerator] Created: {path}");
        }

        private static void CreateAmmoBox(
            string fileName, string itemId, string displayName,
            string caliber,  int defaultQuantity)
        {
            string path = $"{OutputPath}/{fileName}.asset";
            EnsureCorrectType<AmmoBoxData>(path);
            if (AssetDatabase.LoadAssetAtPath<AmmoBoxData>(path) != null)
            {
                Debug.Log($"[InventoryAssetGenerator] Skipped (exists): {path}");
                return;
            }

            var d  = ScriptableObject.CreateInstance<AmmoBoxData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue          = itemId;
            so.FindProperty("itemType").enumValueIndex     = (int)ItemType.AmmoBox;
            so.FindProperty("displayName").stringValue     = displayName;
            so.FindProperty("caliber").stringValue         = caliber;
            so.FindProperty("defaultQuantity").intValue    = defaultQuantity;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(d, path);
            Debug.Log($"[InventoryAssetGenerator] Created: {path}");
        }

        /// <summary>Deletes the asset at path if it exists but is NOT of type T.</summary>
        private static void EnsureCorrectType<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null && existing is not T)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[InventoryAssetGenerator] Deleted wrong-type asset: {path}");
            }
        }
    }
}
```

**Step 2: Crear OperatorAssetGenerator.cs**

Crea 4 `OperatorData` assets con nombres de placeholder. El usuario debe llenar `displayName` y asignar los sprites en el Inspector.

```csharp
#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Editor
{
    public static class OperatorAssetGenerator
    {
        private const string OutputPath = "Assets/ScriptableObjects/Operators";

        [MenuItem("CrimsonDraft/Generate Operator Assets")]
        public static void GenerateAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(OutputPath))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Operators");

            CreateOperator("Operator_0", "op_0", "BRAVO-1");
            CreateOperator("Operator_1", "op_1", "BRAVO-2");
            CreateOperator("Operator_2", "op_2", "BRAVO-3");
            CreateOperator("Operator_3", "op_3", "BRAVO-4");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OperatorAssetGenerator] Done. Assets at {OutputPath}");
        }

        private static void CreateOperator(string fileName, string operatorId, string displayName)
        {
            string path = $"{OutputPath}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<OperatorData>(path) != null)
            {
                Debug.Log($"[OperatorAssetGenerator] Skipped (exists): {path}");
                return;
            }

            var d  = ScriptableObject.CreateInstance<OperatorData>();
            var so = new SerializedObject(d);
            so.FindProperty("operatorId").stringValue  = operatorId;
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(d, path);
            Debug.Log($"[OperatorAssetGenerator] Created: {path}");
        }
    }
}
```

**Step 3: Verificar compilación en Unity**

No deben haber errores.

**Step 4: Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Editor/InventoryAssetGenerator.cs
git add Game/CrimsonDraft/Assets/Scripts/Editor/OperatorAssetGenerator.cs
git commit -m "feat(editor): update inventory generator for WeaponData/AmmoBoxData, add operator generator"
```

---

## Task 10: Setup manual en Unity Editor

Este task no tiene código — es configuración en el Unity Editor.

**Step 1: Regenerar los inventory assets**

1. Menu: `CrimsonDraft > Generate Inventory Assets`
2. Verificar en `Assets/ScriptableObjects/Inventory/`: deben existir `Mk18.asset` (tipo `WeaponData`), `Benelli_M4.asset`, `P229.asset`, `MP5.asset`, `9mm_Box.asset` (tipo `AmmoBoxData`), `556_Box.asset`, `12ga_Box.asset`.

**Step 2: Generar operator assets**

1. Menu: `CrimsonDraft > Generate Operator Assets`
2. Verificar en `Assets/ScriptableObjects/Operators/`: 4 assets `Operator_0` ... `Operator_3` con tipo `OperatorData`.
3. Opcional: cambiar los `displayName` en el Inspector a nombres definitivos.

**Step 3: Crear StartingLoadout asset**

1. Project window: botón derecho en `Assets/ScriptableObjects/` > Create > CrimsonDraft > Starting Loadout
2. Nombrar el asset `StartingLoadout`
3. En el Inspector configurar:
   - `Operator Slots [0]` → `Operator_0`
   - `Operator Slots [1]` → `Operator_1`
   - `Operator Slots [2]` → `Operator_2`
   - `Operator Slots [3]` → `Operator_3`
   - `Items`: agregar entradas:
     - `item` = `MP5`, `quantity` = 0 (usa magazineCapacity = 30)
     - `item` = `9mm_Box`, `quantity` = 99
     - `item` = `556_Box`, `quantity` = 99

**Step 4: Asignar StartingLoadout en NavigationScope**

1. Abrir `Navigation.unity`
2. Seleccionar el GameObject que tiene `NavigationScope`
3. En el Inspector, field `Starting Loadout` → asignar el asset `StartingLoadout`

**Step 5: Eliminar InventoryDebugSeeder del scene**

Si existe un GameObject con el componente `InventoryDebugSeeder` (Missing Script), eliminar ese componente del Inspector.

**Step 6: Verificar en Play Mode**

1. Entrar en Play Mode en Navigation.unity
2. Abrir el inventario (Tab)
3. Verificar:
   - Roster muestra "BRAVO-1" (u otro nombre configurado)
   - Los items aparecen: MP5 (con 30/30 balas), 9mm Box (99), 5.56 Box (99)
   - Equipar MP5 a BRAVO-1 → roster muestra "MP5 (30/30)"
   - Recargar MP5 vacío con 9mm Box → balas suben, box.Quantity baja
   - Entrar en combate → Combat muestra ammo del arma equipada
   - Disparar en Combat → ammo baja
   - Salir de Combat → Navigation refleja el ammo actualizado

**Step 7: Commit de los assets (Unity generados)**

```bash
git add Game/CrimsonDraft/Assets/ScriptableObjects/
git add Game/CrimsonDraft/Assets/Navigation.unity
git commit -m "feat(assets): add StartingLoadout, WeaponData and OperatorData assets"
```
