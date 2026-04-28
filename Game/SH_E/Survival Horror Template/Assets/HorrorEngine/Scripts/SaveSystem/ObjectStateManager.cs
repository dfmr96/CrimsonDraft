using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    [Serializable]
    public struct SpawnedObjectDataEntry
    {
        public string SceneName;
        public string ObjectUniqueId;
        public string PrefabUniqueId;
    }

    // --------------------------------------------------------------------

    [Serializable]
    public struct ObjectStateSavable
    {
        public List<ObjectStateSaveDataEntry> ObjectStates;
        public List<SpawnedObjectDataEntry> ObjectsSpawned;
    }

    // --------------------------------------------------------------------

    public class ObjectStateManager : Singleton<ObjectStateManager>, ISavable<ObjectStateSavable>
    {
        Dictionary<string, ObjectStateSaveDataEntry> m_HashedStates = new Dictionary<string, ObjectStateSaveDataEntry>();
        Dictionary<string, SpawnedObjectDataEntry> m_HashedSpawned = new Dictionary<string, SpawnedObjectDataEntry>();

        // --------------------------------------------------------------------

        public void CaptureStates()
        {
            SavableObjectState[] objs = GameObject.FindObjectsByType<SavableObjectState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SavableObjectState o in objs)
            {
                if (o.CanBeSaved)
                {
                    SetState(o.GetSavableData());
                }
            }
        }

        // --------------------------------------------------------------------

        public void ApplyStates()
        {
            SavableObjectState[] objs = GameObject.FindObjectsByType<SavableObjectState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SavableObjectState o in objs)
            {
                ObjectUniqueId uid = o.GetComponent<ObjectUniqueId>();
                if (GetState(uid.Id, out ObjectStateSaveDataEntry data))
                {
                    PooledGameObject pooled = o.GetComponent<PooledGameObject>();
                    if (!pooled || !pooled.IsInPool) // Ignore pooled objects with the same id since those are most likely spawnable (and should be spawned before the state is applied)
                        o.SetFromSavedData(data);
                }
            }
        }

        // --------------------------------------------------------------------

        public void ClearStates()
        {
            m_HashedStates.Clear();
        }

        // --------------------------------------------------------------------

        public void SetState(ObjectStateSaveDataEntry data)
        {
            m_HashedStates[data.UniqueId] = data;
        }

        // --------------------------------------------------------------------

        public bool GetState(string id, out ObjectStateSaveDataEntry data)
        {
            if (!m_HashedStates.ContainsKey(id))
            {
                data = new ObjectStateSaveDataEntry();
                return false;
            }
            else
            {
                data = m_HashedStates[id];
                return true;
            }
        }

        // --------------------------------------------------------------------

        public void AddSpawned(SpawnableSavable spawned) 
        {
            ObjectUniqueId uniqueid = spawned.GetComponent<ObjectUniqueId>();
            SpawnedObjectDataEntry sObj = new SpawnedObjectDataEntry()
            {
                SceneName = spawned.gameObject.scene.name,
                ObjectUniqueId = uniqueid.Id,
                PrefabUniqueId = spawned.PrefabId
            };

            if (!m_HashedSpawned.ContainsKey(uniqueid.Id)) 
            {
                m_HashedSpawned.Add(uniqueid.Id, sObj);
            }
            else
            {
                Debug.Assert(m_HashedSpawned[uniqueid.Id].SceneName == spawned.gameObject.scene.name, "A savable spawned object with the same Id already exists in a different scene");
                m_HashedSpawned[uniqueid.Id] = sObj;
            }
        }

        // --------------------------------------------------------------------

        public void InstantiateSpawned(Scene scene, SpawnableSavableDatabase database)
        {
            foreach(var spawned in m_HashedSpawned)
            {
                SpawnedObjectDataEntry spawnedEntry = spawned.Value;
                if (spawnedEntry.SceneName == scene.name)
                {
                    var prefab = database.GetPrefab(spawnedEntry.PrefabUniqueId);
                    
                    PooledGameObject instance = GameObjectPool.Instance.GetFromPool(prefab);
                    instance.GetComponent<ObjectUniqueId>().SetId(spawnedEntry.ObjectUniqueId);

                    SpawnableSavable spawnedSavable = instance.GetComponent<SpawnableSavable>();
                    spawnedSavable.SetPrefab(prefab);

                    instance.transform.SetParent(null);
                    SceneManager.MoveGameObjectToScene(instance.gameObject, scene);

                    //Don't deactivate, ApplyStates will activate accordingly
                }
            }
        }

        // --------------------------------------------------------------------

        public void UpdateSpawnedScene(SpawnableSavable spawned, string sceneName)
        {
            ObjectUniqueId uniqueId = spawned.GetComponent<ObjectUniqueId>();

            string objectUniqueId = uniqueId.Id;
            
            if (m_HashedSpawned.ContainsKey(objectUniqueId))
            {
                SpawnedObjectDataEntry entry = m_HashedSpawned[objectUniqueId];
                entry.SceneName = sceneName;
                m_HashedSpawned[objectUniqueId] = entry;
            }
            else
            {
                Debug.LogWarning($"UpdateSpawnedScene: No spawned object found with ID {objectUniqueId}");
            }
        }
        // --------------------------------------------------------------------

        public bool HasSpawnedObject(string objectUniqueId)
        {
            return m_HashedSpawned.ContainsKey(objectUniqueId);
        }

        // --------------------------------------------------------------------

        public ObjectStateSavable GetSavableData()
        {
            List<ObjectStateSaveDataEntry> objectStates = new List<ObjectStateSaveDataEntry>();
            foreach (KeyValuePair<string, ObjectStateSaveDataEntry> data in m_HashedStates)
            {
                objectStates.Add(data.Value);
            }

            List<SpawnedObjectDataEntry> objectsSpawned = new List<SpawnedObjectDataEntry>();
            foreach (KeyValuePair<string, SpawnedObjectDataEntry> data in m_HashedSpawned)
            {
                objectsSpawned.Add(data.Value);
            }

            return new ObjectStateSavable()
            {
                ObjectStates = objectStates,
                ObjectsSpawned = objectsSpawned
            };
        }

        // --------------------------------------------------------------------

        public void SetFromSavedData(ObjectStateSavable data)
        {
            m_HashedStates.Clear();
            foreach (ObjectStateSaveDataEntry entry in data.ObjectStates)
            {
                m_HashedStates.Add(entry.UniqueId, entry);
            }

            m_HashedSpawned.Clear();
            foreach (SpawnedObjectDataEntry entry in data.ObjectsSpawned)
            {
                m_HashedSpawned.Add(entry.ObjectUniqueId, entry);
            }
        }
    }

}