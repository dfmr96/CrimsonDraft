using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class ActorStateTransition : MonoBehaviour
    {
        [SerializeField] private bool m_FromAllStates;
        [SerializeField] private List<ActorState> m_FromStates;
        [SerializeField] private List<ActorState> m_ExcludeStates;
        [SerializeField] private ActorStateBase m_ToState;

        private ActorStateController m_StateController;

        private void Awake()
        {
            m_StateController = GetComponentInParent<ActorStateController>();
        }

        public bool CanTrigger()
        {
            if (!m_StateController.enabled)
                return false;

            ActorState currentState = m_StateController.CurrentState as ActorState;
            bool fromStateIsValid = m_FromAllStates || m_FromStates.Contains(currentState);
            return fromStateIsValid && !m_ExcludeStates.Contains(currentState);
        }

        public void Trigger()
        {
            if (CanTrigger())
            {
                m_StateController.SetState(m_ToState);
            }
        }

        public void ForceTrigger()
        {
            m_StateController.SetState(m_ToState);
        }
    }
}