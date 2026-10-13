#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // A layer drawn over an enemy's aim silhouette (a vest now, weak-point blisters later).
    // The zone mask still decides where a pellet hit; the overlay's mask only adds a modifier
    // on top. Every sprite must share the base silhouette sprite's size and pivot. Visible
    // sprites are the art on a transparent background; mask sprites are black & white (white =
    // covered) and need Read/Write enabled (sampled with GetPixel).
    [CreateAssetMenu(fileName = "SilhouetteOverlay", menuName = "CrimsonDraft/Combat/Silhouette Overlay")]
    public sealed class SilhouetteOverlay : ScriptableObject
    {
        [SerializeField] private OverlayKind kind = OverlayKind.Armor;
        [SerializeField] private Sprite?     visibleSprite;
        [SerializeField] private Sprite?     maskSprite;
        [SerializeField] private Sprite?     staggeredVisibleSprite;
        [SerializeField] private Sprite?     staggeredMaskSprite;

        public OverlayKind Kind => this.kind;

        // null when that pose's art isn't assigned -- an overlay without art is neither drawn
        // nor blocking, so an asset created before its sprites exist is harmless.
        public ActiveOverlay? Resolve(bool staggered)
        {
            Sprite? visible = staggered ? this.staggeredVisibleSprite : this.visibleSprite;
            Sprite? mask    = staggered ? this.staggeredMaskSprite    : this.maskSprite;
            if (visible == null || mask == null) return null;
            return new ActiveOverlay(this.kind, visible, mask);
        }
    }
}
