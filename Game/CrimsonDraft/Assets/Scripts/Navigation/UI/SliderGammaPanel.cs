#nullable enable

using CrimsonDraft.Infrastructure.Graphics;
using CrimsonDraft.UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// Pause menu's Video tab content: the single Gamma slider, navigated like every other
    /// Options tab (see <see cref="OptionsTabController"/>) instead of through the EventSystem.
    /// Left/Right steps the slider by one of the shared gamma positions and pushes the value to
    /// <see cref="IGraphicsSettingsService"/> (live, since the pause menu lifts its gamma
    /// suppression while this tab is up -- unlike the main menu, here the gamma is judged against
    /// the actual scene). The slider also follows changes made elsewhere through GammaChanged.
    /// </summary>
    public sealed class SliderGammaPanel : MonoBehaviour, IOptionsChannelPanel
    {
        [SerializeField] private Slider slider = null!;
        [Tooltip("Relleno del slider: se tine de blanco mientras el canal esta seleccionado.")]
        [SerializeField] private Image fillImage = null!;
        [SerializeField] private Color selectedColor = Color.white;

        private IGraphicsSettingsService graphics = null!;
        private MainMenuSfxData sfx = null!;
        private Color normalColor;
        private bool initialized;
        private bool subscribed;

        public int ChannelCount => 1;

        [Inject]
        public void Construct(IGraphicsSettingsService graphics, MainMenuSfxData sfx)
        {
            this.graphics = graphics;
            this.sfx      = sfx;
        }

        private void EnsureInitialized()
        {
            if (this.initialized) return;
            this.initialized = true;
            this.normalColor = this.fillImage.color;

            this.slider.wholeNumbers = true;
            this.slider.minValue     = 0;
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (this.graphics == null || this.subscribed) { Sync(); return; }
            this.subscribed = true;
            this.graphics.GammaChanged += Sync;
            Sync();
        }

        private void OnDisable()
        {
            if (!this.subscribed || this.graphics == null) return;
            this.graphics.GammaChanged -= Sync;
            this.subscribed = false;
        }

        private void Sync()
        {
            if (this.graphics == null) return;
            EnsureInitialized();
            this.slider.maxValue = this.graphics.GammaSteps;
            this.slider.SetValueWithoutNotify(Mathf.RoundToInt(this.graphics.Gamma * this.graphics.GammaSteps));
        }

        public bool IsSelectable(int index) => true;

        public bool RepeatsOnHold(int index) => true;

        public void Confirm(int index) { }

        public void ShowOutline(int index)
        {
            EnsureInitialized();
            this.fillImage.color = this.selectedColor;
        }

        public void HideOutlines()
        {
            EnsureInitialized();
            this.fillImage.color = this.normalColor;
        }

        public void Adjust(int index, int direction)
        {
            EnsureInitialized();
            int steps   = this.graphics.GammaSteps;
            int current = Mathf.RoundToInt(this.slider.value);
            int clamped = Mathf.Clamp(current + direction, 0, steps);

            if (clamped == current) this.sfx.PlayKnobLimit(gameObject);
            else this.sfx.PlayKnobTick(gameObject);

            this.slider.SetValueWithoutNotify(clamped);
            this.graphics.SetGamma((float)clamped / steps);
        }
    }
}
