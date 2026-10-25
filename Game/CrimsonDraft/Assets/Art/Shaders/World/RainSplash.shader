// Ring that spreads out where a rain drop hits the water (horizontal-billboard particles). Drive its
// size and alpha with the particle's size / colour over lifetime. Additive and depth-tested.
Shader "CrimsonDraft/World/RainSplash"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 8)) = 1.2
        _RingRadius ("Ring Radius (0-1)", Range(0.2, 0.95)) = 0.7
        _RingWidth ("Ring Width", Range(0.03, 0.4)) = 0.14
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One
        Offset -1, -1

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half _RingRadius;
                half _RingWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = length(IN.uv - 0.5) * 2.0;
                float d = (r - _RingRadius) / _RingWidth;
                float ring = exp(-d * d);
                ring *= 1.0 - smoothstep(0.85, 1.0, r);

                half3 rgb = IN.color.rgb * (IN.color.a * ring * _Intensity);
                return half4(rgb, 0);
            }
            ENDHLSL
        }
    }
}
