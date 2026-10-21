#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuScope : LifetimeScope
    {
        [SerializeField] private MainMenuSfxData mainMenuSfxData = null!;

        // Two ControlOptionsController instances exist (Options > Control and New Game), so they
        // can't both be found by type -- inject each explicitly instead.
        [SerializeField] private ControlOptionsController optionsControls = null!;
        [SerializeField] private ControlOptionsController newGameControls = null!;

        // Same for the gamma control: Options > Video and the second New Game step.
        [SerializeField] private GeneralMenuController optionsGamma = null!;
        [SerializeField] private GeneralMenuController newGameGamma = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.mainMenuSfxData);
            builder.RegisterBuildCallback(resolver =>
            {
                var controlScheme = resolver.Resolve<IControlSchemeService>();
                var sfx           = resolver.Resolve<InventorySfxData>(); // shared UI sounds, from the root scope
                this.optionsControls.Setup(controlScheme, () => sfx.PlayCursor(this.optionsControls.gameObject), () => sfx.PlayDecide(this.optionsControls.gameObject));
                this.newGameControls.Setup(controlScheme, () => sfx.PlayCursor(this.newGameControls.gameObject), () => sfx.PlayDecide(this.newGameControls.gameObject));
            });
            builder.RegisterComponentInHierarchy<NewGameNavigator>();
            builder.RegisterComponentInHierarchy<MainMenuController>();
            builder.RegisterComponentInHierarchy<MainMenuCameraTravel>();
            builder.RegisterComponentInHierarchy<OptionsTabController>();
            builder.RegisterComponentInHierarchy<OptionsMenuController>();
            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Inject(this.optionsGamma);
                resolver.Inject(this.newGameGamma);
            });
        }
    }
}
