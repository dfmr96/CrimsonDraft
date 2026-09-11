#nullable enable
namespace CrimsonDraft.Combat
{
    // Numeric values are serialized directly into CommandPanel.prefab's CommandEntry array --
    // Melee is appended at the end (not reordered to the front) so Shoot/Items/FocusFire's
    // existing int values (0/1/2) never shift and silently corrupt already-serialized entries.
    // Its "before Shoot" position in the UI is controlled purely by entries[] order in the
    // prefab, independent of this enum's declaration order.
    public enum CombatCommand { Shoot, Items, FocusFire, Melee }
}
