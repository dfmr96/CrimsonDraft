Shader "HorrorEngine/ZombieAddOn/ZombieDetailsMix"
{
    Properties
    {
        _Skin ("Skin", 2D) = "white" {}
        _Face("Face", 2D) = "white" {}
        _Hair("Hair", 2D) = "white" {}
        _Shoes("Shoes", 2D) = "white" {}
        _Forearms("Forearms", 2D) = "white" {}
        _Overlay("Overlay", 2D) = "white" {}
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _Skin;
            sampler2D _Face;
            sampler2D _Hair;
            sampler2D _Shoes;
            sampler2D _Forearms;
            sampler2D _Overlay;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                return o;
            }

            fixed4 BlendNormal(fixed4 base, fixed4 blend) 
            {
                fixed4 result = base;
                result.r = lerp(base.r, blend.r, blend.a);
                result.g = lerp(base.g, blend.g, blend.a);
                result.b = lerp(base.b, blend.b, blend.a);
                return result;
            }

            fixed4 BlendMultiply(fixed4 base, fixed4 blend)
            {
                fixed4 result = base;
                result.r *= blend.r;
                result.g *= blend.g;
                result.b *= blend.b;
                return result;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 skinCol = tex2D(_Skin, i.uv);
                fixed4 faceCol = tex2D(_Face, i.uv);
                fixed4 hairCol = tex2D(_Hair, i.uv);
                fixed4 shoesCol = tex2D(_Shoes, i.uv);
                fixed4 forearmsCol = tex2D(_Forearms, i.uv);
                fixed4 overlayCol = tex2D(_Overlay, i.uv);

                fixed4 result = BlendNormal(skinCol, faceCol);
                result = BlendNormal(result, hairCol);
                result = BlendNormal(result, shoesCol);
                result = BlendNormal(result, forearmsCol);
                result = BlendMultiply(result, overlayCol);

                return result;
            }
            ENDCG
        }
    }
}
