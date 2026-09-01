#nullable enable

using MessagePipe;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Infrastructure.Audio;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Infrastructure.Graphics;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.Infrastructure
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private InputActionAsset inputActions   = null!;
        [SerializeField] private AudioSettingsData audioSettingsData = null!;
        [SerializeField] private GameOverView gameOverViewPrefab = null!;

        protected override void Awake()
        {
            // Must run before base.Awake(): VContainer's LifetimeScope.Build() builds this
            // root's own container fine, but then immediately tries to build any scoped
            // children that were waiting on it (AwakeWaitingChildren) -- e.g. MainMenuScope. If
            // a scene transition (New Game -> Deck_B) unloads MainMenu mid-build, that child
            // build throws (its RegisterComponentInHierarchy scan finds an empty/unloaded
            // scene), and the exception propagates straight out of base.Awake() uncaught. If
            // DontDestroyOnLoad were still below that call, it would never run, and this whole
            // GameObject -- with every singleton in it (input, inventory, camera, graphics...)
            // -- would be destroyed along with the unloading scene, orphaning every DI consumer
            // in whatever scene loads next.
            DontDestroyOnLoad(gameObject);

            base.Awake();

            // The game is fully keyboard/gamepad-driven — the OS cursor is never used for
            // input, but builds still show it by default.
            Cursor.visible = false;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (this.inputActions == null)
                throw new System.InvalidOperationException(
                    $"{nameof(this.inputActions)} is not assigned in {nameof(GameLifetimeScope)}.");

            builder.RegisterInstance(this.inputActions);
            builder.Register<InputService>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterInstance(this.audioSettingsData);
            builder.Register<AudioSettingsService>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<GraphicsSettingsService>(Lifetime.Singleton).AsImplementedInterfaces();

            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<CombatStartedEvent>(options);
            builder.RegisterMessageBroker<CombatEndedEvent>(options);
            builder.RegisterMessageBroker<ShootConfigurationRequestedEvent>(options);
            builder.RegisterMessageBroker<FocusFireConfigurationRequestedEvent>(options);

            builder.Register<CameraService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ScreenFader>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            if (this.gameOverViewPrefab == null)
                throw new System.InvalidOperationException(
                    $"{nameof(this.gameOverViewPrefab)} is not assigned in {nameof(GameLifetimeScope)}.");
            builder.RegisterComponentInNewPrefab(this.gameOverViewPrefab, Lifetime.Singleton).DontDestroyOnLoad().AsSelf();

            builder.Register<SceneTransitionService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<EncounterContext>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
            builder.Register<DoorStateRegistry>(Lifetime.Singleton);
            builder.Register<RoomStateRegistry>(Lifetime.Singleton);
            builder.Register<KnownMapsRegistry>(Lifetime.Singleton);
            builder.Register<PickupRegistry>(Lifetime.Singleton);
            builder.Register<NoteRegistry>(Lifetime.Singleton);
            builder.Register<InventoryStateRegistry>(Lifetime.Singleton);
            builder.Register<RosterHealthRegistry>(Lifetime.Singleton);
            builder.Register<EnemyStateRegistry>(Lifetime.Singleton);
            builder.Register<OperatorCorpseRegistry>(Lifetime.Singleton);

            builder.Register<WorldStateRegistries>(Lifetime.Singleton);

            builder.Register<SaveGameService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<GameStateResetter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<PlaytimeTracker>(Lifetime.Singleton);
        }
    }
}
