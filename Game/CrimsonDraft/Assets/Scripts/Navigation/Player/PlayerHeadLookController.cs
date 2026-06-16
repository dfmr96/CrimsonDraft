#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Player
{
    // NOTA: Este script fue migrado de rig Humanoid a Generic cuando se cambió el modelo 3D del
    // jugador (RestPoseDariusFBX / RE1MostAnimations). Con Humanoid, Unity provee IK nativa:
    //
    //   private void OnAnimatorIK(int layerIndex)
    //   {
    //       m_Weight = Mathf.MoveTowards(m_Weight, target != null ? 1f : 0f, weightSpeed * Time.deltaTime);
    //       m_Animator.SetLookAtWeight(m_Weight);
    //       if (m_Weight > 0f)
    //           m_Animator.SetLookAtPosition(m_LastLookPosition);
    //   }
    //
    // SetLookAtWeight / SetLookAtPosition son APIs exclusivas de Humanoid — con Generic se ignoran
    // silenciosamente. La implementación actual replica el mismo comportamiento manipulando el
    // Transform del hueso directamente en LateUpdate (corre después de que el Animator aplica
    // sus curvas de animación). Si el rig vuelve a Humanoid: reemplazar Awake + LateUpdate por
    // OnAnimatorIK, eliminar headBone / lookAxisOffset / m_RestLocalRotation, y restaurar m_Animator.

    [RequireComponent(typeof(Animator))]
    public sealed class PlayerHeadLookController : MonoBehaviour
    {
        [SerializeField] private Transform headBone      = null!;
        // Corrección de ejes para riggs cuyo hueso de cabeza no apunta hacia +Z.
        // Valores actuales para el rig de Darius: (-90, 180, 0).
        [SerializeField] private Vector3   lookAxisOffset;
        [SerializeField] private float detectionRadius   = 3f;
        [SerializeField] private float maxAngle          = 60f;
        [SerializeField] private float weightSpeed       = 3f;
        [SerializeField] private float detectionInterval = 0.3f;
        [SerializeField] private LayerMask lookableLayer;

        private Quaternion m_RestLocalRotation;
        private Lookable?  m_CurrentTarget;
        private Vector3    m_LastLookPosition;
        private float      m_Weight;
        private float      m_DetectionTimer;

        private readonly Collider[] m_OverlapResults = new Collider[16];

        private void Awake() => m_RestLocalRotation = headBone.localRotation;

        private void Update()
        {
            m_DetectionTimer -= Time.deltaTime;
            if (m_DetectionTimer > 0f) return;

            m_DetectionTimer = detectionInterval;
            int count = Physics.OverlapSphereNonAlloc(
                transform.parent.position, detectionRadius, m_OverlapResults, lookableLayer);
            m_CurrentTarget = SelectBest(
                m_OverlapResults, count, transform.parent.position, transform.parent.forward, maxAngle);
        }

        private void LateUpdate()
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

            if (m_Weight <= 0f)
            {
                headBone.localRotation = m_RestLocalRotation;
                return;
            }

            Vector3 dir = m_LastLookPosition - headBone.position;
            if (dir.sqrMagnitude < 0.001f)
            {
                headBone.localRotation = m_RestLocalRotation;
                return;
            }

            Quaternion worldRest = headBone.parent.rotation * m_RestLocalRotation;
            headBone.rotation = Quaternion.Slerp(
                worldRest,
                Quaternion.LookRotation(dir) * Quaternion.Euler(lookAxisOffset),
                m_Weight);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var origin = transform.parent != null ? transform.parent.position : transform.position;
            var fwd    = transform.parent != null ? transform.parent.forward : transform.forward;

            UnityEngine.Gizmos.color = new UnityEngine.Color(1f, 1f, 0f, 0.15f);
            UnityEngine.Gizmos.DrawWireSphere(origin, detectionRadius);

            UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
            var leftDir  = UnityEngine.Quaternion.Euler(0f, -maxAngle, 0f) * fwd;
            var rightDir = UnityEngine.Quaternion.Euler(0f,  maxAngle, 0f) * fwd;
            UnityEngine.Gizmos.DrawRay(origin, fwd      * detectionRadius);
            UnityEngine.Gizmos.DrawRay(origin, leftDir  * detectionRadius);
            UnityEngine.Gizmos.DrawRay(origin, rightDir * detectionRadius);

            if (m_Weight > 0.01f && headBone != null)
            {
                UnityEngine.Gizmos.color = UnityEngine.Color.cyan;
                UnityEngine.Gizmos.DrawLine(headBone.position, m_LastLookPosition);
                UnityEngine.Gizmos.DrawSphere(m_LastLookPosition, 0.04f);
            }
        }
#endif

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
