using UnityEngine;

namespace HorrorEngine
{
    public class Aimable : MonoBehaviour
    {
        public Vector3 AimingOffset = Vector3.zero;

        [Tooltip("Use this to indicate multiple points to be used for visibility checks")]
        public Vector3[] VisibilityTracePoints = { Vector3.zero };

        public Vector3 AimingPoint => transform.TransformPoint(AimingOffset);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.TransformPoint(AimingOffset), 0.25f);

            Gizmos.color = Color.cyan;
            foreach(Vector3 v in VisibilityTracePoints)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(v), 0.125f);
            }
        }
    }
}
