Shader "CrimsonDraft/VFX/BloodBlob"
{
    // Blood pool under an enemy corpse (see CorpseBloodBlob), ported from the Horror Engine
    // BloodBlob. Unlike BlobShadow it doesn't paint a colour over the floor: it multiplies the
    // floor by the texture, so the stain darkens whatever surface it lies on. The texture is
    // point-filtered, which gives the stepped PSX edge once the quad is stretched to full size.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        // Just before BlobShadow (Transparent-1), so a character's shadow still draws on top.
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-2" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BloodBlob"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Legacy Particles/Multiply: fades toward white (no change) where alpha is zero.
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                return half4(lerp(half3(1, 1, 1), tex.rgb, tex.a), 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
