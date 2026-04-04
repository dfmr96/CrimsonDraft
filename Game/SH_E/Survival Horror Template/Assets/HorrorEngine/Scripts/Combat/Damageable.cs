using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class OnDamageEvent : UnityEvent<Vector3, Vector3> { }

    public class Damageable : MonoBehaviour
    {
        public Combatant Owner { get; private set; }

        public DamageableType Type;

        [Tooltip("Event fired before the damage is applied")]
        public OnDamageEvent OnPreDamage;
        [Tooltip("Event fired after the damage has been applied")]
        public OnDamageEvent OnDamage;

        [Tooltip("Priority of this damageable (higher is better)")]

        public int Priority = 0;

        private Health m_Health;

        // --------------------------------------------------------------------

        private void Awake()
        {
            Owner = GetComponentInParent<Combatant>();
            m_Health = GetComponentInParent<Health>();
        }

        // --------------------------------------------------------------------

        public void Damage(float damage, AttackInfo info)
        {
            if (!m_Health) // Edge case where this is called before Awake (e.g. when an attack is overlapping a damageable at start)
                m_Health = GetComponentInParent<Health>();

            if (!m_Health.IsDead)
            {
                if (damage > 0)
                {
                    OnPreDamage?.Invoke(info.ImpactPoint, info.ImpactDir);
                    m_Health.TakeDamage(damage, this, info.Attack);
                    OnDamage?.Invoke(info.ImpactPoint, info.ImpactDir);
                }
            }
        }
    }
}
