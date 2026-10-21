#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Screen-wide "latido" (heartbeat) -- a brief lub-dub dark pulse via a CrimsonDraft/UI/AimDebuffTint
    // material. All rhythm data (Color, CalmBeatInterval, PanicBeatInterval, DubStrength,
    // DubOffsetFraction, PulseDecay) comes from the profile's HeartbeatSlot, the same fields
    // tunable live in AimDebuffPreviewRig. Scoped to the overlay Image's own rect (AimView), not
    // the whole screen.
    public sealed class ScreenHeartbeatDebuffEffect : FadingDebuffEffectBase
    {
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private readonly Material material;
        private readonly Color    color;
        private readonly float    calmBeatInterval;
        private readonly float    panicBeatInterval;
        private readonly float    dubStrength;
        private readonly float    dubOffsetFraction;
        private readonly float    pulseDecay;

        public ScreenHeartbeatDebuffEffect(
            Material material, Color color,
            float calmBeatInterval, float panicBeatInterval,
            float dubStrength, float dubOffsetFraction, float pulseDecay)
        {
            this.material           = material;
            this.color              = color;
            this.calmBeatInterval   = calmBeatInterval;
            this.panicBeatInterval  = panicBeatInterval;
            this.dubStrength        = dubStrength;
            this.dubOffsetFraction  = dubOffsetFraction;
            this.pulseDecay         = pulseDecay;
        }

        protected override void OnActivate() =>
            this.material.SetColor(ColorId, this.color);

        protected override void Apply(float envelope01)
        {
            float beatInterval = Mathf.Lerp(this.calmBeatInterval, this.panicBeatInterval, this.TargetIntensity);
            int   beatIndex    = Mathf.FloorToInt(this.ElapsedTotal / Mathf.Max(beatInterval, 0.001f));
            float cycleT       = this.ElapsedTotal - beatIndex * beatInterval;

            float lub  = PulseEnvelope(cycleT, this.pulseDecay);
            float dub  = PulseEnvelope(cycleT - beatInterval * this.dubOffsetFraction, this.pulseDecay) * this.dubStrength;
            float beat = Mathf.Max(lub, dub);

            this.material.SetFloat(IntensityId, envelope01 * this.TargetIntensity * beat);
        }

        protected override void OnDeactivate() =>
            this.material.SetFloat(IntensityId, 0f);

        private static float PulseEnvelope(float t, float decay) => t < 0f ? 0f : Mathf.Exp(-decay * t);
    }
}
