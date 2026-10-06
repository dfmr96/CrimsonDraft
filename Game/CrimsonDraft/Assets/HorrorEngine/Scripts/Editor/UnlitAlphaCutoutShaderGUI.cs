using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    internal class UnlitAlphaCutoutShaderGUI : ShaderGUI
    {
      
        private static class Styles
        {
            public static GUIContent twoSidedEnabled = EditorGUIUtility.TrTextContent("Two Sided", "Render both front and back faces of the particle geometry.");
        }

     
        MaterialProperty cullMode = null;
       

        MaterialEditor m_MaterialEditor;

        public void FindProperties(MaterialProperty[] props)
        {
            cullMode = FindProperty("_Cull", props);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            base.OnGUI(materialEditor, props);
            
            FindProperties(props); // MaterialProperties can be animated so we do not cache them but fetch them every event to ensure animated values are updated correctly

            m_MaterialEditor = materialEditor;

            Material material = materialEditor.target as Material;

            TwoSidedPopup(material);
        }

        void TwoSidedPopup(Material material)
        {
            EditorGUI.showMixedValue = cullMode.hasMixedValue;
            var enabled = (cullMode.floatValue == (float)UnityEngine.Rendering.CullMode.Off);

            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.Toggle(Styles.twoSidedEnabled, enabled);
            if (EditorGUI.EndChangeCheck())
            {
                m_MaterialEditor.RegisterPropertyChangeUndo("Two Sided Enabled");
                cullMode.floatValue = enabled ? (float)UnityEngine.Rendering.CullMode.Off : (float)UnityEngine.Rendering.CullMode.Back;
            }

            EditorGUI.showMixedValue = false;
        }

    }
}