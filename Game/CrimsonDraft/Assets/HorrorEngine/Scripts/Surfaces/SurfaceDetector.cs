using UnityEngine;

namespace HorrorEngine
{
    public class SurfaceDetector : MonoBehaviour
    {
        [SerializeField] SurfaceType m_DefaultSurface;
        
        public SurfaceType CurrentSurface 
        { 
            get
            {
                return m_SurfaceOverride ? m_SurfaceOverride : m_Surface;
            }
            private set
            {
                m_Surface = value;
            }
        }

        private GroundDetector m_GroundDetector;

        private SurfaceType m_Surface;
        private SurfaceType m_SurfaceOverride;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_GroundDetector = this.GetComponentInParent<GroundDetector>();
            m_GroundDetector.OnGroundChanged.AddListener(OnGroundChanged);

            CurrentSurface = m_DefaultSurface;
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            m_GroundDetector.OnGroundChanged.RemoveListener(OnGroundChanged);
        }

        // --------------------------------------------------------------------

        private void OnGroundChanged(Collider ground)
        {
            if (ground.TryGetComponent(out Surface surface))
            {
                CurrentSurface = surface.Type;
            }
            else
            {
                CurrentSurface = m_DefaultSurface;
            }
        }

        // --------------------------------------------------------------------

        public void SetOverride(SurfaceType surface)
        {
            m_SurfaceOverride = surface;
        }

        // --------------------------------------------------------------------

        public void ClearOverride()
        {
            m_SurfaceOverride = null;
        }

    }
}