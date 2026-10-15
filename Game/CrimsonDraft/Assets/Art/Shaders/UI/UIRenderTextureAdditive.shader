// Composites the UI CRT camera render texture over the world.
// Additive blend: black = no contribution, so the world shows through untouched.
// _EdgeFade softens the borders of the quad/screen so the UI blends into the scene.
Shader "CrimsonDraft/UI/RenderTextureAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Render Texture", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _EdgeFade ("Edge Fade (uv)", Range(0, 0.5)) = 0.08
        _EdgePower ("Edge Power", Range(0.5, 4)) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanvasOverlay" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        // Premultiplied alpha: opaque UI (e.g. black panels) covers the world,
        // transparent areas (alpha 0) leave the world untouched.
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _EdgeFade;
                half _EdgePower;
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
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float2 d = min(IN.uv, 1.0 - IN.uv);
                float edge = saturate(min(d.x, d.y) / max(_EdgeFade, 1e-4));
                edge = pow(edge, _EdgePower);

                // Treat the RT as straight alpha, output premultiplied.
                half a = saturate(src.a) * IN.color.a * edge;
                return half4(src.rgb * IN.color.rgb * _Intensity * a, a);
            }
            ENDHLSL
        }
    }
}
