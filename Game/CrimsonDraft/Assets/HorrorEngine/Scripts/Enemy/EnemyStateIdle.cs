using UnityEngine;

namespace HorrorEngine
{
    public class EnemyStateIdle : ActorState
    {
        [SerializeField] EnemyStateAlerted m_AlertedState;
        [SerializeField] EnemyStateWander m_WanderState;
        [SerializeField] float TimeBetweenWander = 3;

        private float m_StateTime;
        private EnemySensesController m_EnemySenses;

        protected override void Awake()
        {
            base.Awake();

            m_EnemySenses = GetComponentInParent<EnemySensesController>();
        }

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);
            m_StateTime = 0;
        }

        public override void StateExit(IActorState intoState)
        {
            base.StateExit(intoState);
        }

        public override void StateUpdate()
        {
            base.StateUpdate();

            m_StateTime += Time.deltaTime;
            if (m_WanderState && m_StateTime > TimeBetweenWander)
            { 
                SetState(m_WanderState);
            }

            if (m_EnemySenses.IsPlayerDetected && m_EnemySenses.IsPlayerInReach)
            {
                SetState(m_AlertedState);
            }
        }

    }
}