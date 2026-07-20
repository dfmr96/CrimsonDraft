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
        public bool IsStaggered   { get; }

        public EnemyDamageResult(int slotIndex, int damageApplied, int remainingHp, bool isDead, bool isStaggered)
        {
            this.SlotIndex     = slotIndex;
            this.DamageApplied = damageApplied;
            this.RemainingHp   = remainingHp;
            this.IsDead        = isDead;
            this.IsStaggered   = isStaggered;
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
        EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage);
        bool IsEnemyStaggered(int slotIndex);
        bool HasAliveEnemies();
        UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots);
#if UNITY_EDITOR || DEBUG_COMBAT
        (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex);
#endif
    }
}
