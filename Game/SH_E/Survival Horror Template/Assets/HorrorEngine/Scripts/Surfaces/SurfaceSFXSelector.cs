using UnityEngine;

namespace HorrorEngine
{
    [System.Serializable]
    public class SurfaceSFXEntry
    {
        public SurfaceType Surface;
        public SoundCue[] Cues;
    }

    [CreateAssetMenu(menuName = "Horror Engine/Surfaces/Surface SFX Selector")]
    public class SurfaceSFXSelector : SFXSelector
    {
        [SerializeField] private SurfaceSFXEntry[] m_SurfaceEffects;

        public override SoundCue Select(SoundEffectPlayer player)
        {
            var surfaceDet = player.GetComponentInParent<SurfaceDetector>();
            if (surfaceDet && surfaceDet.CurrentSurface)
            {
                foreach (var entry in m_SurfaceEffects)
                {
                    if (entry.Surface == surfaceDet.CurrentSurface)
                    {
                        return entry.Cues[Random.Range(0, entry.Cues.Length)];
                    }
                }
            }

            return base.Select(player);
        }
    }
}