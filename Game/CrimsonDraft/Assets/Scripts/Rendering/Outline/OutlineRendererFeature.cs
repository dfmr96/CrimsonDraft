#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.Rendering.Outline
{
    // Screen-space selection outline, same technique as Unity's own editor selection gizmo:
    // whatever sits on outlineLayer gets redrawn solid-white into a separate mask texture
    // (OutlineMask.shader), then a full-screen pass edge-detects that mask and draws the rim
    // over the camera color (OutlineComposite.shader). Camera-angle independent and works on
    // any mesh/sub-mesh count, unlike the KnobOutline inverted-hull technique used for the
    // main-menu knobs, which needs one contiguous mesh per outlined target.
    public sealed class OutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private LayerMask       outlineLayer       = 0;
        [SerializeField] private Color           outlineColor       = new(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private float           outlineWidthPixels = 2f;
        [SerializeField] private RenderPassEvent renderEvent        = RenderPassEvent.BeforeRenderingPostProcessing;

        private Shader?   maskShader;
        private Shader?   compositeShader;
        private Material? maskMaterial;
        private Material? compositeMaterial;
        private OutlinePass? pass;

        public override void Create()
        {
            if (this.maskShader == null)
                this.maskShader = Shader.Find("Hidden/CrimsonDraft/OutlineMask");
            if (this.compositeShader == null)
                this.compositeShader = Shader.Find("Hidden/CrimsonDraft/OutlineComposite");

            if (this.maskMaterial == null && this.maskShader != null)
                this.maskMaterial = CoreUtils.CreateEngineMaterial(this.maskShader);
            if (this.compositeMaterial == null && this.compositeShader != null)
                this.compositeMaterial = CoreUtils.CreateEngineMaterial(this.compositeShader);

            this.pass ??= new OutlinePass { renderPassEvent = this.renderEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (this.maskMaterial == null || this.compositeMaterial == null || this.pass == null)
                return;

            this.pass.Setup(this.maskMaterial, this.compositeMaterial, this.outlineLayer, this.outlineColor, this.outlineWidthPixels);
            renderer.EnqueuePass(this.pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (this.maskMaterial != null)
            {
                CoreUtils.Destroy(this.maskMaterial);
                this.maskMaterial = null;
            }
            if (this.compositeMaterial != null)
            {
                CoreUtils.Destroy(this.compositeMaterial);
                this.compositeMaterial = null;
            }
            base.Dispose(disposing);
        }
    }
}
