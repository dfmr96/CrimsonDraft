#nullable enable

using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public interface IItemSpawner
    {
        bool HasSpace(ItemData data);
        void Spawn(ItemData data, InventoryGrid? preferredGrid = null);
        void SpawnExisting(InventoryItem item, InventoryGrid? preferredGrid = null);
    }
}
