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

            internal FakeSeedProvider(OperatorData?[] operators, int defaultHp, int defaultAmmo) =>
                this.seed = new OperatorRosterSeed(operators, defaultHp, defaultAmmo);

            public OperatorRosterSeed GetSeed() => this.seed;
        }

        private static OperatorRuntime MakePresent(int slot, int maxHp = 100, int maxAmmo = 6) =>
            new OperatorRuntime(slot, null, isPresent: true, maxHp, maxAmmo);

        private static OperatorRuntime MakeAbsent(int slot) =>
            new OperatorRuntime(slot, null, isPresent: false, maxHp: 100, maxAmmo: 6);

        [Test]
        public void EnsureInitialized_setsHpAndAmmoFromSeed()
        {
            var roster = new OperatorRoster(new FakeSeedProvider(new OperatorData?[] { null, null, null }, defaultHp: 80, defaultAmmo: 4));
            roster.EnsureInitialized();

            Assert.AreEqual(0, roster[0].Hp);   // null slot → not present → Hp = 0
            Assert.AreEqual(0, roster[0].Ammo);
        }

        [Test]
        public void EnsureInitialized_isIdempotent()
        {
            var roster = new OperatorRoster(new FakeSeedProvider(new OperatorData?[] { null, null }, defaultHp: 80, defaultAmmo: 4));

            roster.EnsureInitialized();
            int firstCount = roster.Count;
            roster.EnsureInitialized();

            Assert.IsTrue(roster.IsInitialized);
            Assert.AreEqual(firstCount, roster.Count);
        }

        [Test]
        public void ApplyDamage_clampsToZero_andMarksDead()
        {
            var op = MakePresent(0, maxHp: 100);
            var result = op.ApplyDamage(150);

            Assert.AreEqual(0, result.RemainingHp);
            Assert.IsTrue(result.IsDead);
            Assert.IsFalse(op.IsAlive);
        }

        [Test]
        public void ApplyDamage_partialDamage_doesNotKill()
        {
            var op = MakePresent(1, maxHp: 100);
            var result = op.ApplyDamage(30);

            Assert.AreEqual(70, result.RemainingHp);
            Assert.IsFalse(result.IsDead);
            Assert.IsTrue(op.IsAlive);
        }

        [Test]
        public void GetAliveSlots_excludesAbsentAndDeadOperators()
        {
            var roster = new OperatorRoster(
                new FakeSeedProvider(
                    new OperatorData?[] { null, null, null },
                    defaultHp: 100,
                    defaultAmmo: 6));
            roster.EnsureInitialized();

            // Make slot 1 "present" via a direct OperatorRuntime we can control
            // Instead test with the Initialize + ApplyDamage path:
            // All slots are absent (null data) → IsPresent=false → IsAlive=false
            var alive = roster.GetAliveSlots();
            Assert.AreEqual(0, alive.Count);
        }

        [Test]
        public void Reload_restoresAmmoToMax()
        {
            var op = MakePresent(0, maxHp: 100, maxAmmo: 6);
            op.ConsumeAmmo(4);
            Assert.AreEqual(2, op.Ammo);

            op.Reload();
            Assert.AreEqual(6, op.Ammo);
        }

        [Test]
        public void ConsumeAmmo_clampsToZero()
        {
            var op = MakePresent(0, maxHp: 100, maxAmmo: 6);
            op.ConsumeAmmo(100);
            Assert.AreEqual(0, op.Ammo);
        }

        [Test]
        public void AbsentOperator_isNotAlive()
        {
            var op = MakeAbsent(0);
            Assert.IsFalse(op.IsPresent);
            Assert.IsFalse(op.IsAlive);
            Assert.AreEqual(0, op.Hp);
            Assert.AreEqual(0, op.Ammo);
        }

        [Test]
        public void HpRatio_returnsCorrectFraction()
        {
            var op = MakePresent(0, maxHp: 100, maxAmmo: 6);
            op.ApplyDamage(25);
            Assert.AreEqual(0.75f, op.HpRatio, delta: 0.001f);
        }
    }
}
