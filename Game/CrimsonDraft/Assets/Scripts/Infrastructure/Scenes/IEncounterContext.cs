#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface IEncounterContext
    {
        string?          CurrentEncounterId { get; }
        ScriptableObject? EncounterAsset    { get; }
    }
}
