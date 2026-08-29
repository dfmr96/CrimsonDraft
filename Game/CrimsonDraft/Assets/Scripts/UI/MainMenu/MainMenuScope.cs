#nullable enable

using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MainMenuController>();
            builder.RegisterComponentInHierarchy<MainMenuCameraTravel>();
            builder.RegisterComponentInHierarchy<OptionsTabController>();
            builder.RegisterComponentInHierarchy<OptionsMenuController>();
            builder.RegisterComponentInHierarchy<GeneralMenuController>();
            builder.RegisterComponentInHierarchy<GammaSliderController>();
        }
    }
}
