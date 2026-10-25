// Skybox for a rainy night with a full moon: a dark gradient sky, a few stars, layered drifting clouds
// (projected onto a plane so they converge towards the horizon) and a moon that the clouds cover and
// uncover, with a halo and silver-lined cloud edges around it. Everything is procedural.
//
// _Exposure and _Tint work like the stock Skybox/Cubemap's (tint is doubled, 0.5 grey = neutral), so
// LightningStrike can flash this sky exactly like it flashed the old one.
Shader "CrimsonDraft/Skybox/MoonStorm"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        _Exposure ("Exposure", Range(0, 8)) = 1

        [Header(Low Res Look)]
        _CellDegrees ("Pixel Size (degrees of sky)", Range(0.02, 2)) = 0.35
        _ColorSteps ("Color Steps", Range(3, 64)) = 14
        _DitherAmount ("Dither", Range(0, 1)) = 0.8

        [Header(Sky)]
        _HorizonColor ("Horizon Color", Color) = (0.07, 0.09, 0.14, 1)
        _ZenithColor ("Zenith Color", Color) = (0.012, 0.018, 0.035, 1)
        _SeaColor ("Below Horizon Color", Color) = (0.008, 0.011, 0.02, 1)
        _StarAmount ("Stars (0 = none)", Range(0, 1)) = 0.4

        [Header(Moon)]
        _MoonDir ("Moon Direction (xyz)", Vector) = (0.4, 0.45, 0.8, 0)
        _MoonColor ("Moon Color", Color) = (0.86, 0.91, 1.0, 1)
        _MoonSize ("Moon Angular Radius (degrees)", Range(0.5, 12)) = 5.5
        _MoonBrightness ("Moon Brightness", Range(0, 4)) = 2.2
        _HaloStrength ("Halo Strength", Range(0, 2)) = 0.45

        [Header(Clouds)]
        _CloudColor ("Cloud Color", Color) = (0.05, 0.06, 0.085, 1)
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.6
        _CloudSpeed ("Cloud Speed", Range(0, 4)) = 1
        _CloudScale ("Cloud Scale", Range(0.3, 4)) = 1.3
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
                half _CellDegrees;
                half _ColorSteps;
                half _DitherAmount;
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _SeaColor;
                half _StarAmount;
                float4 _MoonDir;
                half4 _MoonColor;
                half _MoonSize;
                half _MoonBrightness;
                half _HaloStrength;
                half4 _CloudColor;
                half _CloudCover;
                half _CloudSpeed;
                half _CloudScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
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

            // Cloud density in 0..1 for direction d, from two layers drifting at different speeds.
            float Clouds(float3 d)
            {
                float2 plane = d.xz / (max(d.y, 0.0) + 0.16);
                float t = _Time.y * _CloudSpeed;
                float n1 = Fbm(plane * _CloudScale + float2(0.015, 0.006) * t);
                float n2 = Fbm(plane * _CloudScale * 2.1 + 9.7 + float2(0.03, -0.01) * t);
                float n = n1 * 0.65 + n2 * 0.35;

                float threshold = 1.0 - _CloudCover;
                float dens = smoothstep(threshold - 0.12, threshold + 0.22, n);

                // Thicker towards the horizon, fading out into the sea below it.
                float horizon = smoothstep(-0.02, 0.1, d.y);
                return dens * horizon;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);

                // Low-res look: the whole sky (moon, clouds, stars) is computed on a coarse angular
                // grid so it is as chunky as the PSX-pixelated world in front of it.
                float cell = radians(_CellDegrees);
                float az = floor(atan2(d.x, d.z) / cell + 0.5) * cell;
                float elev = floor(asin(clamp(d.y, -1.0, 1.0)) / cell + 0.5) * cell;
                d = float3(sin(az) * cos(elev), sin(elev), cos(az) * cos(elev));
                float el = d.y;

                // Base sky and the dark sea beyond the horizon.
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(el), 0.45));
                col = lerp(col, _SeaColor.rgb, smoothstep(0.0, -0.06, el));

                float3 md = normalize(_MoonDir.xyz);
                float ang = acos(clamp(dot(d, md), -1.0, 1.0));
                float moonR = radians(_MoonSize);

                // The cloud deck thins out around the moon, so the disc shows through while the rest of the
                // sky stays overcast.
                float dens = Clouds(d) * lerp(0.2, 1.0, smoothstep(moonR * 1.3, moonR * 7.5, ang));

                // Stars, only where it is clear and away from the moon's glare.
                float2 sp = d.xz / (max(d.y, 0.0) + 0.3) * 110.0;
                float star = step(1.0 - 0.004 * _StarAmount, Hash21(floor(sp))) * smoothstep(0.12, 0.4, el);
                col += star * (1.0 - dens) * saturate(ang / (moonR * 6.0)) * 0.7;

                // Moon glow lights the air and the clouds around it.
                float halo = exp(-pow(ang / (moonR * 2.4), 1.5)) * _HaloStrength
                           + exp(-ang / (moonR * 10.0)) * 0.1 * _HaloStrength;
                half3 glow = _MoonColor.rgb * halo;
                col += glow * 0.4;

                // Clouds: dark bodies, lit from behind near the moon, brighter on their thin edges.
                float rim = saturate(dens * (1.0 - dens) * 4.0);
                half3 cloudCol = _CloudColor.rgb + glow * (0.35 + 1.1 * rim);
                col = lerp(col, cloudCol, dens);

                // The moon disc, seen through whatever the clouds leave of it.
                float3 up = abs(md.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 right = normalize(cross(up, md));
                float3 up2 = cross(md, right);
                float2 mp = float2(dot(d, right), dot(d, up2)) / max(sin(moonR), 1e-4);

                float disc = smoothstep(moonR, moonR * 0.94, ang);
                float maria = Fbm(mp * 2.2 + 3.1);
                float rough = Fbm(mp * 7.0 + 11.0);
                half3 moonCol = _MoonColor.rgb * _MoonBrightness
                              * lerp(0.58, 1.0, smoothstep(0.38, 0.62, maria))
                              * lerp(0.9, 1.05, rough)
                              * lerp(0.82, 1.0, sqrt(saturate(1.0 - dot(mp, mp))));
                col = lerp(col, moonCol, disc * saturate(1.0 - dens * 0.93));

                // Few colour steps with an ordered-ish dither, like the rest of the frame.
                float2 px = floor(IN.positionCS.xy);
                float ign = frac(52.9829189 * frac(dot(px, float2(0.06711056, 0.00583715))));
                col = floor(col * _ColorSteps + (ign - 0.5) * _DitherAmount + 0.5) / _ColorSteps;
                col = max(col, 0.0);

                col *= _Exposure * (_Tint.rgb * 2.0);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
