#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;
using Object = UnityEngine.Object;

namespace CrimsonDraft.UI
{
    public sealed class ContainerGridPresenter : IDisposable
    {
        private readonly InventoryItemView itemPrefab;
        private readonly Dictionary<InventoryItem, InventoryItemView> views = new Dictionary<InventoryItem, InventoryItemView>();
        private bool renderPending;

        public ContainerGridPresenter(ItemContainer container, InventoryGrid grid, InventoryItemView itemPrefab)
        {
            this.Container  = container;
            this.Grid       = grid;
            this.itemPrefab = itemPrefab;

            this.Container.Changed += RenderOrDefer;
            this.Grid.Enabled      += OnGridEnabled;
            RenderOrDefer();
        }

        public event Action? Rendered;

        public ItemContainer Container { get; }
        public InventoryGrid Grid      { get; }

        public InventoryItemView? FindView(InventoryItem item) =>
            this.views.TryGetValue(item, out var view) ? view : null;

        public InventoryItemView? ViewAt(Vector2Int cell) =>
            this.Container.GetItemAt(cell) is { } item ? FindView(item) : null;

        public void Dispose()
        {
            this.Container.Changed -= RenderOrDefer;
            if (this.Grid != null) this.Grid.Enabled -= OnGridEnabled;
            DestroyViews();
        }

        private void OnGridEnabled()
        {
            if (this.renderPending) Render();
        }

        private void RenderOrDefer()
        {
            if (this.Grid != null && this.Grid.isActiveAndEnabled) Render();
            else this.renderPending = true;
        }

        private void Render()
        {
            this.renderPending = false;
            DestroyViews();
            foreach (var placement in this.Container.Placements)
            {
                var view = Object.Instantiate(this.itemPrefab, this.Grid.transform);
                view.Bind(placement, this.Grid);
                this.views[placement.Item] = view;
            }
            this.Rendered?.Invoke();
        }

        private void DestroyViews()
        {
            foreach (var view in this.views.Values)
                if (view != null) Object.Destroy(view.gameObject);
            this.views.Clear();
        }
    }
}
