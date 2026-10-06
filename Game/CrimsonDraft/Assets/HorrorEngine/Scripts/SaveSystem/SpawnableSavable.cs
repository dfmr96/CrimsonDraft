using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(SavableObjectState))]
    public class SpawnableSavable : MonoBehaviour
    {
        public string PrefabId => m_PrefabId;

        private string m_PrefabId;

        public void AddToManager()
        {
            ObjectStateManager.Instance.AddSpawned(this);
        }

        public void SetPrefab(GameObject prefab)
        {
            var prefabUniqueId = prefab.GetComponent<ObjectUniqueId>();
            Debug.Assert(prefabUniqueId, "SpawnableSavable: Prefab needs to have a ObjectUniqueId to be properly saved");
            Debug.Assert(!string.IsNullOrEmpty(prefabUniqueId.Id), $"SpawnableSavable : Prefab has an empty UniqueId : {prefab.name}", prefab);

            m_PrefabId = prefabUniqueId.Id;
        }
    }
}