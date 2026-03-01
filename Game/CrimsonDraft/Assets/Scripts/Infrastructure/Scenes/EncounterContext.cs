#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class EncounterContext : IEncounterContext
    {
        public string? CurrentEncounterId { get; private set; }

        [Preserve]
        public EncounterContext() { }

        public void Set(string encounterId) => this.CurrentEncounterId = encounterId;
    }
}
