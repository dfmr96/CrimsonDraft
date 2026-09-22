#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class ItemSocketStateRegistry
    {
        private readonly Dictionary<string, bool[]> inserted = new();

        [Preserve]
        public ItemSocketStateRegistry() { }

        public bool[] GetInserted(string socketId)
            => this.inserted.TryGetValue(socketId, out var value) ? value : System.Array.Empty<bool>();

        public void SetInserted(string socketId, bool[] slots) => this.inserted[socketId] = slots;

        public IReadOnlyDictionary<string, bool[]> GetState() => this.inserted;

        public void LoadState(IReadOnlyDictionary<string, bool[]> saved)
        {
            this.inserted.Clear();
            foreach (var (key, value) in saved)
                this.inserted[key] = value;
        }

        public void ClearAll() => this.inserted.Clear();
    }
}
