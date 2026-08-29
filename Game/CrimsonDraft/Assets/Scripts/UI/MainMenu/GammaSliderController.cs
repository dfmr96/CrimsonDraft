#nullable enable

using CrimsonDraft.Infrastructure.Graphics;
using UnityEngine;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Bridges the New Game gamma-calibration slider to the shared, persistent gamma system
    /// (GraphicsSettingsService) instead of poking a single scene-local Volume directly. The old
    /// direct-Volume approach never wrote to PlayerPrefs and never touched any Volume outside
    /// MainMenu, so a value set on this slider didn't persist and didn't carry into Deck_B/C or
    /// Combat the way Pause/Inventory's gamma control does.
    /// </summary>
    public sealed class GammaSliderController : MonoBehaviour
    {
        private IGraphicsSettingsService graphicsSettingsService = null!;

        [Inject]
        public void Construct(IGraphicsSettingsService graphicsSettingsService)
        {
            this.graphicsSettingsService = graphicsSettingsService;
        }

        public void SetGamma(float sliderValue) => this.graphicsSettingsService.SetGamma(sliderValue);
    }
}
