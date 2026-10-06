using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class InventoryExpansionItemData : ItemData
    {
        public int Amount = 2;

        public override void OnUse(InventoryEntry entry, Actor user)
        {
            base.OnUse(entry, user);

            GameManager.Instance.Inventory.Expand(Amount);

            UIManager.PushAction(new UIStackedAction()
            {
                Action = () =>
                {
                    UIManager.Get<UIInventory>().Show();
                },
                StopProcessingActions = true,
                Name = "InventoryExpansionItemData.OnUse (Show Inventory)"
            });
        }
    }
}
