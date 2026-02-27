using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.Navigation
{
    /// <summary>
    /// DI scope for the ship navigation scene (top-down exploration).
    /// Parent: GameLifetimeScope. Child: CombatScope (loaded additively).
    ///
    /// Assign this component to a GameObject in Navigation.unity.
    /// Set the Parent field in the Inspector to the GameLifetimeScope prefab.
    /// </summary>
    public class NavigationScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // ── Navigation services ───────────────────────────────────────────
            // builder.Register<IGuardAlertSystem,   GuardAlertSystem  >(Lifetime.Scoped);
            // builder.Register<INavigationService,  NavigationService >(Lifetime.Scoped);
            // builder.Register<ICombatSceneLoader,  CombatSceneLoader >(Lifetime.Scoped);

            // ── Character state (persists across combat encounters) ────────────
            // builder.Register<IHealthService,    HealthService   >(Lifetime.Scoped);
            // builder.Register<IInventoryService, InventoryService>(Lifetime.Scoped);
        }
    }
}
