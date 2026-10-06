using System.Collections;
using UnityEngine;

namespace HorrorEngine
{

    [CreateAssetMenu(menuName = "Horror Engine/Combinations/Replenish Status")]
    public class InventoryCombinationReplenishStatus : InventoryItemCombination
    {
        public float Amount = 1f;
        public bool ConsumeRefill = true;

        public override InventoryEntry OnCombine(InventoryEntry entry1, InventoryEntry entry2)
        {
            bool depletable1 = entry1.Item.Flags.HasFlag(ItemFlags.Depletable);
            bool depletable2 = entry2.Item.Flags.HasFlag(ItemFlags.Depletable);
            if (depletable1 || depletable2)
            {
                InventoryEntry depletable = depletable1 ? entry1 : entry2;
                InventoryEntry refill = depletable1 ? entry2 : entry1;

                if (ConsumeRefill)
                    GameManager.Instance.Inventory.Remove(refill);

                depletable.Status += Amount;
                return entry1;
            }

            Debug.LogError("Combination error: None of the items are marked as Depletable");

            return entry1;
        }
    }
}