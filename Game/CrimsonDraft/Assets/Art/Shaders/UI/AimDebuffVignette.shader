Shader "CrimsonDraft/UI/AimDebuffVignette"
{
    // Radial darkening overlay scoped to whatever rect this Image stretches to fill (the AimView
    // panel, not the whole screen) -- pure alpha-blended draw, no background sampling needed, so
    // it composites correctly via ordinary UI blending regardless of what's underneath.
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Vignette Color", Color) = (0,0,0,1)
        _Intensity("Intensity", Range(0,1)) = 0
        _Smoothness("Edge Smoothness", Range(0.01,1)) = 0.3
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
            Name "AimDebuffVignette"

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
            float _Intensity;
            float _Smoothness;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPosition = v.vertex;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texcoord) + _TextureSampleAdd;

                // Distance from the panel's own center, in its own local UV space -- naturally
                // scoped to this Image's rect, never the full screen.
                float2 centered = (i.texcoord - 0.5) * 2.0;
                float dist = length(centered);

                float radius = 1.0 - saturate(_Intensity);
                float alpha = smoothstep(radius, radius + max(_Smoothness, 0.001), dist);

                fixed4 col = _Color;
                col.a *= alpha * tex.a * i.color.a;

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
