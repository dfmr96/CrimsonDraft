using UnityEngine;

namespace CrimsonDraft.UI
{
    public class InventoryGridGroup : MonoBehaviour
    {
        [SerializeField] private InventoryGrid[] grids;

        public int Count => grids.Length;

        public InventoryGrid GetGrid(int index)
        {
            if (index < 0 || index >= grids.Length) return null;
            return grids[index];
        }

        public bool HasNext(int currentIndex) => currentIndex + 1 < grids.Length;
        public bool HasPrev(int currentIndex) => currentIndex - 1 >= 0;
    }
}
