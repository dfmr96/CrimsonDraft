using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class EnemyStateLying : ActorStateWithDuration
    {
        private EnemySensesController m_EnemySenses;
        
        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_EnemySenses = GetComponentInParent<EnemySensesController>();
        }

        // --------------------------------------------------------------------
        
        public override void StateUpdate()
        {
            base.StateUpdate();

            if (((DurationElapsed && m_EnemySenses.IsPlayerDetected) || m_EnemySenses.IsEnemyDamaged) && m_ExitState)
            {
                SetState(m_ExitState);
            }
        }


        // --------------------------------------------------------------------

        protected override void OnStateDurationEnd()
        {
            if (m_EnemySenses.IsPlayerDetected)
            {
                base.OnStateDurationEnd();
            }
        }
    }

}
