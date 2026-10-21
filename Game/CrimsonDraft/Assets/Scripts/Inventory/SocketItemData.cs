#nullable enable

using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "SocketItemData", menuName = "CrimsonDraft/Inventory/Socket Item Data")]
    public sealed class SocketItemData : ItemData
    {
        [Tooltip("Given back to the player once this item has been used on a socket (e.g. the empty flask after pouring acid). Leave empty if the item is simply consumed.")]
        [SerializeField] private ItemData? returnedOnUse;

        public ItemData? ReturnedOnUse => this.returnedOnUse;
    }
}
