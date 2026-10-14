namespace CrimsonDraft.Operators.Appearance
{
    /// <summary>Interchangeable equipment slots of the soldier mesh (see Soldier_Appearance_Variants.md).</summary>
    public enum SoldierAppearanceSlot
    {
        Helmet   = 0,
        Glasses  = 1,
        Backpack = 2,
        Vest     = 3,
        Mask     = 4, // spec-ops masks (gas mask, respirator, shemagh...): no piece in the source FBX
    }

    public static class SoldierAppearanceVariants
    {
        public const int    SlotCount = 5;
        /// <summary>Selecting this hides every variant of the slot (no mesh needed).</summary>
        public const string None      = "None";
        public const string Default   = "Default";

        /// <summary>What a slot shows when nothing was chosen: the original piece, or nothing for Mask.</summary>
        public static string DefaultVariant(SoldierAppearanceSlot slot) =>
            slot == SoldierAppearanceSlot.Mask ? None : Default;
    }
}
