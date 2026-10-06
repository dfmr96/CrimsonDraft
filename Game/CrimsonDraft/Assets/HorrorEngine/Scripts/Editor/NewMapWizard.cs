using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    public class NewMapWizard : EditorWindow
    {
        [SerializeField] private MapController m_MapControllerPrefab;

        private string m_FolderPath = "Assets/";
        private string m_FileName = "NewMap";
        private bool m_CreateMapInLevel = true;

        [MenuItem("Horror Engine/Wizards/New Map")]
        public static void ShowWindow()
        {
            GetWindow<NewMapWizard>("New Map");
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Data Output", m_FolderPath);
            if (GUILayout.Button("Search", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("Select folder", m_FolderPath, "");
                if (!string.IsNullOrEmpty(path))
                    m_FolderPath = path.Substring(path.IndexOf("Assets/")) + "/";
            }
            GUILayout.EndHorizontal();
            m_FileName = EditorGUILayout.TextField("File Name:", m_FileName);
            m_CreateMapInLevel = EditorGUILayout.Toggle("Create map in current level", m_CreateMapInLevel);

            EditorGUILayout.Space(20);

            if (GUILayout.Button("Create Map"))
            {
                MapData mapData = ScriptableObject.CreateInstance<MapData>();
                mapData.GenerateId();
                string filePath = AssetDatabase.GenerateUniqueAssetPath(m_FolderPath + "/" + m_FileName + ".asset");
                AssetDatabase.CreateAsset(mapData, filePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Map Data asset created at {filePath}");

                if (m_CreateMapInLevel)
                {
                    var mapGO = PrefabUtility.InstantiatePrefab(m_MapControllerPrefab.gameObject) as GameObject;
                    MapController map = mapGO.GetComponent<MapController>();
                    map.name = map.name + "_" + m_FileName;

                    map.Data = mapData;
                    EditorUtility.SetDirty(map);

                    MappingEditorWindow.Init(map);
                }

                Close();
            }
        }
    }
}
