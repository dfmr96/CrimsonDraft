#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>One sprite per door state: Unknown (never tried, grey), Locked (tried and
    /// refused, red), Unlocked (opened, blue).</summary>
    [Serializable]
    public sealed class MapDoorSpriteSet
    {
        public Sprite? Unknown;
        public Sprite? Locked;
        public Sprite? Unlocked;

        public Sprite? For(DoorMapState state) => state switch
        {
            DoorMapState.Locked   => this.Locked,
            DoorMapState.Unlocked => this.Unlocked,
            _                     => this.Unknown,
        };
    }

    /// <summary>The door sprites every floor shares, assigned once on the MapDataSet.</summary>
    [Serializable]
    public sealed class MapDoorSprites
    {
        public MapDoorSpriteSet Horizontal = new();
        public MapDoorSpriteSet Vertical   = new();

        public Sprite? For(DoorMapState state, bool vertical)
            => (vertical ? this.Vertical : this.Horizontal).For(state);
    }
}
