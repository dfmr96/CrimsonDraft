using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class EnemyStateAttack : ActorStateWithDuration
    {
        [FormerlySerializedAs("m_AlertedState")]
        [SerializeField] ActorState m_ExitStatePlayerDetected;
        [SerializeField] float m_FacingSpeed = 1f;
        [SerializeField] protected AttackMontage m_AttackMontage;
        [SerializeField] bool m_RotateTowardsTarget = true;

        public float AttackDistance = 1f;
        public float Cooldown = 3;

        protected NavMeshAgent m_NavMeshAgent;
        protected EnemySensesController m_EnemySenses;
        private float m_LastAttackTime;
        private NavMeshPath m_NavPath;

        protected bool InMontageDelay => m_TimeInState < m_AttackMontage.MontageDelay;
        protected virtual bool CanRotate => true;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_NavPath = new NavMeshPath();
            m_NavMeshAgent = GetComponentInParent<NavMeshAgent>();
            m_EnemySenses = GetComponentInParent<EnemySensesController>();
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            if (m_AnimationState)
                Debug.LogWarning("AnimationState will be overwritten by the attack animation", gameObject);

            base.StateEnter(fromState);

            m_Duration = m_AttackMontage.MontageDelay + m_AttackMontage.Duration;

            m_AttackMontage.PreDelay(Actor.MainAnimator); // Not ideal,  the player handles the delay in the state and enemies handle the telegraphing in the montage
            m_AttackMontage.Play(Actor.MainAnimator);
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            // Face target
            if (m_RotateTowardsTarget && CanRotate)
            {
                Vector3 lookPos = m_EnemySenses.LastKnownPosition - transform.position;
                lookPos.y = 0;
                Quaternion rotation = Quaternion.LookRotation(lookPos);
                Actor.transform.rotation = Quaternion.Slerp(Actor.transform.rotation, rotation, Time.deltaTime * m_FacingSpeed);
            }

        }


        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            m_AnimationState = null;
            m_LastAttackTime = Time.time;

            if (m_TimeInState < m_Duration)
                m_AttackMontage.Interrupt();

            base.StateExit(intoState);
        }

        // --------------------------------------------------------------------

        protected override void OnStateDurationEnd()
        {
            base.OnStateDurationEnd();

            if (m_EnemySenses.IsPlayerDetected)
                SetState(m_ExitStatePlayerDetected);
        }

        // --------------------------------------------------------------------

        public virtual bool CanEnter()
        {
            m_NavPath.ClearCorners();
            m_NavMeshAgent.CalculatePath(m_EnemySenses.LastKnownPosition, m_NavPath);
            
            if ((m_NavPath.status != NavMeshPathStatus.PathInvalid) && (m_NavPath.corners.Length > 1))
            {
                float distToTarget = 0;
                for (int i = 1; i < m_NavPath.corners.Length; ++i)
                {
                    distToTarget += Vector3.Distance(m_NavPath.corners[i - 1], m_NavPath.corners[i]);
                    if (distToTarget > AttackDistance)
                        return false;
                }
            }
            else
            {
                return false;
            }

            return (Time.time - m_LastAttackTime) > Cooldown;
        }
    }
}
