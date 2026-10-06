#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public static class OverlayCoverage
    {
        public const float CoveredAlphaThreshold = 0.5f;

        // A texture without Read/Write can't be sampled (GetPixel throws) -- treated as
        // uncovered so a mis-imported mask degrades to "no armor" instead of breaking the QTE.
        public static bool IsCovered(Sprite mask, float u, float v)
        {
            Texture2D? tex = mask.texture;
            if (tex == null || !tex.isReadable) return false;

            Vector2Int px = AimViewController.MapUvToTexturePixel(mask, u, v);
            return tex.GetPixel(px.x, px.y).a >= CoveredAlphaThreshold;
        }
    }
}
