#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class DoorStateRegistry
    {
        private readonly Dictionary<string, bool> state = new();

        [Preserve]
        public DoorStateRegistry() { }

        public bool IsUnlocked(string doorId)
            => this.state.TryGetValue(doorId, out var v) && v;

        public void SetUnlocked(string doorId)
            => this.state[doorId] = true;

        public IReadOnlyDictionary<string, bool> GetState() => this.state;

        public void LoadState(IReadOnlyDictionary<string, bool> saved)
        {
            this.state.Clear();
            foreach (var (k, v) in saved)
                this.state[k] = v;
        }
    }
}
