using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class SenseChangedEvent : UnityEvent<Sense, Transform> { }

    public class SenseController : MonoBehaviour
    {
        public SenseChangedEvent OnSenseChanged = new SenseChangedEvent();

        [SerializeField] List<Sense> m_Senses;
        [Tooltip("If this is set to true the Senses list will be ignored and senses will be found in the object children")]
        [SerializeField] bool m_FindSensesInChildren = true;

        private UnityAction<Sense, Transform> m_OnSenseChangedCallback;

        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            if (m_FindSensesInChildren)
                GetComponentsInChildren(m_Senses);
            m_OnSenseChangedCallback = OnSenseChangedCallback;
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            foreach(var sense in m_Senses)
            {
                sense.Init(this);
                sense.OnChanged.AddListener(m_OnSenseChangedCallback);
                OnSenseChangedCallback(sense, null);

                if (sense.TickFrequency > 0)
                    StartCoroutine(ScheduleSenseUpdate(sense));
            }
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            StopAllCoroutines();

            foreach (var sense in m_Senses)
            {
                sense.OnChanged.RemoveListener(m_OnSenseChangedCallback);
            }
        }

        // --------------------------------------------------------------------

        protected virtual void OnSenseChangedCallback(Sense sense, Transform detected)
        {
            OnSenseChanged?.Invoke(sense, detected);
        }

        // --------------------------------------------------------------------

        IEnumerator ScheduleSenseUpdate(Sense sense)
        {
            while (true)
            {
                sense.Tick();
                yield return Yielders.Time(sense.TickFrequency);
            }
        }
    }
}