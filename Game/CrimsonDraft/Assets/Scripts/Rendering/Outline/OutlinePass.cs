#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.Rendering.Outline
{
    internal sealed class OutlinePass : ScriptableRenderPass
    {
        private const string MaskPassName      = "Outline Mask";
        private const string CompositePassName = "Outline Composite";

        private Material  maskMaterial      = null!;
        private Material  compositeMaterial = null!;
        private LayerMask outlineLayer;
        private Color     outlineColor;
        private float     outlineWidthPixels;

        // Legacy/unlit tags only -- these switches/levers use plain Lit/Unlit materials, no
        // custom lit passes to worry about matching.
        private readonly List<ShaderTagId> shaderTagIds = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        public OutlinePass()
        {
            requiresIntermediateTexture = true;
        }

        public void Setup(Material mask, Material composite, LayerMask layer, Color color, float widthPixels)
        {
            this.maskMaterial       = mask;
            this.compositeMaterial  = composite;
            this.outlineLayer       = layer;
            this.outlineColor       = color;
            this.outlineWidthPixels = widthPixels;
        }

        private sealed class MaskPassData
        {
            public RendererListHandle rendererListHandle;
        }

        private sealed class CompositePassData
        {
            public TextureHandle src;
            public TextureHandle mask;
            public Material      material = null!;
            public Color         color;
            public float         widthPixels;
            public Vector4       maskTexelSize;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData  resourceData  = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData    cameraData    = frameData.Get<UniversalCameraData>();
            UniversalLightData     lightData     = frameData.Get<UniversalLightData>();

            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;

            TextureDesc maskDesc = renderGraph.GetTextureDesc(src);
            maskDesc.name        = "_OutlineMaskTex";
            maskDesc.clearBuffer = true;
            maskDesc.clearColor  = Color.clear;
            TextureHandle maskHandle = renderGraph.CreateTexture(maskDesc);

            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(MaskPassName, out var maskPassData))
            {
                var sortFlags      = cameraData.defaultOpaqueSortFlags;
                var filterSettings = new FilteringSettings(RenderQueueRange.opaque, this.outlineLayer);
                var drawSettings   = RenderingUtils.CreateDrawingSettings(this.shaderTagIds, renderingData, cameraData, lightData, sortFlags);
                drawSettings.overrideMaterial = this.maskMaterial;

                var param = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
                maskPassData.rendererListHandle = renderGraph.CreateRendererList(param);

                builder.UseRendererList(maskPassData.rendererListHandle);
                builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.rendererListHandle);
                });
            }

            TextureDesc dstDesc = renderGraph.GetTextureDesc(src);
            dstDesc.name = CompositePassName;
            TextureHandle dst = renderGraph.CreateTexture(dstDesc);

            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(CompositePassName, out var compositePassData))
            {
                compositePassData.src         = src;
                compositePassData.mask        = maskHandle;
                compositePassData.material    = this.compositeMaterial;
                compositePassData.color       = this.outlineColor;
                compositePassData.widthPixels = this.outlineWidthPixels;
                compositePassData.maskTexelSize = new Vector4(1f / maskDesc.width, 1f / maskDesc.height, maskDesc.width, maskDesc.height);

                builder.UseTexture(compositePassData.src, AccessFlags.Read);
                builder.UseTexture(compositePassData.mask, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);

                builder.SetRenderFunc((CompositePassData data, RasterGraphContext ctx) =>
                {
                    // Bind the mask as a MATERIAL property (same technique CRTRendererPass uses
                    // for its history buffer: data.material.SetTexture("_PrevFrameTex", data.history))
                    // rather than cmd.SetGlobalTexture -- a per-material bind doesn't count as
                    // global state, so it doesn't need AllowGlobalStateModification either.
                    data.material.SetTexture("_OutlineMask", data.mask);
                    data.material.SetColor("_OutlineColor", data.color);
                    data.material.SetFloat("_OutlineWidth", data.widthPixels);
                    data.material.SetVector("_OutlineMask_TexelSize", data.maskTexelSize);
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            resourceData.cameraColor = dst;
        }
    }
}
