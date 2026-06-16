#nullable enable

using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class EncounterContext : IEncounterContext
    {
        public string?           CurrentEncounterId { get; private set; }
        public ScriptableObject? EncounterAsset     { get; private set; }

        [Preserve]
        public EncounterContext() { }

        public void Set(string encounterId, ScriptableObject? asset)
        {
            this.CurrentEncounterId = encounterId;
            this.EncounterAsset     = asset;
        }
    }
}
