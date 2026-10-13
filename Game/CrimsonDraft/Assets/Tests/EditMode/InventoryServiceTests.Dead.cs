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
            Assert.AreEqual(0, Op(s, 0).Placements.Count());
            Assert.AreEqual(1, Op(s, 1).Placements.Count());
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
            Assert.AreEqual(1, Op(s, 0).Placements.Count());
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
    }
}
