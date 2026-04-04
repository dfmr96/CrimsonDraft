using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class HitSphere : HitShape
    {
        [SerializeField] Vector3 m_Center;
        [SerializeField] float m_Radius;
        [SerializeField] private LayerMask m_LayerMask;
        [SerializeField] bool m_ShowGizmo;

        private Collider[] m_OverlapResults = new Collider[10];

        private void Awake()
        {
            Debug.Assert(m_Radius > 0, "HitSphere radius can't be negative, this might lead to incorrect behaviour", gameObject);
        }

        // --------------------------------------------------------------------

        public override void GetOverlappingDamageables(List<Damageable> damageables, string debugCategory)
        {
            damageables.Clear();

            var sphereOrigin = transform.TransformPoint(m_Center);

            int count = Physics.OverlapSphereNonAlloc(sphereOrigin, m_Radius, m_OverlapResults, m_LayerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; ++i)
            {
                if (m_OverlapResults[i].TryGetComponent(out Damageable d))
                    damageables.Add(d);
            }

            RuntimeDebug.DrawWireSphere(sphereOrigin, m_Radius, damageables.Count > 0 ? Color.green : Color.red, Vector3.one, 3f, debugCategory);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            if (m_ShowGizmo)
            {
                Gizmos.color = Color.red;

                Matrix4x4 transformMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

                Gizmos.matrix = transformMatrix;
                Gizmos.DrawWireSphere(m_Center, m_Radius);
                Gizmos.matrix = Matrix4x4.identity;

                Gizmos.color = Color.white;
               
            }
        }
    }
}
