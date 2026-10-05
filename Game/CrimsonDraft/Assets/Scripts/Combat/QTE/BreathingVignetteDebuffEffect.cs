#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Gentle, slow-breathing vignette -- an atmospheric "getting hurt" cue. All shader data (Color,
    // Smoothness, BreathHz, BreathDepth) comes from the profile's BreathingVignetteSlot, the same
    // fields tunable live in AimDebuffPreviewRig. Shares the same overlay material/Image as
    // VignetteDebuffEffect -- safe since tiers are mutually exclusive (see AimDebuffController).
    public sealed class BreathingVignetteDebuffEffect : FadingDebuffEffectBase
    {
        private static readonly int ColorId      = Shader.PropertyToID("_Color");
        private static readonly int IntensityId  = Shader.PropertyToID("_Intensity");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private readonly Material material;
        private readonly Color    color;
        private readonly float    smoothness;
        private readonly float    breathHz;
        private readonly float    breathDepth;

        public BreathingVignetteDebuffEffect(Material material, Color color, float smoothness, float breathHz, float breathDepth)
        {
            this.material    = material;
            this.color       = color;
            this.smoothness  = smoothness;
            this.breathHz    = breathHz;
            this.breathDepth = breathDepth;
        }

        protected override void OnActivate()
        {
            this.material.SetColor(ColorId, this.color);
            this.material.SetFloat(SmoothnessId, this.smoothness);
        }

        protected override void Apply(float envelope01)
        {
            float breath = 1f - this.breathDepth * 0.5f * (1f + Mathf.Sin(this.ElapsedTotal * this.breathHz * Mathf.PI * 2f));
            this.material.SetFloat(IntensityId, envelope01 * this.TargetIntensity * breath);
        }

        protected override void OnDeactivate() =>
            this.material.SetFloat(IntensityId, 0f);
    }
}
