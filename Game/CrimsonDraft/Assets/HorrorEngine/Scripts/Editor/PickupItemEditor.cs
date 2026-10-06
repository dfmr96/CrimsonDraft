using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace HorrorEngine
{
    [CustomEditor(typeof(PickupItem))]
    public class PickupItemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var pickup = target as PickupItem;
            
            if (pickup.IsObsolete)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    pickup.RetrievePickupData();

                    Debug.LogWarning($"PickupItem {pickup.name} variable \"Entry\" was automatically updated. Save the prefab to apply changes", pickup.gameObject);

                    EditorUtility.SetDirty(pickup.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                    EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}