using UnityEngine;

namespace CrimsonDraft.UI
{
    public class Ui_manager : MonoBehaviour
    {
        [SerializeField] private InventoryGrid inventoryGrid;

        void Start()
        {
            if (inventoryGrid == null)
                inventoryGrid = FindFirstObjectByType<InventoryGrid>();
        }

        // Query helper — true if the screen position is inside the grid.
        public bool IsOverGrid(Vector2 screenPos, out Vector2Int cell)
        {
            return inventoryGrid.WorldToCell(screenPos, out cell);
        }
    }
}

