#nullable enable

using MessagePipe;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Milestones;
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
        private readonly NavigationTimeScale                     timeScale;
        private readonly IMilestoneService                       milestones;

        [Preserve]
        public EnemyBootstrap(
            EnemyNavAgent[]                         enemies,
            ISceneTransitionService                 sceneTransitionService,
            ISubscriber<CombatEndedEvent>           combatEndedSubscriber,
            ISubscriber<DialogueActiveChangedEvent> dialogueSubscriber,
            IEncounterContext                       encounterContext,
            IPublisher<EnemyAlertChangedEvent>      enemyAlertPublisher,
            PlayerController                        playerController,
            EnemyStateRegistry                      registry,
            NavigationTimeScale                     timeScale,
            IMilestoneService                       milestones)
        {
            this.enemies                = enemies;
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.dialogueSubscriber     = dialogueSubscriber;
            this.encounterContext       = encounterContext;
            this.enemyAlertPublisher    = enemyAlertPublisher;
            this.playerController       = playerController;
            this.registry               = registry;
            this.timeScale              = timeScale;
            this.milestones             = milestones;
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
                    this.timeScale,
                    key);

                if (agent.TryGetComponent<EnemyReviveOnMilestone>(out var reviveTrigger))
                    reviveTrigger.Construct(this.milestones, this.playerController);

                if (!this.registry.IsDefeated(key))
                {
                    // Placed as a corpse and never got up yet: lies where it was put, no blood.
                    if (agent.StartsAsCorpse && !this.registry.IsRevived(key))
                        agent.RestoreAsCorpse(null, null, showBlood: false);
                    continue;
                }

                if (this.registry.TryGetCorpsePose(key, out var pose))
                    agent.RestoreAsCorpse(pose.Position, pose.Rotation);
                else
                    agent.RestoreAsCorpse(null, null);
            }
        }
    }
}