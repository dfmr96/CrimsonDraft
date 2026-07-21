#nullable enable

using NUnit.Framework;
using UnityEngine;
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

        private static OperatorData MakeOperatorData(int maxHp)
        {
            var d  = ScriptableObject.CreateInstance<OperatorData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("maxHp").intValue = maxHp;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        [Test]
        public void EnsureInitialized_setsHpFromSeed()
        {
            var roster = new OperatorRoster(new FakeSeedProvider(new OperatorData?[] { null, null, null }, defaultHp: 80));
            roster.EnsureInitialized();

            Assert.AreEqual(0, roster[0].Hp);   // null slot -> not present -> Hp = 0
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
        public void GetHpSnapshot_returnsCurrentHpPerSlot()
        {
            var roster = new OperatorRoster(
                new FakeSeedProvider(new OperatorData?[] { null, null }, defaultHp: 100));
            roster.EnsureInitialized();
            roster[0].ApplyDamage(30);

            var snapshot = roster.GetHpSnapshot();

            Assert.AreEqual(new[] { 0, 0 }, snapshot, "operators seeded with null data are not present, so Hp is 0");
        }

        [Test]
        public void GetHpSnapshot_capturesPresentOperatorsDamage()
        {
            var data   = MakeOperatorData(maxHp: 100);
            var roster = new OperatorRoster(new FakeSeedProvider(new OperatorData?[] { data }, defaultHp: 100));
            roster.EnsureInitialized();
            roster[0].ApplyDamage(40);

            var snapshot = roster.GetHpSnapshot();

            Assert.AreEqual(60, snapshot[0]);
        }

        [Test]
        public void RestoreHp_appliesSnapshotValuesToSlots()
        {
            var roster = new OperatorRoster(
                new FakeSeedProvider(new OperatorData?[] { null, null }, defaultHp: 100));
            roster.EnsureInitialized();

            roster.RestoreHp(new[] { 55, 10 });

            Assert.AreEqual(55, roster[0].Hp);
            Assert.AreEqual(10, roster[1].Hp);
        }

        [Test]
        public void RestoreHp_clampsToSlotMaxHp()
        {
            var roster = new OperatorRoster(
                new FakeSeedProvider(new OperatorData?[] { null }, defaultHp: 50));
            roster.EnsureInitialized();

            roster.RestoreHp(new[] { 999 });

            Assert.AreEqual(50, roster[0].Hp);
        }

        [Test]
        public void RestoreHp_snapshotShorterThanRoster_onlyUpdatesMatchingSlots()
        {
            var roster = new OperatorRoster(
                new FakeSeedProvider(new OperatorData?[] { null, null, null }, defaultHp: 100));
            roster.EnsureInitialized();

            roster.RestoreHp(new[] { 20 });

            Assert.AreEqual(20, roster[0].Hp);
            Assert.AreEqual(0, roster[1].Hp, "operators seeded with null data are not present, unaffected by restore");
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
            Assert.IsNull(op.PrimaryWeapon);

            var fakeWeapon = new FakeWeaponSlot(Caliber._9mm, 15, 15);
            op.SetEquippedWeapon(fakeWeapon);

            Assert.AreEqual(fakeWeapon, op.PrimaryWeapon);
            Assert.AreEqual(15, op.PrimaryWeapon!.CurrentAmmo);
        }

        [Test]
        public void SetEquippedWeapon_null_clearsWeapon()
        {
            var op = MakePresent(0);
            op.SetEquippedWeapon(new FakeWeaponSlot(Caliber._9mm, 15, 15));
            op.SetEquippedWeapon(null);

            Assert.IsNull(op.PrimaryWeapon);
        }

        private sealed class FakeWeaponSlot : IWeaponSlot
        {
            public Caliber Caliber     { get; }
            public GunType GunType     => GunType.Pistols;
            public int     BaseDamage  => 20;
            public int     CurrentAmmo { get; private set; }
            public int     MaxAmmo     { get; }

            internal FakeWeaponSlot(Caliber caliber, int currentAmmo, int maxAmmo)
            {
                this.Caliber     = caliber;
                this.CurrentAmmo = currentAmmo;
                this.MaxAmmo     = maxAmmo;
            }

            public void SetAmmo(int value) => this.CurrentAmmo = value < 0 ? 0 : value > this.MaxAmmo ? this.MaxAmmo : value;
        }
    }
}
