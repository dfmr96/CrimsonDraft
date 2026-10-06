using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Database/SpawnableSavable")]
    public class SpawnableSavableDatabase : RegisterDatabase
    {
        public List<SpawnableSavable> Entries;
        private readonly Dictionary<string, SpawnableSavable> m_HashedEntries = new Dictionary<string, SpawnableSavable>();

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            HashRegisters();
        }

        // --------------------------------------------------------------------

        public override void HashRegisters()
        {
            m_HashedEntries.Clear();
            if (Entries != null)
            {
                foreach (SpawnableSavable reg in Entries)
                {
                    if (reg)
                        m_HashedEntries[reg.GetComponent<ObjectUniqueId>().Id] = reg;
                }
            }
        }

        // --------------------------------------------------------------------

        public GameObject GetPrefab(string id)
        {
            if (m_HashedEntries.Count == 0)
                HashRegisters();

            Debug.Assert(m_HashedEntries.ContainsKey(id), $"Spawnable prefab not found in SpawnableSavableDatabase with id : {id}", this);
            return m_HashedEntries.ContainsKey(id) ? m_HashedEntries[id].gameObject : null;
        }

#if UNITY_EDITOR
        // --------------------------------------------------------------------

        public override void UpdateDatabase()
        {
            Entries.Clear();

            SpawnableSavable[] allFoundScripts = Resources.FindObjectsOfTypeAll<SpawnableSavable>();

            foreach (SpawnableSavable foundScript in allFoundScripts)
            {
                Entries.Add(foundScript);
            }
            
            CheckIds();
        }

        // --------------------------------------------------------------------

        public void CheckIds()
        {
            Dictionary<string, SpawnableSavable> objs = new Dictionary<string, SpawnableSavable>();
            foreach (var savable in Entries)
            {
                var uniqueId = savable.GetComponent<ObjectUniqueId>();
                string id = uniqueId.Id;
                Debug.Assert(!string.IsNullOrEmpty(id), $"SpawnableSavableDatabase : Found an object with an emptu Id on ObjectUniqueId {savable.name}", savable);

                if (!objs.ContainsKey(id))
                    objs[id] = savable;
                else
                {
                    Debug.LogError($"SpawnableSavableDatabase : Id is already in use {id} by : {objs[id].name}, duplicated by: {savable.name}");
                }
            }
        }

        // --------------------------------------------------------------------
#endif
    }
}