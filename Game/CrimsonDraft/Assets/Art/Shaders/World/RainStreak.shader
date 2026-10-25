// Soft additive streak for rain drops (stretched-billboard particles). The streak is brightest in
// the middle and fades to both ends and to its sides; colour and alpha come from the particle
// (start colour / colour over lifetime). Depth-tested, so the ship hides the drops behind it.
Shader "CrimsonDraft/World/RainStreak"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 8)) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One

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
                float across = 1.0 - abs(IN.uv.x - 0.5) * 2.0;
                across = saturate(across);
                across *= across;

                float along = sin(saturate(IN.uv.y) * 3.14159265);
                along = pow(max(along, 0.0), 0.6);

                half3 rgb = IN.color.rgb * (IN.color.a * across * along * _Intensity);
                return half4(rgb, 0);
            }
            ENDHLSL
        }
    }
}
