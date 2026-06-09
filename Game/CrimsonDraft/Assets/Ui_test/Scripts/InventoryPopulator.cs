#nullable enable

using System.Collections.Generic;
using UnityEngine;
using CrimsonDraft.Inventory;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NaughtyAttributes;
#endif

namespace CrimsonDraft.UI
{
    public class InventoryPopulator : MonoBehaviour, IItemSpawner
    {
        [SerializeField] private InventoryGridGroup gridGroup   = null!;
        [SerializeField] private InventoryItemView  itemPrefab  = null!;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private List<ItemData> itemsToPlace         = new();
        [SerializeField] private int            maxPlacementAttempts = 50;

        [Header("Testing")]
        [SerializeField] private ItemData? itemToAdd;
#endif

        void Start()
        {
            if (this.gridGroup == null)
                this.gridGroup = GetComponentInParent<InventoryGridGroup>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var itemData in itemsToPlace)
                TryPlaceItem(itemData);
#endif
        }

        // ── IItemSpawner ─────────────────────────────────────────────────────

        public bool HasSpace(ItemData data)
        {
            for (int g = 0; g < this.gridGroup.Count; g++)
                if (TryFindSlot(this.gridGroup.GetGrid(g), data, out _)) return true;
            return false;
        }

        public void Spawn(ItemData data, InventoryGrid? preferredGrid = null)
        {
            if (preferredGrid != null && TryFindSlot(preferredGrid, data, out var preferred))
            {
                SpawnItemView(data, preferredGrid, preferred);
                return;
            }

            for (int g = 0; g < this.gridGroup.Count; g++)
            {
                InventoryGrid grid = this.gridGroup.GetGrid(g);
                if (grid == preferredGrid) continue;
                if (TryFindSlot(grid, data, out var origin))
                {
                    SpawnItemView(data, grid, origin);
                    return;
                }
            }

            Debug.LogWarning($"[InventoryPopulator] No space for: {data.DisplayName}");
        }

        public void SpawnExisting(InventoryItem item, InventoryGrid? preferredGrid = null)
        {
            if (preferredGrid != null && TryFindSlot(preferredGrid, item.Data, out var preferred))
            {
                SpawnItemViewFromItem(item, preferredGrid, preferred);
                return;
            }

            for (int g = 0; g < this.gridGroup.Count; g++)
            {
                InventoryGrid grid = this.gridGroup.GetGrid(g);
                if (grid == preferredGrid) continue;
                if (TryFindSlot(grid, item.Data, out var origin))
                {
                    SpawnItemViewFromItem(item, grid, origin);
                    return;
                }
            }

            Debug.LogWarning($"[InventoryPopulator] No space for existing item: {item.Data.DisplayName}");
        }

        private void SpawnItemViewFromItem(InventoryItem item, InventoryGrid grid, Vector2Int origin)
        {
            InventoryItemView view = Instantiate(this.itemPrefab, grid.transform);
            view.Initialize(item, origin, grid.CellSize);
            view.SetOwnerGrid(grid);

            var rt = view.GetComponent<RectTransform>();
            rt.anchoredPosition = grid.CellToLocal(origin);

            grid.PlaceItem(view);
            view.RefreshQuantity();
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private bool TryFindSlot(InventoryGrid grid, ItemData data, out Vector2Int origin)
        {
            int maxCol = grid.Columns - data.GridSize.x;
            int maxRow = grid.Rows    - data.GridSize.y;
            if (maxCol < 0 || maxRow < 0) { origin = default; return false; }

            for (int row = 0; row <= maxRow; row++)
                for (int col = 0; col <= maxCol; col++)
                {
                    var o = new Vector2Int(col, row);
                    if (grid.CanPlace(o, data.GridSize)) { origin = o; return true; }
                }

            origin = default;
            return false;
        }

        private void SpawnItemView(ItemData itemData, InventoryGrid grid, Vector2Int origin)
        {
            InventoryItem domainItem = itemData switch
            {
                WeaponData     wd => new WeaponItem(wd),
                AmmoBoxData    ad => new AmmoBoxItem(ad, 0),
                ConsumableData cd => new ConsumableItem(cd),
                KeyItemData    kd => new KeyItem(kd),
                SocketItemData sd => new SocketItem(sd),
                _                => new InventoryItem(itemData)
            };

            InventoryItemView view = Instantiate(this.itemPrefab, grid.transform);
            view.Initialize(domainItem, origin, grid.CellSize);
            view.SetOwnerGrid(grid);

            var rt = view.GetComponent<RectTransform>();
            rt.anchoredPosition = grid.CellToLocal(origin);

            grid.PlaceItem(view);
            view.RefreshQuantity();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void TryPlaceItem(ItemData itemData)
        {
            var validGrids = new List<(InventoryGrid grid, Vector2Int origin)>();

            for (int g = 0; g < this.gridGroup.Count; g++)
            {
                InventoryGrid grid = this.gridGroup.GetGrid(g);
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

        [Button]
        public void SpawnItem()
        {
            if (this.itemToAdd != null) TryPlaceItem(this.itemToAdd);
        }
#endif
    }
}
