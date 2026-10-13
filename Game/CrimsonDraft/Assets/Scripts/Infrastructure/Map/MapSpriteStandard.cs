#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>Map room sprites share one pixels-per-unit and filter mode. The standard of a
    /// floor is whatever most of its sprites already use; the Map Editor flags the rest.</summary>
    public static class MapSpriteStandard
    {
        public static (float PixelsPerUnit, FilterMode Filter)? Majority(IEnumerable<Sprite?> sprites)
        {
            var group = sprites
                .Where(sprite => sprite != null)
                .GroupBy(sprite => (sprite!.pixelsPerUnit, sprite.texture.filterMode))
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return group == null ? null : group.Key;
        }

        public static bool Matches(Sprite sprite, (float PixelsPerUnit, FilterMode Filter) standard)
            => Mathf.Approximately(sprite.pixelsPerUnit, standard.PixelsPerUnit)
               && sprite.texture.filterMode == standard.Filter;
    }
}
