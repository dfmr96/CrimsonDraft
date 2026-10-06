#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
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
        private const float  HitStopDuration   = 0.12f; // beat de freeze-frame clásico (FF/Pokémon) al conectar el golpe que inicia combate

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

        public async UniTask StartCombatAsync(string encounterId, UnityEngine.ScriptableObject? encounterAsset = null, bool operatorsStartFull = false, Action? onScreenCovered = null)
        {
            if (this.isInCombat)
                return;

            this.isInCombat = true;

            // NavigationEnemyData-side enemies freeze via NavigationTimeScale (set to 0 by the
            // CombatStartedEvent published below) instead of the global Time.timeScale -- Combat,
            // loaded additively right after, needs normal time for its own ATB ticking.
            this.combatStartedPublisher.Publish(new CombatStartedEvent { EncounterId = encounterId });
            this.encounterContext.Set(encounterId, encounterAsset, operatorsStartFull);
            this.inputService.SwitchToCombat();

            // Freeze-frame breve sobre el golpe que dispara el combate (estilo FF/Pokémon) antes
            // de que arranque la transición. Usar el Time.timeScale global acá es seguro porque
            // Combat todavía no cargó -- nada ahí depende de tiempo normal hasta después de esto.
            Time.timeScale = 0f;
            await UniTask.Delay(TimeSpan.FromSeconds(HitStopDuration), DelayType.UnscaledDeltaTime);
            Time.timeScale = 1f;

            await this.screenFader.FadeOutAsync();
            onScreenCovered?.Invoke();
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

            // Async rather than the bare synchronous LoadScene(Single) this used to call --
            // that tears the gameplay scene down and activates MainMenu in the same frame,
            // with no gap for Wwise to unregister the old AkAudioListener before the new one
            // registers, which drops all audio in the destination scene.
            var operation = SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            await operation.ToUniTask();
            await this.screenFader.FadeInAsync();
        }

        void IDisposable.Dispose()
        {
            this.combatEndedSubscription?.Dispose();
        }
    }
}