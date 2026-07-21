#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public enum DoorMapState
    {
        Unknown  = 0,
        Locked   = 1,
        Unlocked = 2,
    }

    public sealed class DoorStateRegistry
    {
        private readonly Dictionary<string, DoorMapState> state = new();

        [Preserve]
        public DoorStateRegistry() { }

        public bool IsUnlocked(string doorId)
            => GetMapState(doorId) == DoorMapState.Unlocked;

        public void SetUnlocked(string doorId) => MarkUnlocked(doorId);

        public void MarkLocked(string doorId)
        {
            if (GetMapState(doorId) == DoorMapState.Unlocked)
                return;

            this.state[doorId] = DoorMapState.Locked;
        }

        public void MarkUnlocked(string doorId)
            => this.state[doorId] = DoorMapState.Unlocked;

        public DoorMapState GetMapState(string doorId)
            => this.state.TryGetValue(doorId, out var value) ? value : DoorMapState.Unknown;

        public IReadOnlyDictionary<string, DoorMapState> GetState() => this.state;

        public void LoadState(IReadOnlyDictionary<string, DoorMapState> saved)
        {
            this.state.Clear();
            foreach (var (k, v) in saved)
                this.state[k] = v;
        }
    }
}
