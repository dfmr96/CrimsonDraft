#nullable enable

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
    }
}
