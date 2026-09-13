#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine.EventSystems;
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
        private const string CombatSceneName   = "Combat";
        private const string MainMenuSceneName = "MainMenu";

        private readonly IInputService inputService;
        private readonly IPublisher<CombatStartedEvent> combatStartedPublisher;
        private readonly ISubscriber<CombatEndedEvent> combatEndedSubscriber;
        private readonly EncounterContext encounterContext;
        private readonly ICameraService cameraService;
        private readonly ScreenFader screenFader;
        private readonly GameOverView gameOverView;

        private IDisposable? combatEndedSubscription;
        private bool isInCombat;

        public bool IsInCombat => this.isInCombat;

        [Preserve]
        public SceneTransitionService(
            IInputService inputService,
            IPublisher<CombatStartedEvent> combatStartedPublisher,
            ISubscriber<CombatEndedEvent> combatEndedSubscriber,
            EncounterContext encounterContext,
            ICameraService cameraService,
            ScreenFader screenFader,
            GameOverView gameOverView)
        {
            this.inputService          = inputService;
            this.combatStartedPublisher = combatStartedPublisher;
            this.combatEndedSubscriber = combatEndedSubscriber;
            this.encounterContext      = encounterContext;
            this.cameraService         = cameraService;
            this.screenFader           = screenFader;
            this.gameOverView          = gameOverView;
        }

        void IInitializable.Initialize()
        {
            this.gameOverView.Hide();
            this.combatEndedSubscription = this.combatEndedSubscriber.Subscribe(OnCombatEnded);
        }

        public async UniTask StartCombatAsync(string encounterId, UnityEngine.ScriptableObject? encounterAsset = null, bool operatorsStartFull = false)
        {
            if (this.isInCombat)
                return;

            this.isInCombat = true;
            this.combatStartedPublisher.Publish(new CombatStartedEvent { EncounterId = encounterId });
            this.encounterContext.Set(encounterId, encounterAsset, operatorsStartFull);
            this.inputService.SwitchToCombat();

            await this.screenFader.FadeOutAsync();
            await SceneManager.LoadSceneAsync(CombatSceneName, LoadSceneMode.Additive).ToUniTask();
            this.cameraService.ActivateCombatCamera();
            await this.screenFader.FadeInAsync();
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            EndCombatAsync(ev.Victory).Forget();
        }

        private async UniTask EndCombatAsync(bool victory)
        {
            await this.screenFader.FadeOutAsync();
            this.cameraService.ActivateNavigationCamera();

            var scene = SceneManager.GetSceneByName(CombatSceneName);
            if (scene.isLoaded)
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();

            this.isInCombat = false;

            if (victory)
            {
                this.inputService.SwitchToGameplay();
                await this.screenFader.FadeInAsync();
            }
            else
            {
                await ShowGameOverAsync();
            }
        }

        private async UniTask ShowGameOverAsync()
        {
            this.inputService.SwitchToUI();
            this.gameOverView.Show();
            EventSystem.current.SetSelectedGameObject(this.gameOverView.ReturnToMenuButton.gameObject);

            var tcs = new UniTaskCompletionSource();
            void OnReturnToMenuClicked() => tcs.TrySetResult();
            this.gameOverView.ReturnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
            try
            {
                await tcs.Task;
            }
            finally
            {
                this.gameOverView.ReturnToMenuButton.onClick.RemoveListener(OnReturnToMenuClicked);
            }

            this.gameOverView.Hide();
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            await this.screenFader.FadeInAsync();
        }

        void IDisposable.Dispose()
        {
            this.combatEndedSubscription?.Dispose();
        }
    }
}
