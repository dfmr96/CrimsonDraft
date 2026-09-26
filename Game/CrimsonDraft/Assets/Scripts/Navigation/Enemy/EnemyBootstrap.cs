#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyBootstrap : IInitializable
    {
        private readonly EnemyNavAgent[]                         enemies;
        private readonly ISceneTransitionService                 sceneTransitionService;
        private readonly ISubscriber<CombatEndedEvent>           combatEndedSubscriber;
        private readonly ISubscriber<DialogueActiveChangedEvent> dialogueSubscriber;
        private readonly IEncounterContext                       encounterContext;
        private readonly IPublisher<EnemyAlertChangedEvent>      enemyAlertPublisher;
        private readonly PlayerController                        playerController;
        private readonly EnemyStateRegistry                      registry;

        [Preserve]
        public EnemyBootstrap(
            EnemyNavAgent[]                         enemies,
            ISceneTransitionService                 sceneTransitionService,
            ISubscriber<CombatEndedEvent>           combatEndedSubscriber,
            ISubscriber<DialogueActiveChangedEvent> dialogueSubscriber,
            IEncounterContext                       encounterContext,
            IPublisher<EnemyAlertChangedEvent>      enemyAlertPublisher,
            PlayerController                        playerController,
            EnemyStateRegistry                      registry)
        {
            this.enemies                = enemies;
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.dialogueSubscriber     = dialogueSubscriber;
            this.encounterContext       = encounterContext;
            this.enemyAlertPublisher    = enemyAlertPublisher;
            this.playerController       = playerController;
            this.registry               = registry;
        }

        void IInitializable.Initialize()
        {
            foreach (var agent in this.enemies)
            {
                var key = agent.EncounterId;
                agent.Construct(
                    this.sceneTransitionService,
                    this.combatEndedSubscriber,
                    this.dialogueSubscriber,
                    this.encounterContext,
                    this.enemyAlertPublisher,
                    this.playerController,
                    this.registry,
                    key);

                if (this.registry.IsDefeated(key))
                    agent.gameObject.SetActive(false);
            }
        }
    }
}