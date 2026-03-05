using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.UI.HUD;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorEcgWaveGraphicTests
    {
        [Test]
        public void SetVitals_clampsHpAndBpm()
        {
            using var scope = new WaveScope();
            scope.Graphic.SetVitals(2f, 220, true);

            Assert.AreEqual(1f, scope.Graphic.HpRatio, 0.0001f);
            Assert.AreEqual(160, scope.Graphic.Bpm);
        }

        [Test]
        public void SetVitals_inactive_setsInactiveState()
        {
            using var scope = new WaveScope();
            scope.Graphic.SetVitals(0.5f, 72, false);

            Assert.False(scope.Graphic.IsActive);
        }

        [Test]
        public void SetPixelStyle_appliesPixelAndThickness()
        {
            using var scope = new WaveScope();
            scope.Graphic.SetPixelStyle(2, 2);

            Assert.AreEqual(2, scope.Graphic.PixelStep);
            Assert.AreEqual(2, scope.Graphic.LineThickness);
        }

        [Test]
        public void BuildNormalizedSamples_usesPixelStepAcrossWidth()
        {
            using var scope = new WaveScope();
            scope.Graphic.SetPixelStyle(2, 2);

            var samples = scope.Graphic.BuildNormalizedSamples(10);

            Assert.AreEqual(6, samples.Count);
        }

        private sealed class WaveScope : System.IDisposable
        {
            private readonly GameObject root;
            public OperatorEcgWaveGraphic Graphic { get; }

            public WaveScope()
            {
                this.root = new GameObject("wave-test");
                this.Graphic = this.root.AddComponent<OperatorEcgWaveGraphic>();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(this.root);
            }
        }
    }
}
