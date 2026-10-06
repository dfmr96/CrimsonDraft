#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Fades a CrimsonDraft/UI/AimDebuffGrain material's _Intensity in/out. Color/Response/Scale
    // come from the profile's GrainSlot, the same fields tunable live in AimDebuffPreviewRig.
    // Scoped to the overlay Image's own rect (AimView), not the whole screen.
    public sealed class GrainDebuffEffect : FadingDebuffEffectBase
    {
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ResponseId  = Shader.PropertyToID("_Response");
        private static readonly int ScaleId     = Shader.PropertyToID("_Scale");

        private readonly Material material;
        private readonly Color    color;
        private readonly float    response;
        private readonly float    scale;

        public GrainDebuffEffect(Material material, Color color, float response, float scale)
        {
            this.material = material;
            this.color    = color;
            this.response = response;
            this.scale    = scale;
        }

        protected override void OnActivate()
        {
            this.material.SetColor(ColorId, this.color);
            this.material.SetFloat(ResponseId, this.response);
            this.material.SetFloat(ScaleId, this.scale);
        }

        protected override void Apply(float envelope01) =>
            this.material.SetFloat(IntensityId, envelope01 * this.TargetIntensity);

        protected override void OnDeactivate() =>
            this.material.SetFloat(IntensityId, 0f);
    }
}
