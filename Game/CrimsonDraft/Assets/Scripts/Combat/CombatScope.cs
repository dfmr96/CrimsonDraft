#nullable enable

using VContainer;
using VContainer.Unity;

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
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<CombatSessionController>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<CombatActionMenuView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<CommandPanelView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<SubPanelView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<AimViewController>().AsImplementedInterfaces();

            builder.Register<CombatMenuController>(Lifetime.Scoped)
                .AsSelf().AsImplementedInterfaces();
        }
    }
}
