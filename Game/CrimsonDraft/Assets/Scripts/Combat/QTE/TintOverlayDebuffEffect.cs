#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Fades a CrimsonDraft/UI/AimDebuffTint material's _Intensity in/out -- a flat color wash
    // scoped to the overlay Image's own rect. Color comes from the profile's TintSlot, the same
    // field tunable live in AimDebuffPreviewRig. Shared by Desaturation and PaletteShift, each via
    // its own dedicated material/Image instance so they never interfere.
    public sealed class TintOverlayDebuffEffect : FadingDebuffEffectBase
    {
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private readonly Material material;
        private readonly Color    color;

        public TintOverlayDebuffEffect(Material material, Color color)
        {
            this.material = material;
            this.color    = color;
        }

        protected override void OnActivate() =>
            this.material.SetColor(ColorId, this.color);

        protected override void Apply(float envelope01) =>
            this.material.SetFloat(IntensityId, envelope01 * this.TargetIntensity);

        protected override void OnDeactivate() =>
            this.material.SetFloat(IntensityId, 0f);
    }
}
