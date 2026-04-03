#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "AmmoBoxData", menuName = "CrimsonDraft/Inventory/Ammo Box Data")]
    public sealed class AmmoBoxData : ItemData
    {
        [SerializeField] private string caliber         = string.Empty;
        [SerializeField] private int    defaultQuantity = 30;

        public string Caliber         => this.caliber;
        public int    DefaultQuantity => this.defaultQuantity;
        public override bool Stackable => true;
    }
}
