using UnityEngine;

namespace HorrorEngine
{
    [System.Serializable]
    public class SurfaceVFXEntry
    {
        public SurfaceType Surface;
        public GameObject Prefab;
    }

    [CreateAssetMenu(menuName = "Horror Engine/Surfaces/Surface VFX Selector")]
    public class SurfaceVFXSelector : VFXSelector
    {
        [SerializeField] private SurfaceVFXEntry[] m_SurfaceEffects;

        public override GameObject Select(VisualEffectPlayer player)
        {
            var surfaceDet = player.GetComponentInParent<SurfaceDetector>();
            if (surfaceDet && surfaceDet.CurrentSurface)
            {
                foreach (var entry in m_SurfaceEffects)
                {
                    if (entry.Surface == surfaceDet.CurrentSurface)
                    {
                        return entry.Prefab;
                    }
                }
            }

            return base.Select(player);
        }
    }
}