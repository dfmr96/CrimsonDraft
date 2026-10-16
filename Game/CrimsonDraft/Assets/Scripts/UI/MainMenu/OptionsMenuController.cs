#nullable enable

using CrimsonDraft.Navigation.UI;
using System;
using CrimsonDraft.Infrastructure.Audio;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Sound tab content (Master/SFX/Music). No input of its own -- <see cref="OptionsTabController"/>
    /// drives it. The selected channel's label, percentage and underline turn white; the underline
    /// of every other channel stays the label's green.
    /// </summary>
    public sealed class OptionsMenuController : MonoBehaviour, IOptionsChannelPanel
    {
        [Serializable]
        private sealed class SoundChannel
        {
            [SerializeField] public TextMeshProUGUI label      = null!;
            [SerializeField] public TextMeshProUGUI valueLabel = null!;
            [SerializeField] public Image?          underline;

            [NonSerialized] public Color labelColor;
            [NonSerialized] public Color valueColor;
            [NonSerialized] public int   value;
        }

        [Header("Channels (navigation order: top to bottom)")]
        [SerializeField] private SoundChannel master = null!;
        [SerializeField] private SoundChannel sfx    = null!;
        [SerializeField] private SoundChannel music  = null!;

        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private int   stepPercent   = 5;

        private SoundChannel[]    channels = null!;
        private Action<float>[]   applyToService = null!;
        private IAudioSettingsService audioSettingsService = null!;
        private MainMenuSfxData      sfxData               = null!;

        public int ChannelCount => 3;

        [Inject]
        public void Construct(IAudioSettingsService audioSettingsService, MainMenuSfxData sfxData)
        {
            this.audioSettingsService = audioSettingsService;
            this.sfxData              = sfxData;
        }

        private void Awake()
        {
            this.channels = new[] { this.master, this.sfx, this.music };
            foreach (var channel in this.channels)
            {
                channel.labelColor = channel.label.color;
                channel.valueColor = channel.valueLabel.color;
            }
            HideOutlines();
        }

        private void Start()
        {
            // audioSettingsService is injected during the scope's own Awake(); read it in Start().
            this.applyToService = new Action<float>[]
            {
                this.audioSettingsService.SetMasterVolume,
                this.audioSettingsService.SetSfxVolume,
                this.audioSettingsService.SetMusicVolume,
            };

            float[] startValues01 =
            {
                this.audioSettingsService.MasterVolume,
                this.audioSettingsService.SfxVolume,
                this.audioSettingsService.MusicVolume,
            };

            for (int i = 0; i < this.channels.Length; i++)
            {
                this.channels[i].value = Mathf.RoundToInt(startValues01[i] * 100f);
                Apply(this.channels[i]);
            }
        }

        public bool IsSelectable(int index) => true;

        public bool RepeatsOnHold(int index) => true;

        public void Confirm(int index) { }

        public void ShowOutline(int index)
        {
            HideOutlines();
            this.channels[index].label.color      = this.selectedColor;
            this.channels[index].valueLabel.color = this.selectedColor;
            if (this.channels[index].underline != null) this.channels[index].underline!.color = this.selectedColor;
        }

        public void HideOutlines()
        {
            foreach (var channel in this.channels)
            {
                channel.label.color      = channel.labelColor;
                channel.valueLabel.color = channel.valueColor;
                if (channel.underline != null) channel.underline.color = channel.labelColor;
            }
        }

        public void Adjust(int index, int direction)
        {
            var channel = this.channels[index];
            int clamped = Mathf.Clamp(channel.value + direction * this.stepPercent, 0, 100);
            if (clamped == channel.value) this.sfxData.PlayKnobLimit(gameObject);
            else this.sfxData.PlayKnobTick(gameObject);
            channel.value = clamped;
            Apply(channel);
            this.applyToService[index](channel.value / 100f);
        }

        private static void Apply(SoundChannel channel) => channel.valueLabel.text = $"{channel.value}%";
    }
}
