using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorEngine
{

    [Serializable]
    class HierarchyFoldersSettings
    {
        public bool ShowFolders = true;
        public bool ShowFoldersColors = true;
        public string FolderPrefix = "----";
        public string FolderNameTrim = "-";
    }

    // Create a new type of Settings Asset.
    class HorrorEngineSettings : ScriptableObject
    {
        public const string k_HorrorEngineSettingsPath = "Assets/HorrorEngineSettings.asset";
        public const string k_DefaultLevelDesignPalette = "Assets/HorrorEngine/Design/Palettes/DefaultLevelDesignPalette.asset";

        public PrefabPalette LevelDesignPalette;
        public HierarchyFoldersSettings HierarchyFolderSettings;


        internal static HorrorEngineSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HorrorEngineSettings>(k_HorrorEngineSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<HorrorEngineSettings>();
                if (!settings.LevelDesignPalette)
                    settings.LevelDesignPalette = AssetDatabase.LoadAssetAtPath<PrefabPalette>(k_DefaultLevelDesignPalette);

                AssetDatabase.CreateAsset(settings, k_HorrorEngineSettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }


    // Create HorrorEngineSettingsProvider by deriving from SettingsProvider:
    class HorrorEngineSettingsProvider : SettingsProvider
    {
        private SerializedObject m_CustomSettings;

        class Styles
        {
            public static GUIContent designPalette = new GUIContent("Level Design Palette");
            public static GUIContent hierarchyFolders = new GUIContent("Hierarchy Folders");
        }

        
        public HorrorEngineSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope) { }


        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // This function is called when the user clicks on the MyCustom element in the Settings window.
            m_CustomSettings = HorrorEngineSettings.GetSerializedSettings();
        }

        public override void OnGUI(string searchContext)
        {
            // Use IMGUI to display UI:
            EditorGUILayout.PropertyField(m_CustomSettings.FindProperty(nameof(HorrorEngineSettings.LevelDesignPalette)), Styles.designPalette);
            EditorGUILayout.PropertyField(m_CustomSettings.FindProperty(nameof(HorrorEngineSettings.HierarchyFolderSettings)), Styles.hierarchyFolders);
            m_CustomSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateHorrorEngineSettingsProvider()
        {
            HorrorEngineSettings settings = HorrorEngineSettings.GetOrCreateSettings();
            if (settings)
            {
                var provider = new HorrorEngineSettingsProvider("Project/Horror Engine", SettingsScope.Project);

                // Automatically extract all keywords from the Styles.
                provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
                return provider;
            }

            // Settings Asset doesn't exist yet; no need to display anything in the Settings window.
            return null;
        }
    }

}