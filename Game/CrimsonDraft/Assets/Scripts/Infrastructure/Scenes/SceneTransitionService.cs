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
using CrimsonDraft.UI;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class SceneTransitionService : ISceneTransitionService, IInitializable, IDisposable
    {
        private const string CombatSceneName   = "Combat";
        private const string MainMenuSceneName = "MainMenu";
        private const float  SlowMoTimeScale   = 0.1f;
        private const float  SlowMoStaticLead  = 0.25f; // la estática entra a este punto de la ralentización
        private const float  StaticDuration    = 1f;    // tiempo mínimo que la estática cubre la pantalla

        private readonly IInputService inputService;
        private readonly IPublisher<CombatStartedEvent> combatStartedPublisher;
        private readonly ISubscriber<CombatEndedEvent> combatEndedSubscriber;
        private readonly EncounterContext encounterContext;
        private readonly ICameraService cameraService;
        private readonly ScreenFader screenFader;
        private readonly StaticTransitionView staticTransition;
        private readonly GameOverView gameOverView;
        private readonly InventorySfxData sfx;

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
            StaticTransitionView staticTransition,
            GameOverView gameOverView,
            InventorySfxData sfx)
        {
            this.inputService          = inputService;
            this.combatStartedPublisher = combatStartedPublisher;
            this.combatEndedSubscriber = combatEndedSubscriber;
            this.encounterContext      = encounterContext;
            this.cameraService         = cameraService;
            this.screenFader           = screenFader;
            this.staticTransition      = staticTransition;
            this.gameOverView          = gameOverView;
            this.sfx                   = sfx;
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

            // El mundo se ralentiza y aparece estática de TV vieja (cambio de canal). Usar el
            // Time.timeScale global acá es seguro porque Combat todavía no cargó -- nada ahí depende
            // de tiempo normal hasta después de esto. Todo el timing va en tiempo no escalado.
            Time.timeScale = SlowMoTimeScale;
            await UniTask.Delay(TimeSpan.FromSeconds(SlowMoStaticLead), DelayType.UnscaledDeltaTime);
            await this.staticTransition.ShowAsync();
            float staticStart = Time.unscaledTime;
            Time.timeScale = 1f;

            onScreenCovered?.Invoke();
            await SceneManager.LoadSceneAsync(CombatSceneName, LoadSceneMode.Additive).ToUniTask();
            this.cameraService.ActivateCombatCamera();

            float remaining = StaticDuration - (Time.unscaledTime - staticStart);
            if (remaining > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(remaining), DelayType.UnscaledDeltaTime);

            await this.staticTransition.HideAsync();
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            EndCombatAsync(ev.Victory).Forget();
        }

        private async UniTask EndCombatAsync(bool victory)
        {
            // On defeat the static covers the frozen last frame while the black fader is raised
            // and Combat unloads underneath, so the swap is hidden the same way as on entry.
            if (!victory)
                await this.staticTransition.ShowAsync();

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

            // The game over view renders through the UI camera, i.e. *under* the screen-space
            // overlay fader, so the black cover has to come off (behind the static, unseen)
            // before the view can be visible. The view's own background is opaque black.
            await this.screenFader.FadeInAsync();
            await this.staticTransition.HideAsync();

            // Nothing is interactive here: the title types itself out, holds, then we fade out and leave.
            await this.gameOverView.PlayAsync(this.sfx);
            await this.screenFader.FadeOutAsync();
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