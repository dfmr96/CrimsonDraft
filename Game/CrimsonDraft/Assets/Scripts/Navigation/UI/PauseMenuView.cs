#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CrimsonDraft.UI;

namespace CrimsonDraft.Navigation.UI
{
    public enum OptionsWindow { Audio, Video, Controls }

    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject root            = null!;
        [SerializeField] private GameObject dimBackground   = null!;
        [SerializeField] private GameObject mainPanel        = null!;
        [SerializeField] private GameObject optionsPanel     = null!;

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

        [Header("Options Categories")]
        [SerializeField] private Button audioButton    = null!;
        [SerializeField] private Button videoButton    = null!;
        [SerializeField] private Button controlsButton = null!;

        [Header("Options Windows")]
        [SerializeField] private GameObject windowsRoot    = null!;
        [SerializeField] private GameObject audioWindow    = null!;
        [SerializeField] private GameObject videoWindow    = null!;
        [SerializeField] private GameObject controlsWindow = null!;

        [Header("Audio Window")]
        [SerializeField] private Slider masterSlider = null!;
        [SerializeField] private Slider sfxSlider    = null!;
        [SerializeField] private Slider musicSlider  = null!;

        [Header("Video Window")]
        [SerializeField] private Slider gammaSlider  = null!;

        // Menu backdrop layers hidden while the Video window is open, so brightness is judged
        // against the actual scene instead of the dimmed pause background.
        [SerializeField] private GameObject[] videoHiddenBackdrop = System.Array.Empty<GameObject>();

        // Controls window: driven by ControlOptionsController (same panel as the main menu's
        // Options > Control), navigated by PauseMenuController rather than the EventSystem.
        [Header("Controls Window")]
        [SerializeField] private ControlOptionsController controlsPanel = null!;

        public Button ResumeButton            => this.resumeButton;
        public Button OptionsButton           => this.optionsButton;
        public Button QuitButton              => this.quitButton;
        public Button AudioButton             => this.audioButton;
        public Button VideoButton             => this.videoButton;
        public Button ControlsButton          => this.controlsButton;
        public Slider MasterSlider            => this.masterSlider;
        public Slider SfxSlider               => this.sfxSlider;
        public Slider MusicSlider             => this.musicSlider;
        public Slider GammaSlider             => this.gammaSlider;
        public ControlOptionsController ControlsPanel => this.controlsPanel;

        public GameObject FirstMainSelectable    => this.resumeButton.gameObject;
        public GameObject FirstOptionsSelectable => this.audioButton.gameObject;

        public void ShowMain()
        {
            this.root.SetActive(true);
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(true);
            this.optionsPanel.SetActive(false);
            HideWindows();
        }

        public void ShowOptions()
        {
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(true);
            HideWindows();
        }

        // The window replaces the category list; Back (ShowOptions) brings the list back.
        public void ShowWindow(OptionsWindow window)
        {
            this.optionsPanel.SetActive(false);
            this.windowsRoot.SetActive(true);
            this.audioWindow.SetActive(window == OptionsWindow.Audio);
            this.videoWindow.SetActive(window == OptionsWindow.Video);
            this.controlsWindow.SetActive(window == OptionsWindow.Controls);
            SetBackdropVisible(window != OptionsWindow.Video);
        }

        public void HideWindows()
        {
            SetBackdropVisible(true);
            this.windowsRoot.SetActive(false);
            this.audioWindow.SetActive(false);
            this.videoWindow.SetActive(false);
            this.controlsWindow.SetActive(false);
        }

        public void HideAll()
        {
            this.root.SetActive(false);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(false);
            HideWindows();
        }

        private void SetBackdropVisible(bool visible)
        {
            foreach (var layer in this.videoHiddenBackdrop)
                if (layer != null) layer.SetActive(visible);
        }

        public Button CategoryButton(OptionsWindow window) => window switch
        {
            OptionsWindow.Audio => this.audioButton,
            OptionsWindow.Video => this.videoButton,
            _                   => this.controlsButton,
        };

        // Null when the window has nothing selectable yet (e.g. a row still disabled while
        // it's being built) -- caller keeps focus on the category button instead.
        public GameObject? FirstWindowSelectable(OptionsWindow window)
        {
            Selectable? target = window switch
            {
                OptionsWindow.Audio => this.masterSlider,
                OptionsWindow.Video => this.gammaSlider,
                _                   => null,
            };
            return target != null && target.gameObject.activeInHierarchy ? target.gameObject : null;
        }

        public void SetSliderValues(float master, float sfx, float music)
        {
            this.masterSlider.SetValueWithoutNotify(master);
            this.sfxSlider.SetValueWithoutNotify(sfx);
            this.musicSlider.SetValueWithoutNotify(music);
        }

        public void SetGammaValue(float gamma) => this.gammaSlider.SetValueWithoutNotify(gamma);

        public void PlayConfirm() => this.sfx?.PlayDecide(gameObject);
        public void PlayCancel()  => this.sfx?.PlayCancel(gameObject);
        public void PlayCursor()  => this.sfx?.PlayCursor(gameObject);

        public void FadeInventoryVolume(bool show) => VolumeFader.Fade(this.inventoryVolume, show, this.volumeFadeDuration);
    }
}
