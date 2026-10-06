#nullable enable
namespace CrimsonDraft.Combat
{
    // Numeric values are serialized directly into CommandPanel.prefab's CommandEntry array, so
    // they're pinned explicitly -- 2 was FocusFire (removed) and stays unused so Melee keeps 3
    // and already-serialized entries never shift. The UI order of the commands is controlled
    // purely by entries[] order in the prefab, independent of these values.
    public enum CombatCommand { Shoot = 0, Items = 1, Melee = 3 }
}
