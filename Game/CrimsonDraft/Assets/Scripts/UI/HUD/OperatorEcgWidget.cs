#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class OperatorEcgWidget : MonoBehaviour
    {
        [SerializeField] private OperatorEcgWaveGraphic? waveGraphic;
        [SerializeField] private TextMeshProUGUI? bpmValueText;
        [SerializeField] private TextMeshProUGUI? bpmLabelText;

        public void SetVitals(float hpRatio, int bpm, bool isActive)
        {
            EnsureReferences();

            var clampedBpm = OperatorEcgMath.ClampBpm(bpm);
            this.waveGraphic?.SetVitals(hpRatio, clampedBpm, isActive);

            if (this.bpmValueText != null)
            {
                this.bpmValueText.text = clampedBpm.ToString();
            }

            if (this.bpmLabelText != null)
            {
                this.bpmLabelText.text = "BPM";
            }
        }

        public void SetPixelStyle(int stepPx, int thicknessPx)
        {
            EnsureReferences();
            this.waveGraphic?.SetPixelStyle(stepPx, thicknessPx);
        }

        private void EnsureReferences()
        {
            this.waveGraphic ??= GetComponentInChildren<OperatorEcgWaveGraphic>(true);

            if (this.bpmValueText == null || this.bpmLabelText == null)
            {
                var textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var textComponent in textComponents)
                {
                    if (this.bpmValueText == null && textComponent.name.Contains("Value"))
                    {
                        this.bpmValueText = textComponent;
                        continue;
                    }

                    if (this.bpmLabelText == null && textComponent.name.Contains("Label"))
                    {
                        this.bpmLabelText = textComponent;
                    }
                }
            }
        }
    }
}
