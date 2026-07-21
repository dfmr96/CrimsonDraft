#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.UI
{
    public sealed class InventoryScope : LifetimeScope
    {
        [SerializeField] private InventorySfxData inventorySfxData = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            // IInventoryService, IOperatorRoster, ICombineService, CombineRecipeLibrary
            // are resolved from the parent NavigationScope — do NOT register them here.

            builder.RegisterInstance(this.inventorySfxData);

            builder.RegisterComponentInHierarchy<InventoryPopulator>().AsImplementedInterfaces().AsSelf();

            builder.RegisterComponentInHierarchy<GridCursor>();
            builder.RegisterComponentInHierarchy<ItemContextMenu>();
            builder.RegisterComponentInHierarchy<PartyPanelView>();
            builder.RegisterComponentInHierarchy<TabManager>();
            builder.RegisterComponentInHierarchy<InspectPanel>();
            builder.RegisterComponentInHierarchy<InventoryGridGroup>();
            builder.RegisterComponentInHierarchy<InventoryOpenCloseController>().AsSelf().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<CanvasCameraBinder>().AsImplementedInterfaces();

            if (FindFirstObjectByType<FilesTabController>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<FilesTabController>().AsSelf().AsImplementedInterfaces();

            if (FindFirstObjectByType<MapTabController>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<MapTabController>();

            builder.Register<InventoryHUDController>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<InventorySceneInit>(Lifetime.Singleton);
        }
    }
}
