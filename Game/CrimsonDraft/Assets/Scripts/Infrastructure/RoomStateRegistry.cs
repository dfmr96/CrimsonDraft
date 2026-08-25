#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public enum RoomMapState
    {
        Unknown = 0,
        Visited = 1,
    }

    public sealed class RoomStateRegistry
    {
        private readonly Dictionary<string, RoomMapState> state = new();

        [Preserve]
        public RoomStateRegistry() { }

        public RoomMapState GetState(string roomId)
            => this.state.TryGetValue(roomId, out var value) ? value : RoomMapState.Unknown;

        public void MarkVisited(string roomId)
        {
            if (GetState(roomId) == RoomMapState.Visited)
                return;

            this.state[roomId] = RoomMapState.Visited;
        }

        public IReadOnlyDictionary<string, RoomMapState> GetState() => this.state;

        public void LoadState(IReadOnlyDictionary<string, RoomMapState> saved)
        {
            this.state.Clear();
            foreach (var (key, value) in saved)
                this.state[key] = value;
        }

        public void ClearAll() => this.state.Clear();
    }
}
