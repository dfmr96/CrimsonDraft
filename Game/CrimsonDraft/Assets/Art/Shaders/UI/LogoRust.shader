// Rust for the title logo (UI Image). Rust grows from the edges of the letters inwards, in irregular
// patches with a fine grain on top, a few short vertical streaks where rain would have run down,
// and tiny pits eaten out of the metal along the worst patches. Colour and alpha of the sprite stay
// underneath, so the logo's own distressing still shows. Everything is procedural and driven by a
// handful of sliders; _Amount = 0 gives back the plain sprite.
Shader "CrimsonDraft/UI/LogoRust"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Rust)]
        _RustTex ("Rust Texture (RGB)", 2D) = "gray" {}
        _RustTiling ("Rust Texture Tiling", Range(0.2, 6)) = 1.6
        _Amount ("Amount", Range(0, 1)) = 0.7
        _PatchScale ("Patch Size (bigger = smaller patches)", Range(1, 30)) = 7
        _EdgeBias ("Edge Bias (0 = anywhere, 1 = only near edges)", Range(0, 1)) = 0.4
        _EdgeReach ("Edge Reach (texels)", Range(2, 80)) = 28
        _Strength ("Strength", Range(0, 1)) = 0.95
        _DarkKeep ("Rust on Dark Parts (0 = keeps its red, 1 = full rust)", Range(0, 1)) = 0.45
        _LightRust ("Rust Tint", Color) = (1, 1, 1, 1)
        _DarkRust ("Core Darkening", Color) = (0.45, 0.32, 0.28, 1)

        [Header(Detail)]
        _Grain ("Grain", Range(0, 1)) = 0.5
        _Streaks ("Streaks (rain runs)", Range(0, 1)) = 0.3
        _Pitting ("Pitting (holes in the metal)", Range(0, 1)) = 0.35
        _Seed ("Seed", Float) = 3.7

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" "RenderPipeline" = "UniversalPipeline" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_RustTex);
            SAMPLER(sampler_RustTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                half _RustTiling;
                half4 _Color;
                half _Amount;
                half _PatchScale;
                half _EdgeBias;
                half _EdgeReach;
                half _Strength;
                half _DarkKeep;
                half4 _LightRust;
                half4 _DarkRust;
                half _Grain;
                half _Streaks;
                half _Pitting;
                half _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                return OUT;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    v += a * Noise2(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 baseCol = tex * IN.color;

                // Aspect-correct noise space so patches are round, not stretched with the sprite.
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float2 p = float2(IN.uv.x * aspect, IN.uv.y) + _Seed;

                // How close this pixel is to the edge of the lettering: 1 at the rim, 0 deep inside.
                // Eight taps on a ring approximate a blur of the alpha.
                float2 reach = _EdgeReach * _MainTex_TexelSize.xy;
                float around = 0.0;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * 0.785398;
                    around += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(cos(ang), sin(ang)) * reach).a;
                }
                around *= 0.125;
                float edge = saturate(1.0 - around);               // fraction of the ring that is empty
                edge = smoothstep(0.0, 0.5, edge);

                // Irregular patches, pushed towards the edges and stretched a little downwards
                // where the streaks run.
                float patches = Fbm(p * _PatchScale);
                float streakNoise = Fbm(float2(p.x * _PatchScale * 7.0, p.y * _PatchScale * 0.7) + 5.3);
                patches = lerp(patches, patches * 0.6 + streakNoise * 0.6, _Streaks);

                float bias = lerp(1.0, edge * 0.85 + 0.15, _EdgeBias);
                float field = patches * bias;

                float threshold = 1.0 - _Amount;
                float rust = smoothstep(threshold - 0.08, threshold + 0.14, field);

                // Fine grain breaks the patches up so they read as corrosion and not as a smudge.
                float grain = Fbm(p * 90.0);
                rust *= lerp(1.0, smoothstep(0.25, 0.75, grain), _Grain);

                // Colour comes from a real rust photo, darker towards the core of each patch.
                float depth = saturate((field - threshold) * 3.0);
                half3 rustCol = SAMPLE_TEXTURE2D(_RustTex, sampler_RustTex, p * _RustTiling).rgb * _LightRust.rgb;
                rustCol *= lerp(half3(1, 1, 1), _DarkRust.rgb * 2.0, depth * 0.7);

                // The dark red parts of the logo keep more of their own colour so the brand red does
                // not turn orange; the light grey lettering takes the rust fully.
                float lum = dot(baseCol.rgb, half3(0.3, 0.59, 0.11));
                float keep = lerp(_DarkKeep, 1.0, smoothstep(0.1, 0.5, lum));
                rustCol *= lerp(0.6, 1.0, keep);

                half3 rgb = lerp(baseCol.rgb, rustCol, rust * _Strength * keep);

                // Pits: tiny holes where the rust is deepest.
                float pit = smoothstep(0.72, 0.9, Fbm(p * 180.0 + 31.0)) * smoothstep(0.35, 0.9, rust * (0.5 + depth));
                half alpha = baseCol.a * (1.0 - pit * _Pitting);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
