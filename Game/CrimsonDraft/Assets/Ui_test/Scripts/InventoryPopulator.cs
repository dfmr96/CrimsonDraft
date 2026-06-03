#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    public class InventoryPopulator : MonoBehaviour, IItemSpawner
    {
        [SerializeField] private InventoryGridGroup gridGroup;
        [SerializeField] private InventoryItemView  itemPrefab           = null!;
        [SerializeField] private List<ItemData>     itemsToPlace         = new();
        [SerializeField] private int                maxPlacementAttempts = 50;

        [Header("Testing")]
        [SerializeField] private ItemData? itemToAdd;

        void Start()
        {
            if (gridGroup == null)
                gridGroup = GetComponentInParent<InventoryGridGroup>();

            foreach (var itemData in itemsToPlace)
                TryPlaceItem(itemData);
        }

        // ── IItemSpawner ─────────────────────────────────────────────────────

        public bool HasSpace(ItemData data)
        {
            for (int g = 0; g < gridGroup.Count; g++)
                if (TryFindSlot(gridGroup.GetGrid(g), data, out _)) return true;
            return false;
        }

        public void Spawn(ItemData data, InventoryGrid? preferredGrid = null)
        {
            // Try preferred grid first (operator's own grid)
            if (preferredGrid != null && TryFindSlot(preferredGrid, data, out var preferred))
            {
                SpawnItemView(data, preferredGrid, preferred);
                return;
            }

            // Fall back: first available slot across all grids in order
            for (int g = 0; g < gridGroup.Count; g++)
            {
                InventoryGrid grid = gridGroup.GetGrid(g);
                if (grid == preferredGrid) continue;
                if (TryFindSlot(grid, data, out var origin))
                {
                    SpawnItemView(data, grid, origin);
                    return;
                }
            }

            Debug.LogWarning($"[InventoryPopulator] No space for: {data.DisplayName}");
        }

        // Systematic top-left scan — returns first fitting origin, no randomness.
        private bool TryFindSlot(InventoryGrid grid, ItemData data, out Vector2Int origin)
        {
            int maxCol = grid.Columns - data.GridSize.x;
            int maxRow = grid.Rows    - data.GridSize.y;
            if (maxCol < 0 || maxRow < 0) { origin = default; return false; }

            for (int row = 0; row <= maxRow; row++)
            {
                for (int col = 0; col <= maxCol; col++)
                {
                    var o = new Vector2Int(col, row);
                    if (grid.CanPlace(o, data.GridSize)) { origin = o; return true; }
                }
            }
            origin = default;
            return false;
        }

        // ── Testing button ───────────────────────────────────────────────────

        [Button]
        public void SpawnItem()
        {
            if (this.itemToAdd != null) TryPlaceItem(this.itemToAdd);
        }

        // ── Internal ─────────────────────────────────────────────────────────

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
            SpawnItemView(itemData, chosenGrid, chosenOrigin);
        }

        void SpawnItemView(ItemData itemData, InventoryGrid grid, Vector2Int origin)
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
            view.SetOwnerGrid(grid);

            var rt = view.GetComponent<RectTransform>();
            rt.anchoredPosition = grid.CellToLocal(origin);

            grid.PlaceItem(view);
            view.RefreshQuantity();
        }
    }
}

#endif
