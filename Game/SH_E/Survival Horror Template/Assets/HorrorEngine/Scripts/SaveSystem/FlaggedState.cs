using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [RequireComponent(typeof(SavableObjectState))]
    public class FlaggedState : MonoBehaviour, ISavableObjectStateExtra
    {
        public UnityEvent OnFlag;
        public UnityEvent OnLoadedFlagged;

        protected bool m_Flagged;

        public bool IsFlagged
        {
            get { return m_Flagged; }
            private set { m_Flagged = value; }
        }

        public string GetSavableData()
        {
            return m_Flagged.ToString();
        }

        public void SetFromSavedData(string savedData)
        {
            m_Flagged = Convert.ToBoolean(savedData);
            if (m_Flagged)
                OnLoadedFlagged?.Invoke();
        }

        public void Flag()
        {
            m_Flagged = true;
            OnFlag?.Invoke();
        }
    }

}