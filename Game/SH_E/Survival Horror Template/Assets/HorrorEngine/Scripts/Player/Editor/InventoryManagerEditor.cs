using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    public class InventoryManagerEditor : EditorWindow
    {
        public Texture2D WindowIcon;

        [SerializeField] InventoryEntry m_Entry = new InventoryEntry();

        private SerializedObject m_SerializedObj;
        private SerializedProperty m_EntryProp;
        private DocumentData m_Document;

        private int m_ToolbarSelected = 0;
        string[] m_ToolbarStrings = { "Items", "Documents" };
        private Texture m_DeleteIcon;

        [MenuItem("Horror Engine/Inventory Manager")]
        public static void ShowWindow()
        {
            var browser = GetWindow<InventoryManagerEditor>();
            browser.titleContent = new GUIContent("Inventory Manager", browser.WindowIcon);
        }

        private void OnEnable()
        {
            m_Entry.Count = 1;
            m_Entry.Status = 1;

            m_SerializedObj = new SerializedObject(this); // 'this' works if the data is a field in the window
            m_EntryProp = m_SerializedObj.FindProperty("m_Entry");

            m_DeleteIcon = (Texture)AssetDatabase.LoadAssetAtPath("Assets\\HorrorEngine\\Scripts\\Editor\\Resources\\DeleteIcon.png", typeof(Texture));
            m_ToolbarSelected = 0;
        }

        private void OnGUI()
        {
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Inventory Manager will only work in play mode", MessageType.Info);
                return;
            }

            if (!GameManager.Exists)
            {
                EditorGUILayout.HelpBox("No GameManager exist in the scene", MessageType.Error);
                return;
            }

            m_ToolbarSelected = GUILayout.Toolbar(m_ToolbarSelected, m_ToolbarStrings);
            var inventory = GameManager.Instance.Inventory;
            if (inventory == null)
                return;

            if (m_ToolbarSelected == 0)
            {
                DrawItems(inventory);
            }
            else
            {
                DrawDocuments(inventory);
            }
        }

        void DrawItems(Inventory inventory)
        {
            foreach (var item in inventory.Items)
            {
                if (!item.Item)
                    continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(item.Count.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(item.Item.name);
                if (GUILayout.Button(m_DeleteIcon, GUILayout.Width(20), GUILayout.Height(20)))
                {
                    inventory.Remove(item);
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(20);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(m_EntryProp, new GUIContent("Add Item"), true);
            m_SerializedObj.ApplyModifiedProperties();
            EditorGUILayout.EndHorizontal();
            if (m_Entry.Item != null && m_Entry.Count > 0)
            {
                if (GUILayout.Button("Add Item"))
                {
                    inventory.Add(m_Entry);
                }
            }
        }

        void DrawDocuments(Inventory inventory)
        {
            DocumentData deleteDoc = null;
            foreach (var item in inventory.Documents)
            {
                if (!item)
                    continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(item.Name);
                if (GUILayout.Button(m_DeleteIcon, GUILayout.Width(20), GUILayout.Height(20)))
                {
                    deleteDoc = item;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (deleteDoc != null)
                inventory.Documents.Remove(deleteDoc);

            m_Document = EditorGUILayout.ObjectField(new GUIContent("Add Document"), m_Document, typeof(DocumentData)) as DocumentData;
            if (m_Document != null)
            {
                if (GUILayout.Button("Add Document"))
                {
                    inventory.AddDocument(m_Document);
                }
            }
        }
    }
}