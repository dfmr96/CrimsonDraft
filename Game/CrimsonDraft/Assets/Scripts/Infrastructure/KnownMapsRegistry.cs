#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class KnownMapsRegistry
    {
        private readonly HashSet<string> knownMaps = new();

        [Preserve]
        public KnownMapsRegistry() { }

        public bool IsKnown(string mapId) => this.knownMaps.Contains(mapId);

        public void MarkKnown(string mapId) => this.knownMaps.Add(mapId);

        public IReadOnlyCollection<string> GetState() => this.knownMaps;

        public void LoadState(IEnumerable<string> saved)
        {
            this.knownMaps.Clear();
            foreach (var mapId in saved)
                this.knownMaps.Add(mapId);
        }
    }
}
