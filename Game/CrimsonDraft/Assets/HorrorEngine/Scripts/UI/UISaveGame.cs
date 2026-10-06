using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HorrorEngine
{
    public class UISaveGame : MonoBehaviour
    {
        [SerializeField] private UISaveGameList m_SlotList;
        [SerializeField] private GameObject m_SaveInProgressScreen;
        
        [Header("Audio")]
        [SerializeField] private AudioClip m_ShowClip;
        [SerializeField] private AudioClip m_SelectClip;
        [SerializeField] private AudioClip m_CloseClip;

        private IUIInput m_Input;
        private ItemData m_ItemToConsume;
        private string m_Location;
        private bool m_SaveInProgress;

        private EventSystem m_DisabledEventSystem;

        private void Awake()
        {
            m_Input = GetComponentInParent<IUIInput>();

            m_SlotList.OnSubmit.AddListener(OnSubmit);

            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            if (m_DisabledEventSystem)
            {
                m_DisabledEventSystem.enabled = true;
                m_DisabledEventSystem = null;
            }
        }

        // --------------------------------------------------------------------

        void Update()
        {
            if (m_SaveInProgress)
                return;

            if (m_Input.IsCancelDown())
            {
                Close();
            }
            if (m_Input.IsConfirmDown())
            {
                OnSubmit();
            }
        }

        // --------------------------------------------------------------------

        public void Show(string location, ItemData itemToConsume)
        {
            PauseController.Instance.Pause(this);
            CursorController.Instance.SetInUI(true);

            m_ItemToConsume = itemToConsume;
            m_Location = location;
            
            if (m_ShowClip)
                UIManager.Get<UIAudio>().Play(m_ShowClip);

            gameObject.SetActive(true);
            m_SaveInProgress = false;
            m_SaveInProgressScreen.SetActive(false);
            
            m_SlotList.FillSlotsInfo(false);
        }

        // --------------------------------------------------------------------


        private void Close()
        {
            PauseController.Instance.Resume(this);
            CursorController.Instance.SetInUI(false);

            if (m_CloseClip)
                UIManager.Get<UIAudio>().Play(m_CloseClip);

            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------------

        void OnSubmit(GameObject selected = null)
        {
            if (!selected)
            {
                selected = m_SlotList.GetSelected();
            }

            if (m_SaveInProgress || selected == null)
                return;

            int slotIndex = m_SlotList.GetSelectedIndex();

            if (m_ItemToConsume != null)
                GameManager.Instance.Inventory.Remove(m_ItemToConsume);
            
            if (m_SelectClip)
                UIManager.Get<UIAudio>().Play(m_SelectClip);

            GameManager.Instance.IncreaseSaveCount();
            GameSaveData saveData = GameManager.Instance.GetSavableData();
            saveData.SaveLocation = m_Location;

            MessageBuffer<SaveDataEndedMessage>.Subscribe(OnSaveDataEnded);
            StartCoroutine(SaveDataManager<GameSaveData>.Instance.SaveSlot(slotIndex, saveData));
            m_SaveInProgress = true;
            m_SaveInProgressScreen.SetActive(true);

            m_DisabledEventSystem = EventSystem.current;
            m_DisabledEventSystem.enabled = false;
        }

        // --------------------------------------------------------------------

        void OnSaveDataEnded(SaveDataEndedMessage msg) 
        {
            MessageBuffer<SaveDataEndedMessage>.Unsubscribe(OnSaveDataEnded);
            StartCoroutine(StopSaveInProgress());
        }

        // --------------------------------------------------------------------

        IEnumerator StopSaveInProgress()
        {
            yield return Yielders.UnscaledTime(1f);  // Adds some extra time so the Save message doesn't dissappear instantly

            m_SaveInProgressScreen.SetActive(false);
            m_SlotList.FillSlotsInfo(true);

            AppearingText saveText = m_SlotList.GetSelected().GetComponent<AppearingText>();
            if (saveText)
            {
                while (!saveText.HasShownAll)
                {
                    yield return Yielders.EndOfFrame;
                }
            }

            yield return Yielders.UnscaledTime(1f);

            m_SaveInProgress = false;

            if (m_DisabledEventSystem)
            {
                m_DisabledEventSystem.enabled = true;
                m_DisabledEventSystem = null;
            }

            Close();
        }
    }

}