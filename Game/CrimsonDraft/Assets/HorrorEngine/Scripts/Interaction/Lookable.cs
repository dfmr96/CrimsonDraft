using UnityEngine;

namespace HorrorEngine
{
    public class Lookable : MonoBehaviour
    {
        public Vector3 Offset;
        public float Priority;

        public Vector3 LookPosition => transform.TransformPoint(Offset);

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireCube(LookPosition, Vector3.one * 0.15f);
        }
    }
}