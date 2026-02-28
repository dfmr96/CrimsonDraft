#nullable enable

using Cysharp.Threading.Tasks;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface ISceneTransitionService
    {
        bool IsInCombat { get; }
        UniTask StartCombatAsync(string encounterId);
    }
}
