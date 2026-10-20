#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    // Positions of pushables that were explicitly told to keep where they are (see
    // PushableObject.SavePosition). Pushables not recorded here go back to their home point every
    // time their room is entered.
    public sealed class PushableStateRegistry
    {
        private readonly Dictionary<string, Vector3> positions = new();

        [Preserve]
        public PushableStateRegistry() { }

        public void Record(string pushableId, Vector3 position)
        {
            if (string.IsNullOrEmpty(pushableId)) return;
            this.positions[pushableId] = position;
        }

        public bool TryGet(string pushableId, out Vector3 position)
        {
            position = default;
            return !string.IsNullOrEmpty(pushableId) && this.positions.TryGetValue(pushableId, out position);
        }

        public IReadOnlyDictionary<string, Vector3> GetState() => this.positions;

        public void LoadState(IReadOnlyDictionary<string, Vector3> saved)
        {
            this.positions.Clear();
            foreach (var (key, value) in saved)
                this.positions[key] = value;
        }

        public void ClearAll() => this.positions.Clear();
    }
}
