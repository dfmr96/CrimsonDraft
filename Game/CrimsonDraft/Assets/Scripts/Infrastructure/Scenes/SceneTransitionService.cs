#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public sealed class SceneTransitionService : ISceneTransitionService, IInitializable, IDisposable
    {
        private const string CombatSceneName = "Combat";

        private readonly IInputService inputService;
        private readonly ISubscriber<CombatEndedEvent> combatEndedSubscriber;

        private IDisposable? combatEndedSubscription;
        private bool isInCombat;

        public bool IsInCombat => this.isInCombat;

        [Preserve]
        public SceneTransitionService(
            IInputService inputService,
            ISubscriber<CombatEndedEvent> combatEndedSubscriber)
        {
            this.inputService = inputService;
            this.combatEndedSubscriber = combatEndedSubscriber;
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
            this.inputService.SwitchToCombat();
            await SceneManager.LoadSceneAsync(CombatSceneName, LoadSceneMode.Additive).ToUniTask();
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            EndCombatAsync().Forget();
        }

        private async UniTask EndCombatAsync()
        {
            var scene = SceneManager.GetSceneByName(CombatSceneName);
            if (scene.isLoaded)
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();

            this.isInCombat = false;
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.combatEndedSubscription?.Dispose();
        }
    }
}
