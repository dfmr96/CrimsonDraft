#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
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

        // Same shared Volume Inventory/pickup-preview/Inspect fade in -- gives the pause menu
        // the same CRT/PSX-boosted look while it's open. Optional: null just skips the fade.
        [SerializeField] private Volume? inventoryVolume;
        [SerializeField] private float   volumeFadeDuration = 0.3f;

        [Header("Main Panel")]
        [SerializeField] private Button resumeButton  = null!;
        [SerializeField] private Button optionsButton = null!;
        [SerializeField] private Button quitButton    = null!;

        [Header("Options Panel")]
        [SerializeField] private Slider masterSlider = null!;
        [SerializeField] private Slider sfxSlider    = null!;
        [SerializeField] private Slider musicSlider  = null!;
        [SerializeField] private Slider gammaSlider  = null!;
        [SerializeField] private Toggle modernToggle = null!;
        [SerializeField] private Toggle classicToggle = null!;

        public Button ResumeButton            => this.resumeButton;
        public Button OptionsButton           => this.optionsButton;
        public Button QuitButton              => this.quitButton;
        public Slider MasterSlider            => this.masterSlider;
        public Slider SfxSlider               => this.sfxSlider;
        public Slider MusicSlider             => this.musicSlider;
        public Slider GammaSlider             => this.gammaSlider;
        public Toggle ModernToggle            => this.modernToggle;
        public Toggle ClassicToggle           => this.classicToggle;

        public GameObject FirstMainSelectable    => this.resumeButton.gameObject;
        public GameObject FirstOptionsSelectable => this.masterSlider.gameObject;

        public void ShowMain()
        {
            this.root.SetActive(true);
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(true);
            this.optionsPanel.SetActive(false);
        }

        public void ShowOptions()
        {
            this.dimBackground.SetActive(true);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(true);
        }

        public void HideAll()
        {
            this.root.SetActive(false);
            this.mainPanel.SetActive(false);
            this.optionsPanel.SetActive(false);
        }

        public void SetSliderValues(float master, float sfx, float music)
        {
            this.masterSlider.SetValueWithoutNotify(master);
            this.sfxSlider.SetValueWithoutNotify(sfx);
            this.musicSlider.SetValueWithoutNotify(music);
        }

        public void SetGammaValue(float gamma) => this.gammaSlider.SetValueWithoutNotify(gamma);

        public void FadeInventoryVolume(bool show) => VolumeFader.Fade(this.inventoryVolume, show, this.volumeFadeDuration);

        public void SetControlToggle(bool isClassic)
        {
            this.modernToggle.SetIsOnWithoutNotify(!isClassic);
            this.classicToggle.SetIsOnWithoutNotify(isClassic);
        }
    }
}
