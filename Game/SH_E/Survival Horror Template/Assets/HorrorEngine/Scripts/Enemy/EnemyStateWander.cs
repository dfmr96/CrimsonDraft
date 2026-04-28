using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class EnemyStateWander : ActorState
    {
        [FormerlySerializedAs("m_IdleState")]
        [SerializeField] ActorState m_ExitState;
        [FormerlySerializedAs("m_AlertedState")]
        [SerializeField] ActorState m_ExitStatePlayerDetected;
        [SerializeField] float m_Radius = 10f;
        [SerializeField] int m_PointCount = 5;
        [SerializeField] ActorState[] m_FullDurationFromStates;

        private NavMeshAgent m_Agent;
        private EnemySensesController m_EnemySenses;
        private List<Vector3> m_WanderPoints = new List<Vector3>();
        private Vector3 m_CurrentWanderPoint;
        private bool m_DoFullDuration;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Agent = GetComponentInParent<NavMeshAgent>();
            m_EnemySenses = GetComponentInParent<EnemySensesController>();

            if (!SampleNavMesh())
                Debug.LogError("Wander couldn't generate all the points", gameObject);
        }

        // --------------------------------------------------------------------

        private bool SampleNavMesh()
        {
            Debug.Assert(m_PointCount > 0, "Wander won't generate any points if PointsCount = 0", gameObject);
            int numOfAttemps = m_PointCount * 3;
            for (int i = 0; i < numOfAttemps; ++i)
            {
                Vector3 randomDirection = Random.insideUnitSphere * m_Radius;
                randomDirection += transform.position;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, m_Radius, 1))
                {
                    m_WanderPoints.Add(hit.position);
                    if (m_WanderPoints.Count >= m_PointCount)
                        return true;
                }
            }

            return false;
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_Agent.isStopped = false;
            m_Agent.updateRotation = true;
            m_CurrentWanderPoint = m_WanderPoints[Random.Range(0, m_WanderPoints.Count)];

            m_Agent.SetDestination(m_CurrentWanderPoint);

            m_DoFullDuration = m_FullDurationFromStates.Contains(fromState);
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            base.StateExit(intoState);

            if (m_Agent.isOnNavMesh)
                m_Agent.isStopped = true;
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            if (!m_DoFullDuration)
            {
                if (m_EnemySenses.IsPlayerDetected && m_EnemySenses.IsPlayerInReach)
                {
                    SetState(m_ExitStatePlayerDetected);
                    return;
                }
            }

            bool reachedTarget = m_Agent.remainingDistance <= m_Agent.stoppingDistance;
            if (reachedTarget)
            {
                SetState(m_ExitState);
            }
        }
    }

}