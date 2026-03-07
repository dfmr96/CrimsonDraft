#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryDebugSeeder : MonoBehaviour
    {
        [SerializeField] private ItemData[] items = System.Array.Empty<ItemData>();

        private IInventoryService? service;

        [Inject]
        public void Inject(IInventoryService inventoryService) => this.service = inventoryService;

        private void Start()
        {
            if (this.service == null) return;
            foreach (var item in this.items)
                this.service.AddItem(item);
        }
    }
}
