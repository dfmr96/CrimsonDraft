using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.UI
{
    public class InventoryPopulator : MonoBehaviour
    {
        [SerializeField] private InventoryGridGroup gridGroup;
        [SerializeField] private InventoryItem itemPrefab;
        [SerializeField] private List<ItemData> itemsToPlace;
        [SerializeField] private int maxPlacementAttempts = 50;

        void Start()
        {
            if (gridGroup == null)
                gridGroup = GetComponentInParent<InventoryGridGroup>();

            foreach (var itemData in itemsToPlace)
                TryPlaceItem(itemData);
        }

        void TryPlaceItem(ItemData itemData)
        {
            // Collect all grids that could fit this item
            var validGrids = new List<(InventoryGrid grid, Vector2Int origin)>();

            for (int g = 0; g < gridGroup.Count; g++)
            {
                InventoryGrid grid = gridGroup.GetGrid(g);

                int maxCol = grid.Columns - itemData.gridSize.x;
                int maxRow = grid.Rows    - itemData.gridSize.y;

                if (maxCol < 0 || maxRow < 0) continue;

                for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
                {
                    var origin = new Vector2Int(
                        Random.Range(0, maxCol + 1),
                        Random.Range(0, maxRow + 1));

                    if (grid.CanPlace(origin, itemData.gridSize))
                    {
                        validGrids.Add((grid, origin));
                        break;
                    }
                }
            }

            if (validGrids.Count == 0)
            {
                Debug.LogWarning($"[InventoryPopulator] No space found for item: {itemData.primaryName}");
                return;
            }

            // Pick a random valid placement
            var (chosenGrid, chosenOrigin) = validGrids[Random.Range(0, validGrids.Count)];
            SpawnItem(itemData, chosenGrid, chosenOrigin);
        }

        void SpawnItem(ItemData itemData, InventoryGrid grid, Vector2Int origin)
        {
            InventoryItem item = Instantiate(itemPrefab, grid.transform);
            item.Initialize(itemData, origin, grid.CellSize);

            var rt = item.GetComponent<RectTransform>();
            rt.anchoredPosition = grid.CellToLocal(origin);

            grid.PlaceItem(item);
        }
    }
}
