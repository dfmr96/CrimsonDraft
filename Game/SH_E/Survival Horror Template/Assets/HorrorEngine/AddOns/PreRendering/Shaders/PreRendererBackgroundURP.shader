Shader "PreRenderBackgrounds/PreRenderedBackgroundURP"
{
    Properties
    {
        [MainTexture] _ColorTex ("Color", 2D) = "white" {}
        _DepthTex ("Depth", 2D) = "white" {}
        _DepthFactor("DepthFactor", Float) = 100
        _ShadowStrength("ShadowStrength", Float) = 5
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "LightMode" = "UniversalForward"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off Zwrite On
        
        Pass
        {
            Name "FORWARD"
            //Lighting On

            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            //#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight

        
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "PreRendererLib.cginc"

            struct Attributes
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_ColorTex);
            SAMPLER(sampler_ColorTex);
            TEXTURE2D(_DepthTex);
            SAMPLER(sampler_DepthTex);
            
            // Matrix passed from C#
            float4x4 _InverseProjectionMatrix;
            float4 _AmbientColor; // Automatically set by URP
            float _ShadowStrength;
            float3 _PlayerPos;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                // A regular quad goes from -0.5 to 0.5 so multiply by 2 to cover all clip space. Also inverse y
                OUT.pos = float4(IN.vertex.x*2, -IN.vertex.y*2, 0, 1);
                OUT.uv = IN.texcoord;
                
                return OUT;
            }

            half4 frag (Varyings IN, out float depth : SV_Depth) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, IN.uv);

                depth = SAMPLE_TEXTURE2D_X(_DepthTex, sampler_DepthTex, IN.uv).r;
                
                
                float3 worldPos = ReconstructWorldPos(IN.uv, depth, _InverseProjectionMatrix);
                
                float3 shadowAdd = 0;
                int lightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightsCount)
                    Light light = GetAdditionalLight(lightIndex, worldPos);
                    
                    half shadowAtten = AdditionalLightShadow(lightIndex, worldPos, light.direction, 1, 1);
                    shadowAdd += light.color * shadowAtten * light.distanceAttenuation;
                LIGHT_LOOP_END
                

                // --------- Debug world pos grid begin
                //float4 debug = float4(GetGridFromWorldPos(worldPos), 1);
                //debug += 1 - saturate(length(_PlayerPos - worldPos));
                //debug = 0;
                // --------- Debug world pos grid end


                return  float4(col.rgb + shadowAdd * col.rgb,1);// shadowAtten1* light1.shadowAttenuation;// normalize(pow(shadows, _ShadowStrength));// float4(col.rgb, 1);
                
                //return 0.5;
            }
            ENDHLSL
            }
    }
}
