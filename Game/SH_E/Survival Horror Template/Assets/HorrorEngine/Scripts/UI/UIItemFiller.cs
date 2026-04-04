using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIItemFiller : MonoBehaviour
    {
        [SerializeField] private Image m_Icon;
        [SerializeField] public TMPro.TextMeshProUGUI m_ItemName;
        [SerializeField] public TMPro.TextMeshProUGUI m_ItemDescription;
        [Tooltip("This optional object will get enabled/disabled depending on the item stackability, if none is givem ItemCount gets disabled instead")]
        [SerializeField] private GameObject m_ItemCountObject;
        [SerializeField] public TMPro.TextMeshProUGUI m_ItemCount;
        [SerializeField] private LocalizableText m_NoItemName;
        [SerializeField] private Color m_NormalAmountColor = Color.green;
        [SerializeField] private Color m_EmptyAmountColor = Color.red;
        [SerializeField] private Color m_MaxStackAmountColor = Color.blue;
        [SerializeField] private GameObject m_Status;
        [SerializeField] private Image m_StatusFill;
        [SerializeField] private Gradient m_StatusFillColorOverValue;
        [SerializeField] private string m_CountFormat;
        [SerializeField] private TextReplacerBase[] m_DescriptionTextReplacers;

        public UnityEvent OnFilled;
        public UnityEvent<Sprite> OnSetIcon;
        public UnityEvent OnCleared;

        public void Fill(ItemData data, float amount, float status)
        {

            if (m_Icon)
            {
                m_Icon.sprite = data.Image;
                m_Icon.gameObject.SetActive(true);
            }

            OnSetIcon?.Invoke(data.Image);

            if (m_ItemCount)
            {
                m_ItemCount.text = Convert.ToInt32(amount).ToString(m_CountFormat);
                bool isReloadable = data as ReloadableWeaponData;

                bool countEnabled = data.Flags.HasFlag(ItemFlags.Stackable) || amount > 0 || isReloadable;
                if (m_ItemCountObject)
                    m_ItemCountObject.SetActive(countEnabled);
                else
                    m_ItemCount.gameObject.SetActive(countEnabled);

                if (amount == 0)
                    m_ItemCount.color = m_EmptyAmountColor;
                else if (data.MaxStackSize > 0 && amount >= data.MaxStackSize)
                    m_ItemCount.color = m_MaxStackAmountColor;
                else
                    m_ItemCount.color = m_NormalAmountColor;
            }


            if (m_Status)
            {
                m_Status.gameObject.SetActive(data.Flags.HasFlag(ItemFlags.Depletable));
                m_StatusFill.fillAmount = status;
                m_StatusFill.color = m_StatusFillColorOverValue.Evaluate(status);
            }

            if (m_ItemName)
            {
                m_ItemName.text = data.Name;
            }

            if (m_ItemDescription)
            {
                string description = data.Description;
                for (int i = 0; i < m_DescriptionTextReplacers.Length; ++i)
                {
                    description = m_DescriptionTextReplacers[i].Replace(description);
                }
                m_ItemDescription.text = description;
            }

            OnFilled?.Invoke();
        }


        public void Clear()
        {
            if (m_Icon) m_Icon.gameObject.SetActive(false);
            
            if (m_ItemCountObject)
                m_ItemCountObject.SetActive(false);
            else if (m_ItemCount)
                m_ItemCount.gameObject.SetActive(false);

            if (m_Status) m_Status.SetActive(false);

            if (m_ItemName)
            {
                m_ItemName.text = m_NoItemName;
            }

            if (m_ItemDescription)
            {
                m_ItemDescription.text = "";
            }

            OnCleared?.Invoke();
        }

        public void Fill(InventoryEntry entry)
        {
            float amount = 0;
            if (entry != null && entry.Item)
            {
                amount = entry.Item.Flags.HasFlag(ItemFlags.Stackable) ? entry.Count : entry.SecondaryCount;
            }

            Fill(entry.Item, amount, entry.Status);
        }
    }
}