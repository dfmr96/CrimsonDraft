Shader "CrimsonDraft/VFX/TargetHalo"
{
    // Selector drawn in the world around a character's feet: a square ring on the floor split into
    // a few lines with gaps between them, with a soft glow around the arcs (and an HDR-bright core so a
    // camera with Bloom makes it shine). Everything comes from one procedural mesh (TargetHalo) --
    // vertex color R flags which part a fragment belongs to (1 = optional cylinder wall,
    // 0 = floor disc); the wall's UV.y is its normalized height.
    Properties
    {
        _Color          ("Color", Color) = (1, 0.92, 0.7, 1)
        _Intensity      ("Intensity", Range(0, 4)) = 1

        [Header(Floor Arcs)]
        _ArcCount       ("Arc Count", Range(1, 8)) = 4
        _PulseAmount    ("Pulse Size (fraction of the ring radius)", Range(0, 0.2)) = 0.12
        _PulseSpeed     ("Pulse Speed (cycles per second)", Range(0, 6)) = 1.5
        _ArcFill        ("Arc Length (fraction of each section)", Range(0.1, 1)) = 0.5
        _ArcSoftness    ("Arc End Softness", Range(0.001, 0.2)) = 0.03
        _RingRadius     ("Ring Radius (normalized)", Range(0.3, 1)) = 0.8
        _RingWidth      ("Ring Width", Range(0.01, 0.4)) = 0.05
        _RingOpacity    ("Ring Opacity", Range(0, 2)) = 1
        _DiscOpacity    ("Disc Fill Opacity", Range(0, 1)) = 0
        [Toggle] _HardEdge ("Hard Edges (no gradient, no glow)", Float) = 0

        [Header(Glow)]
        _GlowAmount     ("Glow Amount", Range(0, 2)) = 0.7
        _GlowSpread     ("Glow Spread (x ring width)", Range(1, 8)) = 3
        _HDRBoost       ("HDR Core Boost (feeds Bloom)", Range(0, 8)) = 2.5

        [Header(Cylinder Wall)]
        _WallOpacity    ("Wall Opacity", Range(0, 1)) = 0.45
        _HeightFalloff  ("Height Falloff", Range(0.25, 6)) = 2.6
        _BaseGlow       ("Extra Glow At The Base", Range(0, 2)) = 0.8
        _StreakCount    ("Vertical Streaks", Range(0, 48)) = 12
        _StreakAmount   ("Streak Strength", Range(0, 1)) = 0.25
        _StreakScroll   ("Streak Scroll Speed", Range(-2, 2)) = 0.15
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "TargetHalo"
            Cull Off
            ZWrite Off
            ZTest LEqual
            // Hugs the floor without fighting it at grazing camera angles.
            Offset -1, -1
            // Additive: it's light, not paint -- black contributes nothing.
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _Intensity;
                half  _ArcCount;
                half  _PulseAmount;
                half  _PulseSpeed;
                half  _ArcFill;
                half  _ArcSoftness;
                half  _RingRadius;
                half  _RingWidth;
                half  _RingOpacity;
                half  _DiscOpacity;
                half  _HardEdge;
                half  _GlowAmount;
                half  _GlowSpread;
                half  _HDRBoost;
                half  _WallOpacity;
                half  _HeightFalloff;
                half  _BaseGlow;
                half  _StreakCount;
                half  _StreakAmount;
                half  _StreakScroll;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half   isWall      : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.isWall      = IN.color.r;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half alpha;
                half hot = 0; // 0..1: how much of the HDR core boost this fragment gets

                if (IN.isWall > 0.5)
                {
                    // Opaque-ish at the floor, fading to nothing at the top.
                    half h    = saturate(IN.uv.y);
                    half fade = pow(saturate(1.0 - h), _HeightFalloff);
                    half glow = 1.0 + _BaseGlow * pow(saturate(1.0 - h), 4.0);

                    half streak = 0.5 + 0.5 * sin((IN.uv.x * _StreakCount + _Time.y * _StreakScroll) * 6.2831853);
                    half streakMask = lerp(1.0, streak, _StreakAmount);

                    alpha = fade * glow * streakMask * _WallOpacity;
                }
                else
                {
                    float2 c = IN.uv * 2.0 - 1.0;
                    half   d = max(abs(c.x), abs(c.y));                      // square distance: rings are squares
                    if (d > 1.0) discard;

                    // Split the ring into _ArcCount sections, each drawn only over _ArcFill of its length,
                    // centered in its section (with 4 arcs, one per side, gaps at the corners).
                    // Position along the square's perimeter, 0..1, counter-clockwise from the right side's
                    // bottom corner. The lines are static; the whole square breathes in and out instead.
                    half side, u;
                    if (abs(c.x) >= abs(c.y)) { side = c.x > 0 ? 0 : 2; u = c.x > 0 ? c.y : -c.y; }
                    else                      { side = c.y > 0 ? 1 : 3; u = c.y > 0 ? -c.x : c.x; }
                    half a = (side + u * 0.5 + 0.5) * 0.25;
                    half t = frac(a * _ArcCount + _ArcFill * 0.5);           // position inside the section
                    half pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2831853);
                    half dr = d - _RingRadius * pulse;
                    half arcMask, core, glow, fill, edge;
                    if (_HardEdge > 0.5)
                    {
                        // Binary coverage: every fragment is fully on or off, so the ring and arc
                        // ends have crisp edges with no soft falloff and no glow.
                        arcMask = step(0.0, t) * step(t, _ArcFill);
                        core    = step(abs(dr), _RingWidth);
                        glow    = 0;
                        fill    = _DiscOpacity;
                        edge    = 1;
                    }
                    else
                    {
                        arcMask = smoothstep(0.0, _ArcSoftness, t) * smoothstep(0.0, _ArcSoftness, _ArcFill - t);
                        core    = exp(-pow(dr / max(_RingWidth, 0.001), 2.0));
                        glow    = exp(-pow(dr / max(_RingWidth * _GlowSpread, 0.001), 2.0)) * _GlowAmount;
                        fill    = saturate(1.0 - d) * _DiscOpacity;
                        edge    = saturate((1.0 - d) / 0.06); // no hard cut at the quad's boundary
                    }

                    alpha = ((core * _RingOpacity + glow) * arcMask + fill) * edge;
                    hot   = core * arcMask;
                }

                alpha = saturate(alpha * _Intensity * _Color.a);
                half3 rgb = _Color.rgb * (1.0 + hot * _HDRBoost);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
