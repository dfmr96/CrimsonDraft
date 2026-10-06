using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class DepleteEquipment : MonoBehaviour
    {
        [SerializeField] private EquipableItemData m_Item;
        [SerializeField] private float m_Depletion = 0.01f;
        [SerializeField] private float m_DepletionFrequency = 1f;

        public UnityEvent<float> OnDepleted;

        private void Awake()
        {
            Debug.Assert(m_Depletion > 0, "Equipment m_Depletion amount can't be less than 0", gameObject);
            Debug.Assert(m_DepletionFrequency > 0, "Equipment m_DepletionFrequency can't be less than 0", gameObject);
        }

        private void OnEnable()
        {
            InvokeRepeating(nameof(Deplete), m_DepletionFrequency, m_DepletionFrequency);
        }

        private void OnDisable()
        {
            CancelInvoke();
        }

        private void Deplete()
        {
            var equipped = GameManager.Instance.Inventory.GetEquipped(m_Item.Slot);
            Debug.Assert(equipped.Item == m_Item, "Item to deplete do not match equipped item");

            float prevStatus = equipped.Status;
            equipped.Status = Mathf.Clamp01(equipped.Status - m_Depletion);
            
            if (equipped.Status != prevStatus)
                OnDepleted.Invoke(equipped.Status);
        }
    }
}
