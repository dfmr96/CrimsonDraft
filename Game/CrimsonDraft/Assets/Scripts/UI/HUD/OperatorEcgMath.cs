#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI.HUD
{
    internal static class OperatorEcgMath
    {
        private const int MinBpm = 60;
        private const int MaxBpm = 160;
        private static readonly Color Green = new(0.1f, 0.95f, 0.2f, 1f);
        private static readonly Color Yellow = new(1f, 0.85f, 0.2f, 1f);
        private static readonly Color Red = new(1f, 0.2f, 0.2f, 1f);

        internal static int ClampBpm(int bpm) => Mathf.Clamp(bpm, MinBpm, MaxBpm);

        internal static float ClampHpRatio(float hpRatio) => Mathf.Clamp01(hpRatio);

        internal static float ComputeAmplitude(float contentHeight, float hpRatio)
        {
            var safeHeight = Mathf.Max(0f, contentHeight);
            var ratio = ClampHpRatio(hpRatio);
            return safeHeight * (0.12f + (0.30f * ratio));
        }

        internal static Color ComputeEcgColor(float hpRatio)
        {
            var ratio = ClampHpRatio(hpRatio);

            if (ratio > 0.6f)
            {
                return Green;
            }

            if (ratio > 0.3f)
            {
                var tMid = Mathf.InverseLerp(0.3f, 0.6f, ratio);
                return Color.Lerp(Yellow, Green, tMid);
            }

            var tLow = Mathf.InverseLerp(0f, 0.3f, ratio);
            return Color.Lerp(Red, Yellow, tLow);
        }

        internal static float EcgSample(float phase01)
        {
            var phase = Mathf.Repeat(phase01, 1f);

            if (phase > 0.10f && phase < 0.14f)
            {
                return -WaveWindow(phase, 0.10f, 0.14f) * 0.20f;
            }

            if (phase > 0.14f && phase < 0.19f)
            {
                return WaveWindow(phase, 0.14f, 0.19f);
            }

            if (phase > 0.19f && phase < 0.24f)
            {
                return -WaveWindow(phase, 0.19f, 0.24f) * 0.25f;
            }

            if (phase > 0.32f && phase < 0.52f)
            {
                return WaveWindow(phase, 0.32f, 0.52f) * 0.28f;
            }

            return 0f;
        }

        private static float WaveWindow(float phase, float start, float end)
        {
            var t = Mathf.InverseLerp(start, end, phase);
            return Mathf.Sin(t * Mathf.PI);
        }
    }
}
