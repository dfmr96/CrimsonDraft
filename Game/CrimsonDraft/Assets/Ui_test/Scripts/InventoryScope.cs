#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.UI
{
    public sealed class InventoryScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // IInventoryService, IOperatorRoster, ICombineService, CombineRecipeLibrary
            // are resolved from the parent NavigationScope — do NOT register them here.

            builder.RegisterComponentInHierarchy<InventoryPopulator>().AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<GridCursor>();
            builder.RegisterComponentInHierarchy<ItemContextMenu>();
            builder.RegisterComponentInHierarchy<PartyPanelView>();
            builder.RegisterComponentInHierarchy<TabManager>();
            builder.RegisterComponentInHierarchy<InspectPanel>();
            builder.RegisterComponentInHierarchy<InventoryGridGroup>();
            builder.RegisterComponentInHierarchy<InventoryOpenCloseController>().AsImplementedInterfaces();

            if (FindFirstObjectByType<FilesTabController>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<FilesTabController>();

            builder.Register<InventoryHUDController>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<InventorySceneInit>(Lifetime.Singleton);
        }
    }
}
