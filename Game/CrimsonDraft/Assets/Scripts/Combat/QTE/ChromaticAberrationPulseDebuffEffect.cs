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
        private static readonly int AberrationId   = Shader.PropertyToID("_Aberration");
        private static readonly int ChannelRedId   = Shader.PropertyToID("_ChannelRed");
        private static readonly int ChannelGreenId = Shader.PropertyToID("_ChannelGreen");
        private static readonly int ChannelBlueId  = Shader.PropertyToID("_ChannelBlue");

        private readonly Material                material;
        private readonly ChromaticAberrationSlot slot;
        private readonly Color                   channelRed;
        private readonly Color                   channelGreen;
        private readonly Color                   channelBlue;

        public ChromaticAberrationPulseDebuffEffect(Material material, ChromaticAberrationSlot slot)
        {
            this.material     = material;
            this.slot         = slot;
            this.channelRed   = slot.ChannelRed;
            this.channelGreen = slot.ChannelGreen;
            this.channelBlue  = slot.ChannelBlue;
        }

        protected override void OnActivate()
        {
            this.material.SetColor(ChannelRedId, this.channelRed);
            this.material.SetColor(ChannelGreenId, this.channelGreen);
            this.material.SetColor(ChannelBlueId, this.channelBlue);
        }

        protected override void Apply(float envelope01)
        {
            float pulse = this.slot.Evaluate(this.ElapsedTotal);
            this.material.SetFloat(AberrationId, envelope01 * this.TargetIntensity * this.slot.MaxAmount * pulse);
        }

        protected override void OnDeactivate()
        {
            this.material.SetFloat(AberrationId, 0f);
            this.material.SetColor(ChannelRedId, Color.red);
            this.material.SetColor(ChannelGreenId, Color.green);
            this.material.SetColor(ChannelBlueId, Color.blue);
        }
    }
}
