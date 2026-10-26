#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.UI
{
    /// <summary>One operator the item being used can be applied to (shown as a button in the context menu).</summary>
    public readonly struct UseTarget
    {
        public readonly int    Slot;
        public readonly string Label;
        public readonly bool   Enabled;

        public UseTarget(int slot, string label, bool enabled)
        {
            this.Slot    = slot;
            this.Label   = label;
            this.Enabled = enabled;
        }
    }

    public struct ContextMenuOptions
    {
        // When set, "Use" swaps the buttons for these operators instead of using the item right away.
        public IReadOnlyList<UseTarget>? UseTargets;
        public bool CanCombine;
        public bool CanEquip;
        public bool CanUse;
        public bool CanSplit;
        public bool CanInspect;
    }
}
