#nullable enable

namespace CrimsonDraft.Combat
{
    public interface IOperatorEcgFeedback
    {
        void FlashOperatorDamage(int operatorSlotIndex);
        void SetOperatorHealthState(int operatorSlotIndex, float hpRatio, bool isAlive);
    }
}
