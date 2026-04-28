using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace HorrorEngine
{
    [DisallowMultipleComponent]
    public class GameObjectReset : MonoBehaviour
    {
        public bool ResetOnEnable = true;
        public UnityEvent OnReset;

        private List<IResetable> mResetables = new List<IResetable>();
        private bool mHasEnabled;

        // --------------------------------------------------------------------

        private void Awake()
        {
            this.GetComponentsInChildrenStopAt<IResetable, GameObjectReset>(mResetables);
        }

        // --------------------------------------------------------------------

        public void OnEnable()
        {
            if (ResetOnEnable)
            {
                if (!mHasEnabled)
                {
                    mHasEnabled = true;
                    return;
                }

                ResetComponents();
                OnReset?.Invoke();
            }
        }

        // --------------------------------------------------------------------

        public void ResetComponents()
        {
            for (int i = 0; i < mResetables.Count; ++i)
            {
                mResetables[i].OnReset();
            }
        }
    }
}