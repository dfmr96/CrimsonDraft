using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class HitSphere : HitShape
    {
        [SerializeField] Vector3 m_Center;
        [SerializeField] float m_Radius;
        [SerializeField] private LayerMask m_LayerMask;
        [SerializeField] bool m_ShowDebug;

        private Collider[] m_OverlapResults = new Collider[10];

        private void Awake()
        {
            Debug.Assert(m_Radius > 0, "HitSphere radius can't be negative, this might lead to incorrect behaviour", gameObject);
        }

        // --------------------------------------------------------------------

        public override void GetOverlappingDamageables(List<Damageable> damageables, bool showDebug)
        {
            damageables.Clear();

            var sphereOrigin = transform.TransformPoint(m_Center);

            int count = Physics.OverlapSphereNonAlloc(sphereOrigin, m_Radius, m_OverlapResults, m_LayerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; ++i)
            {
                if (m_OverlapResults[i].TryGetComponent(out Damageable d))
                    damageables.Add(d);
            }

            if (showDebug)
                DebugUtils.DrawSphere(sphereOrigin, transform.rotation, Vector3.one, m_Radius, damageables.Count > 0 ? Color.green : Color.red, 3);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            if (m_ShowDebug)
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
