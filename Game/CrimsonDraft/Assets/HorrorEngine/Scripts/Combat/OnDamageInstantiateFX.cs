using UnityEngine;

namespace HorrorEngine
{
    public enum ImpactFXPoint
    {
        ImpactPoint,
        DamageableCenter
    }

    public class OnDamageInstantiateFX  : MonoBehaviour
    {
        [SerializeField] private GameObject m_ImpactVFX;
        [SerializeField] private ImpactFXPoint m_ImpactVFXPosition = ImpactFXPoint.ImpactPoint;
        [SerializeField] private bool OnlyOnDeath;

        private Health m_Health;

        private void Awake()
        {
            m_Health = GetComponentInParent<Health>();
            GetComponent<Damageable>().OnDamage.AddListener(OnDamage);
        }

        private void OnDestroy()
        {
            GetComponent<Damageable>().OnDamage.RemoveListener(OnDamage);
        }

        void OnDamage(Vector3 impactPoint, Vector3 impactDir)
        {
            if (m_ImpactVFX && (!OnlyOnDeath || m_Health.IsDead))
            {
                var pooledVFX = GameObjectPool.Instance.GetFromPool(m_ImpactVFX, null);
                pooledVFX.transform.position = m_ImpactVFXPosition == ImpactFXPoint.ImpactPoint ? impactPoint : transform.position;
                pooledVFX.transform.rotation = Quaternion.LookRotation(-impactDir, Vector3.up);
                pooledVFX.gameObject.SetActive(true);
            }
        }
    }
}
