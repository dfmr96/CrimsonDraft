#nullable enable

using VContainer;
using VContainer.Unity;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation
{
    /// <summary>
    /// DI scope for the ship navigation scene (top-down exploration).
    /// Parent: GameLifetimeScope. Child: CombatScope (loaded additively).
    ///
    /// Assign this component to a GameObject in Navigation.unity.
    /// Set the Parent field in the Inspector to the GameLifetimeScope prefab.
    /// </summary>
    public sealed class NavigationScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<PlayerController>();
        }
    }
}
