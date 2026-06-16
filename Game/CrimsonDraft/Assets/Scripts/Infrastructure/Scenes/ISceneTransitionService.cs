#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface ISceneTransitionService
    {
        bool IsInCombat { get; }
        UniTask StartCombatAsync(string encounterId, ScriptableObject? encounterAsset = null);
    }
}
