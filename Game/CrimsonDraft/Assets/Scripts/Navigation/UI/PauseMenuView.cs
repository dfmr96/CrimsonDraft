#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CrimsonDraft.UI;
using CrimsonDraft.UI.MainMenu;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// The pause menu's panels. Main (Resume / Settings / Quit) is navigated through the
    /// EventSystem with a single sliding <see cref="MenuSelectionBar"/>; Settings works like the
    /// main menu's Options screen -- the tab row (Video / Control / Sound) and the active tab's
    /// content are on screen together, driven by <see cref="OptionsTabController"/>.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject root            = null!;
        [SerializeField] private GameObject dimBackground   = null!;
        [SerializeField] private GameObject mainPanel        = null!;
        [Tooltip("Fila de tabs de Settings (Video / Control / Sound).")]
        [SerializeField] private GameObject optionsPanel     = null!;
        [Tooltip("Contenido de los tabs (Options-Windows).")]
        [SerializeField] private GameObject windowsRoot      = null!;

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

        [Header("Settings tabs")]
        [SerializeField] private OptionsTabController optionsTabs = null!;
        [Tooltip("Video: slider de gamma.")]
        [SerializeField] private SliderGammaPanel gammaPanel = null!;
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
        public OptionsTabController OptionsTabs => this.optionsTabs;
        public SliderGammaPanel GammaPanel      => this.gammaPanel;
        public ControlOptionsController ControlsPanel => this.controlsPanel;
        public OptionsMenuController SoundPanel => this.soundPanel;

        public GameObject FirstMainSelectable => this.resumeButton.gameObject;

        private GameObject? lastSelected;

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

        public void ShowMain()
        {
            this.root.SetActive(true);
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(true);
            this.optionsPanel.SetActive(false);
            this.windowsRoot.SetActive(false);
        }

        public void ShowOptions()
        {
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(true);
            this.windowsRoot.SetActive(true);
        }

        public void HideAll()
        {
            SetVideoUiMode(false);
            this.root.SetActive(false);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(false);
            this.windowsRoot.SetActive(false);
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
