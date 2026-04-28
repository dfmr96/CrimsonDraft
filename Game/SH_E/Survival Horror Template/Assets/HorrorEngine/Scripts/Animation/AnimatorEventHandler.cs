using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class AnimatorEventCallback
    {
        public string Event;
        public UnityEvent OnEvent;
        public bool TriggerOnlyOnce;

        private bool m_Triggered; 

        public void CheckAndInvoke(AnimationEvent e)
        {
            bool canBeTriggered = !TriggerOnlyOnce || (TriggerOnlyOnce && !m_Triggered);
            if (canBeTriggered && Event == e.stringParameter)
            {
                OnEvent?.Invoke();
                m_Triggered = true;
            }
        }

        public void Clear()
        {
            m_Triggered = false;
        }
    }

    public class AnimatorEventHandler : MonoBehaviour
    {
        [HideInInspector]
        public UnityEvent<AnimationEvent> OnEvent;


        public void TriggerEvent(AnimationEvent e)
        {
            OnEvent?.Invoke(e);
        }
    }
}