using UnityEngine;
using UnityEngine.AI;

namespace HorrorEngine
{
    public class EnemyStateJumpAttack : EnemyStateAttack
    {
        [SerializeField] float m_MaxHeightDifference = 2.0f;
        [SerializeField] float m_MinJumpDistance = 2.0f;
        [SerializeField] float m_JumpOvershootDistance = 1.5f;
        [SerializeField] bool m_StickToGroundOnLand = true;
        [SerializeField] LayerMask m_ObstacleLayerMask;
        [SerializeField] int m_AllowedNavMeshAreas = 1;
        [SerializeField] float m_JumpPathCheckVerticalOffset = 0.5f;
        [SerializeField] AnimationCurve m_HeightOverJumpT;
        [SerializeField] float m_LandDuration;
        
        private Vector3 m_JumpStartPos;
        private Vector3 m_JumpEndPos;
        
        protected override bool CanRotate => InMontageDelay;


        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_JumpStartPos = Actor.transform.position;

            if (CanPerformJump(out Vector3 pos, out Quaternion rot))
            {
                m_JumpEndPos = pos;
                m_NavMeshAgent.enabled = false;
            }
            else
            {
                SetState(m_ExitState);
            }
        }

        // --------------------------------------------------------------------

        private bool CanPerformJump(out Vector3 outPosition, out Quaternion outRotation)
        {
            outPosition = Actor.transform.position;
            outRotation = Actor.transform.rotation;

            Vector3 initialPos = Actor.transform.position;
            Vector3 targetPosition = m_EnemySenses.LastKnownPosition;
            Vector3 jumpDirection = (targetPosition - initialPos).normalized;
            Vector3 idealLandingSpot = targetPosition + jumpDirection * m_JumpOvershootDistance;

            // Get the closest landing spot on the NavMesh
            NavMeshHit navMeshHit;
            if (NavMesh.SamplePosition(idealLandingSpot, out navMeshHit, AttackDistance + m_JumpOvershootDistance, m_AllowedNavMeshAreas))
            {
                if (Mathf.Abs(initialPos.y - navMeshHit.position.y) > m_MaxHeightDifference)
                    return false;

                float distance = (navMeshHit.position - initialPos).magnitude;
                if (distance < m_MinJumpDistance)
                    return false;

                // Check if the jump path is obstructed by something
                Vector3 pathDirection = (navMeshHit.position - initialPos).normalized;
                RaycastHit raycastHit;
                if (Physics.SphereCast(initialPos + Vector3.up * m_JumpPathCheckVerticalOffset,
                                        m_NavMeshAgent.radius,
                                        pathDirection,
                                        out raycastHit,
                                        distance,
                                        m_ObstacleLayerMask))
                {
                    return false;
                }

                outPosition = navMeshHit.position;
                outRotation = Quaternion.LookRotation(jumpDirection);


                return true;

            }

            return false;
        }

        // --------------------------------------------------------------------

        public override bool CanEnter()
        {
            return base.CanEnter() && CanPerformJump(out Vector3 pos, out Quaternion rot);
        }

        // --------------------------------------------------------------------

        public override void StateFixedUpdate()
        {
            base.StateFixedUpdate();

            UpdateJumpProgress();
        }

        // --------------------------------------------------------------------

        private void UpdateJumpProgress()
        {
            if (InMontageDelay)
                return;

            float t = GetJumpProgress();
            if (t <= 1.0f)
            {
                Vector3 newPos = Vector3.Lerp(m_JumpStartPos, m_JumpEndPos, t);
                float yOffset = m_HeightOverJumpT.Evaluate(t);
                newPos.y += yOffset;

                Actor.transform.position = newPos;
            }
        }


        // --------------------------------------------------------------------

        private float GetJumpProgress()
        {
            float extraTime = m_AttackMontage.MontageDelay + m_LandDuration;
            float t = (m_TimeInState - m_AttackMontage.MontageDelay) / (m_Duration - extraTime);
            return t;
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            if (GetJumpProgress() >= 1.0f)
            {
                Actor.transform.position = m_JumpEndPos;   
            }

            if (m_StickToGroundOnLand)
            {
                m_NavMeshAgent.enabled = true;
                m_NavMeshAgent.Warp(Actor.transform.position);
            }

            base.StateExit(intoState);
        }

    }
}