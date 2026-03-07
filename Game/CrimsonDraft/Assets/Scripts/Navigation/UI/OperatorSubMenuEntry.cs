#nullable enable

namespace CrimsonDraft.Navigation.UI
{
    public readonly struct OperatorSubMenuEntry
    {
        public int    SlotIndex      { get; }
        public string OperatorName   { get; }
        public string EquippedWeapon { get; }
        public bool   IsValid        { get; }

        public OperatorSubMenuEntry(int slotIndex, string operatorName, string equippedWeapon, bool isValid)
        {
            SlotIndex      = slotIndex;
            OperatorName   = operatorName;
            EquippedWeapon = equippedWeapon;
            IsValid        = isValid;
        }
    }
}
