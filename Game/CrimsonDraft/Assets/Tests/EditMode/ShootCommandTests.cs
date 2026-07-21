#nullable enable

using Cysharp.Threading.Tasks;
using NUnit.Framework;
using CrimsonDraft.Combat;
using CrimsonDraft.Combat.Commands;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public sealed class ShootCommandTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────

        private sealed class FakeWeaponSlot : IWeaponSlot
        {
            public Caliber Caliber     => Caliber._9mm;
            public GunType GunType     => GunType.Pistols;
            public int     BaseDamage  { get; set; } = 25;
            public int     CurrentAmmo { get; private set; }
            public int     MaxAmmo     => 30;
            public int     PoiseDamage => 0;

            public FakeWeaponSlot(int currentAmmo) => this.CurrentAmmo = currentAmmo;

            public void SetAmmo(int value) => this.CurrentAmmo = value;
        }

        private sealed class FakeBattlefieldView : IBattlefieldView
        {
            public int LastDamagedSlot { get; private set; } = -1;
            public int LastDamage      { get; private set; }

            public void Populate(EncounterData encounter)              { }
            public void SetOperatorIndicator(int slotIndex)            { }
            public void DimOperatorIndicator()                         { }
            public void PlayEnemyAttackFeedback(int enemySlotIndex)    { }
            public void ShowOperatorDamage(int operatorSlotIndex, int damage) { }
            public void SetEnemyTargetIndicator(int slotIndex)         { }
            public void HideEnemyTargetIndicator()                     { }
            public int[] GetOccupiedEnemySlots()                       => System.Array.Empty<int>();
            public AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex) => null;
            public bool HasAliveEnemies()                              => true;
            public UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots) =>
                UniTask.CompletedTask;
            public void TriggerEnemyStagger(int slotIndex)  { }
            public void RecoverEnemyStagger(int slotIndex)  { }
            public void FinalizeEnemyDeath(int slotIndex)   { }
            public int[] NotifyActionDequeued()             => System.Array.Empty<int>();
            public bool IsEnemyStaggered(int slotIndex)     => false;
            public bool IsEnemyDead(int slotIndex)          => false;
#if UNITY_EDITOR || DEBUG_COMBAT
            public (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex) => (100, 100, false, 0, false);
#endif

            public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage)
            {
                this.LastDamagedSlot = slotIndex;
                this.LastDamage      = hpDamage;
                return new EnemyDamageResult(slotIndex, hpDamage, 100 - hpDamage, false, false);
            }
        }

        private static OperatorRuntime MakeOperator() =>
            new OperatorRuntime(0, null, isPresent: true, maxHp: 100);

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Execute_withActiveWeapon_deductsAmmoByShotCount()
        {
            var op     = MakeOperator();
            var weapon = new FakeWeaponSlot(currentAmmo: 10);
            op.SetEquippedWeapon(weapon);
            var battlefield = new FakeBattlefieldView();

            new ShootCommand(op, targetSlot: 1, shotCount: 3, battlefield).Execute();

            Assert.AreEqual(7, weapon.CurrentAmmo);
        }

        [Test]
        public void Execute_withActiveWeapon_appliesDamageUsingWeaponBaseDamage()
        {
            var op     = MakeOperator();
            var weapon = new FakeWeaponSlot(currentAmmo: 10) { BaseDamage = 25 };
            op.SetEquippedWeapon(weapon);
            var battlefield = new FakeBattlefieldView();

            new ShootCommand(op, targetSlot: 2, shotCount: 3, battlefield).Execute();

            Assert.AreEqual(2, battlefield.LastDamagedSlot);
            Assert.AreEqual(75, battlefield.LastDamage, "3 shots * 25 base damage");
        }

        [Test]
        public void Execute_withNoActiveWeapon_fallsBackToCombatMenuControllerBaseDamage()
        {
            var op          = MakeOperator();
            var battlefield = new FakeBattlefieldView();

            new ShootCommand(op, targetSlot: 0, shotCount: 2, battlefield).Execute();

            Assert.AreEqual(2 * CombatMenuController.BaseDamage, battlefield.LastDamage);
        }

        [Test]
        public void Execute_withNoActiveWeapon_doesNotThrowWhenSettingAmmo()
        {
            var op          = MakeOperator();
            var battlefield = new FakeBattlefieldView();

            Assert.DoesNotThrow(() => new ShootCommand(op, targetSlot: 0, shotCount: 1, battlefield).Execute());
        }
    }
}
