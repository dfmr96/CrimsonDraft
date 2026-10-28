#nullable enable

using System.Linq;
using MessagePipe;
using Unity.Cinemachine;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using NaughtyAttributes;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Milestones;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Interactables.UI;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Pushables;
using CrimsonDraft.Navigation.Radio;
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
        [SerializeField] private ItemDatabase          itemDatabase         = null!;
        [SerializeField] private RoomCatalog?          roomCatalog;
        [SerializeField] private SaveSlotListView      saveSlotListView     = null!;
        [SerializeField] private OperatorCorpseSettings corpseSettings      = null!;

        [SerializeField] private DialogueRunner          generalRunner    = null!;
        [SerializeField] private InMemoryVariableStorage generalStorage   = null!;
        [SerializeField] private DialogueRunner          pickupRunner     = null!;
        [SerializeField] private InMemoryVariableStorage pickupStorage    = null!;
        [SerializeField] private DialogueRunner          inspectPromptRunner  = null!;
        [SerializeField] private InMemoryVariableStorage inspectPromptStorage = null!;

        // Radio calls (Radio_UI). All optional: a scene without them just has no radio.
        [SerializeField] private DialogueRunner?          radioRunner;
        [SerializeField] private InMemoryVariableStorage? radioStorage;
        [SerializeField] private RadioCastData?           radioCast;

        [SerializeField] private RoomDoorInteractable[]  cachedRoomDoors      = System.Array.Empty<RoomDoorInteractable>();
        [SerializeField] private SceneDoorInteractable[] cachedSceneDoors     = System.Array.Empty<SceneDoorInteractable>();
        [SerializeField] private PickupInteractable[]    cachedPickups        = System.Array.Empty<PickupInteractable>();
        [SerializeField] private MapPickupInteractable[] cachedMapPickups     = System.Array.Empty<MapPickupInteractable>();
        [SerializeField] private DocumentInteractable[]  cachedDocumentPickups = System.Array.Empty<DocumentInteractable>();
        [SerializeField] private EnemyNavAgent[]         cachedEnemies        = System.Array.Empty<EnemyNavAgent>();
        [SerializeField] private FixedCameraZoneTrigger[] cachedCameraZoneTriggers = System.Array.Empty<FixedCameraZoneTrigger>();
        [SerializeField] private BeeperReceiverInteractable[] cachedBeeperReceivers = System.Array.Empty<BeeperReceiverInteractable>();
        [SerializeField] private ItemSocketInteractable[] cachedItemSockets = System.Array.Empty<ItemSocketInteractable>();
        [SerializeField] private PushableObject[]         cachedPushables   = System.Array.Empty<PushableObject>();
        // MonoBehaviour[] because Unity can't serialize interface arrays; holds only IPuzzle components.
        [SerializeField] private MonoBehaviour[]          cachedPuzzles     = System.Array.Empty<MonoBehaviour>();
        // Assets, not scene objects: filled from the project by the cache button below.
        [SerializeField] private MilestoneGraph[]         cachedMilestoneGraphs = System.Array.Empty<MilestoneGraph>();

        protected override void Configure(IContainerBuilder builder)
        {
            if (this.corpseSettings == null)
                throw new System.InvalidOperationException(
                    $"{nameof(this.corpseSettings)} is not assigned in {nameof(NavigationScope)}.");

            builder.RegisterInstance(this.startingLoadout);
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.RegisterInstance(this.mapDataSet);
            builder.RegisterInstance(this.itemDatabase);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<PlayerAimController>();
            builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
            builder.Register<InventoryBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<NavigationCameraRegistrar>().AsImplementedInterfaces();

            // Discover zones at startup so newly placed triggers receive their service
            // even when the editor cache has not been refreshed.
            builder.RegisterInstance(FindObjectsByType<FixedCameraZoneTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));
            builder.Register<FixedCameraZoneService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FixedCameraZoneBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<CinemachineBrain>();
            builder.Register<CameraRelativeMovementService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<MapSceneConfig>();
            builder.Register<StartingLoadoutRosterSeedProvider>(Lifetime.Singleton).As<IOperatorRosterSeedProvider>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();
            builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
            builder.RegisterComponentInHierarchy<PickupRegistryDebugView>();

            builder.RegisterInstance(new GeneralDialogueRunnerRef(this.generalRunner, this.generalStorage));
            builder.RegisterInstance(new PickupDialogueRunnerRef(this.pickupRunner, this.pickupStorage));
            builder.RegisterInstance(new InspectDialogueRunnerRef(this.inspectPromptRunner, this.inspectPromptStorage));
            builder.Register<DialogueService>(Lifetime.Scoped).AsSelf().As<IDialogueService>();
            builder.Register<PickupDialogueService>(Lifetime.Scoped).As<IPickupDialogueService>();
            builder.Register<InspectDialogueService>(Lifetime.Scoped).As<IInspectDialogueService>();

            // Radio: the conversation service is always there (the inventory controller reports to
            // it); the rest only when this scene has the Radio_UI wired up. Optional components
            // use FindFirstObjectByType because RegisterComponentInHierarchy throws when absent.
            builder.Register<RadioConversationService>(Lifetime.Singleton).As<IRadioConversationService>();
            var radioView = FindFirstObjectByType<RadioView>(FindObjectsInactive.Include);
            if (this.radioRunner != null && this.radioStorage != null && this.radioCast != null && radioView != null)
            {
                builder.RegisterInstance(new RadioDialogueRunnerRef(this.radioRunner, this.radioStorage));
                builder.RegisterInstance(this.radioCast);
                builder.Register<RadioDialogueService>(Lifetime.Singleton).AsSelf();
                builder.RegisterComponent(radioView).AsSelf().AsImplementedInterfaces();
            }
            var radioIndicator = FindFirstObjectByType<RadioIndicatorView>(FindObjectsInactive.Include);
            if (radioIndicator != null)
                builder.RegisterComponent(radioIndicator);
            // Several zones can share a scene; RegisterComponent per instance throws a VContainer
            // "Conflict implementation type" for the second one, so inject them directly instead.
            var radioZones = FindObjectsByType<RadioConversationZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (radioZones.Length > 0)
                builder.RegisterBuildCallback(resolver =>
                {
                    foreach (var zone in radioZones)
                        resolver.Inject(zone);
                });

            builder.RegisterComponentInHierarchy<PauseMenuView>();
            // The pause menu's Settings reuses the main menu's Options components; they sit
            // under PauseMenuView rather than being registered themselves, so inject them here.
            builder.RegisterBuildCallback(resolver =>
            {
                var pauseView = resolver.Resolve<PauseMenuView>();
                resolver.Inject(pauseView.OptionsNavigator);
                resolver.Inject(pauseView.GammaPanel);
                resolver.Inject(pauseView.SoundPanel);
            });
            builder.Register<PauseMenuController>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<InteractionReaderView>();
            builder.Register<DocumentController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<PuzzleViewController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<InspectionController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // GeneratorSwitchPanel takes its dependencies via [Inject] Construct() but was
            // never registered anywhere -- Activate() threw a NullReferenceException on
            // this.inputService before ever reaching UpdateHighlight(), so the selection
            // outline never got applied. Only the Generator's own scene has one, though --
            // RegisterComponentInHierarchy uses FindComponentProvider, which THROWS at scope
            // build time if the type isn't in the scene (unlike a plain FindObjectOfType
            // null-check), which took down the whole container -- and everything after it in
            // this method -- in every other scene sharing NavigationScope (e.g. Deck B on new
            // game). Same optional-registration pattern as RadioInteractable
            // below: only register it if this scene actually has one.
            var generatorSwitchPanel = FindObjectOfType<GeneratorSwitchPanel>(true);
            if (generatorSwitchPanel != null)
                builder.RegisterComponent(generatorSwitchPanel);

            builder.RegisterInstance(this.saveSlotListView);
            builder.Register<SaveController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            builder.Register<PickupPreviewController>(Lifetime.Scoped).AsSelf();

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
            builder.RegisterMessageBroker<EnemyAlertChangedEvent>(msgOptions);
            builder.RegisterMessageBroker<NoteCollectedEvent>(msgOptions);
            builder.RegisterMessageBroker<DialogueActiveChangedEvent>(msgOptions);
            builder.RegisterMessageBroker<StorageOpenRequestedEvent>(msgOptions);
            builder.RegisterMessageBroker<PickupPreviewStartedEvent>(msgOptions);
            builder.RegisterMessageBroker<PickupPreviewEndedEvent>(msgOptions);

            builder.Register<RoomOrchestrator>(Lifetime.Singleton)
                   .AsSelf()
                   .AsImplementedInterfaces();
            builder.Register<SaveGameLoader>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MapStateTracker>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<WeatherAmbienceController>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<MusicManagerController>().AsImplementedInterfaces();

            // Optional room prop — not every scene has a physical radio placed yet,
            // so only register it if present, same as GeneratorSwitchPanel above.
            var radio = FindObjectOfType<RadioInteractable>(true);
            if (radio != null)
                builder.RegisterComponent(radio);

            // Optional — only rooms gated by a remote ItemSocketInteractable (e.g. the
            // Lavatory's steam, cleared once Deck C's handle socket is filled) have one.
            foreach (var gate in FindObjectsByType<RemoteSocketGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(gate);

            // Optional — only the save room has a telegraph station (needs the world registries
            // for its one-time intro flag).
            foreach (var station in FindObjectsByType<TelegraphSaveStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(station);

            // Optional — only the pipe-valve socket (Deck C) has one.
            foreach (var tighten in FindObjectsByType<SocketValveTighten>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                builder.RegisterComponent(tighten);

            // Optional — sockets whose item mutates after a later room visit (e.g. Morph).
            builder.RegisterInstance(FindObjectsByType<RoomVisitMutation>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            builder.Register<RoomVisitMutationTracker>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.cachedEnemies);
            builder.Register<NavigationTimeScale>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
            builder.Register<EnemyBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.corpseSettings);
            builder.Register<CorpseProximityTracker>(Lifetime.Singleton).AsSelf().As<ICorpseAccess>();
            builder.Register<OperatorCorpseSpawner>(Lifetime.Singleton).As<IOperatorCorpseSpawner>();
            builder.Register<OperatorCorpseBootstrap>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<DeadOperatorGearReleaser>(Lifetime.Singleton).AsImplementedInterfaces();
            // After SaveGameLoader, so the leader is picked from the loaded HP.
            builder.Register<PlayerOperatorAppearance>(Lifetime.Singleton).AsImplementedInterfaces();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<NavigationDebugKeys>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<DebugWorldOverlay>(Lifetime.Singleton).AsImplementedInterfaces();
            if (this.roomCatalog != null)
            {
                builder.RegisterInstance(this.roomCatalog);
                builder.Register<DebugRoomTeleport>(Lifetime.Singleton).AsImplementedInterfaces();
            }
#endif

            builder.RegisterInstance(new DoorCache(this.cachedRoomDoors, this.cachedSceneDoors));
            builder.Register<DoorBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(this.cachedPickups);
            builder.Register<PickupBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(this.cachedMapPickups);
            builder.Register<MapPickupBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(this.cachedDocumentPickups);
            builder.Register<DocumentPickupBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.cachedBeeperReceivers);
            builder.Register<BeeperReceiverBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.cachedItemSockets);
            builder.Register<ItemSocketBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterInstance(this.cachedPushables);
            builder.Register<PushableBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.cachedPuzzles.OfType<IPuzzle>().ToArray());
            builder.Register<PuzzleBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();

            // Completion of sockets / puzzles / radio calls behind one id, read by milestones and camera zones.
            builder.Register<GateStateService>(Lifetime.Singleton).As<IGateStates>();

            // After SaveGameLoader, so the tracker's first pass sees the loaded state.
            builder.RegisterInstance(this.cachedMilestoneGraphs);
            builder.Register<MilestoneWorldState>(Lifetime.Singleton).As<IMilestoneWorldState>();
            builder.Register<MilestoneTracker>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MilestoneService>(Lifetime.Singleton).AsImplementedInterfaces();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<MilestoneDebugOverlay>(Lifetime.Singleton).AsImplementedInterfaces();
#endif
        }

#if UNITY_EDITOR
        // Runs every Cache Scene button below in one go -- press after adding or removing any
        // cached kind of object, then save the scene.
        [Button("Cache Everything")]
        private void CacheEverything()
        {
            CacheSceneDoors();
            CacheScenePickups();
            CacheSceneDocumentPickups();
            CacheSceneEnemies();
            CacheSceneCameraZoneTriggers();
            CacheSceneBeeperReceivers();
            CacheSceneItemSockets();
            CacheScenePushables();
            CacheScenePuzzles();
            CacheMilestoneGraphs();

            Debug.Log($"{name}: cached {this.cachedRoomDoors.Length} room doors, {this.cachedSceneDoors.Length} scene doors, " +
                      $"{this.cachedPickups.Length} pickups, {this.cachedMapPickups.Length} map pickups, " +
                      $"{this.cachedDocumentPickups.Length} document pickups, {this.cachedEnemies.Length} enemies, " +
                      $"{this.cachedCameraZoneTriggers.Length} camera zone triggers, {this.cachedBeeperReceivers.Length} beeper receivers, " +
                      $"{this.cachedItemSockets.Length} item sockets, {this.cachedPushables.Length} pushables, {this.cachedPuzzles.Length} puzzles, {this.cachedMilestoneGraphs.Length} milestone graphs.", this);
        }

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
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Scene Camera Zone Triggers")]
        private void CacheSceneCameraZoneTriggers()
        {
            this.cachedCameraZoneTriggers = FindObjectsByType<FixedCameraZoneTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Scene Beeper Receivers")]
        private void CacheSceneBeeperReceivers()
        {
            this.cachedBeeperReceivers = FindObjectsByType<BeeperReceiverInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);

            // Every receiver is a gate: without a unique id it can't be saved or waited on.
            foreach (var receiver in this.cachedBeeperReceivers)
                if (!GateIds.TryParse(receiver.GateId, out _, out _))
                    Debug.LogWarning($"[NavigationScope] Beeper receiver '{receiver.name}' has no gate id; it won't be saved.", receiver);
            foreach (var duplicate in this.cachedBeeperReceivers.GroupBy(r => r.GateId).Where(g => g.Count() > 1))
                Debug.LogWarning($"[NavigationScope] {duplicate.Count()} beeper receivers share {duplicate.Key}; give each its own gate id.", duplicate.First());
        }

        [Button("Cache Scene Item Sockets")]
        private void CacheSceneItemSockets()
        {
            this.cachedItemSockets = FindObjectsByType<ItemSocketInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Scene Pushables")]
        private void CacheScenePushables()
        {
            this.cachedPushables = FindObjectsByType<PushableObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Milestone Graphs")]
        private void CacheMilestoneGraphs()
        {
            this.cachedMilestoneGraphs = UnityEditor.AssetDatabase.FindAssets("t:MilestoneGraph")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<MilestoneGraph>)
                .Where(g => g != null)
                .ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("Cache Scene Puzzles")]
        private void CacheScenePuzzles()
        {
            this.cachedPuzzles = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b is IPuzzle)
                .ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
