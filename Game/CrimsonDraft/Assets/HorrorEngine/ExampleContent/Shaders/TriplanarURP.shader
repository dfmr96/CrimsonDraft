Shader"URP/HorrorEngine/Tri-Planar World URP"
{
    Properties
    {
        _Side ("Side", 2D) = "white" {}
        _Top ("Top", 2D) = "white" {}
        _Bottom ("Bottom", 2D) = "white" {}

        _SideScale ("Side Scale", Float) = 2
        _TopScale ("Top Scale", Float) = 2
        _BottomScale ("Bottom Scale", Float) = 2

        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Back

        Pass
        { 
            Name"ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldViewDir : TEXCOORD2;
            };

            sampler2D _Side, _Top, _Bottom;
            float _SideScale, _TopScale, _BottomScale;
            float _Smoothness, _Metallic;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 worldPos = mul(GetObjectToWorldMatrix(), IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(worldPos.xyz);
                OUT.worldPos = worldPos.xyz;

                float3 worldNormal = normalize(mul((float3x3) GetObjectToWorldMatrix(), IN.normalOS));
                OUT.worldNormal = worldNormal;

                OUT.worldViewDir = normalize(_WorldSpaceCameraPos - OUT.worldPos);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.worldNormal);
                float3 projNormal = saturate(pow(normal * 1.4, 4));

                float4 x = tex2D(_Side, frac(IN.worldPos.zy * _SideScale)) * abs(normal.x);
                float4 y = normal.y > 0
                                            ? tex2D(_Top, frac(IN.worldPos.zx * _TopScale)) * abs(normal.y)
                                            : tex2D(_Bottom, frac(IN.worldPos.zx * _BottomScale)) * abs(normal.y);
                float4 z = tex2D(_Side, frac(IN.worldPos.xy * _SideScale)) * abs(normal.z);

                float4 baseColor = z;
                baseColor = lerp(baseColor, x, projNormal.x);
                baseColor = lerp(baseColor, y, projNormal.y);
    
                // Surface description
                SurfaceData surfaceData;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.alpha = baseColor.a;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = 0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1;
                surfaceData.emission = 0;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                // Fill InputData for lighting
                InputData inputData;
                inputData.positionWS = IN.worldPos;
                inputData.normalWS = normalize(normal);
                inputData.viewDirectionWS = normalize(IN.worldViewDir);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                inputData.fogCoord = 0;
                //inputData.vertexLighting = float3(0, 0, 0);
                inputData.vertexLighting = VertexLighting(IN.worldPos, inputData.normalWS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(IN.worldPos);

                // Evaluate lighting
                float3 color = UniversalFragmentPBR(inputData, surfaceData);

                return float4(color, 1.0);
            }

            ENDHLSL
        }
    }

    FallBack"Hidden/InternalErrorShader"
}
