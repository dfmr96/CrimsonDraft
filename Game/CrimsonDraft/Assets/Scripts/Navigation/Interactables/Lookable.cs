#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class Lookable : MonoBehaviour
    {
        [SerializeField] private Vector3 offset;
        [SerializeField] private int priority;

        public int Priority => priority;
        public Vector3 LookPosition => transform.TransformPoint(offset);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
            UnityEngine.Gizmos.DrawLine(transform.position, LookPosition);
            UnityEngine.Gizmos.DrawSphere(LookPosition, 0.05f);
        }
#endif
    }
}
