#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryController : IInitializable, IDisposable
    {
        private readonly IInputService     inputService;
        private readonly IInventoryService inventoryService;
        private readonly IOperatorRoster   roster;
        private readonly InventoryView     view;

        [Preserve]
        public InventoryController(
            IInputService     inputService,
            IInventoryService inventoryService,
            IOperatorRoster   roster,
            InventoryView     view)
        {
            this.inputService     = inputService;
            this.inventoryService = inventoryService;
            this.roster           = roster;
            this.view             = view;
        }

        void IInitializable.Initialize() { }
        void IDisposable.Dispose()       { }
    }
}
