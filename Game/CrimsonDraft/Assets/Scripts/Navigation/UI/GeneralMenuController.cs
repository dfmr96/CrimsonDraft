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

        [Tooltip("Imagen de calibracion (Gamma_Correction). Simula el gamma: su color pasa de negro (posicion 0) a blanco (ultima posicion) " +
                 "en pasos de gris iguales, uno por posicion de la barra. No depende del volumen global, que no alcanza a esta imagen " +
                 "porque vive en el pass de UI, y el gamma ni siquiera se aplica en el menu.")]
        [SerializeField] private Image? gammaPreview;

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

        private bool initialized;

        private void Awake() => EnsureInitialized();

        // The New Game copy sits on a canvas that is inactive until its second step, yet the
        // navigator calls into it (HideOutlines on open) before Unity has run its Awake().
        private void EnsureInitialized()
        {
            if (this.initialized) return;
            this.initialized          = true;
            this.fillImage            = this.fillBar.GetComponent<Image>();
            this.normalColor          = this.fillImage.color;
            this.baseAnchoredPosition = this.fillBar.anchoredPosition;
            this.baseWidth            = this.fillBar.rect.width;
        }

        // graphicsSettingsService is injected during the scope's own Awake(), which can land after
        // this component's OnEnable -- so Start() covers the first enable and OnEnable every later
        // one (the New Game copy lives on a canvas that is toggled on every visit).
        private bool subscribed;

        private void Start() => Subscribe();

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!this.subscribed) return;
            this.graphicsSettingsService.GammaChanged -= SyncFromService;
            this.subscribed = false;
        }

        private void Subscribe()
        {
            if (this.subscribed || this.graphicsSettingsService == null) return;
            this.subscribed = true;
            this.graphicsSettingsService.GammaChanged += SyncFromService;
            SyncFromService();
        }

        // Two instances exist (Options > Video and New Game), so a change made on either one -- or
        // loaded from PlayerPrefs -- has to reach the other.
        private void SyncFromService()
        {
            this.gammaValue = Mathf.RoundToInt(this.graphicsSettingsService.Gamma * this.graphicsSettingsService.GammaSteps);
            ApplyGamma();
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
            int clamped = Mathf.Clamp(this.gammaValue + direction, 0, this.graphicsSettingsService.GammaSteps);
            if (clamped == this.gammaValue) this.sfx.PlayKnobLimit(gameObject);
            else this.sfx.PlayKnobTick(gameObject);
            this.gammaValue = clamped;
            ApplyGamma();
            this.graphicsSettingsService.SetGamma((float)this.gammaValue / this.graphicsSettingsService.GammaSteps);
        }

        private void ApplyGamma()
        {
            EnsureInitialized();
            float fill = (float)this.gammaValue / this.graphicsSettingsService.GammaSteps;

            // The bar is center-pivoted: shrink the scale and shift by the trimmed half-width so
            // the LEFT edge stays fixed and it reads as filling left-to-right.
            var scale = this.fillBar.localScale;
            scale.x = fill;
            this.fillBar.localScale = scale;

            var pos = this.baseAnchoredPosition;
            pos.x -= (this.baseWidth / 2f) * (1f - fill);
            this.fillBar.anchoredPosition = pos;

            // fill is gammaValue / GammaSteps, so each bar position lands on its own evenly spaced
            // gray: 0 -> (0,0,0), GammaSteps -> (255,255,255).
            if (this.gammaPreview != null)
                this.gammaPreview.color = new Color(fill, fill, fill, 1f);
        }
    }
}
