using UnityEngine;
using System;

namespace HorrorEngine
{
    public interface ISavableObjectStateExtra : ISavable<string> { }


    [Serializable]
    public struct ObjectStateSaveDataEntry
    {
        public string Name;
        public string UniqueId;
        public string PrefabUniqueId;
        public Vector3 LocalPosition;
        public Vector3 LocalScale;
        public Vector3 LocalRotation;
        public bool Active;
        public string[] SerializedComponentsTypes;
        public string[] SerializedComponents;
        
        public string GetComponentData<T>() where T : MonoBehaviour
        {
            string[] serializedComponentsTypes = SerializedComponentsTypes;
            string[] serializedComponents = SerializedComponents;
            for (int i = 0; i < serializedComponentsTypes.Length; ++i)
            {
                string typeName = typeof(T).Name;
                if (serializedComponentsTypes[i] == typeName)
                {
                    return serializedComponents[i];
                }
            }

            return "";
        }
    }

    [RequireComponent(typeof(ObjectUniqueId))]
    public class SavableObjectState : MonoBehaviour, ISavable<ObjectStateSaveDataEntry>
    {
        [SerializeField] private bool m_ApplySavedTransform = true;
        private ObjectUniqueId m_ObjectId;
        private bool m_HasStarted;

        public bool CanBeSaved => enabled && m_HasStarted;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_ObjectId = GetComponent<ObjectUniqueId>();
        }

        // --------------------------------------------------------------------

        public void Start()
        {
            ApplySavedState();
            m_HasStarted = true;
        }

        // --------------------------------------------------------------------

        [ContextMenu("Apply Saved State")]
        private void ApplySavedState()
        {
            if (!m_ObjectId)
                m_ObjectId = GetComponent<ObjectUniqueId>();

            if (ObjectStateManager.Instance.GetState(m_ObjectId.Id, out ObjectStateSaveDataEntry data))
                SetFromSavedData(data);
        }

        // --------------------------------------------------------------------

        [ContextMenu("Save State")]
        public void SaveState()
        {
            ObjectStateManager.Instance.SetState(GetSavableData());
        }

        // --------------------------------------------------------------------
        // ISavable Implementation
        // --------------------------------------------------------------------

        public ObjectStateSaveDataEntry GetSavableData()
        {
            if (!m_ObjectId)
                m_ObjectId = GetComponent<ObjectUniqueId>();

            
            ObjectStateSaveDataEntry entry = new ObjectStateSaveDataEntry()
            {
                Name = m_ObjectId.name,
                UniqueId = m_ObjectId.Id,
                LocalPosition = transform.localPosition,
                LocalScale = transform.localScale,
                LocalRotation = transform.localRotation.eulerAngles,
                Active = gameObject.activeSelf
            };


            ISavableObjectStateExtra[] savableComponents = GetComponents<ISavableObjectStateExtra>();
            entry.SerializedComponents = new string[savableComponents.Length];
            entry.SerializedComponentsTypes = new string[savableComponents.Length];

            int index = 0;
            foreach (ISavableObjectStateExtra savable in savableComponents)
            {
                entry.SerializedComponentsTypes[index] = savable.GetType().Name;
                entry.SerializedComponents[index] = savable.GetSavableData();
                ++index;
            }

            return entry;
        }

        // --------------------------------------------------------------------

        public void SetFromSavedData(ObjectStateSaveDataEntry savedData)
        {
            if (!m_ObjectId)
                m_ObjectId = GetComponent<ObjectUniqueId>();

            if (m_ApplySavedTransform)
            {
                Debug.Assert(savedData.UniqueId == m_ObjectId.Id, "Object id doesn't match");
                transform.localPosition = savedData.LocalPosition;
                transform.localScale = savedData.LocalScale;
                transform.localRotation = Quaternion.Euler(savedData.LocalRotation);
            }

            gameObject.SetActive(savedData.Active);

            string[] serializedComponentsTypes = savedData.SerializedComponentsTypes;
            string[] serializedComponents = savedData.SerializedComponents;
            for (int i = 0; i < serializedComponentsTypes.Length; ++i)
            {
                ISavable<string> c = GetComponent(serializedComponentsTypes[i]) as ISavable<string>;
                if (c != null)
                {
                    c.SetFromSavedData(serializedComponents[i]);
                }
            }
        }
    }


}