#nullable enable

using VContainer;
using VContainer.Unity;
using UnityEngine;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Navigation.Combat;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Interactables.UI;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

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
        [SerializeField] private StartingLoadout        startingLoadout      = null!;
        [SerializeField] private CombineRecipeLibrary  combineRecipeLibrary = null!;

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

            builder.RegisterComponentInHierarchy<CombatTrigger>();
            builder.RegisterComponentInHierarchy<NavigationCameraRegistrar>().AsImplementedInterfaces();
            builder.Register<StartingLoadoutRosterSeedProvider>(Lifetime.Singleton).As<IOperatorRosterSeedProvider>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();
            builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
            builder.RegisterComponentInHierarchy<PoiDialogView>();
            builder.Register<PoiController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<InteractionReaderView>();
            builder.Register<DocumentController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<ContainerView>();
            builder.Register<ContainerController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

        }
    }
}
