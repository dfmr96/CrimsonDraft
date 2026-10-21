Shader "CrimsonDraft/RetroDitherFadeSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Fade("Fade", Range(0,1)) = 0
        _DitherPixelSize("Dither Pixel Size", Range(1,8)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _Fade;
                half   _DitherPixelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half Bayer4x4(int x, int y)
            {
                static const half bayer[16] =
                {
                    0.0h / 16.0h,  8.0h / 16.0h,  2.0h / 16.0h, 10.0h / 16.0h,
                    12.0h / 16.0h, 4.0h / 16.0h, 14.0h / 16.0h, 6.0h / 16.0h,
                    3.0h / 16.0h, 11.0h / 16.0h, 1.0h / 16.0h,  9.0h / 16.0h,
                    15.0h / 16.0h, 7.0h / 16.0h, 13.0h / 16.0h, 5.0h / 16.0h
                };
                return bayer[(y & 3) * 4 + (x & 3)];
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // Use texture-space dithering so preview and scene behave consistently.
                float pixelScale = max(1.0, (float)_DitherPixelSize);
                float2 texPixels = IN.uv * (1.0 / _MainTex_TexelSize.xy);
                int2 p = int2(floor(texPixels / pixelScale));
                half threshold = Bayer4x4(p.x, p.y);

                // Fade = 0 => fully visible, Fade = 1 => fully vanished.
                half opacity = 1.0h - saturate(_Fade);
                half keep = step(threshold, opacity);
                clip(baseCol.a * keep - 0.001h);

                return baseCol;
            }
            ENDHLSL
        }

        // Fallback pass used by some previews/cameras.
        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _Fade;
                half   _DitherPixelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half Bayer4x4(int x, int y)
            {
                static const half bayer[16] =
                {
                    0.0h / 16.0h,  8.0h / 16.0h,  2.0h / 16.0h, 10.0h / 16.0h,
                    12.0h / 16.0h, 4.0h / 16.0h, 14.0h / 16.0h, 6.0h / 16.0h,
                    3.0h / 16.0h, 11.0h / 16.0h, 1.0h / 16.0h,  9.0h / 16.0h,
                    15.0h / 16.0h, 7.0h / 16.0h, 13.0h / 16.0h, 5.0h / 16.0h
                };
                return bayer[(y & 3) * 4 + (x & 3)];
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                float pixelScale = max(1.0, (float)_DitherPixelSize);
                float2 texPixels = IN.uv * (1.0 / _MainTex_TexelSize.xy);
                int2 p = int2(floor(texPixels / pixelScale));
                half threshold = Bayer4x4(p.x, p.y);
                half opacity = 1.0h - saturate(_Fade);
                half keep = step(threshold, opacity);
                clip(baseCol.a * keep - 0.001h);
                return baseCol;
            }
            ENDHLSL
        }
    }
}
