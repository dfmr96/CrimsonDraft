#nullable enable

using Cysharp.Threading.Tasks;

namespace CrimsonDraft.Combat
{
    public readonly struct EnemyDamageResult
    {
        public int SlotIndex      { get; }
        public int DamageApplied  { get; }
        public int RemainingHp    { get; }
        public bool IsDead        { get; }

        public EnemyDamageResult(int slotIndex, int damageApplied, int remainingHp, bool isDead)
        {
            this.SlotIndex     = slotIndex;
            this.DamageApplied = damageApplied;
            this.RemainingHp   = remainingHp;
            this.IsDead        = isDead;
        }
    }

    public interface IBattlefieldView
    {
        void Populate(EncounterData encounter);
        void SetOperatorIndicator(int slotIndex);
        void DimOperatorIndicator();
        void PlayEnemyAttackFeedback(int enemySlotIndex);
        void ShowOperatorDamage(int operatorSlotIndex, int damage);
        void SetEnemyTargetIndicator(int slotIndex);
        void HideEnemyTargetIndicator();
        int[] GetOccupiedEnemySlots();
        AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex);
        EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int damage);
        bool HasAliveEnemies();
        UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots);
#if UNITY_EDITOR || DEBUG_COMBAT
        (int Current, int Max, bool IsDead) GetEnemyHpDebug(int slotIndex);
#endif
    }
}
