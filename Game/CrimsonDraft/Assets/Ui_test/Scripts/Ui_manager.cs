#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    public class Ui_manager : MonoBehaviour
    {
        [SerializeField] private InventoryGrid inventoryGrid = null!;

        public bool IsOverGrid(Vector2 screenPos, out Vector2Int cell)
        {
            return this.inventoryGrid.WorldToCell(screenPos, out cell);
        }
    }
}
