Shader "CrimsonDraft/UI/ECGSweep"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Sweep("Sweep", Range(0,1)) = 0
        _HaloX("Halo X", Range(0,1)) = 1
        _LeadColor("Lead Color", Color) = (1,1,1,1)
        _HaloColor("Halo Color", Color) = (1,1,1,1)
        _HaloWidth("Halo Width", Range(0.001,0.1)) = 0.02
        _HaloIntensity("Halo Intensity", Range(0,2)) = 0.6
        _GapWidth("Gap Width", Range(0,1)) = 0.05
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
            Name "ECGSweep"

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
            float _Sweep;
            float _HaloX;
            fixed4 _LeadColor;
            fixed4 _HaloColor;
            float _HaloWidth;
            float _HaloIntensity;
            float _GapWidth;

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
                fixed4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                float haloX = saturate(_HaloX);
                float haloWidth = max(0.0001, _HaloWidth);
                float halfHalo = haloWidth * 0.5;
                float haloStart = haloX - halfHalo;
                float haloEnd = haloX + halfHalo;
                float gapEnd = haloEnd + max(0.0, _GapWidth);
                float x = i.texcoord.x;

                fixed4 lead = _LeadColor * i.color;
                fixed4 haloCol = _HaloColor * i.color;
                float halo = saturate(1.0 - abs(x - haloX) / haloWidth);

                if (x < haloStart)
                {
                    col = lead;
                }
                else if (x <= haloEnd)
                {
                    col = haloCol;
                    col.rgb += halo * _HaloIntensity;
                }
                else if (x < gapEnd)
                {
                    col = lead;
                    col.a = 0.0;
                }
                else
                {
                    col = lead;
                }

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
