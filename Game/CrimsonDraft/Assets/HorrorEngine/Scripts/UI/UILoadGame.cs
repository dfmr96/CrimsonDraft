using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class UILoadGame : MonoBehaviour
    {
        [SerializeField] private AudioClip m_CloseSlotsClip;

        private IUIInput m_Input;
        private UISaveGameList m_SlotList;

        public UnityEvent OnShow;
        public UnityEvent OnHide;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_SlotList = GetComponentInChildren<UISaveGameList>();
            m_SlotList.OnSubmit.AddListener(OnLoadSlotSubmit);

            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnLoadSlotSubmit(GameObject selected)
        {
            int slotIndex = m_SlotList.GetSelectedIndex();

            SaveDataManager<GameSaveData> saveMgr = SaveDataManager<GameSaveData>.Instance;
            bool slotExists = saveMgr.SlotExists(slotIndex);

            if (slotExists)
            {
                gameObject.SetActive(false);
                GameSaveUtils.LoadSlot(slotIndex);
            }
            else
            {
                Hide();
            }
        }

        // --------------------------------------------------------------------

        public void Show(IUIInput input)
        {
            m_Input = input;
            m_Input.Flush(); // Prevents selecting the first slot immediately

            gameObject.SetActive(true);
            m_SlotList.FillSlotsInfo();

            OnShow?.Invoke();
        }

        // --------------------------------------------------------------------

        private void Hide()
        {
            gameObject.SetActive(false);

            if (m_CloseSlotsClip)
                UIManager.Get<UIAudio>().Play(m_CloseSlotsClip);

            OnHide?.Invoke();
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_Input.IsConfirmDown())
            {
                OnLoadSlotSubmit(m_SlotList.GetSelected());
            }
            else if (m_Input.IsCancelDown())
            {
                Hide();
            }
        }

    }
}