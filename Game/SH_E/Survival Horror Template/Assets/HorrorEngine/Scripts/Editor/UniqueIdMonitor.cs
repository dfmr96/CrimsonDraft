using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    [InitializeOnLoad]
    public static class HierarchyMonitor
    {
        static HierarchyMonitor()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        // --------------------------------------------------------------------

        static void OnHierarchyChanged()
        {
            CheckUniqueIdDuplication();
        }

        // --------------------------------------------------------------------

        static void OnSceneSaved(Scene scene)
        {
            CheckUniqueIdDuplication();
        }

        // --------------------------------------------------------------------

        static void CheckUniqueIdDuplication()
        {
            Dictionary<string, ObjectUniqueId> hashSet = new Dictionary<string, ObjectUniqueId>();
            var all = GameObject.FindObjectsByType<ObjectUniqueId>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in all)
            {
                ObjectUniqueId uniqueId = obj as ObjectUniqueId;

                if (!hashSet.ContainsKey(uniqueId.Id))
                {
                    hashSet.Add(uniqueId.Id, uniqueId);
                    if (!uniqueId.IsUniqueInstance &&
                        PrefabUtility.GetPrefabAssetType(uniqueId.gameObject) == PrefabAssetType.Regular &&
                        PrefabUtility.GetPrefabAssetType(uniqueId.gameObject) == PrefabAssetType.Variant)
                    {
                        string pathToPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(uniqueId.gameObject);
                        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(uniqueId.gameObject, pathToPrefab);
                        ObjectUniqueId prefabId = prefab.GetComponent<ObjectUniqueId>();
                        Debug.Assert(prefabId, $"Prefab Instance {uniqueId.gameObject} has a ObjectUniqueId but the component is not on the prefab {prefab.name}. This can be unintended or tick IsUniqueInstance", uniqueId.gameObject);
                        if (prefabId && uniqueId.Id == prefabId.Id)
                        {
                            Debug.Log($"The Id of the object {uniqueId.gameObject} was the same as the prefab, assigning a new unique Id", uniqueId);
                            uniqueId.RegenerateId();
                            EditorUtility.SetDirty(uniqueId);
                        }
                    }
                }
                else
                {
                    var otherObject = hashSet[uniqueId.Id];
                    if (!otherObject.IntentionallyDuplicated)
                        Debug.LogError($"The Id on the ObjectUniqueId component {uniqueId.gameObject} is already in use by {otherObject}, you need to resolve this conflict or save/load won't work correctly", uniqueId);
                }
            }
        }
    }
}