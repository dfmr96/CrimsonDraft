#nullable enable

using System.Linq;
using NUnit.Framework;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    // Picked-up ammo goes, whole, to the operator with a matching weapon equipped who has the
    // fewest rounds of that calibre (loaded + boxed); the old first-with-space rule is the fallback.
    public sealed class AmmoPickupRoutingTests
    {
        private WeaponData  pistol   = null!;
        private WeaponData  shotgun  = null!;
        private AmmoBoxData ammo9mm  = null!;
        private AmmoBoxData ammo12ga = null!;

        [SetUp]
        public void SetUp()
        {
            this.pistol   = Weapon(Caliber._9mm,  magazine: 15, id: "pistol");
            this.shotgun  = Weapon(Caliber._12ga, magazine: 6,  id: "shotgun");
            this.ammo9mm  = Ammo(Caliber._9mm,  defaultQuantity: 30, maxStack: 60, id: "ammo_9mm");
            this.ammo12ga = Ammo(Caliber._12ga, defaultQuantity: 12, maxStack: 12, id: "ammo_12ga");
        }

        private static InventoryService Build(FakeRoster roster) =>
            new InventoryService(roster, FakeCombineService.None, FakeCorpseAccess.None);

        private static WeaponItem GiveWeapon(InventoryService inventory, WeaponData data, int slot, int loaded, bool equip = true)
        {
            inventory.TryAdd(data, ContainerId.Operator(slot));
            var weapon = inventory.GetContainer(ContainerId.Operator(slot)).Placements
                .Select(p => p.Item).OfType<WeaponItem>().Last();
            weapon.SetAmmo(loaded);
            if (equip) inventory.Equip(weapon, slot);
            return weapon;
        }

        private static int Rounds(InventoryService inventory, int slot, AmmoBoxData data) =>
            inventory.GetContainer(ContainerId.Operator(slot)).Placements
                .Where(p => p.Item.Data.ItemId == data.ItemId)
                .Sum(p => p.Item.Quantity);

        private static int Boxes(InventoryService inventory, int slot, AmmoBoxData data) =>
            inventory.GetContainer(ContainerId.Operator(slot)).Placements
                .Count(p => p.Item.Data.ItemId == data.ItemId);

        [Test]
        public void Ammo_goesToTheArmedOperatorWithLessAmmo_notTheOneAlreadyHoldingBoxes()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol, slot: 0, loaded: 15);
            inventory.TryAdd(this.ammo9mm, ContainerId.Operator(0), 30);
            GiveWeapon(inventory, this.pistol, slot: 1, loaded: 6);

            Assert.IsTrue(inventory.TryAdd(this.ammo9mm));

            Assert.AreEqual(30, Rounds(inventory, 0, this.ammo9mm), "operator 1 keeps only the box it had");
            Assert.AreEqual(30, Rounds(inventory, 1, this.ammo9mm));
        }

        [Test]
        public void Ammo_goesToTheOnlyOperatorWithAMatchingWeapon()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol,  slot: 0, loaded: 0);
            GiveWeapon(inventory, this.shotgun, slot: 2, loaded: 6);

            Assert.IsTrue(inventory.TryAdd(this.ammo12ga));

            Assert.AreEqual(12, Rounds(inventory, 2, this.ammo12ga));
            Assert.AreEqual(0,  Rounds(inventory, 0, this.ammo12ga));
        }

        [Test]
        public void LoadedRoundsCount_towardsWhoHasLessAmmo()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol, slot: 0, loaded: 15);
            GiveWeapon(inventory, this.pistol, slot: 1, loaded: 2);

            inventory.TryAdd(this.ammo9mm);

            Assert.AreEqual(30, Rounds(inventory, 1, this.ammo9mm));
        }

        [Test]
        public void Tie_goesToTheFirstOperatorInTheRoster()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol, slot: 1, loaded: 5);
            GiveWeapon(inventory, this.pistol, slot: 3, loaded: 5);

            inventory.TryAdd(this.ammo9mm);

            Assert.AreEqual(30, Rounds(inventory, 1, this.ammo9mm));
            Assert.AreEqual(0,  Rounds(inventory, 3, this.ammo9mm));
        }

        [Test]
        public void FullStack_overflowsIntoANewBox_onTheSameOperator()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol, slot: 0, loaded: 15);
            inventory.TryAdd(this.ammo9mm, ContainerId.Operator(0), 60);   // 75 rounds in all
            GiveWeapon(inventory, this.pistol, slot: 1, loaded: 0);
            inventory.TryAdd(this.ammo9mm, ContainerId.Operator(1), 50);   // 10 short of the 60 stack

            inventory.TryAdd(this.ammo9mm);                                // 30 more

            Assert.AreEqual(80, Rounds(inventory, 1, this.ammo9mm));
            Assert.AreEqual(2,  Boxes(inventory, 1, this.ammo9mm));
            Assert.AreEqual(60, Rounds(inventory, 0, this.ammo9mm), "the box is never split across operators");
        }

        [Test]
        public void PreferredOperatorWithoutRoom_passesTheWholeBoxToTheNextCandidate()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol, slot: 0, loaded: 15);
            GiveWeapon(inventory, this.pistol, slot: 1, loaded: 0);
            var filler = Sized(Consumable(id: "filler"), 1, 1);
            while (inventory.TryAdd(filler, ContainerId.Operator(1))) { }   // operator 2's grid is full

            Assert.IsTrue(inventory.TryAdd(this.ammo9mm));

            Assert.AreEqual(30, Rounds(inventory, 0, this.ammo9mm));
        }

        [Test]
        public void NobodyWithAMatchingWeapon_fallsBackToTheFirstOperatorWithRoom()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol, slot: 2, loaded: 0);

            inventory.TryAdd(this.ammo12ga);

            Assert.AreEqual(12, Rounds(inventory, 0, this.ammo12ga));
        }

        [Test]
        public void DeadOperator_neverReceivesAmmo_evenWithAMatchingWeapon()
        {
            var roster    = new FakeRoster(Alive(0), Dead(1), Alive(2), Alive(3));
            var inventory = Build(roster);
            GiveWeapon(inventory, this.pistol, slot: 0, loaded: 15);
            inventory.TryAdd(this.pistol, ContainerId.Operator(1));
            inventory.GetContainer(ContainerId.Operator(1)).Placements
                .Select(p => p.Item).OfType<WeaponItem>().Single().SetEquipped(1, 0);

            inventory.TryAdd(this.ammo9mm);

            Assert.AreEqual(0,  Rounds(inventory, 1, this.ammo9mm));
            Assert.AreEqual(30, Rounds(inventory, 0, this.ammo9mm));
        }

        [Test]
        public void UnequippedWeaponInTheGrid_doesNotCount()
        {
            var inventory = Build(FakeRoster.WithOperators(4));
            GiveWeapon(inventory, this.pistol, slot: 0, loaded: 15);
            GiveWeapon(inventory, this.pistol, slot: 1, loaded: 0, equip: false);

            inventory.TryAdd(this.ammo9mm);

            Assert.AreEqual(30, Rounds(inventory, 0, this.ammo9mm));
            Assert.AreEqual(0,  Rounds(inventory, 1, this.ammo9mm));
        }
    }
}
