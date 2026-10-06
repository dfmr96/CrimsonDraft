using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class AnimatorEventEntry
    {
        public string Identifier;
        public UnityEvent OnEvent;
    }

    public class AnimatorEventPropagator : MonoBehaviour
    {
        [SerializeField]
        List<AnimatorEventEntry> m_Entries;

        void PropagateEvent(string identifier)
        {
            foreach (var entry in m_Entries)
            {
                if (entry.Identifier == identifier)
                {
                    entry.OnEvent?.Invoke();
                }
            }
        }
    }
}