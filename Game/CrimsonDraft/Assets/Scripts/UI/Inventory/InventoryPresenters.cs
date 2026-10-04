#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public sealed class InventoryPresenters : IInitializable, IDisposable
    {
        private readonly IInventoryService  inventory;
        private readonly InventoryGridGroup gridGroup;
        private readonly Dictionary<ContainerId, ContainerGridPresenter> presenters = new Dictionary<ContainerId, ContainerGridPresenter>();
        private readonly List<ContainerId> order = new List<ContainerId>();

        [Preserve]
        public InventoryPresenters(IInventoryService inventory, InventoryGridGroup gridGroup)
        {
            this.inventory = inventory;
            this.gridGroup = gridGroup;
        }

        public event Action<ContainerId>? Rendered;

        public ContainerId FirstGrid => this.order[0];

        public void Initialize()
        {
            // A grid whose operator slot is empty in the roster has no container; it stays a
            // blank card and navigation skips it (see BuildLinks).
            var available = new HashSet<ContainerId>(this.inventory.OperatorContainers.Select(c => c.Id));
            for (int i = 0; i < this.gridGroup.Count; i++)
            {
                var grid = this.gridGroup.GetGrid(i);
                var id   = grid.ContainerId;
                if (!available.Contains(id)) continue;

                var presenter = new ContainerGridPresenter(this.inventory.GetContainer(id), grid, this.gridGroup.ItemViewPrefab);
                presenter.Rendered += () => this.Rendered?.Invoke(id);
                this.presenters[id] = presenter;
                this.order.Add(id);
            }
        }

        public bool Has(ContainerId id) => this.presenters.ContainsKey(id);

        public ContainerGridPresenter Get(ContainerId id) => this.presenters[id];

        public InventoryItemView? FindView(InventoryItem item)
        {
            var id = this.inventory.FindContainerOf(item);
            return id.HasValue && this.presenters.TryGetValue(id.Value, out var presenter) ? presenter.FindView(item) : null;
        }

        public IReadOnlyDictionary<ContainerId, GridLinks> BuildLinks() =>
            this.presenters.ToDictionary(
                pair => pair.Key,
                pair => new GridLinks(
                    NextPresented(pair.Value.Grid, g => g.Left),
                    NextPresented(pair.Value.Grid, g => g.Right),
                    NextPresented(pair.Value.Grid, g => g.Up),
                    NextPresented(pair.Value.Grid, g => g.Down)));

        public void Dispose()
        {
            foreach (var presenter in this.presenters.Values) presenter.Dispose();
            this.presenters.Clear();
            this.order.Clear();
        }

        private ContainerId? NextPresented(InventoryGrid from, Func<InventoryGrid, InventoryGrid?> step)
        {
            var visited = new HashSet<InventoryGrid> { from };
            var next    = step(from);
            while (next != null && !this.presenters.ContainsKey(next.ContainerId))
            {
                if (!visited.Add(next)) return null;
                next = step(next);
            }
            return next != null ? next.ContainerId : (ContainerId?)null;
        }
    }
}
