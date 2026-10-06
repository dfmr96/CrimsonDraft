using UnityEngine;

namespace HorrorEngine
{
    public class UIExamineItemUtility : MonoBehaviour
    {
        public GameObject Visuals;
        [SerializeField] private LayerMask m_ExpectedMask;

        private InventoryEntry m_Entry = new InventoryEntry();
        // --------------------------------------------------------------------

        private void Awake()
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            foreach(var r in renderers)
            {
                int objectLayerMask = 1 << r.gameObject.layer;
                Debug.Assert((objectLayerMask & m_ExpectedMask.value) != 0, $"Renderer {r.name} is not using the expected layer for rendering during item inspection");
            }
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one);
        }

        // --------------------------------------------------------------------

        public void GiveItemToPlayer(ItemData item)
        {
            GameManager gameMgr = GameManager.Instance;

            m_Entry.Item = item;
            m_Entry.Count = 1;
            m_Entry.SecondaryCount = 0;
            m_Entry.Status = 0;
            InventoryEntry entry = gameMgr.Inventory.TryAddAndShowFullMessage(m_Entry, true);

            if (entry == null && item.Flags.HasFlag(ItemFlags.CreatePickupOnDrop))
            {
                gameMgr.Player.GetComponentInChildren<PickupDropper>().Drop(m_Entry);
            }

            // Show the splash anyway to show what has been found
            ItemPickedUpMessage msg = new ItemPickedUpMessage();
            msg.Data = item;
            MessageBuffer<ItemPickedUpMessage>.Dispatch(msg);

        }

        // --------------------------------------------------------------------

        public void RemoveItemFromPlayer(ItemData item)
        {
            GameManager.Instance.Inventory.Remove(item);
        }

        // --------------------------------------------------------------------

        public void CloseExamination()
        {
            UIManager.Get<UIExamineItem>().Hide();
        }

        // --------------------------------------------------------------------

        public void OpenInventory()
        {
            UIManager.Get<UIInventory>().Show();
        }
    }
}