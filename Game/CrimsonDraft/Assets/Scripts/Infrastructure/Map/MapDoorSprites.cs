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

        public Sprite? StairsHorizontalLeft;
        public Sprite? StairsHorizontalRight;
        public Sprite? StairsVerticalTop;
        public Sprite? StairsVerticalDown;
        public Sprite? ChuteDrop;

        public Sprite? For(DoorMapState state, bool vertical)
            => (vertical ? this.Vertical : this.Horizontal).For(state);

        /// <summary>The sprite a door draws: its stairs sprite when it is stairs, else the
        /// door sprite for its state.</summary>
        public Sprite? For(DoorMapState state, bool vertical, MapStairKind stairs) => stairs switch
        {
            MapStairKind.HorizontalLeft  => this.StairsHorizontalLeft,
            MapStairKind.HorizontalRight => this.StairsHorizontalRight,
            MapStairKind.VerticalTop     => this.StairsVerticalTop,
            MapStairKind.VerticalDown    => this.StairsVerticalDown,
            MapStairKind.ChuteDrop       => this.ChuteDrop,
            _                            => For(state, vertical),
        };
    }
}
