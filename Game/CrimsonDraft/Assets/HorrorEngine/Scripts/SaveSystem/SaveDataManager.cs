//Uncomment the line below to enable saving into PlayerPrefs
//#define SAVEPLAYERPREFS

using System.IO;
using System.Collections;
using UnityEngine;
using System.Text;
using System;

namespace HorrorEngine
{
    public class SaveDataStartedMessage : BaseMessage
    {
    }
    public class SaveDataEndedMessage : BaseMessage
    {
    }

    public class SaveDataManager<TGameData> : Singleton<SaveDataManager<TGameData>>
    {
        public static readonly string k_EncryptionKey = "!kj#j%MYeHt1I";
        private static readonly int k_MaxSlots = 25;
        private static readonly string k_SaveSlotName = "saveGameSlot";
        private static readonly string k_SaveFileExt = ".dat";

        private int m_CurrentSlot = -1;
        private SaveData m_SaveData;

        public struct SaveData
        {
            public TGameData GameData;
            public ObjectStateSavable ObjectStates;
            public GameAttributesSavable GameValues;
        }

        // --------------------------------------------------------------------

        public IEnumerator Save(TGameData data)
        {
            yield return SaveSlot(m_CurrentSlot, data);
        }

        // --------------------------------------------------------------------

        public IEnumerator SaveSlot(int slot, TGameData data)
        {
            Debug.Assert(slot < k_MaxSlots, "Slot index is higher than MaxSlots");

            MessageBuffer<SaveDataStartedMessage>.Dispatch();

            yield return null;

            m_SaveData.GameData = data;
            ObjectStateManager.Instance.CaptureStates();
            m_SaveData.ObjectStates = ObjectStateManager.Instance.GetSavableData();

            yield return null;
            
            string saveDataJson = JsonUtility.ToJson(m_SaveData, true);

#if SAVEPLAYERPREFS
            PlayerPrefs.SetString(k_SaveSlotName + slot, saveDataJson);
            PlayerPrefs.Save();
#else
            byte[] saveDataByteArray = Encoding.ASCII.GetBytes(saveDataJson);
            byte[] encryptedByteArray = DataEncryptor.Encrypt(saveDataByteArray, k_EncryptionKey);
            File.WriteAllBytes(GetSlotPath(slot), encryptedByteArray);
#endif


#if UNITY_EDITOR
            yield return null;

            File.WriteAllText(GetSlotPath(slot, ".txt"), saveDataJson);

            Debug.Log("Saved Game : " + GetSlotPath(slot));
            Debug.Log("---------------");
            Debug.Log(saveDataJson);
            Debug.Log("---------------");
#endif

            yield return null;

            MessageBuffer<SaveDataEndedMessage>.Dispatch();
        }

        public int[] GetExistingSlots()
        {
            int[] slots = null;

#if SAVEPLAYERPREFS
            List<int> slotList = new List<int>();
            for (int i = 0; i < k_MaxSlots; ++i)
            {
                if (SlotExists(i))
                {
                    slotList.Add(i);
                }
            }
            slots = slotList.ToArray();
#else
            DirectoryInfo dir = new DirectoryInfo(Application.persistentDataPath);
            FileInfo[] info = dir.GetFiles("*"+ k_SaveFileExt);
            slots = new int[info.Length];
            int index = 0;
            foreach(var i in info)
            {
                string slotNumber = i.Name.Split('-')[1].Split('.')[0];
                slots[index] = Convert.ToInt32(slotNumber);
                ++index;
            }
#endif
            return slots;
        }

        // --------------------------------------------------------------------

        public TGameData LoadSlot(int slot)
        {
            Debug.Assert(slot < k_MaxSlots, "Slot index is higher than MaxSlots");

            m_CurrentSlot = slot;
            m_SaveData = GetSaveData(slot);

            // ApplyStates should be done in GameSaveUtils::OnSceneLoad
            ObjectStateManager.Instance.SetFromSavedData(m_SaveData.ObjectStates);

            return m_SaveData.GameData;
        }

        // --------------------------------------------------------------------

        public SaveData GetSaveData(int slot)
        {
            try
            {
#if SAVEPLAYERPREFS
            string saveDataJson = PlayerPrefs.GetString(k_SaveSlotName + slot);
#else
            byte[] data = File.ReadAllBytes(GetSlotPath(slot));
            byte[] decryptedData = DataEncryptor.Decrypt(data, k_EncryptionKey);
            string saveDataJson = Encoding.ASCII.GetString(decryptedData);
#endif

#if UNITY_EDITOR
                Debug.Log("Loaded Game : " + GetSlotPath(slot));
                Debug.Log("---------------");
                Debug.Log(saveDataJson);
                Debug.Log("---------------");
#endif
                return JsonUtility.FromJson<SaveData>(saveDataJson);
            }
            catch(Exception e)
            {
                Debug.LogWarning($"Saved slot wasn't properly encrypted, trying to load as unencrypted data : {e.Message}");

                byte[] data = File.ReadAllBytes(GetSlotPath(slot));
                string saveDataJson = Encoding.ASCII.GetString(data);

                return JsonUtility.FromJson<SaveData>(saveDataJson);
            }
        }

        // --------------------------------------------------------------------

        public string GetSlotPath(int slot, string extension = "")
        {
#if SAVEPLAYERPREFS
            extension = ".txt";
#else
            if (string.IsNullOrEmpty(extension))
                extension = k_SaveFileExt;
#endif
            return string.Format("{0}/{1}-{2}{3}", Application.persistentDataPath, k_SaveSlotName, slot, extension);
        }

        // --------------------------------------------------------------------

        public bool SlotExists(int slot)
        {
#if SAVEPLAYERPREFS
            return PlayerPrefs.HasKey(k_SaveSlotName + slot);
#else
            return File.Exists(GetSlotPath(slot));
#endif
        }

        // --------------------------------------------------------------------

        public void ClearSlot(int slot)
        {
#if SAVEPLAYERPREFS
            PlayerPrefs.DeleteKey(k_SaveSlotName + slot);
#else
            string path = GetSlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
#endif

#if UNITY_EDITOR
            string pathTXT = GetSlotPath(slot, ".txt");
            if (File.Exists(pathTXT))
            {
                File.Delete(pathTXT);
            }
#endif
        }


        // --------------------------------------------------------------------

        public bool HasSaveData()
        {
            int[] slots = GetExistingSlots();
            return slots != null && slots.Length > 0;
        }

    }
}