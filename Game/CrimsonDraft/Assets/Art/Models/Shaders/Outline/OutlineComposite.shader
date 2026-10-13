Shader "Hidden/CrimsonDraft/OutlineComposite"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.1, 1)
        _OutlineWidth ("Outline Width (px)", Range(1, 8)) = 2
        [HideInInspector] _OutlineMask ("Outline Mask", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "OutlineComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_OutlineMask);
            float4 _OutlineMask_TexelSize;
            float4 _OutlineColor;
            float  _OutlineWidth;

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv    = IN.texcoord;
                float2 texel = _OutlineMask_TexelSize.xy * _OutlineWidth;

                float center = SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv).r;

                // Edge = "this pixel isn't marked, but a neighbour within outlineWidth is" --
                // same idea as an inverted-hull outline, just done as a screen-space dilate
                // against the silhouette mask instead of extruding geometry.
                float neighbourMax = 0;
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2( texel.x, 0)).r);
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2(-texel.x, 0)).r);
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2(0,  texel.y)).r);
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2(0, -texel.y)).r);
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2( texel.x,  texel.y)).r);
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2(-texel.x,  texel.y)).r);
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2( texel.x, -texel.y)).r);
                neighbourMax = max(neighbourMax, SAMPLE_TEXTURE2D(_OutlineMask, sampler_LinearClamp, uv + float2(-texel.x, -texel.y)).r);

                float isEdge = saturate(neighbourMax - center);

                float3 scene = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float3 outCol = lerp(scene, _OutlineColor.rgb, isEdge * _OutlineColor.a);
                return half4(outCol, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
