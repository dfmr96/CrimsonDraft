using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PuzzleBase : MonoBehaviour, ISavableObjectStateExtra
    {
        public UnityEvent OnSolved;
        public UnityEvent OnLoadedSolved;

        protected bool m_Solved;

        // --------------------------------------------------------------------

        public bool IsSolved { 
            get { return m_Solved; }
            private set { m_Solved = value; }
        }

        // --------------------------------------------------------------------

        public virtual void Solve()
        {
            m_Solved = true;
            OnSolved?.Invoke();
        }

        //------------------------------------------------------
        // ISavable implementation
        //------------------------------------------------------

        public virtual string GetSavableData()
        {
            return m_Solved.ToString();
        }

        // --------------------------------------------------------------------

        public virtual void SetFromSavedData(string savedData)
        {
            m_Solved = Convert.ToBoolean(savedData);
            if (m_Solved)
                OnLoadedSolved?.Invoke();
        }

    }

}