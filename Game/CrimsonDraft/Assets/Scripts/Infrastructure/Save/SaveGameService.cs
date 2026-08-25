#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    public sealed class SaveGameService : ISaveGameService
    {
        public const int SlotCount = 20;
        private const string SaveFolderName = "Saves";

        private SaveGameData? pendingLoad;

        [Preserve]
        public SaveGameService() { }

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);

        private static string SlotPath(int slot) => Path.Combine(SaveDirectory, $"slot_{slot:D2}.json");

        public IReadOnlyList<SaveSlotSummary> ListSlotSummaries()
        {
            var summaries = new List<SaveSlotSummary>(SlotCount);
            for (int i = 0; i < SlotCount; i++)
            {
                var data = ReadFromDisk(i);
                summaries.Add(data == null
                    ? new SaveSlotSummary { slot = i, isEmpty = true }
                    : new SaveSlotSummary
                    {
                        slot            = i,
                        isEmpty         = false,
                        roomId          = data.roomId,
                        timestampIso    = data.timestampIso,
                        playtimeSeconds = data.playtimeSeconds,
                        saveCount       = data.saveCount,
                    });
            }
            return summaries;
        }

        public void WriteToDisk(int slot, SaveGameData data)
        {
            Directory.CreateDirectory(SaveDirectory);
            string json     = JsonUtility.ToJson(data, prettyPrint: true);
            string path     = SlotPath(slot);
            string tempPath = path + ".tmp";

            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, path, overwrite: true);
            File.Delete(tempPath);
        }

        public SaveGameData? ReadFromDisk(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<SaveGameData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameService] Failed to read slot {slot}: {e}");
                return null;
            }
        }

        public bool DeleteSlot(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return false;

            File.Delete(path);
            return true;
        }

        public bool LoadSlot(int slot)
        {
            var data = ReadFromDisk(slot);
            if (data == null) return false;

            this.pendingLoad = data;
            SceneManager.LoadScene(data.sceneName, LoadSceneMode.Single);
            return true;
        }

        public SaveGameData? ConsumePendingLoad()
        {
            var data = this.pendingLoad;
            this.pendingLoad = null;
            return data;
        }
    }
}
