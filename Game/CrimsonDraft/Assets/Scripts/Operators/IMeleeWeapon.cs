#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    // Mirrors IWeaponSlot's role for firearms -- lets Operators reference melee weapon data
    // (defined in the Inventory assembly) without creating a circular assembly reference.
    public interface IMeleeWeapon
    {
        Sprite?    Icon     { get; }
        Vector2Int GridSize { get; }
    }
}
