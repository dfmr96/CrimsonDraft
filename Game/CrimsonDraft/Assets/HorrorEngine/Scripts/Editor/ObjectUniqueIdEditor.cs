using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    [CustomEditor(typeof(ObjectUniqueId))]
    public class ObjectUniqueIdEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            base.OnInspectorGUI();

            // Get the target ObjectUniqueId component
            ObjectUniqueId objectUniqueId = (ObjectUniqueId)target;

            // Add a button to regenerate the ID
            if (GUILayout.Button("Regenerate Id"))
            {
                objectUniqueId.RegenerateId();
                EditorUtility.SetDirty(objectUniqueId);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}