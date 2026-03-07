#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "CrimsonDraft/Inventory/Weapon Data")]
    public sealed class WeaponData : ItemData
    {
        [SerializeField] private string caliber          = string.Empty;
        [SerializeField] private int    magazineCapacity = 1;

        public string Caliber          => this.caliber;
        public int    MagazineCapacity => this.magazineCapacity;
    }
}
