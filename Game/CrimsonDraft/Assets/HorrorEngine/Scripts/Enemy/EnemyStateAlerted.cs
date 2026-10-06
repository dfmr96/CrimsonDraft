 using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class EnemyStateAlerted : ActorStateWithDuration
    {
        [SerializeField] EnemyStateIdle m_IdleState;
        [SerializeField] EnemyStateAttack[] m_AttackStates;
        [SerializeField] EnemyStateGrab[] m_GrabStates;
        [SerializeField] float m_InitialDelay = 1f;
        [SerializeField] float m_MinTimeBetweenAttacks = 1f;
        [SerializeField] float m_FacingSpeedBetweenAttacks = 1f;
        [SerializeField] bool m_ShowDebug;

        private NavMeshAgent m_Agent;

        private EnemySensesController m_EnemySenses;
        private float m_Delay;
        private UnityAction m_OnPlayerUnreachable;
        private ActorState m_CurrentAttack;
        private List<ActorState> m_AttackCandidates = new List<ActorState>();

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Agent = GetComponentInParent<NavMeshAgent>();
            m_EnemySenses = GetComponentInParent<EnemySensesController>();
            m_OnPlayerUnreachable = OnPlayerUnreachable;
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_Delay = 0;
            m_CurrentAttack = null;
            m_EnemySenses.OnPlayerUnreachable.AddListener(m_OnPlayerUnreachable);
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            base.StateExit(intoState);

            if (m_Agent && m_Agent.isOnNavMesh)
                m_Agent.isStopped = true;

            m_EnemySenses.OnPlayerUnreachable.RemoveListener(m_OnPlayerUnreachable);
        }

        // --------------------------------------------------------------------

        private void OnPlayerUnreachable()
        {
            SetState(m_IdleState);
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            if (m_Delay < m_InitialDelay)
            {
                m_Delay += Time.deltaTime;

                // Face target
                Vector3 lookPos = m_EnemySenses.LastKnownPosition - Actor.transform.position;
                lookPos.y = 0;
                Quaternion rotation = Quaternion.LookRotation(lookPos);
                Actor.transform.rotation = Quaternion.Slerp(Actor.transform.rotation, rotation, Time.deltaTime * m_FacingSpeedBetweenAttacks);
                return;
            }

            if (m_ShowDebug)
                DebugUtils.DrawBox(m_EnemySenses.LastKnownPosition, Quaternion.identity, Vector3.one, Vector3.one * 0.25f, Color.white, 1f);

            float distance = Vector3.Distance(Actor.transform.position, m_EnemySenses.LastKnownPosition);
            if (m_Agent && distance > m_Agent.stoppingDistance)
            {
                m_Agent.SetDestination(m_EnemySenses.LastKnownPosition);
                m_Agent.isStopped = false;
            }

            if (m_EnemySenses.IsPlayerGrabbed)
            {
                return;
            }

            if (m_CurrentAttack == null)
            {
                m_CurrentAttack = PickAttack();
            }
            
            if (m_CurrentAttack != null)
            {
                if (m_EnemySenses.IsPlayerDetected)
                {
                    if (m_TimeInState > m_MinTimeBetweenAttacks)
                    {
                        SetState(m_CurrentAttack as ActorState);
                        return;
                    }
                    else
                    {

                        if (m_Agent)
                            m_Agent.updateRotation = false;

                        if (m_ShowDebug)
                            Debug.DrawLine(Actor.transform.position, m_EnemySenses.LastKnownPosition, Color.magenta, 5);

                        // Face target
                        Vector3 lookPos = m_EnemySenses.LastKnownPosition - Actor.transform.position;
                        lookPos.y = 0;
                        Quaternion rotation = Quaternion.LookRotation(lookPos);
                        Actor.transform.rotation = Quaternion.Slerp(Actor.transform.rotation, rotation, Time.deltaTime * m_FacingSpeedBetweenAttacks);
                    }
                }
                else
                {
                    SetState(m_IdleState);
                }
            }
            else
            {

                if (m_Agent)
                    m_Agent.updateRotation = true;
            }   
        }

        // --------------------------------------------------------------------

        private ActorState PickAttack()
        {
            m_AttackCandidates.Clear();

            foreach (var attack in m_GrabStates)
            {
                if (attack.CanEnter())
                {
                    m_AttackCandidates.Add(attack);
                    break;
                }
            }

            foreach (var attack in m_AttackStates)
            {
                if (attack.CanEnter())
                {
                    m_AttackCandidates.Add(attack);
                    break;
                }
            }

            if (m_AttackCandidates.Count == 0)
            {
                return null;
            }
            else
            {
                return m_AttackCandidates[Random.Range(0, m_AttackCandidates.Count)];
            }
        }
    }

}