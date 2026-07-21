#nullable enable

using MessagePipe;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using NaughtyAttributes;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Map;
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
        [SerializeField] private SceneEntryContext     sceneEntryContext     = null!;
        [SerializeField] private RoomController        startingRoom         = null!;
        [SerializeField] private MapDataSet            mapDataSet           = null!;

        [SerializeField] private DialogueRunner          generalRunner    = null!;
        [SerializeField] private InMemoryVariableStorage generalStorage   = null!;
        [SerializeField] private DialogueRunner          pickupRunner     = null!;
        [SerializeField] private InMemoryVariableStorage pickupStorage    = null!;

        [SerializeField] private RoomDoorInteractable[]  cachedRoomDoors      = System.Array.Empty<RoomDoorInteractable>();
        [SerializeField] private SceneDoorInteractable[] cachedSceneDoors     = System.Array.Empty<SceneDoorInteractable>();
        [SerializeField] private PickupInteractable[]    cachedPickups        = System.Array.Empty<PickupInteractable>();
        [SerializeField] private MapPickupInteractable[] cachedMapPickups     = System.Array.Empty<MapPickupInteractable>();
        [SerializeField] private DocumentInteractable[]  cachedDocumentPickups = System.Array.Empty<DocumentInteractable>();
        [SerializeField] private EnemyNavAgent[]         cachedEnemies        = System.Array.Empty<EnemyNavAgent>();
        [SerializeField] private CombatTrigger[]         cachedCombatTriggers = System.Array.Empty<CombatTrigger>();

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.startingLoadout);
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.RegisterInstance(this.mapDataSet);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<PlayerAimController>();
            builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
            builder.Register<InventoryBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.cachedEnemies);
            builder.RegisterInstance(this.cachedCombatTriggers);
            builder.Register<EnemyBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<NavigationCameraRegistrar>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<MapSceneConfig>();
            builder.RegisterComponentInHierarchy<MapRenderer>();
            builder.Register<StartingLoadoutRosterSeedProvider>(Lifetime.Singleton).As<IOperatorRosterSeedProvider>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();
            builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
            builder.RegisterComponentInHierarchy<PickupRegistryDebugView>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Optional debug overlay — only registered if present in the scene, so it
            // never breaks scope build when the GameObject isn't added.
            var inventoryDebug = FindObjectOfType<InventoryDebugPrinter>(true);
            if (inventoryDebug != null)
                builder.RegisterComponent(inventoryDebug);
#endif
            builder.RegisterInstance(new GeneralDialogueRunnerRef(this.generalRunner, this.generalStorage));
            builder.RegisterInstance(new PickupDialogueRunnerRef(this.pickupRunner, this.pickupStorage));
            builder.Register<DialogueService>(Lifetime.Scoped).AsSelf().As<IDialogueService>();
            builder.Register<PickupDialogueService>(Lifetime.Scoped).As<IPickupDialogueService>();

            builder.RegisterComponentInHierarchy<InteractionReaderView>();
            builder.Register<DocumentController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<ContainerView>();
            builder.Register<ContainerController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<PuzzleViewController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // ── Room transition ──────────────────────────────────────────────
            this.roomTransitionContext.SetStartingRoom(this.startingRoom);
            builder.RegisterInstance(this.roomTransitionContext);
            builder.RegisterInstance(this.sceneEntryContext);
            builder.Register<FloorTransitionService>(Lifetime.Singleton).AsImplementedInterfaces();

            // Reuse parent's MessagePipeOptions so room events share the same
            // pub/sub bus as global events (CombatEndedEvent, etc.).
            // Calling RegisterMessagePipe() here would create a separate bus,
            // breaking cross-scope subscriptions.
            var msgOptions = Parent!.Container.Resolve<MessagePipeOptions>();
            builder.RegisterMessageBroker<RoomTransitionStartedEvent>(msgOptions);
            builder.RegisterMessageBroker<RoomTransitionedEvent>(msgOptions);
            builder.RegisterMessageBroker<GuardAlertChangedEvent>(msgOptions);
            builder.RegisterMessageBroker<NoteCollectedEvent>(msgOptions);
            builder.RegisterMessageBroker<DialogueActiveChangedEvent>(msgOptions);

            builder.Register<RoomOrchestrator>(Lifetime.Singleton)
                   .AsSelf()
                   .AsImplementedInterfaces();
            builder.Register<MapStateTracker>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<WeatherAmbienceController>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<MusicManagerController>().AsImplementedInterfaces();

            // Optional room prop — not every scene has a physical radio placed yet,
            // so only register it if present, same as InventoryDebugPrinter above.
            var radio = FindObjectOfType<RadioInteractable>(true);
            if (radio != null)
                builder.RegisterComponent(radio);
            builder.RegisterInstance(new DoorCache(this.cachedRoomDoors, this.cachedSceneDoors));
            builder.Register<DoorBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(this.cachedPickups);
            builder.Register<PickupBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(this.cachedMapPickups);
            builder.Register<MapPickupBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(this.cachedDocumentPickups);
            builder.Register<DocumentPickupBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
        }

#if UNITY_EDITOR
        [Button("Cache Scene Doors")]
        private void CacheSceneDoors()
        {
            this.cachedRoomDoors  = FindObjectsByType<RoomDoorInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            this.cachedSceneDoors = FindObjectsByType<SceneDoorInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Scene Pickups")]
        private void CacheScenePickups()
        {
            this.cachedPickups = FindObjectsByType<PickupInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            this.cachedMapPickups = FindObjectsByType<MapPickupInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Scene Document Pickups")]
        private void CacheSceneDocumentPickups()
        {
            this.cachedDocumentPickups = FindObjectsByType<DocumentInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Scene Enemies")]
        private void CacheSceneEnemies()
        {
            this.cachedEnemies = FindObjectsByType<EnemyNavAgent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            this.cachedCombatTriggers = FindObjectsByType<CombatTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
