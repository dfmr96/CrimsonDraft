using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    public static class GameSaveUtils
    {
        public class SaveUtilsBehaviour : MonoBehaviour { }

        private static GameSaveData m_LoadedData;
        private static SaveUtilsBehaviour m_Behaviour;

        // --------------------------------------------------------------------

        private static SaveUtilsBehaviour Behaviour
        {
            get
            {
                if (!m_Behaviour)
                {
                    GameObject go = new GameObject("SaveUtils");
                    m_Behaviour = go.AddComponent<SaveUtilsBehaviour>();
                }
                return m_Behaviour;
            }
        }

        // --------------------------------------------------------------------

        public static int GetLastSavedSlot()
        {
            SaveDataManager<GameSaveData> saveMgr = SaveDataManager<GameSaveData>.Instance;
            int[] slots = saveMgr.GetExistingSlots();
            DateTime lastTime = DateTime.MinValue;
            int lastIndex = -1;
            foreach (var slotIndex in slots)
            {
                SaveDataManager<GameSaveData>.SaveData saveData = saveMgr.GetSaveData(slotIndex);
                DateTime time = string.IsNullOrEmpty(saveData.GameData.Date) ? DateTime.MinValue : DateTime.Parse(saveData.GameData.Date);
                if (lastIndex == -1 || time > lastTime)
                {
                    lastIndex = slotIndex;
                    lastTime = time;
                }
            }
            return lastIndex;
        }

        // --------------------------------------------------------------------


        public static void LoadSlot(int slot)
        {
            m_LoadedData = SaveDataManager<GameSaveData>.Instance.LoadSlot(slot);
            SceneManager.LoadScene(m_LoadedData.SceneName);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // --------------------------------------------------------------------

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Behaviour.InvokeActionNextFrame(() => // Give time to scene objects to initialize
            {
                GameManager.Instance.Clear(); // Needs to happen before ResetComponents or some components will use old data
                GameManager.Instance.Player.GetComponent<GameObjectReset>().ResetComponents();
                GameManager.Instance.SetFromSavedData(m_LoadedData);

                var spawnableDB = GameManager.Instance.GetDatabase<SpawnableSavableDatabase>();
                if (spawnableDB)
                {
                    ObjectStateManager.Instance.InstantiateSpawned(scene, spawnableDB);
                }

                // Some object might be deactivated (e.g. spawnables), we need to call this to apply the state to all objects
                ObjectStateManager.Instance.ApplyStates(); 

                GameManager.Instance.StartGame();
            });
        }
    }
}