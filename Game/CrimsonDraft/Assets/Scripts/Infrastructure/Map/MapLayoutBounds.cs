#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Axis-aligned bounds of a floor in map pixels. Each sprite is centred on its
    /// Position; odd quarter turns swap its width and height.</summary>
    public static class MapLayoutBounds
    {
        public static Rect Compute(IReadOnlyList<MapRoomVisual> visuals)
        {
            if (visuals.Count == 0)
                return Rect.zero;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var visual in visuals)
            {
                var half   = SizeOf(visual.Sprite, visual.QuarterTurns) * 0.5f;
                var centre = (Vector2)visual.Position;
                min = Vector2.Min(min, centre - half);
                max = Vector2.Max(max, centre + half);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Vector2 SizeOf(Sprite sprite, int quarterTurns)
        {
            var size = sprite.rect.size;
            return quarterTurns % 2 != 0 ? new Vector2(size.y, size.x) : size;
        }
    }
}
