using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorEngine
{
    public class EnemyStateWanderAroundTarget : ActorStateWithDuration
    {
        [SerializeField] EnemyStateAlerted m_AlertedState;
        [SerializeField] float m_Radius = 10f;

        private NavMeshAgent m_Agent;
        private EnemySensesController m_EnemySenses;
        private Vector3 m_Destination;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Agent = GetComponentInParent<NavMeshAgent>();
            m_EnemySenses = GetComponentInParent<EnemySensesController>();
        }

        // --------------------------------------------------------------------

        private bool FindDestination()
        {
            Vector3 randomDirection = Random.insideUnitSphere * m_Radius;
            randomDirection += m_EnemySenses.LastKnownPosition;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, m_Radius, 1))
            {
                m_Destination = hit.position;
                return true;
            }

            return false;
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_Agent.isStopped = false;
            m_Agent.updateRotation = true;

            if (FindDestination())
                m_Agent.SetDestination(m_Destination);
            else
                SetState(m_AlertedState);
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            base.StateExit(intoState);
            m_Agent.isStopped = true;
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            bool reachedTarget = m_Agent.remainingDistance <= m_Agent.stoppingDistance;
            if (reachedTarget)
            {
                if (Vector3.Distance(m_Destination, m_EnemySenses.LastKnownPosition) > m_Agent.stoppingDistance)
                {
                    if (FindDestination())
                        m_Agent.SetDestination(m_Destination);
                    else
                        SetState(m_AlertedState);
                }
            }
        }

    }

}