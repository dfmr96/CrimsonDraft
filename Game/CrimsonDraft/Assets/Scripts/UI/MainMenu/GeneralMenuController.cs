#nullable enable

using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Infrastructure.Graphics;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Video tab content: just the Gamma slider (IGraphicsSettingsService.GammaSteps discrete
    /// positions, same as every other gamma control). Selection is shown by tinting the fill bar
    /// white; deselecting restores its original green.
    /// </summary>
    public sealed class GeneralMenuController : MonoBehaviour, IOptionsChannelPanel
    {
        [Tooltip("Barra de relleno (Bg_bar/Bar) -- se escala con el valor y se tiñe de blanco al seleccionarla.")]
        [SerializeField] private RectTransform fillBar = null!;
        [SerializeField] private Color selectedColor = Color.white;

        private Image fillImage = null!;
        private Color normalColor;
        private Vector2 baseAnchoredPosition;
        private float baseWidth;
        private int gammaValue;
        private IGraphicsSettingsService graphicsSettingsService = null!;
        private MainMenuSfxData sfx = null!;

        public int ChannelCount => 1;

        [Inject]
        public void Construct(IGraphicsSettingsService graphicsSettingsService, MainMenuSfxData sfx)
        {
            this.graphicsSettingsService = graphicsSettingsService;
            this.sfx                     = sfx;
        }

        private void Awake()
        {
            this.fillImage            = this.fillBar.GetComponent<Image>();
            this.normalColor          = this.fillImage.color;
            this.baseAnchoredPosition = this.fillBar.anchoredPosition;
            this.baseWidth            = this.fillBar.rect.width;
        }

        private void Start()
        {
            // graphicsSettingsService is injected during the scope's own Awake(); read it in Start().
            this.gammaValue = Mathf.RoundToInt(this.graphicsSettingsService.Gamma * this.graphicsSettingsService.GammaSteps);
            ApplyGamma();
        }

        public bool IsSelectable(int index) => true;

        public bool RepeatsOnHold(int index) => true;

        public void Confirm(int index) { }

        public void ShowOutline(int index) => this.fillImage.color = this.selectedColor;

        public void HideOutlines() => this.fillImage.color = this.normalColor;

        public void Adjust(int index, int direction)
        {
            int clamped = Mathf.Clamp(this.gammaValue + direction, 0, this.graphicsSettingsService.GammaSteps);
            if (clamped == this.gammaValue) this.sfx.PlayKnobLimit(gameObject);
            else this.sfx.PlayKnobTick(gameObject);
            this.gammaValue = clamped;
            ApplyGamma();
            this.graphicsSettingsService.SetGamma((float)this.gammaValue / this.graphicsSettingsService.GammaSteps);
        }

        private void ApplyGamma()
        {
            float fill = (float)this.gammaValue / this.graphicsSettingsService.GammaSteps;

            // The bar is center-pivoted: shrink the scale and shift by the trimmed half-width so
            // the LEFT edge stays fixed and it reads as filling left-to-right.
            var scale = this.fillBar.localScale;
            scale.x = fill;
            this.fillBar.localScale = scale;

            var pos = this.baseAnchoredPosition;
            pos.x -= (this.baseWidth / 2f) * (1f - fill);
            this.fillBar.anchoredPosition = pos;
        }
    }
}
