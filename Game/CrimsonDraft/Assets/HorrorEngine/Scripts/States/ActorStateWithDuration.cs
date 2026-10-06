using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{

    public class ActorStateWithDuration : ActorState
    {
        [SerializeField] protected float m_Duration;
        [FormerlySerializedAs("m_GoToStateAfterDuration")]
        [SerializeField] protected ActorState m_ExitState;

        protected float m_TimeInState;
        private bool m_Finished;

        protected bool DurationElapsed => m_TimeInState > m_Duration;

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_Finished = false;
            m_TimeInState = 0f;
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            m_TimeInState += Time.deltaTime;
            if (DurationElapsed && !m_Finished)
            {
                OnStateDurationEnd();
                m_Finished = true;
            }            
        }

        // --------------------------------------------------------------------

        protected virtual void OnStateDurationEnd()
        {
            if (m_ExitState)
            {
                SetState(m_ExitState);
            }
        }
    }
}