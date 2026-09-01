#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject root            = null!;
        [SerializeField] private GameObject dimBackground   = null!;
        [SerializeField] private GameObject mainPanel        = null!;
        [SerializeField] private GameObject optionsPanel     = null!;
        [SerializeField] private GameObject brightnessPanel  = null!;

        [Header("Main Panel")]
        [SerializeField] private Button resumeButton  = null!;
        [SerializeField] private Button optionsButton = null!;
        [SerializeField] private Button quitButton    = null!;

        [Header("Options Panel")]
        [SerializeField] private Slider masterSlider          = null!;
        [SerializeField] private Slider sfxSlider              = null!;
        [SerializeField] private Slider musicSlider            = null!;
        [SerializeField] private Button adjustBrightnessButton = null!;

        [Header("Brightness Panel")]
        [SerializeField] private Slider gammaSlider = null!;

        public Button ResumeButton            => this.resumeButton;
        public Button OptionsButton           => this.optionsButton;
        public Button QuitButton              => this.quitButton;
        public Slider MasterSlider            => this.masterSlider;
        public Slider SfxSlider               => this.sfxSlider;
        public Slider MusicSlider             => this.musicSlider;
        public Button AdjustBrightnessButton  => this.adjustBrightnessButton;
        public Slider GammaSlider             => this.gammaSlider;

        public GameObject FirstMainSelectable       => this.resumeButton.gameObject;
        public GameObject FirstOptionsSelectable    => this.masterSlider.gameObject;
        public GameObject FirstBrightnessSelectable => this.gammaSlider.gameObject;

        public void ShowMain()
        {
            this.root.SetActive(true);
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(true);
            this.optionsPanel.SetActive(false);
            this.brightnessPanel.SetActive(false);
        }

        public void ShowOptions()
        {
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(true);
            this.brightnessPanel.SetActive(false);
        }

        // Hides the dim overlay along with the rest of the menu so brightness is calibrated
        // against the real, undimmed scene rather than a version already darkened by the menu.
        public void ShowBrightnessCalibration()
        {
            this.dimBackground.SetActive(false);
            this.optionsPanel.SetActive(false);
            this.brightnessPanel.SetActive(true);
        }

        public void HideAll()
        {
            this.root.SetActive(false);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(false);
            this.brightnessPanel.SetActive(false);
        }

        public void SetSliderValues(float master, float sfx, float music)
        {
            this.masterSlider.SetValueWithoutNotify(master);
            this.sfxSlider.SetValueWithoutNotify(sfx);
            this.musicSlider.SetValueWithoutNotify(music);
        }

        public void SetGammaValue(float gamma) => this.gammaSlider.SetValueWithoutNotify(gamma);
    }
}
