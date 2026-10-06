using UnityEngine;

namespace PreRenderBackgrounds
{
    public class ScrollingTarget : MonoBehaviour
    {
        private static ScrollingTarget m_Instance;
        public static ScrollingTarget Instance
        {
            get
            {
                if (!m_Instance || !m_Instance.isActiveAndEnabled)
                {
                    m_Instance = FindFirstObjectByType<ScrollingTarget>();
                }

                return m_Instance;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
}