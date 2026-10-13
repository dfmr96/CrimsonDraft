#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // One overlay resolved for the enemy's current pose (standing or staggered): what the aim
    // view draws over the silhouette and what it samples to decide coverage.
    public readonly struct ActiveOverlay
    {
        public OverlayKind Kind          { get; }
        public Sprite      VisibleSprite { get; }
        public Sprite      MaskSprite    { get; }

        public ActiveOverlay(OverlayKind kind, Sprite visibleSprite, Sprite maskSprite)
        {
            this.Kind          = kind;
            this.VisibleSprite = visibleSprite;
            this.MaskSprite    = maskSprite;
        }
    }
}
