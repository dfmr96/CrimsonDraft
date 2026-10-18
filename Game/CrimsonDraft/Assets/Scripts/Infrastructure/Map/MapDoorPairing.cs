#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace CrimsonDraft.Infrastructure.Map
{
    /// <summary>One half of a doorway as the scene bake sees it: the door, the room it sits in
    /// and the room it leads to (null when it doesn't lead to a room on this floor).</summary>
    public readonly struct MapDoorEnd
    {
        public MapDoorEnd(string doorId, string roomId, string? destinationRoomId)
        {
            this.DoorId            = doorId;
            this.RoomId            = roomId;
            this.DestinationRoomId = destinationRoomId;
        }

        public string  DoorId            { get; }
        public string  RoomId            { get; }
        public string? DestinationRoomId { get; }
    }

    /// <summary>Matches each door half excluded from the map with the drawn half of the same
    /// doorway: the drawn door in the room the excluded one leads to, leading back. Door ids of
    /// a pair follow no naming rule (DR_187 / DR_32B), so the rooms are the only link.</summary>
    public static class MapDoorPairing
    {
        public sealed class Result
        {
            private readonly Dictionary<string, List<MapDoorSide>> otherSides = new();

            public List<MapDoorEnd> Unpaired  { get; } = new();
            public List<MapDoorEnd> Ambiguous { get; } = new();

            public IReadOnlyList<MapDoorSide> OtherSidesOf(string drawnDoorId)
                => this.otherSides.TryGetValue(drawnDoorId, out var sides) ? sides : Array.Empty<MapDoorSide>();

            internal void Add(string drawnDoorId, MapDoorEnd excluded)
            {
                if (!this.otherSides.TryGetValue(drawnDoorId, out var sides))
                    this.otherSides[drawnDoorId] = sides = new List<MapDoorSide>();
                sides.Add(new MapDoorSide { DoorId = excluded.DoorId, RoomId = excluded.RoomId });
            }
        }

        public static Result Pair(IReadOnlyList<MapDoorEnd> drawn, IReadOnlyList<MapDoorEnd> excluded)
        {
            var result = new Result();
            foreach (var end in excluded)
            {
                var matches = end.DestinationRoomId == null
                    ? new List<MapDoorEnd>()
                    : drawn.Where(d => d.RoomId == end.DestinationRoomId && d.DestinationRoomId == end.RoomId).ToList();

                if (matches.Count == 1)
                    result.Add(matches[0].DoorId, end);
                else if (matches.Count == 0)
                    result.Unpaired.Add(end);
                else
                    result.Ambiguous.Add(end);
            }
            return result;
        }
    }
}
