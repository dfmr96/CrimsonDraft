Shader "CrimsonDraft/UI/ECGTrailFade"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Head("Head", Range(0,1)) = 1
        _TrailLength("Trail Length", Range(0.001,1)) = 0.18
        _FadeExponent("Fade Exponent", Range(0.1,4)) = 1
        _SpriteRect("Sprite UV Rect (uMin,vMin,uMax,vMax)", Vector) = (0,0,1,1)

        // Damage glitch -- driven from ECGSweepAnimator, 0 = off. Horizontal bands of the trace tear
        // sideways and drop out in steps, with a red / cyan ghost fringe, like a CRT losing signal.
        _GlitchAmount("Glitch Amount", Range(0,1)) = 0
        _GlitchTint("Glitch Tint (red ghost)", Color) = (1,0.2,0.15,1)
        _GlitchBands("Glitch Bands", Range(2,32)) = 12
        _GlitchSeed("Glitch Seed", Float) = 0
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
            Name "ECGTrailFade"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Head;
            float _TrailLength;
            float _FadeExponent;
            float4 _SpriteRect;
            float _GlitchAmount;
            fixed4 _GlitchTint;
            float _GlitchBands;
            float _GlitchSeed;

            float hash11(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPosition = v.vertex;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // i.texcoord is in atlas space, not 0..1 local space, when the sprite is
                // packed alongside others in a shared texture. Remap to the sprite's own
                // 0..1 range so the sweep always travels the full visible width.
                float localWidth = max(_SpriteRect.z - _SpriteRect.x, 0.0001);
                float localX = saturate((i.texcoord.x - _SpriteRect.x) / localWidth);

                fixed4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                if (_GlitchAmount > 0.001)
                {
                    float localHeight = max(_SpriteRect.w - _SpriteRect.y, 0.0001);
                    float localY = saturate((i.texcoord.y - _SpriteRect.y) / localHeight);

                    // Each horizontal band may tear sideways; more bands tear as the glitch gets stronger.
                    float band   = floor(localY * _GlitchBands);
                    float pick   = hash11(band * 13.7 + _GlitchSeed);
                    float torn   = step(1.0 - _GlitchAmount * 0.8, pick);
                    float shift  = (hash11(band * 7.3 + _GlitchSeed * 1.7) - 0.5) * 0.45 * _GlitchAmount * torn;
                    float split  = (0.02 + 0.05 * torn) * _GlitchAmount;
                    float drop   = step(hash11(band * 3.1 + _GlitchSeed * 2.3), _GlitchAmount * 0.25); // a few bands lose signal

                    float2 uvC = i.texcoord + float2(shift * localWidth, 0);
                    float2 uvR = uvC + float2(split * localWidth, 0);
                    float2 uvB = uvC - float2(split * localWidth, 0);
                    uvC.x = clamp(uvC.x, _SpriteRect.x, _SpriteRect.z);
                    uvR.x = clamp(uvR.x, _SpriteRect.x, _SpriteRect.z);
                    uvB.x = clamp(uvB.x, _SpriteRect.x, _SpriteRect.z);

                    fixed4 sC = tex2D(_MainTex, uvC) + _TextureSampleAdd;
                    float aR = (tex2D(_MainTex, uvR) + _TextureSampleAdd).a;
                    float aB = (tex2D(_MainTex, uvB) + _TextureSampleAdd).a;

                    // Centre keeps the trace colour; the shifted copies show up as red / cyan fringes.
                    float ghostR = saturate(aR - sC.a);
                    float ghostB = saturate(aB - sC.a);
                    fixed3 rgb = sC.rgb * i.color.rgb;
                    rgb = lerp(rgb, _GlitchTint.rgb, ghostR);
                    rgb = lerp(rgb, fixed3(0.25, 0.95, 1.0), ghostB);

                    float alpha = max(sC.a, max(aR, aB) * 0.85) * i.color.a * (1.0 - drop);
                    col = fixed4(rgb, alpha);
                }

                float trail = max(_TrailLength, 0.0001);
                float behind = _Head - localX;
                behind = behind < 0.0 ? behind + 1.0 : behind; // wrap around so the trail never has to regrow from empty
                float fade = saturate(1.0 - (behind / trail));
                fade = pow(fade, max(_FadeExponent, 0.0001));

                col.a *= fade;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001f);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
