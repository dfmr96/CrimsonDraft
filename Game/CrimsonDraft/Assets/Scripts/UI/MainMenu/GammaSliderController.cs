#nullable enable

using CrimsonDraft.Infrastructure.Graphics;
using UnityEngine;
using UnityEngine.UI;
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
        private Slider slider = null!;
        private IGraphicsSettingsService graphicsSettingsService = null!;

        [Inject]
        public void Construct(IGraphicsSettingsService graphicsSettingsService)
        {
            this.graphicsSettingsService = graphicsSettingsService;
        }

        private void Awake() => this.slider = GetComponent<Slider>();

        private void OnEnable()
        {
            // MainMenuCameraTravel toggles this panel via SetActive() on every visit, but the
            // Slider's own m_Value only reflects whatever it was last left at (or its serialized
            // editor default) -- it never tracked Gamma set from the Options knob. Without this,
            // the handle could sit far from the real persisted value, so the first nudge here
            // jumped gamma by a huge amount instead of the intended small step.
            this.slider.SetValueWithoutNotify(this.graphicsSettingsService.Gamma * this.graphicsSettingsService.GammaSteps);
        }

        // Slider is configured as whole numbers 0..GammaSteps (9 positions) so it matches every
        // other gamma control in the game -- rescale back down to the 0..1 value the service stores.
        public void SetGamma(float sliderValue) => this.graphicsSettingsService.SetGamma(sliderValue / this.graphicsSettingsService.GammaSteps);
    }
}
