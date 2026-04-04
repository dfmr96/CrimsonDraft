using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public abstract class HitShape : MonoBehaviour
    {
        public abstract void GetOverlappingDamageables(List<Damageable> damageables, string debugCategory);
    }

    public class HitBox : HitShape
    {
        [SerializeField] private Vector3 m_Center;
        [SerializeField] private Vector3 m_Size = Vector3.one;
        [SerializeField] private LayerMask m_LayerMask;
        [SerializeField] private bool m_ShowGizmo;

        private Collider[] m_OverlapResults = new Collider[10];

        private void Awake()
        {
            Debug.Assert(m_Size.x > 0 || m_Size.y > 0 || m_Size.z > 0, "HitBox size is not valid, this might lead to incorrect behaviour. Make sure all size axis are greater than 0", gameObject);
        }

        // --------------------------------------------------------------------

        public override void GetOverlappingDamageables(List<Damageable> damageables, string debugCategory)
        {
            damageables.Clear();

            var boxOrigin = transform.TransformPoint(m_Center);
            var scaledSize = new Vector3(m_Size.x * transform.lossyScale.x, m_Size.y * transform.lossyScale.y, m_Size.z * transform.lossyScale.z);

            int count = Physics.OverlapBoxNonAlloc(boxOrigin, scaledSize * 0.5f, m_OverlapResults, transform.rotation, m_LayerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; ++i)
            {
                if (m_OverlapResults[i].TryGetComponent(out Damageable d))
                    damageables.Add(d);
            }

            RuntimeDebug.DrawWireBox(boxOrigin, scaledSize, transform.rotation, damageables.Count > 0 ? Color.green : Color.red, Vector3.one, 3f, debugCategory);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            if (m_ShowGizmo)
            {
                Gizmos.color = Color.red;

                Matrix4x4 transformMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

                // Set Gizmos matrix
                Gizmos.matrix = transformMatrix;
                Gizmos.DrawWireCube(m_Center, m_Size);

                Gizmos.matrix = Matrix4x4.identity;

                Gizmos.color = Color.white;
            }
        }

    }
}