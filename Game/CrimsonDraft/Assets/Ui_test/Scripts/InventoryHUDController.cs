#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    // Stub — implementación completa en Commit D (refactor MVP GridCursor)
    public sealed class InventoryHUDController : IInitializable, System.IDisposable
    {
        [Preserve]
        public InventoryHUDController(
            IInventoryService inventoryService,
            ICombineService   combineService,
            IOperatorRoster   roster,
            GridCursor        cursor,
            ItemContextMenu   contextMenu,
            PartyPanelView    partyPanel) { }

        public void Initialize() { }
        public void Dispose()    { }
    }
}
