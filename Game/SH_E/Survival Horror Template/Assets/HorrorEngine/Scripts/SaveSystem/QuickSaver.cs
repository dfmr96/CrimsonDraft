using UnityEngine;

namespace HorrorEngine
{
    public class QuickSaver : MonoBehaviour
    {
        public LocalizableText Location;

        private bool m_SaveInProgress;

        public void TriggerSave(int slotIndex)
        {
            if (m_SaveInProgress)
            {
                Debug.LogError("Save already in progress, cannot start another save until the current one is finished!");
                return;
            }

            var gameMgr = GameManager.Instance;
            gameMgr.IncreaseSaveCount();
            GameSaveData saveData = gameMgr.GetSavableData();
            saveData.SaveLocation = Location; 

            MessageBuffer<SaveDataEndedMessage>.Subscribe(OnSaveDataEnded);
            gameMgr.StartCoroutine(SaveDataManager<GameSaveData>.Instance.SaveSlot(slotIndex, saveData));
            m_SaveInProgress = true;
        }

        private void OnSaveDataEnded(SaveDataEndedMessage ev)
        {
            m_SaveInProgress = false;
            MessageBuffer<SaveDataEndedMessage>.Unsubscribe(OnSaveDataEnded);
        }
    }
}