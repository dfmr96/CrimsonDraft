#nullable enable

using CrimsonDraft.Infrastructure.Graphics;
using UnityEngine;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// General tab content: Language and Control are physical knobs too, same as Sound, but
    /// locked for now (Adjust is a no-op -- the knob/outline exist, rotating just isn't wired up
    /// yet). Gamma is a real 0-100 value: rotates its knob exactly like a volume knob and also
    /// keeps the canvas fill bar in sync. Selection is shown purely via each knob's outline
    /// (never in the flat canvas), matching Sound.
    /// </summary>
    public sealed class GeneralMenuController : MonoBehaviour, IOptionsChannelPanel
    {
        private const int LanguageIndex = 0;
        private const int GammaIndex    = 1;
        private const int ControlIndex  = 2;

        [System.Serializable]
        private sealed class LockedChannel
        {
            [Tooltip("Perilla física (sin rotar por ahora) -- solo existe para colgar el outline.")]
            [SerializeField] public GameObject outline = null!;
        }

        [System.Serializable]
        private sealed class GammaChannel
        {
            [SerializeField] public Transform     knob    = null!;
            [SerializeField] public GameObject    outline = null!;
            [Tooltip("Barra de relleno (Bg_bar/Bar) del canvas -- se mantiene en sync con la perilla.")]
            [SerializeField] public RectTransform fillBar = null!;

            [System.NonSerialized] public Quaternion baseRotation;
            [System.NonSerialized] public Vector2    baseAnchoredPosition;
            [System.NonSerialized] public float      baseWidth;
        }

        [Header("Language / Control -- bloqueados por ahora")]
        [SerializeField] private LockedChannel language = null!;
        [SerializeField] private LockedChannel control  = null!;

        [Header("Gamma (0-100)")]
        [SerializeField] private GammaChannel gamma = null!;
        [Tooltip("Eje local (previo a la rotación base) sobre el que gira la perilla de Gamma.")]
        [SerializeField] private Vector3 spinAxis     = Vector3.up;
        [SerializeField] private float   sweepDegrees = 270f;
        [SerializeField] private int     stepPercent  = 5;

        private GameObject[] outlines = null!;
        private int          gammaValue;
        private IGraphicsSettingsService graphicsSettingsService = null!;

        public int ChannelCount => 3;

        [Inject]
        public void Construct(IGraphicsSettingsService graphicsSettingsService)
        {
            this.graphicsSettingsService = graphicsSettingsService;
        }

        private void Awake()
        {
            this.outlines = new[] { this.language.outline, this.gamma.outline, this.control.outline };
            foreach (var outline in this.outlines)
                outline.SetActive(false);

            this.gamma.baseRotation         = this.gamma.knob.localRotation;
            this.gamma.baseAnchoredPosition = this.gamma.fillBar.anchoredPosition;
            this.gamma.baseWidth            = this.gamma.fillBar.rect.width;
        }

        private void Start()
        {
            // Reads graphicsSettingsService, injected via Construct() during the scope's own
            // Awake() -- deferring to Start() guarantees that already ran (see
            // MainMenuController.Start()/OptionsMenuController.Start() for the same reasoning).
            this.gammaValue = Mathf.RoundToInt(this.graphicsSettingsService.Gamma * 100f);
            ApplyGamma();
        }

        public void ShowOutline(int index)
        {
            for (int i = 0; i < this.outlines.Length; i++)
                this.outlines[i].SetActive(i == index);
        }

        public void HideOutlines()
        {
            foreach (var outline in this.outlines)
                outline.SetActive(false);
        }

        public void Adjust(int index, int direction)
        {
            if (index != GammaIndex) return; // Language and Control are locked for now.

            this.gammaValue = Mathf.Clamp(this.gammaValue + direction * this.stepPercent, 0, 100);
            ApplyGamma();
            this.graphicsSettingsService.SetGamma(this.gammaValue / 100f);
        }

        private void ApplyGamma()
        {
            float angle = Mathf.Lerp(0f, this.sweepDegrees, this.gammaValue / 100f);
            this.gamma.knob.localRotation = this.gamma.baseRotation * Quaternion.AngleAxis(angle, this.spinAxis);

            // The bar is center-pivoted, so shrinking its scale alone would shrink toward the
            // middle from both sides. Nudging the anchored position by the same half-width we
            // just trimmed keeps the LEFT edge fixed, so it reads as a slider filling left-to-right.
            float fill = this.gammaValue / 100f;
            var scale = this.gamma.fillBar.localScale;
            scale.x = fill;
            this.gamma.fillBar.localScale = scale;

            var pos = this.gamma.baseAnchoredPosition;
            pos.x -= (this.gamma.baseWidth / 2f) * (1f - fill);
            this.gamma.fillBar.anchoredPosition = pos;
        }
    }
}
