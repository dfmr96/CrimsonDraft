#nullable enable

using System;
using System.Linq;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    public sealed partial class InventoryService
    {
        public bool TryCombine(InventoryItem a, InventoryItem b, out InventoryItem? result)
        {
            result = null;
            if (a == b) return false;
            var containerA = FindCarriedContainerOf(a);
            var containerB = FindCarriedContainerOf(b);
            if (containerA == null || containerB == null) return false;

            if (a.Data.ItemId == b.Data.ItemId && a.Data.Stackable)
                return TryMergeStacks(a, containerA, b, containerB, out result);

            var ammo   = a as AmmoBoxItem ?? b as AmmoBoxItem;
            var weapon = a as WeaponItem  ?? b as WeaponItem;
            if (ammo != null && weapon != null)
            {
                if (!TransferAmmo(ammo, weapon)) return false;
                result = weapon;
                return true;
            }

            return TryCombineRecipe(a, containerA, b, containerB, out result);
        }

        public bool TryUseConsumable(InventoryItem item, int operatorSlot)
        {
            if (item.Data is not ConsumableData consumable) return false;
            var container = FindCarriedContainerOf(item);
            if (container == null) return false;

            var target = this.roster[operatorSlot];
            if (target.IsAlive) target.Heal(consumable.HealAmount);

            item.Quantity -= 1;
            if (item.Quantity <= 0) container.Remove(item);
            container.NotifyChanged();
            return true;
        }

        public bool CanReload(InventoryItem ammo, int operatorSlot)
        {
            if (ammo is not AmmoBoxItem box || box.Quantity <= 0 || FindCarriedContainerOf(box) == null) return false;
            var op     = this.roster[operatorSlot];
            var weapon = op.ActiveWeapon;
            return op.IsAlive && weapon != null && weapon.Caliber == box.Data.Caliber && weapon.CurrentAmmo < weapon.MaxAmmo;
        }

        public bool TryReload(InventoryItem ammo, int operatorSlot) =>
            CanReload(ammo, operatorSlot) && TransferAmmo((AmmoBoxItem)ammo, this.roster[operatorSlot].ActiveWeapon!);

        public void Equip(WeaponItem weapon, int operatorSlot)
        {
            if (FindCarriedContainerOf(weapon) == null) return;
            int weaponSlot = (int)weapon.Data.WeaponSlot;
            var replaced   = AllPlacements()
                .Select(p => p.Item)
                .OfType<WeaponItem>()
                .Where(w => w != weapon && w.EquippedBySlot == operatorSlot && w.EquippedWeaponSlot == weaponSlot)
                .ToList();
            foreach (var previous in replaced) previous.ClearEquipped();

            if (weapon.IsEquipped) UnequipInternal(weapon);
            weapon.SetEquipped(operatorSlot, weaponSlot);
            this.roster[operatorSlot].SetEquippedWeapon(weapon, weaponSlot);

            NotifyChanged(replaced.Append(weapon).Select(FindContainerObjectOf).ToArray());
        }

        public void Unequip(WeaponItem weapon)
        {
            if (!weapon.IsEquipped) return;
            UnequipInternal(weapon);
            NotifyChanged(FindContainerObjectOf(weapon));
        }

        public bool HasEquippedWeapon(int operatorSlot) =>
            AllPlacements().Any(p => p.Item is WeaponItem w && w.EquippedBySlot == operatorSlot);

        public KeyUseOutcome TryUseKey(string keyItemId)
        {
            var key = AllPlacements().Select(p => p.Item).OfType<KeyItem>().FirstOrDefault(k => k.Data.ItemId == keyItemId);
            if (key == null) return new KeyUseOutcome(KeyUseResult.NotFound, (KeyItem?)null);
            if (key.UsesRemaining == 0) return new KeyUseOutcome(KeyUseResult.AlreadyDepleted, key);
            return new KeyUseOutcome(key.Consume() ? KeyUseResult.DepletedAfterUse : KeyUseResult.Success, key);
        }

        private static bool TryMergeStacks(
            InventoryItem source, ItemContainer sourceContainer,
            InventoryItem target, ItemContainer targetContainer,
            out InventoryItem? result)
        {
            result = null;
            int transfer = Math.Min(source.Quantity, target.Data.MaxStack - target.Quantity);
            if (transfer <= 0) return false;

            target.Quantity += transfer;
            source.Quantity -= transfer;
            if (source.Quantity == 0) sourceContainer.Remove(source);

            NotifyChanged(sourceContainer, targetContainer);
            result = target;
            return true;
        }

        private bool TransferAmmo(AmmoBoxItem ammo, IWeaponSlot weapon)
        {
            if (ammo.Quantity <= 0 || ammo.Data.Caliber != weapon.Caliber || weapon.CurrentAmmo >= weapon.MaxAmmo)
                return false;

            int taken = Math.Min(weapon.MaxAmmo - weapon.CurrentAmmo, ammo.Quantity);
            weapon.SetAmmo(weapon.CurrentAmmo + taken);
            ammo.Quantity -= taken;

            var ammoContainer   = FindContainerObjectOf(ammo);
            var weaponContainer = weapon is InventoryItem weaponItem ? FindContainerObjectOf(weaponItem) : null;
            if (ammo.Quantity == 0) ammoContainer?.Remove(ammo);
            NotifyChanged(ammoContainer, weaponContainer);
            return true;
        }

        private bool TryCombineRecipe(
            InventoryItem a, ItemContainer containerA,
            InventoryItem b, ItemContainer containerB,
            out InventoryItem? result)
        {
            result = null;
            if (a.IsEquipped || b.IsEquipped) return false;
            var resultData = this.combineService.TryGetResult(a.Data, b.Data);
            if (resultData == null) return false;

            var combined   = InventoryItemFactory.Create(resultData);
            var placementA = containerA.GetPlacement(a)!;
            var placementB = containerB.GetPlacement(b)!;
            containerA.Remove(a);
            containerB.Remove(b);

            var candidates = EnsureContainers().OrderBy(c => c == containerA ? 0 : 1).ToList();
            if (!TryFindFreeCell(candidates, resultData.GridSize, out var target, out var origin))
            {
                containerA.Place(a, placementA.Origin, placementA.Rotation);
                containerB.Place(b, placementB.Origin, placementB.Rotation);
                return false;
            }

            target!.Place(combined, origin, 0);
            NotifyChanged(containerA, containerB, target);
            result = combined;
            return true;
        }
    }
}
