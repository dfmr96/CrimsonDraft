#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Pulses the shared CrimsonDraft/UI/AimDebuffDistort material's _BlurSize in slow waves.
    // PulseHz/MaxAmount come from the profile's PulseDistortSlot, the same fields tunable live in
    // AimDebuffPreviewRig. That material is assigned directly on the aim selectors/silhouette
    // Images, so this blurs only those specific elements -- their own sprite texture, multi-tap
    // sampled (see AimDebuffDistort.shader) -- never the rest of the screen. No grab pass or depth
    // buffer involved, unlike the old URP Depth of Field version.
    public sealed class BlurPulseDebuffEffect : FadingDebuffEffectBase
    {
        private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");

        private readonly Material material;
        private readonly float    pulseHz;
        private readonly float    maxAmount;

        public BlurPulseDebuffEffect(Material material, float pulseHz, float maxAmount)
        {
            this.material  = material;
            this.pulseHz   = pulseHz;
            this.maxAmount = maxAmount;
        }

        protected override void OnActivate() { }

        protected override void Apply(float envelope01)
        {
            float pulse = 0.5f * (1f + Mathf.Sin(this.ElapsedTotal * this.pulseHz * Mathf.PI * 2f));
            this.material.SetFloat(BlurSizeId, envelope01 * this.TargetIntensity * this.maxAmount * pulse);
        }

        protected override void OnDeactivate() =>
            this.material.SetFloat(BlurSizeId, 0f);
    }
}
