using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace HorrorEngine
{
    [System.Serializable]
    public class DirectionalDamageEntry
    {
        public Vector3 Direction;
        public float Angle;
        public UnityEvent OnDamageEvent;
    }

    public class OnDirectionalDamageCallback : MonoBehaviour
    {
        [SerializeField] private List<DirectionalDamageEntry> m_Directions;

        private Health m_Health;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Health = GetComponentInParent<Health>();
            Damageable damageable = GetComponent<Damageable>();
            if (damageable != null)
            {
                damageable.OnDamage.AddListener(OnDamage);
            }
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            Damageable damageable = GetComponent<Damageable>();
            if (damageable != null)
            {
                damageable.OnDamage.RemoveListener(OnDamage);
            }
        }

        // --------------------------------------------------------------------

        private void OnDamage(Vector3 impactPoint, Vector3 impactDir)
        {
            if (m_Health == null || m_Health.IsDead)
            {
                return;
            }

            foreach (var entry in m_Directions)
            {
                float entryAngle = Vector3.Angle(transform.TransformDirection(entry.Direction), -impactDir);
                if (entryAngle <= entry.Angle)
                {
                    entry.OnDamageEvent.Invoke();
                    break;
                }
            }
        }
    }
}
