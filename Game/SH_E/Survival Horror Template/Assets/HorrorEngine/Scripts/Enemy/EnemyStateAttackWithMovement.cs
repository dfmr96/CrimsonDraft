using UnityEngine;
using UnityEngine.AI;

namespace HorrorEngine
{
    public class EnemyStateAttackWithMovement : EnemyStateAttack
    {
        [Tooltip("This is the displacemet amount over the normalized attack duration")]
        [SerializeField] AnimationCurve m_DisplacementForward;
        [Tooltip("This is the displacemet amount over the normalized attack duration")]
        [SerializeField] AnimationCurve m_DisplacementUp;
        [Tooltip("The attack won't be performed if the angle between the enemy facing direction and the player is greater than this")]
        [SerializeField] float m_MaxAngle = 360f;

        private Vector3 m_InitPos;
        private Vector3 m_Direction;
        private EnemySensesController m_Senses;

        protected override void Awake()
        {
            base.Awake();

            m_Senses = GetComponentInParent<EnemySensesController>();
        }

        // --------------------------------------------------------------------

        public override bool CanEnter()
        {
            Vector3 dirToTarget = (m_Senses.LastKnownPosition - Actor.transform.position).normalized;
            float angle = Vector3.Angle(dirToTarget, Actor.transform.forward);
            return base.CanEnter() && angle < m_MaxAngle;
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_InitPos = Actor.transform.position;
            Vector3 dirToTarget = (m_Senses.LastKnownPosition - Actor.transform.position).normalized;
            m_Direction = dirToTarget;
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            float displacementFwd = m_DisplacementForward.Evaluate(m_TimeInState / m_Duration);
            float displacementUp = m_DisplacementUp.Evaluate(m_TimeInState / m_Duration);
            Vector3 endpos = m_InitPos + m_Direction * displacementFwd + Vector3.up * displacementUp;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(endpos, out hit, 1.0f, NavMesh.AllAreas))
                Actor.transform.position = hit.position;
        }
    }
}
