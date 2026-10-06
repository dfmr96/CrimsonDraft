using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIInventoryContextMenu : MonoBehaviour
    {
        public Button UseButton;
        public Button EquipButton;
        public Button ExamineButton;
        public Button CombineButton;
        public Button DropButton;
        public Button ExchangeButton;

        public UnityEvent OnClose;

        private IUIInput m_Input;
        private Button m_DefaultButton;

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();

            UseButton?.onClick.AddListener(OnOptionSelected);
            EquipButton?.onClick.AddListener(OnOptionSelected);
            ExamineButton?.onClick.AddListener(OnOptionSelected);
            CombineButton?.onClick.AddListener(OnOptionSelected);
            DropButton?.onClick.AddListener(OnOptionSelected);
            ExchangeButton?.onClick.AddListener(OnOptionSelected);
        }

        // --------------------------------------------------------------------

        private void OnOptionSelected()
        {
            m_Input.Flush(); // Flush so an object is not immediately selected after this
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        public bool Show(ItemData item)
        {
            Fill(item);
            if (!m_DefaultButton)
            {
                return false;
            }

            SelectDefault();
            gameObject.SetActive(true);

            return true;
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            OnClose?.Invoke();
        }

        // --------------------------------------------------------------------

        private void Fill(ItemData item)
        {
            m_DefaultButton = null;

            UseButton?.gameObject.SetActive(false);
            EquipButton?.gameObject.SetActive(false);
            ExamineButton?.gameObject.SetActive(false);
            CombineButton?.gameObject.SetActive(false);
            DropButton?.gameObject.SetActive(false);

            if (UseButton && item.InventoryAction.HasFlag(InventoryMainAction.Use))
            {
                UseButton.gameObject.SetActive(true);
                m_DefaultButton = UseButton;
            }
            
            if(EquipButton && item.InventoryAction.HasFlag(InventoryMainAction.Equip))
            {
                var equipable = item as EquipableItemData;
                if (equipable.Slot != EquipmentSlot.None)
                {
                    EquipButton.gameObject.SetActive(true);
                    if (!m_DefaultButton) m_DefaultButton = EquipButton;
                }
            }

            if (CombineButton && item.Flags.HasFlag(ItemFlags.Combinable))
            {
                CombineButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = CombineButton;
            }

            if (ExamineButton && item.Flags.HasFlag(ItemFlags.Examinable))
            {
                ExamineButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = ExamineButton;
            }

            if (DropButton && item.Flags.HasFlag(ItemFlags.Droppable))
            {
                DropButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = DropButton;
            }

            if (ExchangeButton)
            {
                ExchangeButton.gameObject.SetActive(true);
                if (!m_DefaultButton) m_DefaultButton = ExchangeButton;
            }

            FixNavigation();
        }

        // --------------------------------------------------------------------

        private void FixNavigation() 
        {
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.Explicit;

            Button[] buttons = GetComponentsInChildren<Button>();
            int lastIndex = buttons.Length - 1;
            for (int i = 0; i < buttons.Length; ++i)
            {
                navigation.selectOnUp = i == 0 ? buttons[lastIndex] : buttons[i - 1];
                navigation.selectOnDown = i == lastIndex ? buttons[0] : buttons[i + 1];
                buttons[i].navigation = navigation;
            }
        }

        // --------------------------------------------------------------------

        private void SelectDefault()
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(m_DefaultButton.gameObject);
        }
    }
}