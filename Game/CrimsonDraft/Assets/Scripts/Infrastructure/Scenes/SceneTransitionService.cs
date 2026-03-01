#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class SceneTransitionService : ISceneTransitionService, IInitializable, IDisposable
    {
        private const string CombatSceneName = "Combat";

        private readonly IInputService inputService;
        private readonly ISubscriber<CombatEndedEvent> combatEndedSubscriber;
        private readonly EncounterContext encounterContext;
        private readonly ICameraService cameraService;
        private readonly ScreenFader screenFader;

        private IDisposable? combatEndedSubscription;
        private bool isInCombat;

        public bool IsInCombat => this.isInCombat;

        [Preserve]
        public SceneTransitionService(
            IInputService inputService,
            ISubscriber<CombatEndedEvent> combatEndedSubscriber,
            EncounterContext encounterContext,
            ICameraService cameraService,
            ScreenFader screenFader)
        {
            this.inputService          = inputService;
            this.combatEndedSubscriber = combatEndedSubscriber;
            this.encounterContext      = encounterContext;
            this.cameraService         = cameraService;
            this.screenFader           = screenFader;
        }

        void IInitializable.Initialize()
        {
            this.combatEndedSubscription = this.combatEndedSubscriber.Subscribe(OnCombatEnded);
        }

        public async UniTask StartCombatAsync(string encounterId)
        {
            if (this.isInCombat)
                return;

            this.isInCombat = true;
            this.encounterContext.Set(encounterId);
            this.inputService.SwitchToCombat();

            await this.screenFader.FadeOutAsync();
            await SceneManager.LoadSceneAsync(CombatSceneName, LoadSceneMode.Additive).ToUniTask();
            this.cameraService.ActivateCombatCamera();
            await this.screenFader.FadeInAsync();
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            EndCombatAsync().Forget();
        }

        private async UniTask EndCombatAsync()
        {
            await this.screenFader.FadeOutAsync();
            this.cameraService.ActivateNavigationCamera();

            var scene = SceneManager.GetSceneByName(CombatSceneName);
            if (scene.isLoaded)
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();

            this.isInCombat = false;
            this.inputService.SwitchToGameplay();
            await this.screenFader.FadeInAsync();
        }

        void IDisposable.Dispose()
        {
            this.combatEndedSubscription?.Dispose();
        }
    }
}
