#nullable enable

namespace CrimsonDraft.Combat
{
    public interface IBattlefieldView
    {
        void Populate(EncounterData encounter);
        void SetOperatorIndicator(int slotIndex);
        void DimOperatorIndicator();
        void SetEnemyTargetIndicator(int slotIndex);
        void HideEnemyTargetIndicator();
        int[] GetOccupiedEnemySlots();
        AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex);
    }
}
