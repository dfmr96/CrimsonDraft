using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(Projectile))]
    public class ProjectileUtils : MonoBehaviour
    {
        private Projectile m_Projectile;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Projectile = GetComponent<Projectile>();
        }

        // --------------------------------------------------------------------

        public void Stop() 
        {
            if (!m_Projectile) // Edge case where this is called before Awake (when a projectile is already colliding a wall at start)
                m_Projectile = GetComponent<Projectile>();

            m_Projectile.enabled = false;
        }

        // --------------------------------------------------------------------

        public void Kill()
        {
            m_Projectile.GetComponent<PooledGameObject>().ReturnToPool();
        }

        // --------------------------------------------------------------------

        public void AttachToDamageable(AttackInfo attackInfo)
        {
            Stop();
            transform.SetParent(attackInfo.Damageable.transform);
        }

        // --------------------------------------------------------------------

        public void AttachToCollision(Collision collision)
        {
            Stop();
            transform.SetParent(collision.transform);
        }
    }
}