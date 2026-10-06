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

        private readonly Material        material;
        private readonly PulseDistortSlot slot;

        public BlurPulseDebuffEffect(Material material, PulseDistortSlot slot)
        {
            this.material = material;
            this.slot     = slot;
        }

        protected override void OnActivate() { }

        protected override void Apply(float envelope01)
        {
            float pulse = this.slot.Evaluate(this.ElapsedTotal);
            this.material.SetFloat(BlurSizeId, envelope01 * this.TargetIntensity * this.slot.MaxAmount * pulse);
        }

        protected override void OnDeactivate() =>
            this.material.SetFloat(BlurSizeId, 0f);
    }
}
