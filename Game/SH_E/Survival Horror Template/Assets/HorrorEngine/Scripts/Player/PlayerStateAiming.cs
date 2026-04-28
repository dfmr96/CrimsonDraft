using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
   
    public class PlayerStateAiming : ActorState
    {
        public static readonly int k_AimVerticalityHash = Animator.StringToHash("AimVerticality");
        public static readonly int k_MaxAimableResults = 10;
        

        [SerializeField] private SightCheck m_EnemySightCheck;
        [SerializeField] private ActorState m_AttackState;
        [SerializeField] private ActorState m_MotionState;
        [SerializeField] private ActorState m_ReloadState;
        [SerializeField] private bool m_AllowManualReload = true;
        [SerializeField] private bool m_DisableLookAt = true;

        [FormerlySerializedAs("MovementConstrains")]
        [SerializeField] private PlayerMovement.MovementConstrain m_MovementConstrains = PlayerMovement.MovementConstrain.Movement;

        [Header("AutoAiming")]
        [SerializeField] private AutoAimSelection m_AutoAiming = AutoAimSelection.BestDirectionMatch;
        [SerializeField] private bool m_DisableMovementWhileAutoAimingRotation = true;
        [SerializeField] private float m_AutoAimingRange;
        [SerializeField] protected float m_AutoAimingDuration;
        [SerializeField] private LayerMask m_AutoAimingMask;
        [SerializeField] private bool m_AutoAimingSkipInvulnerableTargets;
        [SerializeField] private float m_AimingHighPriorityCheckDistanceFactor = 0.5f;

        private IPlayerVerticalAiming m_VerticalAiming;

        protected IPlayerInput m_Input;
        private PlayerMovement m_Movement;
        private PlayerLookAtLookable m_LookAt;
        protected Rigidbody m_Rigidbody;
        private Collider[] m_AutoAimingResults = new Collider[k_MaxAimableResults];
        
        protected Aimable m_AutoRotatingAtAimable; // This variable is cleared after the rotation is done
        private Aimable m_LastAimedAt;
        private List<Aimable> m_DetectedAimables = new List<Aimable>();

        private Vector3 m_AutoAimingDir;
        private float m_AutoAimingAngle;
        

        public bool IsAiming => m_Input.IsAimingHeld();
        public Aimable CurrentAimable => m_LastAimedAt;

        public enum AutoAimSelection
        {
            Deactivated,
            CloserTarget,
            BestDirectionMatch
        }

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_VerticalAiming = GetComponent<IPlayerVerticalAiming>();
            m_Input = GetComponentInParent<IPlayerInput>();
            m_Movement = GetComponentInParent<PlayerMovement>();
            m_LookAt = Actor.MainAnimator.GetComponent<PlayerLookAtLookable>();
            m_Rigidbody = GetComponentInParent<Rigidbody>();
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            if (m_VerticalAiming != null)
            {
                m_VerticalAiming.Verticality = Actor.MainAnimator.GetFloat(k_AimVerticalityHash);
            }

            m_AutoRotatingAtAimable = null;
            if (m_AutoAiming != AutoAimSelection.Deactivated)
            {
                SetInitialAimTarget();
            }

            if (m_AutoAiming == AutoAimSelection.Deactivated || m_AutoRotatingAtAimable == null || !m_DisableMovementWhileAutoAimingRotation)
            {
                m_Movement.enabled = true;
                m_Movement.AddConstrain(m_MovementConstrains);
            }

            if (m_DisableLookAt && m_LookAt)
                m_LookAt.LookIntensity = 0f;
        }

        // --------------------------------------------------------------------

        private void SetInitialAimTarget()
        {
            RefreshAvailableTargets(true);
            if (m_LastAimedAt) // Aim again last aimed target
            {
                int index = m_DetectedAimables.IndexOf(m_LastAimedAt);
                if (index >= 0)
                    SetAimAt(m_DetectedAimables[index]);
                else
                    m_LastAimedAt = null;
            }

            if (!m_LastAimedAt) // LastAimed was cleared since it was no longer a valid target
            {
                Aimable aimable = GetBestTargetFromDetected();
                if (aimable)
                    SetAimAt(aimable);
            }
        }

        // --------------------------------------------------------------------

        protected void RefreshAvailableTargets(bool clearDetected)
        {
            if (clearDetected)
            {
                m_DetectedAimables.Clear();
            }
            else
            {
                ClearDeadTargets();
            }

            // High priority check
            AddAimablesAtRange(m_AutoAimingRange * m_AimingHighPriorityCheckDistanceFactor);
           
            // Lower priority check
            if (m_DetectedAimables.Count == 0 && m_AimingHighPriorityCheckDistanceFactor < 1f)
            {
                AddAimablesAtRange(m_AutoAimingRange);
            }
        }

        // --------------------------------------------------------------------

        private void AddAimablesAtRange(float range)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, m_AutoAimingResults, m_AutoAimingMask);
            for (int i = 0; i < count; ++i)
            {
                Aimable aimable = m_AutoAimingResults[i].GetComponent<Aimable>();
                if (IsValidAimable(aimable))
                {
                    if (!m_DetectedAimables.Contains(aimable))
                    {
                        m_DetectedAimables.Add(aimable);
                    }
                }
            }
        }

        // --------------------------------------------------------------------

        private bool IsValidAimable(Aimable aimable)
        {
            if (!aimable)
                return false;

            if (!IsAimableInSight(aimable))
                return false;

            Health health = aimable.GetComponentInParent<Health>();
            if (health)
            {
                if (health.IsDead)
                    return false;

                if (m_AutoAimingSkipInvulnerableTargets)
                    return !health.Invulnerable;
            }

            return true;
        }
        
        // --------------------------------------------------------------------

        private bool IsAimableInSight(Aimable aimable)
        {
            foreach (Vector3 v in aimable.VisibilityTracePoints) 
            {
                Vector3 worldPos = aimable.transform.TransformPoint(v);
                if (m_EnemySightCheck.IsInSight(worldPos))
                    return true;
            }

            return false;
        }

        // --------------------------------------------------------------------

        private Aimable GetBestTargetFromDetected()
        {
            Aimable aimed = null;
            if (m_AutoAiming == AutoAimSelection.CloserTarget)
            {
                aimed = GetCloserAimableFromDetected();
            }
            else if (m_AutoAiming == AutoAimSelection.BestDirectionMatch)
            {
                aimed = GetBestDirectionMatchedAimableFromDetected();
            }

            return aimed;
        }

        // --------------------------------------------------------------------

        protected Aimable GetBestDirectionMatchedAimableFromDetected()
        {
            Aimable aimed = null;
            float maxDot = int.MinValue;
            Vector3 playerPos = m_ActorTransform.position;
            Vector3 playerFwd = m_ActorTransform.forward;
            foreach (Aimable aimable in m_DetectedAimables)
            {
                float dot = Vector3.Dot(playerFwd, (aimable.transform.position - playerPos).normalized);
                if (dot > maxDot)
                {
                    aimed = aimable;
                    maxDot = dot;
                }
            }

            return aimed;
        }

        // --------------------------------------------------------------------

        private Aimable GetCloserAimableFromDetected()
        {
            Aimable aimed = null;
            float minDist = int.MaxValue;
            Vector3 playerPos = m_ActorTransform.position;
            foreach (Aimable aimable in m_DetectedAimables)
            {
                float distance = Vector3.SqrMagnitude(playerPos - aimable.transform.position);
                if (distance < minDist)
                {
                    aimed = aimable;
                    minDist = distance;
                }
            }

            return aimed;
        }

        // --------------------------------------------------------------------

        private void ClearDeadTargets()
        {
            for (int i = m_DetectedAimables.Count - 1; i >= 0; --i)
            {
                Aimable aimable = m_DetectedAimables[i];
                Health health = aimable.GetComponentInParent<Health>();
                if (health.IsDead)
                {
                    m_DetectedAimables.Remove(aimable);
                }
            }
        }

        // --------------------------------------------------------------------

        protected void ChangeAimTarget()
        {
            RefreshAvailableTargets(false);

            if (m_DetectedAimables.Count > 0) 
            {
                int index = m_LastAimedAt ? m_DetectedAimables.IndexOf(m_LastAimedAt) : 0;
                ++index;

                if (index >= m_DetectedAimables.Count)
                    index = 0;

                SetAimAt(m_DetectedAimables[index]);
            }
            else
            {
                m_LastAimedAt = null;
                m_AutoRotatingAtAimable = null;
            }

        }

        // --------------------------------------------------------------------

        protected void SetAimAt(Aimable aimable)
        {
            m_AutoRotatingAtAimable = aimable; // This will be cleared once the rotation is done
            m_LastAimedAt = aimable;
            m_AutoAimingDir = m_AutoRotatingAtAimable.AimingPoint - m_ActorTransform.position;
            m_AutoAimingDir.y = 0;
            m_AutoAimingDir.Normalize();
            m_AutoAimingAngle = Vector3.Angle(m_AutoAimingDir, m_ActorTransform.forward);
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            

            if (m_VerticalAiming != null)
            {
                Actor.MainAnimator.SetFloat(k_AimVerticalityHash, m_VerticalAiming.Verticality);
            }

            if (m_Input.IsAttackDown())
                SetState(m_AttackState);
            else if (!m_Input.IsAimingHeld())
                SetState(m_MotionState);
            else if (m_AllowManualReload && m_Input.IsReloadDown() && GameManager.Instance.Inventory.CanReloadEquippedWeapon())
                SetState(m_ReloadState);
            else if (m_Input.IsChangeAimTargetDown())
                ChangeAimTarget();
        }

        // --------------------------------------------------------------------

        public override void StateFixedUpdate()
        {
            base.StateFixedUpdate();

            if (m_AutoRotatingAtAimable)
                RotateToAutoAimingTarget();
        }


        // --------------------------------------------------------------------

        protected virtual void RotateToAutoAimingTarget()
        {
            float t = (Time.deltaTime / Mathf.Max(m_AutoAimingDuration, Mathf.Epsilon));
            m_Rigidbody.MoveRotation(Quaternion.RotateTowards(m_Rigidbody.rotation, Quaternion.LookRotation(m_AutoAimingDir), m_AutoAimingAngle * t));
            
            float angleToTarget = Vector3.Angle(m_AutoAimingDir, m_ActorTransform.forward);
            if (angleToTarget < Mathf.Epsilon)
            {
                m_AutoRotatingAtAimable = null;
                m_Movement.enabled = true;
                m_Movement.AddConstrain(m_MovementConstrains);
            }
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            m_Movement.enabled = false;
            m_Movement.RemoveConstrain(m_MovementConstrains);

            if (m_DisableLookAt && m_LookAt)
                m_LookAt.LookIntensity = 1f;
            
            if ((ActorState)intoState != m_AttackState)
            {
                m_LastAimedAt = null;
            }
            
            base.StateExit(intoState);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (m_AutoAiming != AutoAimSelection.Deactivated)
            {
                Gizmos.DrawWireSphere(transform.position, m_AutoAimingRange * m_AimingHighPriorityCheckDistanceFactor);
                Gizmos.DrawWireSphere(transform.position, m_AutoAimingRange);
            }
        }
    }
}