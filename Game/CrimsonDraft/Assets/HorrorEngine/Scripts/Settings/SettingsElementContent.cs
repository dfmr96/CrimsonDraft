using System;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    
    [Serializable]
    public abstract class SettingsElementContent : ScriptableObject
    {
        private static readonly string k_PlayerPrefSettingsPrefix = "Settings.";
        public LocalizableText Name;
        public string SettingsKey;
        [FormerlySerializedAs("Prefab")]
        public GameObject UIPrefab;
        public SettingInitializationOrder Initialization = SettingInitializationOrder.Subsystem;
        public bool ApplyImmediate;

        /* This apply the change to any concerning system */
        public abstract void Apply();

        public void SaveInPlayerPrefs()
        {
            SettingsManager.Instance.Get(this, out string savedVal);
            PlayerPrefs.SetString(k_PlayerPrefSettingsPrefix + SettingsKey, savedVal);
        }

        public string GetPlayerPrefsValue()
        {
            return PlayerPrefs.GetString(k_PlayerPrefSettingsPrefix + SettingsKey);
        }

        public abstract string GetDefaultValue();

        public T GetAsEnum<T>() where T : struct
        {
            SettingsManager.Instance.GetEnum<T>(this, out T outVal);
            return outVal;
        }

        public float GetAsFloat()
        {
            SettingsManager.Instance.GetFloat(this, out float outVal);
            return outVal;
        }

        public int GetAsInt()
        {
            SettingsManager.Instance.GetInt(this, out int outVal);
            return outVal;
        }
        public bool GetAsBool()
        {
            SettingsManager.Instance.GetBool(this, out bool outVal);
            return outVal;
        }

    }

}