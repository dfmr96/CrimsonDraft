#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/ContainerData")]
    public sealed class ContainerData : ScriptableObject
    {
        [SerializeField] private ItemData[] items   = System.Array.Empty<ItemData>();
        [SerializeField] private bool       emptied = false;

        public ItemData[] Items   => this.items;
        public bool       Emptied { get => this.emptied; set => this.emptied = value; }
    }
}
