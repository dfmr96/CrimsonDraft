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

        /// <summary>Debug teleport to another deck: same scene swap, no door animation, and the new
        /// scene starts in <paramref name="roomId"/>.</summary>
        UniTask TeleportToFloorAsync(string fromScene, string toScene, string roomId);
    }
}
