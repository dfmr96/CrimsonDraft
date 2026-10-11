#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public sealed class HeldItemView : MonoBehaviour
    {
        [SerializeField] private InventoryItemView itemViewPrefab   = null!;
        [SerializeField] private Color             holdTint         = new Color(154f / 255f, 159f / 255f, 92f / 255f, 1f);
        [SerializeField] private float             alphaCannotPlace = 100f / 255f;

        private InventoryItemView? view;

        public void Show(InventoryItem item, int rotation, InventoryGrid grid, Vector2Int cell, bool canPlace)
        {
            if (this.view == null)
                this.view = Instantiate(this.itemViewPrefab, grid.transform);
            else if (this.view.transform.parent != grid.transform)
                this.view.transform.SetParent(grid.transform, false);

            this.view.Bind(new ItemPlacement(item, cell, rotation), grid);
            this.view.transform.SetAsLastSibling();

            var tint = this.holdTint;
            if (!canPlace) tint.a = this.alphaCannotPlace;
            this.view.SetTint(tint);
        }

        public void Hide()
        {
            if (this.view != null) Destroy(this.view.gameObject);
            this.view = null;
        }
    }
}
