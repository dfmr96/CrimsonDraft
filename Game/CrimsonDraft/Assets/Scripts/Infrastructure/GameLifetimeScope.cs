#nullable enable

using MessagePipe;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.Infrastructure
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private InputActionAsset inputActions = null!;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (this.inputActions == null)
                throw new System.InvalidOperationException(
                    $"{nameof(this.inputActions)} is not assigned in {nameof(GameLifetimeScope)}.");

            builder.RegisterInstance(this.inputActions);
            builder.Register<InputService>(Lifetime.Singleton).AsImplementedInterfaces();

            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<CombatStartedEvent>(options);
            builder.RegisterMessageBroker<CombatEndedEvent>(options);
            builder.RegisterMessageBroker<ShootConfigurationRequestedEvent>(options);

            builder.Register<CameraService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ScreenFader>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

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
        }
    }
}
