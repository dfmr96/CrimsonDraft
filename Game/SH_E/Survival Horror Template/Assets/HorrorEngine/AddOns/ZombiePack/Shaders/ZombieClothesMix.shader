Shader "HorrorEngine/ZombieAddOn/ZombieClothesMix"
{
    Properties
    {
        _Skin ("Skin", 2D) = "white" {}
        _UpperBody("Upper Body", 2D) = "white" {}
        _LowerBody("Lower Body", 2D) = "white" {}
        _Sleeves("Sleeves", 2D) = "white" {}
        _Accesories("Accesories", 2D) = "white" {}
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
            sampler2D _UpperBody;
            sampler2D _LowerBody;
            sampler2D _Sleeves;
            sampler2D _Accesories;
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
                fixed4 upperCol = tex2D(_UpperBody, i.uv);
                fixed4 lowerCol = tex2D(_LowerBody, i.uv);
                fixed4 sleevesCol = tex2D(_Sleeves, i.uv);
                fixed4 accesoriesCol = tex2D(_Accesories, i.uv);
                fixed4 overlayCol = tex2D(_Overlay, i.uv);

                fixed4 result = BlendNormal(skinCol, upperCol);
                result = BlendNormal(result, lowerCol);
                result = BlendNormal(result, sleevesCol);
                result = BlendNormal(result, accesoriesCol);
                result = BlendMultiply(result, overlayCol);

                return result;
            }
            ENDCG
        }
    }
}
