#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    /// <summary>
    /// Radio calls (Yarn node names) the player has already heard. Cross-scene and save-backed like
    /// the other world-state registries, so a call that played never triggers again after a load.
    /// </summary>
    public sealed class RadioCallRegistry
    {
        private readonly HashSet<string> heard = new();

        [Preserve]
        public RadioCallRegistry() { }

        public bool IsHeard(string nodeName)  => this.heard.Contains(nodeName);
        public void SetHeard(string nodeName) => this.heard.Add(nodeName);

        public IReadOnlyCollection<string> HeardIds => this.heard;

        public void LoadState(IEnumerable<string> saved)
        {
            this.heard.Clear();
            foreach (var id in saved)
                this.heard.Add(id);
        }

        public void ClearAll() => this.heard.Clear();
    }
}
