using UnityEngine;

namespace HorrorEngine 
{
    public class ObjectInstantiator : MonoBehaviour
    {
        [SerializeField] private GameObject Prefab;
        [SerializeField] private ObjectInstantiationSettings Settings;

        private SocketController m_SocketController;

        private void Awake()
        {
            m_SocketController = GetComponentInParent<SocketController>();
        }

        public void Instantiate() // This version needs to stay as is since it's called from UnityEvents
        {
            GameObject instance;
            Instantiate(null, out instance);
        }

        public void Instantiate(GameObject prefab, out GameObject instance)
        {
            instance = null;

            if (prefab)
                Prefab = prefab;

            if (Prefab)
            {
               instance = Settings.Instantiate(Prefab, m_SocketController);
            }
        }
    }
}