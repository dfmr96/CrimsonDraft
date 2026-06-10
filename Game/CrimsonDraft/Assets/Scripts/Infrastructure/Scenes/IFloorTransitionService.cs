#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface IFloorTransitionService
    {
        UniTask TransitionToFloorAsync(
            string     fromScene,
            string     toScene,
            string     entryPointId,
            GameObject doorPrefab);
    }
}
