#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerHeadLookController : MonoBehaviour
    {
        [SerializeField] private float detectionRadius   = 3f;
        [SerializeField] private float maxAngle          = 60f;
        [SerializeField] private float weightSpeed       = 3f;
        [SerializeField] private float detectionInterval = 0.3f;
        [SerializeField] private LayerMask lookableLayer;

        private Animator  m_Animator        = null!;
        private Lookable? m_CurrentTarget;
        private Vector3   m_LastLookPosition;
        private float     m_Weight;
        private float     m_DetectionTimer;

        private readonly Collider[] m_OverlapResults = new Collider[16];

        private void Awake() => m_Animator = GetComponent<Animator>();

        private void Update()
        {
            m_DetectionTimer -= Time.deltaTime;
            if (m_DetectionTimer > 0f) return;

            m_DetectionTimer = detectionInterval;
            int count = Physics.OverlapSphereNonAlloc(
                transform.parent.position, detectionRadius, m_OverlapResults, lookableLayer);
            m_CurrentTarget = SelectBest(
                m_OverlapResults, count, transform.parent.position, transform.forward, maxAngle);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (m_CurrentTarget != null)
            {
                m_Weight           = Mathf.MoveTowards(m_Weight, 1f, weightSpeed * Time.deltaTime);
                m_LastLookPosition = m_CurrentTarget.LookPosition;
            }
            else
            {
                m_Weight = Mathf.MoveTowards(m_Weight, 0f, weightSpeed * Time.deltaTime);
            }

            m_Animator.SetLookAtWeight(m_Weight);
            if (m_Weight > 0f)
                m_Animator.SetLookAtPosition(m_LastLookPosition);
        }

        public static Lookable? SelectBest(
            Collider[] colliders, int count,
            Vector3 origin, Vector3 forward, float maxAngle)
        {
            Lookable? best     = null;
            float     bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (!colliders[i].TryGetComponent<Lookable>(out var lookable))
                    continue;

                Vector3 dir = lookable.transform.position - origin;
                if (Vector3.Angle(forward, dir) > maxAngle)
                    continue;

                float dist = dir.sqrMagnitude;
                if (best == null
                    || lookable.Priority > best.Priority
                    || (lookable.Priority == best.Priority && dist < bestDist))
                {
                    best     = lookable;
                    bestDist = dist;
                }
            }

            return best;
        }
    }
}
