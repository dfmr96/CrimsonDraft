#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    public sealed class InventoryBootstrap : IInitializable
    {
        private readonly StartingLoadout   loadout;
        private readonly IInventoryService inventory;

        [Preserve]
        public InventoryBootstrap(StartingLoadout loadout, IInventoryService inventory)
        {
            this.loadout   = loadout;
            this.inventory = inventory;
        }

        public void Initialize()
        {
            foreach (var entry in this.loadout.Items)
                this.inventory.AddItem(entry.item, entry.quantity);
        }
    }
}
