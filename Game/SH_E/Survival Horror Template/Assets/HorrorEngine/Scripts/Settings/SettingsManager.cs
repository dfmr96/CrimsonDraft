using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace HorrorEngine
{

    public class SettingsSavedMessage : BaseMessage { }

    public enum SettingInitializationOrder
    {
        Subsystem,
        BeforeSplashScreen,
        AfterSceneLoad,
        DelayedAfterSceneLoad,
        None
    }


    // SettingsManager acts as a cache of selected settings
    // Initialization:
    // It initializes itself at different stages of the game loading process, applying settings with the corresponding initialization order
    // Default Settings:
    // The default settings are specificed in the SettingElementContent assets 
    // Changing & Discarding:
    // When the settings are discarded in the setting menu, the manager simply reloads the settings from PlayerPrefs, which are applied immediately after loading
    // Persistency:
    // Only the settings specified in the GameSettingsDefaults.PersistentSettings asset are loaded from PlayerPrefs
    public class SettingsManager
    {
        private HashSet<SettingsElementContent> m_SavableSettings = new HashSet<SettingsElementContent>();
        private Dictionary<SettingsElementContent, string> m_Settings = new Dictionary<SettingsElementContent, string>();

        public GameSettingsDefaults Defaults;

        public static SettingsManager Instance { get; private set; }

        // ---------------------------------------------------------

        public void Set(SettingsElementContent Key, string Value)
        {
            if (!m_SavableSettings.Contains(Key))
                Debug.LogWarning($"DefaultSettings does not contain a reference to {Key.name}, this will lead to settings not loading correctly at game start");

            m_Settings[Key] = Value;
        }

        // ---------------------------------------------------------

        public void Get(SettingsElementContent Key, out string outValue)
        {
            if (!m_Settings.TryGetValue(Key, out outValue))
            {
                outValue = Key.GetDefaultValue();
            }
        }

        // ---------------------------------------------------------

        public void GetInt(SettingsElementContent Key, out int outValue)
        {
            Get(Key, out string outValueStr);
            outValue = Convert.ToInt32(outValueStr);
        }

        // ---------------------------------------------------------

        public void GetBool(SettingsElementContent Key, out bool outValue)
        {
            Get(Key, out string outValueStr);
            outValue = outValueStr == "1" || outValueStr.ToLower() == "true";
        }

        // ---------------------------------------------------------

        public void GetEnum<T>(SettingsElementContent Key, out T outValue) where T : struct
        {
            Get(Key, out string outValueStr);
            outValue = Enum.Parse<T>(outValueStr);
        }

        // ---------------------------------------------------------

        public void GetFloat(SettingsElementContent Key, out float outValue)
        {
            Get(Key, out string outValueStr);
            outValue = (float)Convert.ToDouble(outValueStr);
        }

        // ---------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void LoadSubsystem()
        {
            Instance = new SettingsManager();
            Instance.Defaults = Resources.Load<GameSettingsDefaults>("DefaultSettings");

            // Cache the settings to check if we are operating over valid settings 
            foreach(var setting in Instance.Defaults.PersistentSettings)
            {
                Instance.m_SavableSettings.Add(setting);
            }

            Instance.LoadSettingsFromPlayerPrefs();
            Instance.ApplyOnly(SettingInitializationOrder.Subsystem);
        }

        // ---------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void LoadBeforeSplash()
        {
            Instance.ApplyOnly(SettingInitializationOrder.BeforeSplashScreen);
        }

        // ---------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void LoadAfterSceneLoad()
        {
            Instance.ApplyOnly(SettingInitializationOrder.AfterSceneLoad);
            Instance.DelayedApply();
        }

        // ---------------------------------------------------------

        // #HACK - This delay mode is a dirty hack needed to support AudioMixer parameter change after start
        async void DelayedApply() 
        {
            await Task.Delay(1);
            Instance.ApplyOnly(SettingInitializationOrder.DelayedAfterSceneLoad);
        }

        // ---------------------------------------------------------

        public void ApplyOnly(SettingInitializationOrder initializationOrder)
        {
            foreach(var pair in m_Settings)
            {
                if (pair.Key.Initialization == initializationOrder)
                    pair.Key.Apply();
            }
        }

        // ---------------------------------------------------------

        public void DeleteAll()
        {
            foreach (var setting in m_Settings)
            {
                SettingsElementContent content = setting.Key;
                content.DeletePlayerPrefs();
            }
        }

        // ---------------------------------------------------------

        public void Discard()
        {
            m_Settings.Clear();
            LoadSettingsFromPlayerPrefs();
            foreach (var setting in m_Settings)
            {
                SettingsElementContent content = setting.Key;
                content.Apply();
            }
        }

        // ---------------------------------------------------------

        private void LoadSettingsFromPlayerPrefs()
        {
            foreach (var content in Instance.Defaults.PersistentSettings)
            {
                string val = content.GetPlayerPrefsValue();
                if (string.IsNullOrEmpty(val))
                {
                    val = content.GetDefaultValue();
                    Debug.Log("Setting not found in PlayerPrefs, loading default value for " + content.name + " = " + val);
                }

                m_Settings.Add(content, val);
            }
        }

        // ---------------------------------------------------------

        public void SaveAndApply()
        {
            foreach (var setting in m_Settings)
            {
                SettingsElementContent content = setting.Key;
                content.SaveInPlayerPrefs();
                content.Apply();
            }

            MessageBuffer<SettingsSavedMessage>.Dispatch();
        }
    }

}