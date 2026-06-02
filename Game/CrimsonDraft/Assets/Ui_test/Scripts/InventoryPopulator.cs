#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public class InventoryPopulator : MonoBehaviour
    {
        [SerializeField] private InventoryGridGroup         gridGroup;
        [SerializeField] private InventoryItemView          itemPrefab    = null!;
        [SerializeField] private List<ItemData>             itemsToPlace  = new();
        [SerializeField] private int                        maxPlacementAttempts = 50;

        void Start()
        {
            if (gridGroup == null)
                gridGroup = GetComponentInParent<InventoryGridGroup>();

            foreach (var itemData in itemsToPlace)
                TryPlaceItem(itemData);
        }

        void TryPlaceItem(ItemData itemData)
        {
            var validGrids = new List<(InventoryGrid grid, Vector2Int origin)>();

            for (int g = 0; g < gridGroup.Count; g++)
            {
                InventoryGrid grid = gridGroup.GetGrid(g);

                int maxCol = grid.Columns - itemData.GridSize.x;
                int maxRow = grid.Rows    - itemData.GridSize.y;

                if (maxCol < 0 || maxRow < 0) continue;

                for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
                {
                    var origin = new Vector2Int(
                        Random.Range(0, maxCol + 1),
                        Random.Range(0, maxRow + 1));

                    if (grid.CanPlace(origin, itemData.GridSize))
                    {
                        validGrids.Add((grid, origin));
                        break;
                    }
                }
            }

            if (validGrids.Count == 0)
            {
                Debug.LogWarning($"[InventoryPopulator] No space found for item: {itemData.DisplayName}");
                return;
            }

            var (chosenGrid, chosenOrigin) = validGrids[Random.Range(0, validGrids.Count)];
            int gridIndex = gridGroup.IndexOf(chosenGrid);
            SpawnItem(itemData, chosenGrid, chosenOrigin, gridIndex);
        }

        void SpawnItem(ItemData itemData, InventoryGrid grid, Vector2Int origin, int gridIndex)
        {
            Inventory.InventoryItem domainItem = itemData switch
            {
                Inventory.WeaponData     wd => new Inventory.WeaponItem(wd),
                Inventory.AmmoBoxData    ad => new Inventory.AmmoBoxItem(ad, 0),
                Inventory.ConsumableData cd => new Inventory.ConsumableItem(cd),
                Inventory.KeyItemData    kd => new Inventory.KeyItem(kd),
                Inventory.SocketItemData sd => new Inventory.SocketItem(sd),
                _                          => new Inventory.InventoryItem(itemData)
            };
            InventoryItemView view = Instantiate(itemPrefab, grid.transform);
            view.Initialize(domainItem, origin, grid.CellSize);
            view.SetSlotIndex(gridIndex * 4);

            var rt = view.GetComponent<RectTransform>();
            rt.anchoredPosition = grid.CellToLocal(origin);

            grid.PlaceItem(view);
        }
    }
}

#endif
