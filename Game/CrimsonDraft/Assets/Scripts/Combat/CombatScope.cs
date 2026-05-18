#nullable enable

using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Cameras;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// DI scope for combat encounters. Loaded additively on top of Navigation.
    /// Everything registered here is disposed when the combat scene unloads.
    /// Parent: NavigationScope → GameLifetimeScope.
    ///
    /// Assign to a GameObject in Combat.unity.
    /// </summary>
    public sealed class CombatScope : LifetimeScope
    {
        [SerializeField] private EncounterDatabase encounterDatabase = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<CombatSessionController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<CombatActionMenuView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<CommandPanelView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<SubPanelView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<ShotCountView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<AimViewController>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<BattlefieldView>().AsImplementedInterfaces();

            builder.RegisterInstance(this.encounterDatabase);

            builder.Register<ATBSystem>(Lifetime.Scoped).AsSelf();
            builder.Register<CombatActionQueue>(Lifetime.Scoped).AsSelf();
            builder.RegisterComponentInHierarchy<CombatOrchestrator>()
                .AsSelf().AsImplementedInterfaces();

            builder.Register<BattlefieldPresenter>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<CombatCameraRegistrar>().AsImplementedInterfaces();

            builder.Register<CombatMenuController>(Lifetime.Scoped)
                .AsSelf().AsImplementedInterfaces();
        }
    }
}
