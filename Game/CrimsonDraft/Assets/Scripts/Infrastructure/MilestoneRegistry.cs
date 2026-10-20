#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    // Completed progression milestones (MilestoneGraph node ids). Filled by MilestoneTracker,
    // saved with the game. A milestone never becomes uncompleted.
    public sealed class MilestoneRegistry
    {
        private readonly HashSet<string> completed = new();

        [Preserve]
        public MilestoneRegistry() { }

        public event Action<string>? Completed;

        public IReadOnlyCollection<string> CompletedIds => this.completed;

        public bool IsCompleted(string milestoneId) =>
            !string.IsNullOrEmpty(milestoneId) && this.completed.Contains(milestoneId);

        public bool Complete(string milestoneId)
        {
            if (string.IsNullOrEmpty(milestoneId) || !this.completed.Add(milestoneId)) return false;
            Completed?.Invoke(milestoneId);
            return true;
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
