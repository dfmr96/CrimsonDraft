#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>The floors the MAP tab can show, in MapDataSet order (index 0 = top floor), and
    /// which one is on screen. Steps clamp at both ends.</summary>
    public sealed class MapFloors
    {
        private readonly IReadOnlyList<MapData> available;
        private int index;

        public MapFloors(IReadOnlyList<MapData> available, MapData? playerFloor)
        {
            this.available = available;
            int playerIndex = playerFloor == null ? -1 : IndexOf(available, playerFloor);
            this.index = playerIndex >= 0 ? playerIndex : 0;
        }

        public MapData? Current => this.available.Count > 0 ? this.available[this.index] : null;
        public bool     HasUp   => this.available.Count > 0 && this.index > 0;
        public bool     HasDown => this.index < this.available.Count - 1;

        public bool Step(int direction)
        {
            int next = this.index + direction;
            if (direction == 0 || next < 0 || next >= this.available.Count)
                return false;

            this.index = next;
            return true;
        }

        /// <summary>Known floors in set order. The player's own floor is always listed (appended
        /// last when the set doesn't contain it), so the tab never loses the floor you stand on.</summary>
        public static IReadOnlyList<MapData> Available(
            IEnumerable<MapData?> maps,
            RoomStateRegistry rooms,
            KnownMapsRegistry knownMaps,
            MapData? playerFloor = null)
        {
            var result = maps
                .Where(map => map != null && MapStateResolver.IsDeckKnown(map, rooms, knownMaps))
                .Select(map => map!)
                .ToList();

            if (playerFloor != null && IndexOf(result, playerFloor) < 0)
                result.Add(playerFloor);

            return result;
        }

        private static int IndexOf(IReadOnlyList<MapData> maps, MapData target)
        {
            for (int i = 0; i < maps.Count; i++)
                if (ReferenceEquals(maps[i], target))
                    return i;
            return -1;
        }
    }
}
