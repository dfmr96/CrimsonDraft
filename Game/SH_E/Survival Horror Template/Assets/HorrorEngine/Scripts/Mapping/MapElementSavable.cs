using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(SavableObjectState))]
    public class MapElementSavable : MonoBehaviour
    {
        private SavableObjectState m_SavableState;
        private ObjectUniqueId m_UniqueId;

        // --------------------------------------------------------------------

        private void CacheComponents()
        {
            if (!m_SavableState)
                m_SavableState = GetComponent<SavableObjectState>();

            if (!m_UniqueId)
                m_UniqueId = GetComponent<ObjectUniqueId>();
        }

        // --------------------------------------------------------------------

        public string GetId()
        {
            if (!m_UniqueId)
                CacheComponents();

            return m_UniqueId.Id;
        }

        // --------------------------------------------------------------------

        public void ActivateOnMap()
        {
            if (!m_SavableState)
                CacheComponents();
            
            gameObject.SetActive(true);
            m_SavableState.SaveState();
        }

        // --------------------------------------------------------------------

        public void DeactivateOnMap()
        {
            if (!m_SavableState)
                CacheComponents();

            gameObject.SetActive(false);
            m_SavableState.SaveState();
        }
    }
}