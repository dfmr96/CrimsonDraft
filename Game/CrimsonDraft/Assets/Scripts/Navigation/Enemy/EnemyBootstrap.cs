#nullable enable

using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Combat;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyBootstrap : IInitializable
    {
        private readonly EnemyNavAgent[]                    enemies;
        private readonly CombatTrigger[]                    triggers;
        private readonly ISceneTransitionService            sceneTransitionService;
        private readonly ISubscriber<CombatEndedEvent>      combatEndedSubscriber;
        private readonly IEncounterContext                  encounterContext;
        private readonly IPublisher<GuardAlertChangedEvent> guardAlertPublisher;
        private readonly PlayerController                   playerController;
        private readonly EnemyStateRegistry                 registry;

        [Preserve]
        public EnemyBootstrap(
            EnemyNavAgent[]                    enemies,
            CombatTrigger[]                    triggers,
            ISceneTransitionService            sceneTransitionService,
            ISubscriber<CombatEndedEvent>      combatEndedSubscriber,
            IEncounterContext                  encounterContext,
            IPublisher<GuardAlertChangedEvent> guardAlertPublisher,
            PlayerController                   playerController,
            EnemyStateRegistry                 registry)
        {
            this.enemies                = enemies;
            this.triggers               = triggers;
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.encounterContext       = encounterContext;
            this.guardAlertPublisher    = guardAlertPublisher;
            this.playerController       = playerController;
            this.registry               = registry;
        }

        void IInitializable.Initialize()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            foreach (var agent in this.enemies)
            {
                var key = $"{sceneName}/{agent.gameObject.name}";
                agent.Construct(
                    this.sceneTransitionService,
                    this.combatEndedSubscriber,
                    this.encounterContext,
                    this.guardAlertPublisher,
                    this.playerController,
                    this.registry,
                    key);

                if (this.registry.IsDefeated(key))
                    agent.gameObject.SetActive(false);
            }

            foreach (var trigger in this.triggers)
            {
                trigger.Construct(
                    this.sceneTransitionService,
                    this.combatEndedSubscriber,
                    this.encounterContext);
            }
        }
    }
}
