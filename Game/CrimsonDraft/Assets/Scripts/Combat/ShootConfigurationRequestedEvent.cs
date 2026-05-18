#nullable enable

namespace CrimsonDraft.Combat
{
    public readonly struct ShootConfigurationRequestedEvent
    {
        public int OperatorSlot { get; }

        public ShootConfigurationRequestedEvent(int operatorSlot)
        {
            this.OperatorSlot = operatorSlot;
        }
    }
}
