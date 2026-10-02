#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Per-effect-kind shader data, shared by AimDebuffTierProfile (production) and
    // AimDebuffPreviewRig (the dev preview tool) -- the same fields in both places so a value
    // tuned live in the preview can be copied verbatim into a tier profile.
    [System.Serializable]
    public sealed class TunnelVignetteSlot
    {
        public bool Enabled;
        [Range(0f, 1f)] public float Intensity = 0.5f;
        public Color Color = Color.black;
        [Range(0.01f, 1f)] public float Smoothness = 0.2f;
    }

    [System.Serializable]
    public sealed class BreathingVignetteSlot
    {
        public bool Enabled;
        [Range(0f, 1f)] public float Intensity = 0.3f;
        public Color Color = Color.black;
        [Range(0.01f, 1f)] public float Smoothness = 0.35f;
        public float BreathHz = 0.35f;
        [Range(0f, 1f)] public float BreathDepth = 0.35f;

        // Runtime-only: elapsed time since this slot was last enabled, used by
        // AimDebuffPreviewRig to animate the breathing wave directly (production instead tracks
        // this inside FadingDebuffEffectBase, per shot).
        [System.NonSerialized] public float ElapsedTotal;
    }

    [System.Serializable]
    public sealed class GrainSlot
    {
        public bool Enabled;
        [Range(0f, 1f)] public float Intensity = 0.5f;
        public Color Color = Color.white;
        [Range(0.1f, 4f)] public float Response = 1.2f;
        [Range(1f, 8f)] public float Scale = 2.5f;
    }

    [System.Serializable]
    public sealed class TintSlot
    {
        public bool Enabled;
        [Range(0f, 1f)] public float Intensity = 0.5f;
        public Color Color = Color.white;
    }

    [System.Serializable]
    public sealed class HeartbeatSlot
    {
        public bool Enabled;
        [Range(0f, 1f)] public float Intensity = 0.5f;
        public Color Color = Color.black;
        public float CalmBeatInterval = 1.1f;
        public float PanicBeatInterval = 0.5f;
        [Range(0f, 1f)] public float DubStrength = 0.5f;
        [Range(0f, 1f)] public float DubOffsetFraction = 0.16f;
        public float PulseDecay = 16f;

        // Runtime-only -- see BreathingVignetteSlot.ElapsedTotal.
        [System.NonSerialized] public float ElapsedTotal;
    }

    [System.Serializable]
    public sealed class PulseDistortSlot
    {
        public bool Enabled;
        [Range(0f, 1f)] public float Intensity = 0.5f;
        public float PulseHz = 1f;
        [Range(0f, 0.05f)] public float MaxAmount = 0.02f;

        // Runtime-only -- see BreathingVignetteSlot.ElapsedTotal.
        [System.NonSerialized] public float ElapsedTotal;
    }
}
