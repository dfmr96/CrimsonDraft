using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    [CustomEditor(typeof(ShapeCollider))]
    public class ShapeColliderEditor : Editor
    {
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.UpdateIfRequiredOrScript();

            
            if (GUILayout.Button("Update"))
            {
                var shapeCol = target as ShapeCollider;
                shapeCol.UpdateCollider();
            }

            serializedObject.ApplyModifiedProperties();
        }

    }
}
