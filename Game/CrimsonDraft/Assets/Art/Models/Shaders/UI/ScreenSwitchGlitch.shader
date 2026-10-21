Shader "CrimsonDraft/UI/ScreenSwitchGlitch"
{
    // Old-CRT "screen change" distortion for plain UI images: horizontal bands tear sideways, the
    // picture splits into a cyan / red fringe, scanlines crawl and rows drop out -- like a terminal
    // wiping its text before drawing the next page. Driven by CardSwitchGlitch (0 -> 1 -> 0).
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _SpriteRect("Sprite UV Rect (uMin,vMin,uMax,vMax)", Vector) = (0,0,1,1)
        _GlitchAmount("Glitch Amount", Range(0,1)) = 0
        _GlitchSeed("Glitch Seed", Float) = 0
        _GlitchBands("Glitch Bands", Range(4,64)) = 24
        _FringeA("Fringe A", Color) = (1,0.2,0.15,1)
        _FringeB("Fringe B", Color) = (0.25,0.95,1,1)

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
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
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "ScreenSwitchGlitch"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
            float4 _SpriteRect;
            float _GlitchAmount;
            float _GlitchSeed;
            float _GlitchBands;
            fixed4 _FringeA;
            fixed4 _FringeB;

            float hash11(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                if (_GlitchAmount > 0.001)
                {
                    float localWidth  = max(_SpriteRect.z - _SpriteRect.x, 0.0001);
                    float localHeight = max(_SpriteRect.w - _SpriteRect.y, 0.0001);
                    float localY = saturate((i.texcoord.y - _SpriteRect.y) / localHeight);

                    float band  = floor(localY * _GlitchBands);
                    float pick  = hash11(band * 13.7 + _GlitchSeed);
                    float torn  = step(1.0 - _GlitchAmount * 0.9, pick);
                    float shift = (hash11(band * 7.3 + _GlitchSeed * 1.7) - 0.5) * 0.9 * _GlitchAmount * torn;
                    float split = (0.02 + 0.07 * torn) * _GlitchAmount;
                    float drop  = step(hash11(band * 3.1 + _GlitchSeed * 2.3), _GlitchAmount * 0.45);

                    float2 uvC = i.texcoord + float2(shift * localWidth, 0);
                    float2 uvR = uvC + float2(split * localWidth, 0);
                    float2 uvB = uvC - float2(split * localWidth, 0);
                    uvC.x = clamp(uvC.x, _SpriteRect.x, _SpriteRect.z);
                    uvR.x = clamp(uvR.x, _SpriteRect.x, _SpriteRect.z);
                    uvB.x = clamp(uvB.x, _SpriteRect.x, _SpriteRect.z);

                    fixed4 sC = tex2D(_MainTex, uvC) + _TextureSampleAdd;
                    float aR = (tex2D(_MainTex, uvR) + _TextureSampleAdd).a;
                    float aB = (tex2D(_MainTex, uvB) + _TextureSampleAdd).a;

                    fixed3 rgb = sC.rgb * i.color.rgb;
                    rgb = lerp(rgb, _FringeA.rgb, saturate(aR - sC.a));
                    rgb = lerp(rgb, _FringeB.rgb, saturate(aB - sC.a));

                    // Crawling scanlines: dark every other row, drifting with the seed.
                    float scan = 0.5 + 0.5 * sin((localY * _GlitchBands * 3.0 + _GlitchSeed * 0.7) * 6.2831);
                    rgb *= 1.0 - 0.35 * _GlitchAmount * scan;
                    // Torn bands burn bright for a frame, like a signal spike.
                    rgb += torn * 0.25 * _GlitchAmount;

                    float alpha = max(sC.a, max(aR, aB) * 0.9) * i.color.a * (1.0 - drop);
                    col = fixed4(rgb, alpha);
                }

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
