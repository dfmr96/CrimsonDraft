using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public enum GrabPositioningType
    {
        MoveGrabbedToGrabber,
        MoveGrabberToGrabbed,
    }

    [Serializable]
    public class GrabPositioning
    {
        public GrabPositioningType Type;
        public float Distance;
        public float MoveSpeed = 1;
        [NonSerialized] public Vector3 WorldSpaceRefPoint;
    }
    
    [RequireComponent(typeof(OnOverlapAttack))]
    [RequireComponent(typeof(AttackMontage))]
    public class EnemyStateGrab : ActorState
    {
        private const string k_DebugCategory = "StateGrab";

        [SerializeField] private float m_GrabDistance = 1f;
        [Tooltip("Helps finding which player state will be selected. This is the “grabbed” player state")]
        [SerializeField] private string m_GrabTag;
        [SerializeField] private ActorState m_ExitState;
        [SerializeField] private float m_FacingSpeed = 1f;
        [SerializeField] private float m_GrabDelay;
        [SerializeField] private float m_AttackDelay;
        [SerializeField] private float m_AttackDelayRandomOffset;
        [SerializeField] private float m_ApproachSpeedMultiplier = 1;
        [SerializeField] private int m_Cooldown;
        [SerializeField] private AttackBase m_OnGrabAttack;
        [Tooltip("The HitShape used for this attack. If null it will be try to be obtained from this object")]
        [FormerlySerializedAs("m_Hitbox")]
        [SerializeField] private HitShape m_HitShape;

        [Tooltip("Min angle between player/enemy in which this grab can be used")]
        [SerializeField] private int m_MinAngleOfEntry = 0;
        [Tooltip("Max angle between player/enemy in which this grab can be used")]
        [SerializeField] private int m_MaxAngleOfEntry = 180;

        [SerializeField] private GrabPositioning m_GrabPositioning;
        [SerializeField] private bool m_RotateTowardsTarget = true;
        [SerializeField] private bool m_ShowDebug; // OBSOLETE - Delete

        private Grabber m_Grabber;
        private float m_TimeToAttack;
        private PlayerGrabHandler m_PlayerGrabHandle;
        private float m_Time;
        private float m_LastAttackTime;
        private bool m_Releasing;
        private float m_OriginalSpeed;

        private AttackMontage m_AttackMtg;
        private EnemySensesController m_EnemySenses;
        private List<Damageable> m_Damageables = new List<Damageable>();
        private NavMeshAgent m_NavMeshAgent;
        
        private UnityAction<GrabReleaseData> m_OnPlayerGrabReleased;
        private UnityAction<GrabPreventionData> m_OnPlayerGrabPrevented;

        private Coroutine m_DelayedStateChangeRoutine;
        
        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Grabber = GetComponentInParent<Grabber>();
            m_OnPlayerGrabReleased = OnPlayerReleased;
            m_OnPlayerGrabPrevented = OnPlayerPrevented;
            m_AttackMtg = GetComponent<AttackMontage>();
            m_EnemySenses = GetComponentInParent<EnemySensesController>();
            m_NavMeshAgent = GetComponentInParent<NavMeshAgent>();
            
            if (m_HitShape == null)
                m_HitShape = GetComponent<HitShape>();
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            base.StateEnter(fromState);

            m_Time = 0f;
            m_PlayerGrabHandle = null;
            m_Releasing = false;

            m_OriginalSpeed = m_NavMeshAgent.speed;
            m_NavMeshAgent.speed = m_OriginalSpeed * m_ApproachSpeedMultiplier;
            if (m_NavMeshAgent.isOnNavMesh)
                m_NavMeshAgent.isStopped = false; // Keep moving until grabbing something

            CalculateNextAttack();
        }

        // --------------------------------------------------------------------

        private void CalculateNextAttack()
        {
            m_TimeToAttack = UnityEngine.Random.Range(m_AttackDelay - m_AttackDelayRandomOffset, m_AttackDelay + m_AttackDelayRandomOffset);
        }

        // --------------------------------------------------------------------

        public override void StateUpdate()
        {
            base.StateUpdate();

            m_Time += Time.deltaTime;

            if (m_PlayerGrabHandle)
                UpdatePositioning();

            UpdateTargetFacing();

            if (m_Releasing)
            {
                return;
            }

            if (!m_PlayerGrabHandle)
            {
                if (m_Time > m_GrabDelay) // Waiting initial delay 
                {
                    m_HitShape.GetOverlappingDamageables(m_Damageables, k_DebugCategory);
                    foreach (var damageable in m_Damageables)
                    {
                        if (damageable.Owner.TryGetComponent(out m_PlayerGrabHandle))
                        {
                            if (!m_PlayerGrabHandle.CanBeGrabbed)
                            {
                                m_PlayerGrabHandle = null;
                                break;
                            }

                            if (m_OnGrabAttack)
                                m_OnGrabAttack.StartAttack();

                            m_GrabPositioning.WorldSpaceRefPoint = CalculateRefPoint();

                            DebugUtils.DrawCross(m_GrabPositioning.WorldSpaceRefPoint, Actor.transform.right, Actor.transform.up, Actor.transform.forward, 2f, Color.yellow, 5f);

                            m_PlayerGrabHandle.SetGrabbed(m_Grabber, m_GrabTag, m_GrabPositioning);
                            m_PlayerGrabHandle.OnRelease.AddListener(m_OnPlayerGrabReleased);
                            m_PlayerGrabHandle.OnPrevented.AddListener(m_OnPlayerGrabPrevented);

                            m_NavMeshAgent.isStopped = true;
                            break;
                        }
                    }

                    if (!m_PlayerGrabHandle)
                    {
                        SetState(m_ExitState);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            m_TimeToAttack -= Time.deltaTime;
            if (m_TimeToAttack <= 0)
            {
                DoAttack();
            }
        }

        // --------------------------------------------------------------------

        private Vector3 CalculateRefPoint()
        {
            var origin = m_GrabPositioning.Type == GrabPositioningType.MoveGrabberToGrabbed ? m_PlayerGrabHandle.transform.position : Actor.transform.position;
            var destiny = m_GrabPositioning.Type == GrabPositioningType.MoveGrabberToGrabbed ? Actor.transform.position : m_PlayerGrabHandle.transform.position;
            var dir = (destiny - origin).normalized;

            return origin + dir * m_GrabPositioning.Distance;
        }

        // --------------------------------------------------------------------

        private void UpdatePositioning()
        {
            if (m_GrabPositioning.Type == GrabPositioningType.MoveGrabberToGrabbed)
            {
                m_GrabPositioning.WorldSpaceRefPoint = CalculateRefPoint();
                DebugUtils.DrawCross(m_GrabPositioning.WorldSpaceRefPoint, Actor.transform.right, Actor.transform.up, Actor.transform.forward, 0.5f, Color.magenta, 0);
                Actor.transform.position = Vector3.MoveTowards(Actor.transform.position, m_GrabPositioning.WorldSpaceRefPoint, m_GrabPositioning.MoveSpeed * Time.fixedDeltaTime);
            }
        }

        // --------------------------------------------------------------------

        private void UpdateTargetFacing()
        {
            if (m_RotateTowardsTarget)
            {
                Vector3 lookPos = m_EnemySenses.LastKnownPosition - Actor.transform.position;
                lookPos.y = 0;
                Quaternion rotation = Quaternion.LookRotation(lookPos);
                Actor.transform.rotation = Quaternion.Slerp(Actor.transform.rotation, rotation, Time.deltaTime * m_FacingSpeed);
            }
        }

        // --------------------------------------------------------------------

        private void OnPlayerReleased(GrabReleaseData releaseData)
        {
            m_AttackMtg.Complete();

            ActorState exitState = m_ExitState;
            if (!string.IsNullOrEmpty(releaseData.GrabberStateTag))
            {
                exitState = Actor.StateController.GetStateWithTag(releaseData.GrabberStateTag);
                Debug.Assert(exitState, $"State with tag {releaseData.GrabberStateTag} not found", gameObject);
            }

            m_Releasing = true;

            if (releaseData.Delay > 0)
                m_DelayedStateChangeRoutine = StartCoroutine(SetStateDelayed(exitState, releaseData.Delay));
            else
                SetState(exitState);
        }

        // --------------------------------------------------------------------

        IEnumerator SetStateDelayed(ActorState state, float delay)
        {
            yield return Yielders.Time(delay);
            SetState(state);
        }


        // --------------------------------------------------------------------

        private void OnPlayerPrevented(GrabPreventionData preventData)
        {
            ActorState exitState = Actor.StateController.GetStateWithTag(preventData.GrabberStateTag);
            SetState(exitState);
        }

        // --------------------------------------------------------------------

        protected void DoAttack()
        {
            m_AttackMtg.Play(Actor.MainAnimator);

            //CalculateNextAttack();

            // Check if the player is still detected

            m_HitShape.GetOverlappingDamageables(m_Damageables, k_DebugCategory);
            PlayerGrabHandler playerGrabHandler = null;
            foreach (var damageable in m_Damageables)
            {
                if (damageable.Owner.TryGetComponent(out playerGrabHandler))
                {
                    break;
                }
            }
            
            if (playerGrabHandler)
            {
                CalculateNextAttack();
            }
            else
            {
                /// Player no longer detected, release normally
                if (m_PlayerGrabHandle)
                    m_PlayerGrabHandle.Release();

                SetState(m_ExitState);
            }
        }

        // --------------------------------------------------------------------

        public bool CanEnter()
        {
            var playerTransform = m_EnemySenses.PlayerTransform;
            if (!playerTransform)
                return false;

            float angle = Vector3.Angle(-Actor.transform.forward, playerTransform.forward);
            
            bool closeEnough = angle >= m_MinAngleOfEntry && angle <= m_MaxAngleOfEntry &&
                (Time.time - m_LastAttackTime) > m_Cooldown && 
                Vector3.Distance(m_EnemySenses.LastKnownPosition, Actor.transform.position) < m_GrabDistance;

            if (closeEnough) 
            {
                m_HitShape.GetOverlappingDamageables(m_Damageables, k_DebugCategory);
                foreach (var damageable in m_Damageables)
                {
                    if (damageable.Owner.TryGetComponent(out m_PlayerGrabHandle))
                    {
                        return !m_PlayerGrabHandle.Grabber; // Try to grab only if it isn't already 
                    }
                }
            }

            return false;
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            if (m_PlayerGrabHandle)
            {
                m_PlayerGrabHandle.OnRelease.RemoveListener(m_OnPlayerGrabReleased);
                m_PlayerGrabHandle.OnPrevented.RemoveListener(m_OnPlayerGrabPrevented);
                m_PlayerGrabHandle = null;
            }

            m_LastAttackTime = Time.time;

            if (m_DelayedStateChangeRoutine != null)
                StopCoroutine(m_DelayedStateChangeRoutine);

            m_AttackMtg.Complete();

            m_NavMeshAgent.speed = m_OriginalSpeed;
            if (m_NavMeshAgent.isOnNavMesh)
                m_NavMeshAgent.isStopped = true;

            base.StateExit(intoState);
        }
    }
}
