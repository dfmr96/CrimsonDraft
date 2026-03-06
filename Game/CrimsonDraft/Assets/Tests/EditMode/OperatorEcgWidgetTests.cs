using NUnit.Framework;
using TMPro;
using UnityEngine;
using CrimsonDraft.UI.HUD;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorEcgWidgetTests
    {
        [Test]
        public void SetVitals_updatesBpmValueText()
        {
            using var scope = new WidgetScope();

            scope.Widget.SetVitals(0.75f, 72, true);

            Assert.AreEqual("72", scope.BpmValueText.text);
        }

        [Test]
        public void SetVitals_forwardsToWaveGraphic()
        {
            using var scope = new WidgetScope();

            scope.Widget.SetVitals(0.75f, 72, true);

            Assert.AreEqual(0.75f, scope.WaveGraphic.HpRatio, 0.0001f);
            Assert.AreEqual(72, scope.WaveGraphic.Bpm);
            Assert.True(scope.WaveGraphic.IsActive);
        }

        [Test]
        public void SetLineThickness_forwardsLineThicknessToWaveGraphic()
        {
            using var scope = new WidgetScope();

            scope.Widget.SetLineThickness(2);

            Assert.AreEqual(2, scope.WaveGraphic.LineThickness);
        }

        [Test]
        public void SetHealthState_updatesHpRatioAndActiveState()
        {
            using var scope = new WidgetScope();
            scope.Widget.SetVitals(1f, 90, true);

            scope.Widget.SetHealthState(0.25f, false);

            Assert.AreEqual(0.25f, scope.WaveGraphic.HpRatio, 0.0001f);
            Assert.AreEqual(90, scope.WaveGraphic.Bpm);
            Assert.IsFalse(scope.WaveGraphic.IsActive);
        }

        private sealed class WidgetScope : System.IDisposable
        {
            private readonly GameObject root;
            public OperatorEcgWidget Widget { get; }
            public OperatorEcgWaveGraphic WaveGraphic { get; }
            public TextMeshProUGUI BpmValueText { get; }

            public WidgetScope()
            {
                this.root = new GameObject("ecg-widget-root", typeof(RectTransform));
                this.Widget = this.root.AddComponent<OperatorEcgWidget>();

                var waveObject = new GameObject("Wave", typeof(RectTransform));
                waveObject.transform.SetParent(this.root.transform, false);
                this.WaveGraphic = waveObject.AddComponent<OperatorEcgWaveGraphic>();

                var valueObject = new GameObject("BpmValue", typeof(RectTransform));
                valueObject.transform.SetParent(this.root.transform, false);
                this.BpmValueText = valueObject.AddComponent<TextMeshProUGUI>();

                var labelObject = new GameObject("BpmLabel", typeof(RectTransform));
                labelObject.transform.SetParent(this.root.transform, false);
                _ = labelObject.AddComponent<TextMeshProUGUI>();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(this.root);
            }
        }
    }
}
