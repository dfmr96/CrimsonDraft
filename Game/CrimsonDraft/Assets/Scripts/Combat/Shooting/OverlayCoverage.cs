#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public static class OverlayCoverage
    {
        public const float CoveredThreshold = 0.5f;

        // Masks are black & white: a pixel is covered when it's light (white) and opaque. Black
        // and transparent are both uncovered, so a mask exported with a transparent background
        // instead of black still works. A texture without Read/Write can't be sampled (GetPixel
        // throws) -- treated as uncovered so a mis-imported mask degrades to "no armor".
        public static bool IsCovered(Sprite mask, float u, float v)
        {
            Texture2D? tex = mask.texture;
            if (tex == null || !tex.isReadable) return false;

            if (!AimViewController.TryMapUvToSpritePixel(
                    mask.rect, mask.textureRect, mask.textureRectOffset, tex.width, tex.height, u, v, out Vector2Int px))
                return false;

            Color pixel = tex.GetPixel(px.x, px.y);
            return pixel.grayscale >= CoveredThreshold && pixel.a >= CoveredThreshold;
        }
    }
}
