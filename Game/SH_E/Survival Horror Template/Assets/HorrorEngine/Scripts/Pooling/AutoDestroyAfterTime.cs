using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class AutoDestroyAfterTime : MonoBehaviour
    {
        [SerializeField] float m_AfterTime;
        private PooledGameObject m_PooledGameObject;

        public UnityEvent OnDestroy;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_PooledGameObject = GetComponent<PooledGameObject>();
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            Invoke(nameof(DestroyAfterTime), m_AfterTime);
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            CancelInvoke();
        }

        // --------------------------------------------------------------------

        private void DestroyAfterTime()
        {
            OnDestroy?.Invoke();

            if (m_PooledGameObject)
            {
                if (!m_PooledGameObject.IsInPool)
                    m_PooledGameObject.ReturnToPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}