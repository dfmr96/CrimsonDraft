#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class UiCRTScope : LifetimeScope
    {
        [SerializeField] private CombineRecipeLibrary combineRecipeLibrary = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
            builder.Register<InventoryService>(Lifetime.Singleton).As<IInventoryService>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterComponentInHierarchy<PartyTestHarness>().AsImplementedInterfaces();
#endif

            builder.RegisterComponentInHierarchy<GridCursor>();
            builder.RegisterComponentInHierarchy<ItemContextMenu>();
            builder.RegisterComponentInHierarchy<PartyPanelView>();
            builder.RegisterComponentInHierarchy<TabManager>();
            builder.RegisterComponentInHierarchy<InspectPanel>();
            builder.RegisterComponentInHierarchy<InventorySceneInit>();

            builder.Register<InventoryHUDController>(Lifetime.Scoped).AsImplementedInterfaces();
        }
    }
}
