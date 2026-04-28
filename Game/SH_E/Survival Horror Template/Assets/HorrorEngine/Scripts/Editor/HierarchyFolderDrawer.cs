using UnityEngine;
using UnityEditor;
using HorrorEngine;

[InitializeOnLoad]
public static class HierarchyFolderDrawer
{
    private static Texture2D m_FolderIconOpen;
    private static Texture2D m_FolderIconClosed;
    private static Color m_FolderColor = new Color(.15f, .15f, .15f, 1f);

    static HierarchyFolderDrawer()
    {
        m_FolderIconOpen = EditorGUIUtility.IconContent("FolderOpened Icon").image as Texture2D;
        m_FolderIconClosed = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;

        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null) 
            return;

        var settings = HorrorEngineSettings.GetOrCreateSettings();
        if (!settings.HierarchyFolderSettings.ShowFolders)
            return;

        if (obj.name.StartsWith(settings.HierarchyFolderSettings.FolderPrefix))
        {
            SceneLayerObject layer = obj.GetComponent<SceneLayerObject>();
            string cleanName = obj.name.Replace(settings.HierarchyFolderSettings.FolderNameTrim, "").Trim().ToUpper();

            Rect fullRect = new Rect(0, selectionRect.y, Screen.width, selectionRect.height);
            Color folderColor = (layer && layer.Layer && settings.HierarchyFolderSettings.ShowFoldersColors) ? layer.Layer.SceneFolderColor : m_FolderColor;
            EditorGUI.DrawRect(fullRect, folderColor);

            Rect foldoutRect = new Rect(selectionRect.x - 14, selectionRect.y, 16, 16);

            bool isExpanded = SceneHierarchyUtility.IsExpanded(obj);

            GUI.DrawTexture(foldoutRect, isExpanded ? m_FolderIconOpen : m_FolderIconClosed);

            if (Event.current.type == EventType.MouseDown && foldoutRect.Contains(Event.current.mousePosition))
            {
                SceneHierarchyUtility.SetExpanded(obj, !isExpanded);
                Event.current.Use();
            }

            // Text
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.fontStyle = FontStyle.BoldAndItalic;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            EditorGUI.LabelField(fullRect, cleanName, style);
        }
    }
}

public static class SceneHierarchyUtility
{
    private static System.Type m_SceneHierarchyWindowType;
    private static object m_SceneHierarchyWindowInstance;
    private static System.Reflection.MethodInfo m_GetExpandedMethod;
    private static System.Reflection.MethodInfo m_SetExpandedMethod;

    static SceneHierarchyUtility()
    {
        var assembly = typeof(Editor).Assembly;
        m_SceneHierarchyWindowType = assembly.GetType("UnityEditor.SceneHierarchyWindow");
        var window = EditorWindow.GetWindow(m_SceneHierarchyWindowType);
        m_SceneHierarchyWindowInstance = window;

        m_GetExpandedMethod = m_SceneHierarchyWindowType.GetMethod("GetExpanded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        m_SetExpandedMethod = m_SceneHierarchyWindowType.GetMethod("SetExpandedRecursive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    }

    public static bool IsExpanded(GameObject go)
    {
        if (m_GetExpandedMethod != null && m_SceneHierarchyWindowInstance != null)
            return (bool)m_GetExpandedMethod.Invoke(m_SceneHierarchyWindowInstance, new object[] { go.GetInstanceID() });
        return false;
    }

    public static void SetExpanded(GameObject go, bool expand)
    {
        if (m_SetExpandedMethod != null && m_SceneHierarchyWindowInstance != null)
            m_SetExpandedMethod.Invoke(m_SceneHierarchyWindowInstance, new object[] { go.GetInstanceID(), expand });
    }
}
