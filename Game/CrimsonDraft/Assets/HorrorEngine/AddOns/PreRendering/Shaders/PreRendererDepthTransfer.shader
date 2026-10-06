Shader "PreRendering/PreRendererDepthTransfer"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
        }

        Pass
        {
            Name "FORWARD"

            ZWrite On
            ColorMask 0   // Don't write to color buffer
            
            Tags {"LightMode" = "ForwardBase"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight
        
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION; // vertex position
                float2 uv : TEXCOORD0; // texture coordinate
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            uniform float4 _ClippingRect;
            float2 _NearFarPlanes;
            float _DepthOffset;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = (v.uv * _ClippingRect.xy + _ClippingRect.zw); // float2(0.5,1) + float2(0.5,0);
                return o;
            }
            
            float LinearDepthToRawDepth(float linearDepth, float near, float far)
            {
                #if UNITY_REVERSED_Z
                    float Zx = -1 + (far/near);
                    float Zy = 1.0;
                #else
                    float Zx = 1.0 - (far/near);
                    float Zy = far/near;
                #endif
                return (1.0f - linearDepth * Zy) / (linearDepth * Zx);
            }
            
            float frag (v2f i) : SV_Depth
            {
                float depth = tex2D(_MainTex, i.uv).r + _DepthOffset;
                float near = _NearFarPlanes.x;
                float far = _NearFarPlanes.y;

                float nonLinearZ;
                nonLinearZ = LinearDepthToRawDepth(depth, near, far);
                
                return nonLinearZ;
            }

            ENDCG
        }
    } 


    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline"="UniversalRenderPipeline"  }
        LOD 100

        Pass
        {
            ZWrite On
            ColorMask 0   // Don't write to color buffer
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            float2 _NearFarPlanes;
            float4 _ClippingRect;
            float _DepthOffset;

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv * _ClippingRect.xy + _ClippingRect.zw;
                return o;
            }

            float LinearDepthToRawDepth(float linearDepth, float near, float far)
            {
                #if UNITY_REVERSED_Z
                    float Zx = -1 + (far/near);
                    float Zy = 1.0;
                #else
                    float Zx = 1.0 - (far/near);
                    float Zy = far/near;
                #endif
                return (1.0f - linearDepth * Zy) / (linearDepth * Zx);
            }
            
            float frag (v2f i) : SV_Depth
            {
                float depth = tex2D(_MainTex, i.uv).r + _DepthOffset;
                float near = _NearFarPlanes.x;
                float far = _NearFarPlanes.y;

                float nonLinearZ;
                nonLinearZ = LinearDepthToRawDepth(depth, near, far);
                
                return nonLinearZ;
            }
            ENDHLSL
        }
    }
}
