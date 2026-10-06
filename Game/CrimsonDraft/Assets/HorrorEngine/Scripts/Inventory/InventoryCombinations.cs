using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public abstract class InventoryItemCombination : ScriptableObject
    {
        public ItemData Item1;
        public ItemData Item2;

        public virtual bool CanCombine(InventoryEntry entry1, InventoryEntry entry2)
        {
            return Item1 == entry1.Item && Item2 == entry2.Item && entry1 != entry2;
        }

        public abstract InventoryEntry OnCombine(InventoryEntry entry1, InventoryEntry entry2);

    }


}
