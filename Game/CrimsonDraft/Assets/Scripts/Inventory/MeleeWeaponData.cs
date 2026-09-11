#nullable enable

using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    // Permanently-equipped melee weapon data -- never enters the spatial inventory pipeline
    // (no ammo/magazine, no uses), so it's just an identity + icon; no combat stats yet.
    [CreateAssetMenu(fileName = "MeleeWeaponData", menuName = "CrimsonDraft/Inventory/Melee Weapon Data")]
    public sealed class MeleeWeaponData : ItemData, IMeleeWeapon
    {
    }
}
