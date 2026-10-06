using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [Serializable]
    public class RuntimeOverridableReference<T>
    {
        [SerializeField] T m_Value;
        [NonSerialized] T m_Override;
        [NonSerialized] bool m_Overriden;

        public T Get() 
        {
            if (!m_Overriden)
                return m_Value;
            else
                return m_Override;
        }
        public void SetOverride(T inOverride) { 
            m_Override = inOverride;
            m_Overriden = m_Override != null;
        }
    }

    public abstract class ItemOverrideData : ScriptableObject
    {
        public abstract void Override(ItemData item);
    }

    [CreateAssetMenu(menuName = "Horror Engine/Items/Item Override")]
    public class ItemOverride : ScriptableObject
    {
        public ItemData Item;
        public bool ApplyOnAwake = true;
        public ItemOverrideData[] Overrides;
    }
}
