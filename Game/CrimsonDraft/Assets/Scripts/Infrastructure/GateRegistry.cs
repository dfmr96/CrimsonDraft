#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    /// <summary>
    /// Completed generic gates: one-shot things in the world that only need "this happened" (a beeper
    /// receiver that caught its code...) and have no richer state of their own. Sockets, puzzles and
    /// radio calls keep their own registries. Cross-scene and save-backed like the other registries.
    /// Ids are bare (no "gate:" prefix -- that belongs to Navigation's GateIds).
    /// </summary>
    public sealed class GateRegistry
    {
        private readonly HashSet<string> completed = new();

        [Preserve]
        public GateRegistry() { }

        public event Action? Changed;

        public IReadOnlyCollection<string> CompletedIds => this.completed;

        public bool IsCompleted(string gateId) =>
            !string.IsNullOrEmpty(gateId) && this.completed.Contains(gateId);

        public void SetCompleted(string gateId)
        {
            if (string.IsNullOrEmpty(gateId) || !this.completed.Add(gateId)) return;
            Changed?.Invoke();
        }

        public void LoadState(IEnumerable<string> saved)
        {
            this.completed.Clear();
            foreach (var id in saved)
                if (!string.IsNullOrEmpty(id)) this.completed.Add(id);
        }

        public void ClearAll() => this.completed.Clear();
    }
}
