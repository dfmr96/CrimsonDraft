#nullable enable

namespace CrimsonDraft.Inventory
{
    // Must match the operator block's visual InventoryGrid (columns × rows) on
    // Card.prefab / CombatInventoryPanel.prefab. InventoryService uses these to build a real
    // occupancy matrix per operator (see InventoryService.BuildOccupancy) instead of a flat
    // item count, so "no space" reflects actual footprint fit, not an arbitrary slot cap.
    public static class InventoryConstants
    {
        public const int OperatorGridWidth  = 4;
        public const int OperatorGridHeight = 4;
        public const int SlotsPerOperator   = OperatorGridWidth * OperatorGridHeight;
    }
}
