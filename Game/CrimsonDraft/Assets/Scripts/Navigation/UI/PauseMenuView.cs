#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject root          = null!;
        [SerializeField] private GameObject mainPanel      = null!;
        [SerializeField] private GameObject optionsPanel   = null!;

        [Header("Main Panel")]
        [SerializeField] private Button resumeButton  = null!;
        [SerializeField] private Button optionsButton = null!;
        [SerializeField] private Button quitButton    = null!;

        [Header("Options Panel")]
        [SerializeField] private Slider masterSlider = null!;
        [SerializeField] private Slider sfxSlider     = null!;
        [SerializeField] private Slider musicSlider   = null!;

        public Button ResumeButton      => this.resumeButton;
        public Button OptionsButton     => this.optionsButton;
        public Button QuitButton        => this.quitButton;
        public Slider MasterSlider      => this.masterSlider;
        public Slider SfxSlider         => this.sfxSlider;
        public Slider MusicSlider       => this.musicSlider;

        public GameObject FirstMainSelectable    => this.resumeButton.gameObject;
        public GameObject FirstOptionsSelectable => this.masterSlider.gameObject;

        public void ShowMain()
        {
            this.root.SetActive(true);
            this.mainPanel.SetActive(true);
            this.optionsPanel.SetActive(false);
        }

        public void ShowOptions()
        {
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
    }
}
