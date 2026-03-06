#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class OperatorEcgWaveGraphic : MaskableGraphic
    {
        private static readonly Vector2 DefaultMinSize = new(50f, 40f);
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private float[]? baseSamples;
        private int baseWidth;
        private float baseVisibleBeats;
        private Material? runtimeMaterial;

        [SerializeField, Min(1)] private int lineThickness = 2;
        [SerializeField, Min(0.1f)] private float visibleBeats = 2.5f;
        [SerializeField] private Vector2 minSize = DefaultMinSize;

        [SerializeField, Range(0f, 1f)] private float hpRatio = 1f;
        [SerializeField] private int bpm = 72;
        [SerializeField] private bool isActive = true;

        internal float HpRatio => this.hpRatio;
        internal int Bpm => this.bpm;
        internal bool IsActive => this.isActive;
        internal int LineThickness => this.lineThickness;

        protected override void OnEnable()
        {
            base.OnEnable();
            this.runtimeMaterial = null;
            EnsureMinSize();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureMinSize();
        }
#endif

        public void SetVitals(float hpRatio, int bpm, bool isActive)
        {
            this.hpRatio = OperatorEcgMath.ClampHpRatio(hpRatio);
            this.bpm = OperatorEcgMath.ClampBpm(bpm);
            this.isActive = isActive;
            SetVerticesDirty();
        }

        public void SetLineThickness(int thicknessPx)
        {
            this.lineThickness = Mathf.Max(1, thicknessPx);
            SetVerticesDirty();
        }

        internal IReadOnlyList<float> BuildNormalizedSamples(int widthPx)
        {
            if (widthPx <= 0)
            {
                return System.Array.Empty<float>();
            }

            EnsureBaseSamples(widthPx);
            var count = this.baseSamples!.Length;
            var samples = new float[count];
            var beatsPerSecond = this.bpm / 60f;
            var timeBeats = Time.unscaledTime * beatsPerSecond;
            var shift = Mathf.FloorToInt((timeBeats / this.visibleBeats) * widthPx);
            var shiftIndex = Mod(shift, count);

            for (var i = 0; i < count; i++)
            {
                var sourceIndex = i + shiftIndex;
                if (sourceIndex >= count)
                {
                    sourceIndex -= count;
                }

                samples[i] = this.baseSamples[sourceIndex];
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
            const int step = 1;
            var lastSampleIndex = samples.Count - 1;

            for (var i = 0; i < samples.Count - 1; i++)
            {
                var x0 = Mathf.Min(xMax, xMin + (i * step));
                var x1 = Mathf.Min(xMax, xMin + ((i + 1) * step));
                var sampleIndex0 = Mathf.Clamp(i * step, 0, lastSampleIndex);
                var sampleIndex1 = Mathf.Clamp((i + 1) * step, 0, lastSampleIndex);
                var y0 = centerY + (samples[sampleIndex0] * amplitude);
                var y1 = centerY + (samples[sampleIndex1] * amplitude);

                var p0 = new Vector2(x0, y0);
                var p1 = new Vector2(x1, y1);

                var u0 = Mathf.Clamp01(Mathf.InverseLerp(xMin, xMax, p0.x));
                var u1 = Mathf.Clamp01(Mathf.InverseLerp(xMin, xMax, p1.x));
                AddSegment(vh, p0, p1, u0, u1, this.lineThickness, waveColor);
            }
        }

        private void Update()
        {
            UpdateSweep();
        }

        private void EnsureMinSize()
        {
            var safeMinSize = new Vector2(
                Mathf.Max(0f, this.minSize.x),
                Mathf.Max(0f, this.minSize.y));

            if (safeMinSize == Vector2.zero)
            {
                return;
            }

            var rect = rectTransform.rect;
            if (rect.width < safeMinSize.x)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, safeMinSize.x);
            }

            if (rect.height < safeMinSize.y)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, safeMinSize.y);
            }
        }

        private static void AddSegment(VertexHelper vh, Vector2 p0, Vector2 p1, float u0, float u1, int thickness, Color color)
        {
            var delta = p1 - p0;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var halfThickness = Mathf.Max(1, thickness) * 0.5f;
            var normal = new Vector2(-delta.y, delta.x).normalized * halfThickness;
            var v0 = p0 - normal;
            var v1 = p0 + normal;
            var v2 = p1 + normal;
            var v3 = p1 - normal;
            var index = vh.currentVertCount;

            var uv0 = new Vector2(u0, 0f);
            var uv1 = new Vector2(u1, 0f);
            vh.AddVert(v0, color, uv0);
            vh.AddVert(v1, color, uv0);
            vh.AddVert(v2, color, uv1);
            vh.AddVert(v3, color, uv1);

            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private void UpdateSweep()
        {
            var sweepMaterial = GetRuntimeMaterial();
            if (sweepMaterial == null)
            {
                return;
            }

            sweepMaterial.SetFloat(SweepId, 1f);
        }

        private void EnsureBaseSamples(int widthPx)
        {
            if (this.baseSamples != null
                && this.baseWidth == widthPx
                && Mathf.Approximately(this.baseVisibleBeats, this.visibleBeats))
            {
                return;
            }

            var sampleCount = widthPx + 1;
            this.baseSamples = new float[sampleCount];
            this.baseWidth = widthPx;
            this.baseVisibleBeats = this.visibleBeats;

            for (var i = 0; i < sampleCount; i++)
            {
                var normalizedX = i / (float)widthPx;
                var phase = normalizedX * this.visibleBeats;
                this.baseSamples[i] = OperatorEcgMath.EcgSample(phase);
            }

            this.baseSamples[0] = 0f;
            this.baseSamples[^1] = 0f;
        }

        private Material? GetRuntimeMaterial()
        {
            if (this.runtimeMaterial != null)
            {
                return this.runtimeMaterial;
            }

            if (material == null)
            {
                return null;
            }

            this.runtimeMaterial = Instantiate(material);
            material = this.runtimeMaterial;
            return this.runtimeMaterial;
        }

        private static int Mod(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
