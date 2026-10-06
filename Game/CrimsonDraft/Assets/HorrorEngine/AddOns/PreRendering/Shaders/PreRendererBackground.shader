Shader "PreRendering/PreRenderedBackground"
{
    Properties
    {
        _ColorTex ("Color", 2D) = "white" {}
        _DepthTex ("Depth", 2D) = "white" {}
       // _ShadowBias("ShadowBias", Float) = 0.001
        _DepthFactor("DepthFactor", Float) = 100
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
        }
        Cull Off 
        Zwrite On
        ZTest Always
        
        Pass
        {
            Name "FORWARD"
            Lighting On
            Tags {"LightMode" = "ForwardBase"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight
            
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "PreRendererLib.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                //SHADOW_COORDS(1) // shadow parameters to pass from vertex
            };

            sampler2D _DepthTex;
            float4 _DepthTex_ST;
            sampler2D _ColorTex;
            float4 _ColorTex_ST;
            
            sampler2D _CameraDepthTexture;

            float4x4 _FrustumRays;
            
            v2f vert (appdata_tan  v)
            {
                v2f o;

                // A regular quad goes from -0.5 to 0.5 so multiply by 2 to cover all clip space. Also inverse y
                o.pos = float4(v.vertex.x*2, -v.vertex.y*2, 0, 1);
                o.uv = v.texcoord;

                //TRANSFER_SHADOW(o); // pass shadow coordinates to pixel shader
                
                return o;
            }

            fixed4 frag (v2f i, out float depth : SV_Depth) : SV_Target
            {
                fixed4 col = tex2D(_ColorTex, i.uv);
                
                float nlDepth = SAMPLE_DEPTH_TEXTURE(_DepthTex, i.uv);
                depth = max(nlDepth, 0.001);
                
                //float3 worldPos = ReconstructWorldPosFromFrustum(i.uv, LinearEyeDepth(nlDepth), _FrustumRays);
                //UNITY_LIGHT_ATTENUATION(atten, i, worldPos)
                
                float sceneDepth = tex2D(_CameraDepthTexture, i.uv).r;

                // TODO - This is a very dodgy shadow implementation. Ideally we use a normal buffer and do ndotL, not having shadows on the color render               
                float3 finalCol = _LightColor0.rgb;// *saturate(atten);
                finalCol.rgb += UNITY_LIGHTMODEL_AMBIENT;
                finalCol.rgb *= col.rgb;

                float depthDiff = saturate(depth - sceneDepth);
                
                col.rgb = lerp(min(finalCol.rgb, col.rgb), col.rgb, depthDiff);

                // --------- Debug world pos grid begin
                float4 debug = 0; // float4(GetGridFromWorldPos(worldPos), 1);
                // --------- Debug world pos grid end
               
                return float4(col.rgb, 1);
            }
            ENDCG
        }
        
         // Forward additive pass (needed for additional lights).
         Pass
         {
            Name "FORWARD_DELTA"
            
            Tags { "LightMode" = "ForwardAdd" }

            Blend One One
            ZWrite Off
            ZTest Always
            
            CGPROGRAM
           
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "PreRendererLib.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                SHADOW_COORDS(1)
            };

            sampler2D _DepthTex;
            float4 _DepthTex_ST;
            
            sampler2D _ColorTex;
            float4 _ColorTex_ST;

            sampler2D _CameraDepthTexture;

            float4x4 _InverseProjectionMatrix;
            float3 _CamForward;

            v2f vert(appdata_full v)
            {
                v2f o;

                // A regular quad goes from -0.5 to 0.5 so multiply by 2 to cover all clip space. Also inverse y
                o.pos = float4(v.vertex.x * 2, -v.vertex.y * 2, 1, 1);
                o.uv = v.texcoord;
                
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_TARGET
            {
                fixed4 col = tex2D(_ColorTex, i.uv);

                float nlDepth = SAMPLE_DEPTH_TEXTURE(_DepthTex, i.uv);
                
                // Use eye space direction with eye depth to get eye space position
                float eyeDepth = LinearEyeDepth(nlDepth);

                //float3 worldPos = ReconstructWorldPosFromFrustum(i.uv, eyeDepth, _FrustumRays);
                float3 worldPos = ReconstructWorldPos(i.uv, nlDepth, _InverseProjectionMatrix);

                UNITY_LIGHT_ATTENUATION(atten, i, worldPos);
                
                float sceneDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);

                float eyeSceneDepth = LinearEyeDepth(sceneDepth);

                // --------- Debug world pos grid begin
                float4 debug = 0;// float4(GetGridFromWorldPos(worldPos), 1);
                // --------- Debug world pos grid end

                return float4(lerp(atten * col.rgb * _LightColor0.rgb, 0, eyeSceneDepth < eyeDepth), 1) + debug;
            } 

            ENDCG
        }
     
        
    }
    FallBack "Legacy Shaders/VertexLit"
}
