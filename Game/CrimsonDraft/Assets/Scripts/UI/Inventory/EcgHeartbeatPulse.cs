#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    [DisallowMultipleComponent]
    public sealed class EcgHeartbeatPulse : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage = null!;

        [Header("Health State Colors (100-75 / 75-50 / 50-25 / 25-0)")]
        [SerializeField] private Color colorStable   = new Color(0.4901961f, 0.7058824f, 0.29803923f); // 7db44c
        [SerializeField] private Color colorCaution  = new Color(0.6901961f, 0.7058824f, 0.29803923f); // b0b44c
        [SerializeField] private Color colorWarning  = new Color(0.6901961f, 0.5568628f, 0.29803923f); // b08e4c
        [SerializeField] private Color colorCritical = new Color(0.73333335f, 0.44705883f, 0.26666668f); // bb7244

        [Header("Pulse Period (seconds per beat, faster at low HP)")]
        [SerializeField, Min(0.05f)] private float pulsePeriodStable   = 1.8f;
        [SerializeField, Min(0.05f)] private float pulsePeriodCaution  = 1.3f;
        [SerializeField, Min(0.05f)] private float pulsePeriodWarning  = 0.9f;
        [SerializeField, Min(0.05f)] private float pulsePeriodCritical = 0.5f;

        [Header("Pulse Intensity (no alpha change, never pure white/black)")]
        [SerializeField, Range(0f, 0.5f)] private float valueSwing = 0.22f;
        [SerializeField, Range(0f, 0.5f)] private float saturationSwing = 0.15f;
        [SerializeField, Range(0f, 1f)] private float minValue = 0.12f;
        [SerializeField, Range(0f, 1f)] private float maxValue = 0.95f;

        private Color baseColor;
        private float pulsePeriod;
        private float t;

        #region Unity Lifecycle

        private void OnEnable()
        {
            this.t = 0f;
            this.ApplyPulse(0f);
        }

        private void Update()
        {
            this.t += Time.unscaledDeltaTime;
            var phase = (this.t % this.pulsePeriod) / this.pulsePeriod;
            var pulse = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            this.ApplyPulse(pulse);
        }

        #endregion

        #region Health State

        public void SetHealthState(float hpRatio)
        {
            hpRatio = Mathf.Clamp01(hpRatio);

            if (hpRatio <= 0.25f)      { this.baseColor = this.colorCritical; this.pulsePeriod = this.pulsePeriodCritical; }
            else if (hpRatio <= 0.50f) { this.baseColor = this.colorWarning;  this.pulsePeriod = this.pulsePeriodWarning;  }
            else if (hpRatio <= 0.75f) { this.baseColor = this.colorCaution;  this.pulsePeriod = this.pulsePeriodCaution;  }
            else                       { this.baseColor = this.colorStable;  this.pulsePeriod = this.pulsePeriodStable;   }
        }

        #endregion

        #region Pulse

        private void ApplyPulse(float pulse)
        {
            if (this.backgroundImage == null)
                return;

            Color.RGBToHSV(this.baseColor, out var h, out var s, out var v);
            var newV = Mathf.Clamp(v + (pulse - 0.5f) * 2f * this.valueSwing, this.minValue, this.maxValue);
            var newS = Mathf.Clamp01(s - pulse * this.saturationSwing);

            var rgb = Color.HSVToRGB(h, newS, newV);
            rgb.a = this.backgroundImage.color.a;
            this.backgroundImage.color = rgb;
        }

        #endregion
    }
}
