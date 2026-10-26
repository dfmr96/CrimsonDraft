#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CrimsonDraft.UI;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.UI.MainMenu;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// The pause menu's panels, shown one at a time like the main menu's. Main (Resume / Settings /
    /// Quit) is navigated through the EventSystem with a single sliding <see cref="MenuSelectionBar"/>
    /// and its buttons appear one after another (<see cref="StaggeredReveal"/> on the panel).
    /// Both live inside one frame (<see cref="ScreenPowerOn"/>) that opens when the pause menu does;
    /// Settings just swaps what is inside it: three buttons (Video / Control / Sound) and, once one
    /// is confirmed, only that tab's content -- driven by <see cref="OptionsMenuNavigator"/>, the
    /// same component the main menu's Options uses.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject root            = null!;
        [SerializeField] private GameObject dimBackground   = null!;
        [Tooltip("Resume / Settings / Quit. Lleva el StaggeredReveal de los botones.")]
        [SerializeField] private GameObject mainPanel        = null!;
        [Tooltip("Marco de la pantalla (Screen): se abre en Y al abrir el pause. Contiene MainPanel y los botones / contenido de Settings.")]
        [SerializeField] private ScreenPowerOn screen = null!;
        [Tooltip("Los 3 botones de Settings (Screen/Buttons).")]
        [SerializeField] private GameObject settingsButtons = null!;
        [Tooltip("Contenido de las pestañas (Screen/Options_panel).")]
        [SerializeField] private GameObject settingsContent = null!;

        // Same shared Volume Inventory/pickup-preview/Inspect fade in -- gives the pause menu
        // the same CRT/PSX-boosted look while it's open. Optional: null just skips the fade.
        [SerializeField] private Volume? inventoryVolume;
        [SerializeField] private float   volumeFadeDuration = 0.3f;

        // Same confirm/cancel sounds as the inventory. Optional: null plays nothing.
        [SerializeField] private InventorySfxData? sfx;

        [Header("Main Panel")]
        [SerializeField] private Button resumeButton  = null!;
        [SerializeField] private Button optionsButton = null!;
        [SerializeField] private Button quitButton    = null!;

        [Header("Settings")]
        [SerializeField] private OptionsMenuNavigator optionsNavigator = null!;
        [Tooltip("Video: slider de gamma.")]
        [SerializeField] private GeneralMenuController gammaPanel = null!;
        [Tooltip("Control: Classic/Modern y Keyboard/Gamepad.")]
        [SerializeField] private ControlOptionsController controlsPanel = null!;
        [Tooltip("Sound: Master / SFX / UI SFX / Music.")]
        [SerializeField] private OptionsMenuController soundPanel = null!;

        // Video tab: the stacked UI CRT camera post-processes the whole frame (world included),
        // so while Video is up the pause canvas is drawn by a dedicated camera into a render
        // texture instead and composited over the screen -- the UI keeps its CRT look while the
        // world stays raw. Optional: null keeps the canvas on the stacked camera.
        [Header("Video tab (UI-only effect)")]
        [SerializeField] private Camera? stackedUiCamera;
        [SerializeField] private Camera? videoUiCamera;
        [SerializeField] private GameObject? videoUiComposite;

        // Menu backdrop layers hidden while the Video tab is up, so brightness is judged
        // against the actual scene instead of the dimmed pause background.
        [SerializeField] private GameObject[] videoHiddenBackdrop = System.Array.Empty<GameObject>();

        public Button ResumeButton            => this.resumeButton;
        public Button OptionsButton           => this.optionsButton;
        public Button QuitButton              => this.quitButton;
        public OptionsMenuNavigator OptionsNavigator => this.optionsNavigator;
        public GeneralMenuController GammaPanel => this.gammaPanel;
        public ControlOptionsController ControlsPanel => this.controlsPanel;
        public OptionsMenuController SoundPanel => this.soundPanel;

        [Header("PDA")]
        [Tooltip("El PDA que se despliega antes de que aparezca el menu y se pliega al cerrarlo. Opcional.")]
        [SerializeField] private PdaDevice? pda;

        public PdaDevice? Pda => this.pda;

        public GameObject FirstMainSelectable => this.resumeButton.gameObject;

        private GameObject? lastSelected;
        private bool controllerClaimed;

        /// <summary>
        /// A child scope (InventoryScope) re-creates the parent's scoped IInitializables, so two
        /// PauseMenuControllers can exist for this one view. Only the first to claim it may drive it:
        /// with two, every Back is handled twice (tab -> buttons, then buttons -> main panel).
        /// </summary>
        public bool TryClaimController()
        {
            if (this.controllerClaimed) return false;
            this.controllerClaimed = true;
            return true;
        }

        // Watches the EventSystem's selection (Resume / Settings / Quit) and plays the cursor
        // sound on every move between two real selectables -- opening or closing a panel selects
        // from/to nothing, so those keep their own confirm/cancel sounds only. Settings tabs make
        // their own cursor sounds (OptionsTabController).
        private void Update()
        {
            var current = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (current == this.lastSelected) return;

            if (this.lastSelected != null && current != null)
                PlayCursor();
            this.lastSelected = current;
        }

        private void OnDisable() => this.lastSelected = null;

        // The frame opens only when it becomes active, i.e. when the pause menu opens -- going
        // between Main and Settings keeps it as it is.
        public void ShowMain()
        {
            this.root.SetActive(true);
            this.dimBackground.SetActive(true);
            this.screen.gameObject.SetActive(true);
            this.settingsButtons.SetActive(false);
            this.settingsContent.SetActive(false);
            this.mainPanel.SetActive(true);
        }

        public void ShowSettings()
        {
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(false);
            this.settingsButtons.SetActive(true);
        }

        public void HideAll()
        {
            SetVideoUiMode(false);
            this.root.SetActive(false);
            this.mainPanel.SetActive(false);
            this.screen.gameObject.SetActive(false);
            SetBackdropVisible(true);
        }

        public void SetBackdropVisible(bool visible)
        {
            foreach (var layer in this.videoHiddenBackdrop)
                if (layer != null) layer.SetActive(visible);
        }

        /// <summary>Video tab on: UI-only CRT path (render texture + composite). Off: back to the stacked UI camera.</summary>
        public void SetVideoUiMode(bool on)
        {
            if (this.videoUiCamera == null || this.videoUiComposite == null) return;

            var canvas = GetComponent<Canvas>();
            this.videoUiCamera.gameObject.SetActive(on);
            this.videoUiComposite.SetActive(on);
            if (canvas != null) canvas.worldCamera = on ? this.videoUiCamera : this.stackedUiCamera;
        }

        public void PlayConfirm() => this.sfx?.PlayDecide(gameObject);
        public void PlayCancel()  => this.sfx?.PlayCancel(gameObject);
        public void PlayCursor()  => this.sfx?.PlayCursor(gameObject);

        public void FadeInventoryVolume(bool show) => VolumeFader.Fade(this.inventoryVolume, show, this.volumeFadeDuration);
    }
}
