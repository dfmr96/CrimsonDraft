using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    [CustomEditor(typeof(MapController))]
    public class MapControllerEditor : Editor
    {
        [SerializeField] private Texture2D m_MapIcon;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Separator();
            if (GUILayout.Button(new GUIContent("Open Editor", m_MapIcon), GUILayout.Height(30)))
            {
                MappingEditorWindow.Init(target as MapController);
            }
        }
    }
}