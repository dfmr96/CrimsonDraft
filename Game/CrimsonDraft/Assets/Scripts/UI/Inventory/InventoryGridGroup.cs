using UnityEngine;

namespace CrimsonDraft.UI
{
    public class InventoryGridGroup : MonoBehaviour
    {
        [SerializeField] private InventoryGrid[] grids;

        [SerializeField] private InventoryItemView itemViewPrefab;

        public InventoryItemView ItemViewPrefab => itemViewPrefab;

        [SerializeField] private StorageWindow storageWindow;

        public StorageWindow StorageWindow => storageWindow;

        public int Count => grids.Length;

        public InventoryGrid GetGrid(int index)
        {
            if (index < 0 || index >= grids.Length) return null;
            return grids[index];
        }

        public bool HasNext(int currentIndex) => currentIndex + 1 < grids.Length;
        public bool HasPrev(int currentIndex) => currentIndex - 1 >= 0;

        public int IndexOf(InventoryGrid grid)
        {
            for (int i = 0; i < grids.Length; i++)
                if (grids[i] == grid) return i;
            return -1;
        }
    }
}
