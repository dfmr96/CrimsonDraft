#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Pulses the shared CrimsonDraft/UI/AimDebuffDistort material's _Aberration in sharp beats.
    // PulseHz/MaxAmount come from the profile's PulseDistortSlot, the same fields tunable live in
    // AimDebuffPreviewRig. That material is assigned directly on the aim selectors/silhouette
    // Images, so this distorts only those specific elements -- never the rest of the screen -- by
    // splitting and re-sampling their own sprite texture (see AimDebuffDistort.shader), no grab
    // pass or background access involved.
    public sealed class ChromaticAberrationPulseDebuffEffect : FadingDebuffEffectBase
    {
        private static readonly int AberrationId = Shader.PropertyToID("_Aberration");

        private readonly Material material;
        private readonly float    pulseHz;
        private readonly float    maxAmount;

        public ChromaticAberrationPulseDebuffEffect(Material material, float pulseHz, float maxAmount)
        {
            this.material  = material;
            this.pulseHz   = pulseHz;
            this.maxAmount = maxAmount;
        }

        protected override void OnActivate() { }

        protected override void Apply(float envelope01)
        {
            // abs(sin) gives a repeated sharp pulse (0 -> 1 -> 0) instead of a smooth breathe.
            float pulse = Mathf.Abs(Mathf.Sin(this.ElapsedTotal * this.pulseHz * Mathf.PI));
            this.material.SetFloat(AberrationId, envelope01 * this.TargetIntensity * this.maxAmount * pulse);
        }

        protected override void OnDeactivate() =>
            this.material.SetFloat(AberrationId, 0f);
    }
}
