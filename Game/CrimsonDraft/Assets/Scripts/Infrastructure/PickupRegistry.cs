#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class PickupRegistry
    {
        private readonly HashSet<string> collected = new();

        [Preserve]
        public PickupRegistry() { }

        public bool IsCollected(string pickupId)  => this.collected.Contains(pickupId);
        public void SetCollected(string pickupId) => this.collected.Add(pickupId);

        public IReadOnlyCollection<string> CollectedIds => this.collected;

        public void LoadState(IEnumerable<string> saved)
        {
            this.collected.Clear();
            foreach (var id in saved)
                this.collected.Add(id);
        }

        public void ClearAll() => this.collected.Clear();
    }
}
