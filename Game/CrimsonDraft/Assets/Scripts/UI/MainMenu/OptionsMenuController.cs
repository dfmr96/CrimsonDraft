#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Sound tab content only (Master/SFX/Music knobs). Has no cursor or input of its own --
    /// <see cref="OptionsTabController"/> owns navigation across tabs + the sound channel list
    /// and drives this panel purely through <see cref="ShowOutline"/>/<see cref="HideOutlines"/>/<see cref="Adjust"/>.
    /// </summary>
    public sealed class OptionsMenuController : MonoBehaviour, IOptionsChannelPanel
    {
        [System.Serializable]
        private sealed class SoundChannel
        {
            [SerializeField] public Transform          knob        = null!;
            [SerializeField] public TextMeshProUGUI     valueLabel  = null!;
            [Tooltip("Hijo con el mesh duplicado + material de contorno; se prende/apaga al (de)seleccionar esta perilla.")]
            [SerializeField] public GameObject          outline     = null!;

            [System.NonSerialized] public Quaternion baseRotation;
            [System.NonSerialized] public int        value;
        }

        [Header("Channels (navigation order: top to bottom)")]
        [SerializeField] private SoundChannel master = null!;
        [SerializeField] private SoundChannel sfx    = null!;
        [SerializeField] private SoundChannel music  = null!;

        [Header("Knob")]
        [Tooltip("Eje local (previo a la rotación base) sobre el que gira cada perilla, como un tornillo.")]
        [SerializeField] private Vector3 spinAxis     = Vector3.up;
        [SerializeField] private float   sweepDegrees = 270f;
        [SerializeField] private int     stepPercent  = 5;
        [SerializeField] private int     startPercent = 100;

        private SoundChannel[] channels = null!;

        public int ChannelCount => this.channels.Length;

        private void Awake()
        {
            this.channels = new[] { this.master, this.sfx, this.music };
            foreach (var channel in this.channels)
            {
                channel.baseRotation = channel.knob.localRotation;
                channel.value        = this.startPercent;
                Apply(channel);
                channel.outline.SetActive(false);
            }
        }

        public void ShowOutline(int index)
        {
            for (int i = 0; i < this.channels.Length; i++)
                this.channels[i].outline.SetActive(i == index);
        }

        public void HideOutlines()
        {
            foreach (var channel in this.channels)
                channel.outline.SetActive(false);
        }

        public void Adjust(int index, int direction)
        {
            var channel = this.channels[index];
            channel.value = Mathf.Clamp(channel.value + direction * this.stepPercent, 0, 100);
            Apply(channel);
        }

        private void Apply(SoundChannel channel)
        {
            float angle = Mathf.Lerp(0f, this.sweepDegrees, channel.value / 100f);
            channel.knob.localRotation = channel.baseRotation * Quaternion.AngleAxis(angle, this.spinAxis);
            channel.valueLabel.text    = $"{channel.value}%";
        }
    }
}
