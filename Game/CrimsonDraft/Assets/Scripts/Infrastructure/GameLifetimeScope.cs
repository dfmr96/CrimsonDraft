using MessagePipe;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Infrastructure
{
    /// <summary>
    /// Root DI scope — lives in the Boot scene with DontDestroyOnLoad.
    /// Registers: all MessagePipe brokers, global services.
    /// Child scopes: NavigationScope, CombatScope.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ── MessagePipe ───────────────────────────────────────────────────
            // Options configure async dispatch, filter pipeline, etc.
            var pipe = builder.RegisterMessagePipe();

            // Make brokers available to GlobalMessagePipe (escape hatch for legacy code).
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));

            // All event types must be registered explicitly — IL2CPP has no open generics.
            // Combat
            builder.RegisterMessageBroker<CombatStartedEvent>(pipe);
            builder.RegisterMessageBroker<CombatEndedEvent>(pipe);
            builder.RegisterMessageBroker<CharacterDamagedEvent>(pipe);
            builder.RegisterMessageBroker<CharacterDiedEvent>(pipe);
            // QTE
            builder.RegisterMessageBroker<QTEStartedEvent>(pipe);
            builder.RegisterMessageBroker<QTECompletedEvent>(pipe);
            // Inventory
            builder.RegisterMessageBroker<ItemUsedEvent>(pipe);
            builder.RegisterMessageBroker<WeaponReloadedEvent>(pipe);
            // Krokonil
            builder.RegisterMessageBroker<KrokoniлDoseAppliedEvent>(pipe);
            // Navigation
            builder.RegisterMessageBroker<GuardAlertChangedEvent>(pipe);

            // ── Global services ───────────────────────────────────────────────
            // Uncomment as services are implemented:
            // builder.Register<ISaveService,  SaveService >(Lifetime.Singleton);
            // builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
        }
    }
}
