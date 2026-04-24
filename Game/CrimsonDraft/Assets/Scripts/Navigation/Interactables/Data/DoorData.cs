#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DoorData")]
    public sealed class DoorData : ScriptableObject
    {
        [SerializeField] private bool         locked  = false;
        [SerializeField] private KeyItemData? keyItem = null;

        public bool         Locked  => this.locked;
        public KeyItemData? KeyItem => this.keyItem;
    }
}
