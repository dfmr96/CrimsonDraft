#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Axis-aligned bounds of a floor in map pixels. A room's Position is the
    /// bottom-left corner of its footprint, so every edge lands on a whole pixel; odd quarter
    /// turns swap the footprint's width and height.</summary>
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
                var rect = RectOf(visual);
                min = Vector2.Min(min, rect.min);
                max = Vector2.Max(max, rect.max);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>The whole floor's frame: every room that has art, discovered or not, sized by
        /// the larger of its two sprites. Framing the MAP tab on this keeps the zoom and every
        /// room's place fixed while the player discovers rooms.</summary>
        public static Rect Floor(MapData map)
        {
            bool any = false;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var room in map.Rooms)
            {
                var size = Vector2.Max(
                    room.IncompleteSprite != null ? SizeOf(room.IncompleteSprite, room.QuarterTurns) : Vector2.zero,
                    room.CompleteSprite   != null ? SizeOf(room.CompleteSprite,   room.QuarterTurns) : Vector2.zero);
                if (size == Vector2.zero)
                    continue;

                any = true;
                min = Vector2.Min(min, room.Position);
                max = Vector2.Max(max, room.Position + size);
            }

            return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : Rect.zero;
        }

        public static RectInt RectOf(MapRoomVisual visual)
        {
            var size = SizeOf(visual.Sprite, visual.QuarterTurns);
            return new RectInt(visual.Position, new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y)));
        }

        public static Vector2 SizeOf(Sprite sprite, int quarterTurns)
        {
            var size = sprite.rect.size;
            return quarterTurns % 2 != 0 ? new Vector2(size.y, size.x) : size;
        }
    }
}
