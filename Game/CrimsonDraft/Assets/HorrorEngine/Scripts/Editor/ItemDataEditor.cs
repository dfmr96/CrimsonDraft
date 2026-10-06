using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    [CustomEditor(typeof(ItemData), true)]
    public class ItemDataEditor : Editor
    {
        public Texture NoIconImage;
        private Editor m_ExaminationEditor;

        public override void OnInspectorGUI()
        {
            var item = target as ItemData;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle headStyle = new GUIStyle();
            headStyle.fontSize = 24;
            headStyle.normal.textColor = Color.white;
            GUILayout.Label(item.Name, headStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Box(item.Image ? item.Image.texture : NoIconImage, GUILayout.Width(128), GUILayout.Height(128));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            DrawDefaultInspector();

        }

        public override bool HasPreviewGUI()
        {
            var item = target as ItemData;
            return item.ExamineModel != null;
        }

        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            base.OnInteractivePreviewGUI(r, background);

            var item = target as ItemData;
            if (!m_ExaminationEditor)
                m_ExaminationEditor = Editor.CreateEditor(item.ExamineModel);
            
            m_ExaminationEditor.OnInteractivePreviewGUI(r, background);
        }
    }
}