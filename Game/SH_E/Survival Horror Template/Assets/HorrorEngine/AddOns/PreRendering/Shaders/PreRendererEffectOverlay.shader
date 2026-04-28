Shader "PreRenderBackgrounds/EffectInvertTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Overlay("Overlay", 2D) = "white" {}
        _OverlayColor("Overlay Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _Overlay;
            float4 _Overlay_ST;

            float _OverlayOpacity;
            float4 _OverlayColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv0 = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv1 = TRANSFORM_TEX(v.uv, _Overlay);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv0);
                fixed4 overlay = tex2D(_Overlay, i.uv1) * _OverlayColor;
                return lerp(col, col + overlay, _OverlayOpacity);
            }
            ENDCG
        }
    }
}
