using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    [System.Serializable]
    public class RuntimeDebugData
    {
        public List<string> EnabledCategories = new List<string>();
    }

    public class RuntimeDebugEditor : EditorWindow
    {
        private static readonly string PREFS_KEY_ENABLED_CATEGORIES = "RuntimeDebugEditor_EnabledCategories";
        private string m_SearchString = "";
        private Vector2 m_ScrollPosition;

    
        [MenuItem("Horror Engine/Debug/Runtime Debug")]
        public static void ShowWindow()
        {
            GetWindow<RuntimeDebugEditor>("Runtime Debug");
        }

        private void OnEnable()
        {
            LoadFromPrefs();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

            // --- RUNTIME CONTROLS ---
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Enable All", EditorStyles.toolbarButton)) SetAll(true);
            if (GUILayout.Button("Disable All", EditorStyles.toolbarButton)) SetAll(false);
            EditorGUILayout.EndHorizontal();

            m_SearchString = EditorGUILayout.TextField(m_SearchString, EditorStyles.toolbarSearchField);

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            var categories = RuntimeDebug.CategoryRenderingEnabled;
            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("No categories registered yet. They will get registered at runtime", MessageType.Info);
            }
            else
            {
                var orderedCategories = categories.Keys.OrderBy(s => s).ToList();
                foreach (var category in orderedCategories)
                {
                    if (!string.IsNullOrEmpty(m_SearchString) && !category.ToLower().Contains(m_SearchString.ToLower()))
                        continue;

                    EditorGUILayout.BeginHorizontal();
                    bool currentState = RuntimeDebug.CategoryRenderingEnabled[category];
                    bool newState = EditorGUILayout.ToggleLeft(category, currentState, GUILayout.ExpandWidth(true));

                    if (newState != currentState)
                    {
                        RuntimeDebug.CategoryRenderingEnabled[category] = newState;
                        SaveToPrefs();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (Application.isPlaying) 
                Repaint();
        }

        private void SetAll(bool state)
        {
            var keys = new List<string>(RuntimeDebug.CategoryRenderingEnabled.Keys);
            foreach (var key in keys)
            {
                RuntimeDebug.CategoryRenderingEnabled[key] = state;
            }
        }

        private void SaveToPrefs()
        {
            RuntimeDebugData data = new RuntimeDebugData();

            // Grab every category that is currently toggled "ON"
            foreach (var kvp in RuntimeDebug.CategoryRenderingEnabled)
            {
                if (kvp.Value) data.EnabledCategories.Add(kvp.Key);
            }

            string json = JsonUtility.ToJson(data);
            EditorPrefs.SetString(PREFS_KEY_ENABLED_CATEGORIES, json);
        }

        private void LoadFromPrefs()
        {
            string json = EditorPrefs.GetString(PREFS_KEY_ENABLED_CATEGORIES, "");
            if (string.IsNullOrEmpty(json)) 
                return;


            var data = JsonUtility.FromJson<RuntimeDebugData>(json);

            foreach (string cat in data.EnabledCategories)
            {
                RuntimeDebug.Register(cat, true);
            }
        }
    }
}