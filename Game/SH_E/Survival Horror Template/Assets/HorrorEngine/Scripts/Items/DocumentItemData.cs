using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Items/Document")]
    public class DocumentItemData : ItemData
    {
        [SerializeField] DocumentData m_Document;
        [SerializeField] bool m_AddToDocumentList;

        public override void OnUse(InventoryEntry entry, Actor user)
        {
            base.OnUse(entry, user);

            if (m_AddToDocumentList)
            {
                if (!GameManager.Instance.Inventory.Documents.Contains(m_Document))
                {
                    GameManager.Instance.Inventory.Documents.Add(m_Document);
                }
            }

            // Close inventory and push for reopening
            // TODO - Replace show/hide inventory calls with message buffer events
            UIInventory inventory = UIManager.Get<UIInventory>();
            if (inventory.gameObject.activeSelf)
            {
                inventory.Hide();
                UIManager.PushAction(new UIStackedAction()
                {
                    Action = () => { inventory.Show(); },
                    Name = "PostDocUse",
                    StopProcessingActions = false
                });
            }

            UIManager.Get<UIDocument>().Show(m_Document);
        }
    }
}