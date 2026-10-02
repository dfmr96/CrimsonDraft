Shader "CrimsonDraft/UI/AimDebuffDistort"
{
    // Drop-in replacement for the default UI shader on the aim selectors/silhouette: at
    // _BlurSize=0 and _Aberration=0 it renders identically to UI/Default (every tap samples the
    // same texel), so it's safe to leave permanently assigned. Blurs and RGB-channel-splits the
    // element's OWN sprite texture only -- no screen grab-pass, so it only ever affects these
    // specific images, never anything else on screen.
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _BlurSize("Blur Size (UV)", Range(0, 0.05)) = 0
        _Aberration("Chromatic Aberration (UV)", Range(0, 0.05)) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "AimDebuffDistort"

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
            float _BlurSize;
            float _Aberration;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPosition = v.vertex;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // 3x3 box blur of a single channel-shifted sample point -- at _BlurSize=0 every tap
            // lands on the same texel, so this degenerates to a plain tex2D lookup.
            fixed4 SampleBlurred(float2 uv, float2 channelOffset)
            {
                fixed4 sum = fixed4(0, 0, 0, 0);
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    [unroll]
                    for (int y = -1; y <= 1; y++)
                    {
                        float2 offset = float2(x, y) * _BlurSize + channelOffset;
                        sum += tex2D(_MainTex, uv + offset);
                    }
                }
                return sum / 9.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 shift = float2(_Aberration, 0);

                fixed4 r = SampleBlurred(uv, shift);
                fixed4 g = SampleBlurred(uv, float2(0, 0));
                fixed4 b = SampleBlurred(uv, -shift);

                fixed4 tex = fixed4(r.r, g.g, b.b, g.a) + _TextureSampleAdd;
                fixed4 col = tex * i.color;

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
