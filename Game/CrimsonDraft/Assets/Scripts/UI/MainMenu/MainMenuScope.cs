#nullable enable

using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuScope : LifetimeScope
    {
        [SerializeField] private MainMenuSfxData mainMenuSfxData = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.mainMenuSfxData);

            builder.RegisterComponentInHierarchy<MainMenuController>();
            builder.RegisterComponentInHierarchy<MainMenuCameraTravel>();
            builder.RegisterComponentInHierarchy<OptionsTabController>();
            builder.RegisterComponentInHierarchy<OptionsMenuController>();
            builder.RegisterComponentInHierarchy<GeneralMenuController>();
        }
    }
}
