#nullable enable

using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "AmmoBoxData", menuName = "CrimsonDraft/Inventory/Ammo Box Data")]
    public sealed class AmmoBoxData : ItemData
    {
        [SerializeField] private Caliber caliber        = Caliber.None;
        [SerializeField] private int    defaultQuantity = 30;

        public Caliber Caliber        => this.caliber;
        public int    DefaultQuantity => this.defaultQuantity;
        public override bool Stackable => true;
    }
}
