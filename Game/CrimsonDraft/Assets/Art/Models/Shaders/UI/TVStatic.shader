Shader "CrimsonDraft/UI/TVStatic"
{
    // Old-TV "channel change" glitch applied to a frozen copy of what the camera was showing
    // (_MainTex, set by RawImage): horizontal bands tear sideways, rows jitter, the picture rolls a
    // little vertically and the RGB channels split -- all by moving the UVs, so it still reads as
    // the game's own image. A faint, mostly transparent noise and scanlines sit on top.
    // _Intensity scales the distortion + noise; _Alpha fades the whole overlay over the scene below.
    // Driven by _UnscaledTime so it keeps animating while Time.timeScale is slowed or zero.
    Properties
    {
        [PerRendererData] _MainTex("Captured Frame", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Intensity("Distortion + Noise", Range(0,1)) = 1
        _Alpha("Overlay Alpha", Range(0,1)) = 1
        _Tear("Band Tear (uv)", Range(0,0.3)) = 0.07
        _RowJitter("Row Jitter (uv)", Range(0,0.05)) = 0.006
        _VerticalRoll("Vertical Roll (uv)", Range(0,0.2)) = 0.04
        _Chroma("RGB Split (uv)", Range(0,0.05)) = 0.012
        _NoiseOpacity("Faint Noise Opacity", Range(0,1)) = 0.16
        _Scale("Noise Scale (px per cell)", Range(1,8)) = 2
        _Scanlines("Scanline Strength", Range(0,1)) = 0.18
        _UnscaledTime("Unscaled Time (set by script)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            Name "TVStatic"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Intensity;
            float _Alpha;
            float _Tear;
            float _RowJitter;
            float _VerticalRoll;
            float _Chroma;
            float _NoiseOpacity;
            float _Scale;
            float _Scanlines;
            float _UnscaledTime;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float k = saturate(_Intensity);

                // Re-rolls ~30 times a second, like a TV field rate, not every render frame.
                float t = fmod(floor(_UnscaledTime * 30.0), 1000.0);

                // Some horizontal bands tear sideways; every row also jitters a hair.
                float band    = floor(uv.y * 24.0 + t * 0.7);
                float tearOn  = step(0.7, Hash21(float2(band, t)));
                float tear    = tearOn * (Hash21(float2(t, band)) - 0.5) * 2.0 * _Tear * k;
                float row     = floor(uv.y * _ScreenParams.y * 0.5);
                float jitter  = (Hash21(float2(row, t)) - 0.5) * 2.0 * _RowJitter * k;

                // Now and then the whole picture rolls vertically.
                float roll = step(0.82, Hash21(float2(t, 3.0))) * (Hash21(float2(t, 5.0)) - 0.5) * 2.0 * _VerticalRoll * k;

                float2 suv = float2(uv.x + tear + jitter, uv.y + roll);
                float split = _Chroma * k * (1.0 + tearOn * 3.0);
                float r = tex2D(_MainTex, suv + float2(split, 0.0)).r;
                float g = tex2D(_MainTex, suv).g;
                float b = tex2D(_MainTex, suv - float2(split, 0.0)).b;
                float3 col = float3(r, g, b);

                // Faint, zero-mean noise (mostly transparent: it only grains the picture) + scanlines.
                float2 cell = floor(uv * _ScreenParams.xy / max(_Scale, 0.001));
                float n = Hash21(cell + t * 17.3);
                col += (n - 0.5) * _NoiseOpacity * k;
                col *= lerp(1.0, 1.0 - _Scanlines, step(0.5, frac(cell.y * 0.5)) * k);

                return fixed4(saturate(col), _Alpha * i.color.a);
            }
            ENDCG
        }
    }
}
