#nullable enable

using MessagePipe;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Navigation.Combat;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Interactables.UI;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
using CrimsonDraft.Navigation.Enemy;
using CrimsonDraft.Infrastructure.Events;


namespace CrimsonDraft.Navigation
{
    public sealed class NavigationScope : LifetimeScope
    {
        [SerializeField] private StartingLoadout       startingLoadout      = null!;
        [SerializeField] private CombineRecipeLibrary  combineRecipeLibrary = null!;
        [SerializeField] private RoomTransitionContext roomTransitionContext = null!;
        [SerializeField] private RoomController        startingRoom         = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.startingLoadout);
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InventoryView>();
            builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
            builder.Register<InventoryController>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<InventoryBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<PlaceholderOverlayView>();
            builder.Register<PlaceholderOverlayController>(Lifetime.Scoped).AsImplementedInterfaces();

            foreach (var trigger in FindObjectsByType<CombatTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(trigger);
            foreach (var agent in FindObjectsByType<EnemyNavAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(agent);
            builder.RegisterComponentInHierarchy<NavigationCameraRegistrar>().AsImplementedInterfaces();
            builder.Register<StartingLoadoutRosterSeedProvider>(Lifetime.Singleton).As<IOperatorRosterSeedProvider>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();
            builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
            builder.Register<DialogueService>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<InteractionReaderView>();
            builder.Register<DocumentController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<ContainerView>();
            builder.Register<ContainerController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // ── Room transition ──────────────────────────────────────────────
            this.roomTransitionContext.SetStartingRoom(this.startingRoom);
            builder.RegisterInstance(this.roomTransitionContext);

            // Reuse parent's MessagePipeOptions so room events share the same
            // pub/sub bus as global events (CombatEndedEvent, etc.).
            // Calling RegisterMessagePipe() here would create a separate bus,
            // breaking cross-scope subscriptions.
            var msgOptions = Parent!.Container.Resolve<MessagePipeOptions>();
            builder.RegisterMessageBroker<RoomTransitionStartedEvent>(msgOptions);
            builder.RegisterMessageBroker<RoomTransitionedEvent>(msgOptions);
            builder.RegisterMessageBroker<GuardAlertChangedEvent>(msgOptions);

            builder.Register<RoomOrchestrator>(Lifetime.Singleton)
                   .AsSelf()
                   .AsImplementedInterfaces();
        }
    }
}
