using System.Collections;
using UnityEngine;

namespace HorrorEngine
{

    [CreateAssetMenu(menuName = "Horror Engine/Combinations/Reload")]
    public class InventoryCombinationReload : InventoryItemCombination
    {
        public override InventoryEntry OnCombine(InventoryEntry entry1, InventoryEntry entry2)
        {
            ReloadableWeaponData reloadable1 = entry1.Item as ReloadableWeaponData;
            ReloadableWeaponData reloadable2 = entry2.Item as ReloadableWeaponData;
            if (reloadable1 || reloadable2)
            {
                InventoryEntry reloadableEntry = reloadable1 ? entry1 : entry2;
                InventoryEntry ammoEntry = reloadable1 ? entry2 : entry1;

                ReloadableWeaponData reloadable = reloadableEntry.Item as ReloadableWeaponData;
                if (reloadable.AmmoItem == ammoEntry.Item)
                {
                    return GameManager.Instance.Inventory.ReloadWeapon(reloadableEntry, ammoEntry);
                }
            }

            return entry1;
        }
    }
}