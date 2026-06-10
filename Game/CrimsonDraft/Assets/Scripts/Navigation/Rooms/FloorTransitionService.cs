#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class FloorTransitionService : IFloorTransitionService
    {
        private const string TransitionSceneName = "DoorTransition";

        private readonly IInputService        inputService;
        private readonly RoomTransitionContext roomTransitionContext;
        private readonly SceneEntryContext     sceneEntryContext;

        private bool isTransitioning;

        [Preserve]
        public FloorTransitionService(
            IInputService        inputService,
            RoomTransitionContext roomTransitionContext,
            SceneEntryContext     sceneEntryContext)
        {
            this.inputService         = inputService;
            this.roomTransitionContext = roomTransitionContext;
            this.sceneEntryContext     = sceneEntryContext;
        }

        public async UniTask TransitionToFloorAsync(
            string     fromScene,
            string     toScene,
            string     entryPointId,
            GameObject doorPrefab)
        {
            if (this.isTransitioning) return;
            this.isTransitioning = true;

            this.inputService.SwitchToDoorTransition();
            this.sceneEntryContext.SetPendingEntry(entryPointId);

            var tcs = new UniTaskCompletionSource();
            this.roomTransitionContext.Set(
                doorPrefab,
                this.inputService.DoorTransitionSkip,
                () => tcs.TrySetResult());

            await SceneManager.LoadSceneAsync(TransitionSceneName, LoadSceneMode.Additive).ToUniTask();
            await tcs.Task;

            await SceneManager.UnloadSceneAsync(fromScene).ToUniTask();
            await SceneManager.LoadSceneAsync(toScene, LoadSceneMode.Additive).ToUniTask();
            await SceneManager.UnloadSceneAsync(TransitionSceneName).ToUniTask();

            this.inputService.SwitchToGameplay();
            this.isTransitioning = false;
        }
    }
}
