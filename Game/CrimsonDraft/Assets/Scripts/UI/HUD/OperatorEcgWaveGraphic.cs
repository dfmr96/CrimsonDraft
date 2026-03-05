#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class OperatorEcgWaveGraphic : MaskableGraphic
    {
        [SerializeField, Min(1)] private int pixelStep = 2;
        [SerializeField, Min(1)] private int lineThickness = 2;
        [SerializeField, Min(0.1f)] private float visibleBeats = 2.5f;

        [SerializeField, Range(0f, 1f)] private float hpRatio = 1f;
        [SerializeField] private int bpm = 72;
        [SerializeField] private bool isActive = true;

        internal float HpRatio => this.hpRatio;
        internal int Bpm => this.bpm;
        internal bool IsActive => this.isActive;
        internal int PixelStep => this.pixelStep;
        internal int LineThickness => this.lineThickness;

        public void SetVitals(float hpRatio, int bpm, bool isActive)
        {
            this.hpRatio = OperatorEcgMath.ClampHpRatio(hpRatio);
            this.bpm = OperatorEcgMath.ClampBpm(bpm);
            this.isActive = isActive;
            SetVerticesDirty();
        }

        public void SetPixelStyle(int stepPx, int thicknessPx)
        {
            this.pixelStep = Mathf.Max(1, stepPx);
            this.lineThickness = Mathf.Max(1, thicknessPx);
            SetVerticesDirty();
        }

        internal IReadOnlyList<float> BuildNormalizedSamples(int widthPx)
        {
            if (widthPx <= 0)
            {
                return System.Array.Empty<float>();
            }

            var step = Mathf.Max(1, this.pixelStep);
            var sampleCount = (widthPx / step) + 1;
            var samples = new float[sampleCount];
            var beatsPerSecond = this.bpm / 60f;
            var timeBeats = Time.unscaledTime * beatsPerSecond;

            for (var i = 0; i < sampleCount; i++)
            {
                var xPx = i * step;
                var normalizedX = xPx / (float)widthPx;
                var phase = timeBeats + (normalizedX * this.visibleBeats);
                samples[i] = OperatorEcgMath.EcgSample(phase);
            }

            return samples;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!this.isActive)
            {
                return;
            }

            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var samples = BuildNormalizedSamples(Mathf.RoundToInt(rect.width));
            if (samples.Count < 2)
            {
                return;
            }

            var waveColor = OperatorEcgMath.ComputeEcgColor(this.hpRatio);
            var amplitude = OperatorEcgMath.ComputeAmplitude(rect.height, this.hpRatio);
            var centerY = rect.yMin + (rect.height * 0.5f);
            var xMin = rect.xMin;
            var xMax = rect.xMax;
            var step = Mathf.Max(1, this.pixelStep);

            for (var i = 0; i < samples.Count - 1; i++)
            {
                var x0 = Mathf.Min(xMax, xMin + (i * step));
                var x1 = Mathf.Min(xMax, xMin + ((i + 1) * step));
                var y0 = centerY + (samples[i] * amplitude);
                var y1 = centerY + (samples[i + 1] * amplitude);

                var p0 = new Vector2(Quantize(x0), Quantize(y0));
                var p1 = new Vector2(Quantize(x1), Quantize(y1));

                AddSegment(vh, p0, p1, this.lineThickness, waveColor);
            }
        }

        private float Quantize(float value)
        {
            var step = Mathf.Max(1, this.pixelStep);
            return Mathf.Round(value / step) * step;
        }

        private static void AddSegment(VertexHelper vh, Vector2 p0, Vector2 p1, int thickness, Color color)
        {
            var delta = p1 - p0;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var normal = new Vector2(-delta.y, delta.x).normalized * (Mathf.Max(1, thickness) * 0.5f);
            var v0 = p0 - normal;
            var v1 = p0 + normal;
            var v2 = p1 + normal;
            var v3 = p1 - normal;
            var index = vh.currentVertCount;

            vh.AddVert(v0, color, Vector2.zero);
            vh.AddVert(v1, color, Vector2.zero);
            vh.AddVert(v2, color, Vector2.zero);
            vh.AddVert(v3, color, Vector2.zero);

            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
