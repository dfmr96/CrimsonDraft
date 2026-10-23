// Generated from URP Decal.shadergraph, then edited: only screen-space passes are kept and they
// shade the decal with the same lighting as PSXLighting_URP.hlsl (DECAL_PSX_LIT) so it does not look unlit or brighter than the surface.
Shader "CrimsonDraft/Decal PSX Lit"
{
Properties
{
[NoScaleOffset]Base_Map("Base Map", 2D) = "white" {}
[Normal][NoScaleOffset]Normal_Map("Normal Map", 2D) = "bump" {}
Normal_Blend("Normal Blend", Float) = 0.5
[HDR]_BloodColor("Blood Tint (multiplies the texture color)", Color) = (1, 1, 1, 1)
_BloodStrength("Blood Strength", Range(0, 1)) = 1
[HideInInspector]_DrawOrder("Draw Order", Range(-50, 50)) = 0
[HideInInspector][Enum(Default, 0, Depth Bias, 0, View Bias, 1)]_DecalMeshBiasType("DecalMesh BiasType", Float) = 0
[HideInInspector]_DecalMeshDepthBias("DecalMesh DepthBias", Float) = 0
[HideInInspector]_DecalMeshViewBias("DecalMesh ViewBias", Float) = 0
[HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
}
SubShader
{
Tags
{
"RenderPipeline"="UniversalPipeline"
// RenderType: <None>
"PreviewType"="Plane"
// Queue: <None>
"DisableBatching"="False"
"ShaderGraphTargetId"="UniversalDecalSubTarget"
}
Pass
{
    Name "DecalScreenSpaceProjector"
    Tags
    {
        "LightMode" = "DecalScreenSpaceProjector"
    }

    // Render State
    Cull Front
Blend SrcAlpha OneMinusSrcAlpha
ZTest Greater
ZWrite Off

    // Debug
    // <None>

    // --------------------------------------------------
    // Pass

    HLSLPROGRAM

    // Pragmas
    #pragma target 2.5
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#pragma editor_sync_compilation

    // Keywords
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile _DECAL_NORMAL_BLEND_LOW _DECAL_NORMAL_BLEND_MEDIUM _DECAL_NORMAL_BLEND_HIGH
#pragma multi_compile _ _DECAL_LAYERS
    // GraphKeywords: <None>

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    // Defines
    #define ATTRIBUTES_NEED_NORMAL
    #define ATTRIBUTES_NEED_TEXCOORD0
    #define VARYINGS_NEED_NORMAL_WS
    #define VARYINGS_NEED_TEXCOORD0
    #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
    #define VARYINGS_NEED_SH
    #define VARYINGS_NEED_STATIC_LIGHTMAP_UV
    #define VARYINGS_NEED_DYNAMIC_LIGHTMAP_UV
    #define VARYINGS_NEED_SHADOW_COORD

    #define HAVE_MESH_MODIFICATION


    #define SHADERPASS SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR
#define _MATERIAL_AFFECTS_ALBEDO 1
#define _MATERIAL_AFFECTS_NORMAL 1
#define _MATERIAL_AFFECTS_NORMAL_BLEND 1

    // Includes
    #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"

    // -- Properties used by ScenePickingPass
    #ifdef SCENEPICKINGPASS
    float4 _SelectionID;
    #endif
    #if _RENDER_PASS_ENABLED
    #define GBUFFER3 0
    FRAMEBUFFER_INPUT_X_FLOAT(GBUFFER3);
    #define GBUFFER4 1
    FRAMEBUFFER_INPUT_X_UINT(GBUFFER4);
    #endif

    // --------------------------------------------------
    // Structs and Packing

    struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 normalWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 uv0;
};
struct VertexDescriptionInputs
{
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 texCoord0 : INTERP5;
 float4 fogFactorAndVertexLight : INTERP6;
 float3 normalWS : INTERP7;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

    PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
float4 Base_Map_TexelSize;
float4 Normal_Map_TexelSize;
float Normal_Blend;
float4 _BloodColor;
float _BloodStrength;
float _DrawOrder;
float _DecalMeshBiasType;
float _DecalMeshDepthBias;
float _DecalMeshViewBias;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(Base_Map);
SAMPLER(samplerBase_Map);
TEXTURE2D(Normal_Map);
SAMPLER(samplerNormal_Map);

    // Graph Includes
    // GraphIncludes: <None>

    // Graph Functions
    
void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

    // Graph Vertex
    struct VertexDescription
{
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
return description;
}

    // Graph Pixel
    struct SurfaceDescription
{
float3 BaseColor;
float Alpha;
float3 NormalTS;
float NormalAlpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(Base_Map);
float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.r;
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.g;
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.b;
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.a;
UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(Normal_Map);
float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4);
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.r;
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.g;
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.b;
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.a;
float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0_Float = Normal_Blend;
float _Multiply_86a3eb218544413fb0fe50b275affa91_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7_Float, _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0_Float, _Multiply_86a3eb218544413fb0fe50b275affa91_Out_2_Float);
surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.xyz);
surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7_Float;
surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.xyz);
surface.NormalAlpha = _Multiply_86a3eb218544413fb0fe50b275affa91_Out_2_Float;
return surface;
}

    // --------------------------------------------------
    // Build Graph Inputs

    
//     $features.graphVertex:  $include("VertexAnimation.template.hlsl")
//                                       ^ ERROR: $include cannot find file : VertexAnimation.template.hlsl. Looked into:
// Packages/com.unity.shadergraph/Editor/Generation/Templates

    
//     $features.graphPixel:   $include("SharedCode.template.hlsl")
//                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
// Packages/com.unity.shadergraph/Editor/Generation/Templates

    SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */



    output.TangentSpaceNormal =                         float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif



#if defined(UNITY_UIE_INCLUDED)
    output.uv0 =                                        float4(input.texCoord0.x, input.texCoord0.y, 0, 0);
#else
    output.uv0 =                                        input.texCoord0;
#endif
    

    
    
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

    return output;
}

    // --------------------------------------------------
    // Build Surface Data

    void GetSurfaceData(Varyings input, float4 positionCS, float angleFadeFactor, out DecalSurfaceData surfaceData)
    {
        #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
            half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
            half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
            float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
            float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
            input.texCoord0.xy = input.texCoord0.xy * scale + offset;
            half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
            half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
            half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
            half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
            input.normalWS.xyz = normalWS;
        #else
            #if defined(LOD_FADE_CROSSFADE) && USE_UNITY_CROSSFADE
                LODFadeCrossFade(positionCS);
            #endif

            half fadeFactor = half(1.0);
        #endif

        SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
        SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

        // setup defaults -- these are used if the graph doesn't output a value
        ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
        surfaceData.occlusion = half(1.0);
        surfaceData.smoothness = half(0);

        #ifdef _MATERIAL_AFFECTS_NORMAL
            surfaceData.normalWS.w = half(1.0);
        #else
            surfaceData.normalWS.w = half(0.0);
        #endif


        // copy across graph values, if defined
        surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
        surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);

        #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
            #if defined(_MATERIAL_AFFECTS_NORMAL)
                surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
            #else
                surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
            #endif
        #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
            #if defined(_MATERIAL_AFFECTS_NORMAL)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

                // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
            #else
                surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
            #endif
        #endif

        surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;

        // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
    }

    // --------------------------------------------------
    // Main

    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#define DECAL_PSX_LIT
#include "Assets/Art/Decals/Shaders/ShaderPassDecalMultiply.hlsl"

    ENDHLSL
}
Pass
{
    Name "DecalScreenSpaceMesh"
    Tags
    {
        "LightMode" = "DecalScreenSpaceMesh"
    }

    // Render State
    Blend SrcAlpha OneMinusSrcAlpha
ZTest LEqual
ZWrite Off

    // Debug
    // <None>

    // --------------------------------------------------
    // Pass

    HLSLPROGRAM

    // Pragmas
    #pragma target 2.5
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#pragma editor_sync_compilation

    // Keywords
    #pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
#pragma multi_compile _ REFLECTION_PROBE_ROTATION
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile _DECAL_NORMAL_BLEND_LOW _DECAL_NORMAL_BLEND_MEDIUM _DECAL_NORMAL_BLEND_HIGH
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile _ _DECAL_LAYERS
    // GraphKeywords: <None>

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    // Defines
    #define ATTRIBUTES_NEED_NORMAL
    #define ATTRIBUTES_NEED_TANGENT
    #define ATTRIBUTES_NEED_TEXCOORD0
    #define ATTRIBUTES_NEED_TEXCOORD1
    #define ATTRIBUTES_NEED_TEXCOORD2
    #define VARYINGS_NEED_POSITION_WS
    #define VARYINGS_NEED_NORMAL_WS
    #define VARYINGS_NEED_TANGENT_WS
    #define VARYINGS_NEED_TEXCOORD0
    #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
    #define VARYINGS_NEED_SH
    #define VARYINGS_NEED_STATIC_LIGHTMAP_UV
    #define VARYINGS_NEED_DYNAMIC_LIGHTMAP_UV
    #define VARYINGS_NEED_SHADOW_COORD

    #define HAVE_MESH_MODIFICATION


    #define SHADERPASS SHADERPASS_DECAL_SCREEN_SPACE_MESH
#define _MATERIAL_AFFECTS_ALBEDO 1
#define _MATERIAL_AFFECTS_NORMAL 1
#define _MATERIAL_AFFECTS_NORMAL_BLEND 1

    // Includes
    #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"

    // -- Properties used by ScenePickingPass
    #ifdef SCENEPICKINGPASS
    float4 _SelectionID;
    #endif
    #if _RENDER_PASS_ENABLED
    #define GBUFFER3 0
    FRAMEBUFFER_INPUT_X_FLOAT(GBUFFER3);
    #define GBUFFER4 1
    FRAMEBUFFER_INPUT_X_UINT(GBUFFER4);
    #endif

    // --------------------------------------------------
    // Structs and Packing

    struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 uv0;
};
struct VertexDescriptionInputs
{
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 tangentWS : INTERP5;
 float4 texCoord0 : INTERP6;
 float4 fogFactorAndVertexLight : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

    PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
float4 Base_Map_TexelSize;
float4 Normal_Map_TexelSize;
float Normal_Blend;
float4 _BloodColor;
float _BloodStrength;
float _DrawOrder;
float _DecalMeshBiasType;
float _DecalMeshDepthBias;
float _DecalMeshViewBias;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(Base_Map);
SAMPLER(samplerBase_Map);
TEXTURE2D(Normal_Map);
SAMPLER(samplerNormal_Map);

    // Graph Includes
    // GraphIncludes: <None>

    // Graph Functions
    
void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

    // Graph Vertex
    struct VertexDescription
{
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
return description;
}

    // Graph Pixel
    struct SurfaceDescription
{
float3 BaseColor;
float Alpha;
float3 NormalTS;
float NormalAlpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(Base_Map);
float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.r;
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.g;
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.b;
float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7_Float = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.a;
UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(Normal_Map);
float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4);
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.r;
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.g;
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.b;
float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7_Float = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.a;
float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0_Float = Normal_Blend;
float _Multiply_86a3eb218544413fb0fe50b275affa91_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7_Float, _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0_Float, _Multiply_86a3eb218544413fb0fe50b275affa91_Out_2_Float);
surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0_Vector4.xyz);
surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7_Float;
surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0_Vector4.xyz);
surface.NormalAlpha = _Multiply_86a3eb218544413fb0fe50b275affa91_Out_2_Float;
return surface;
}

    // --------------------------------------------------
    // Build Graph Inputs

    
//     $features.graphVertex:  $include("VertexAnimation.template.hlsl")
//                                       ^ ERROR: $include cannot find file : VertexAnimation.template.hlsl. Looked into:
// Packages/com.unity.shadergraph/Editor/Generation/Templates

    
//     $features.graphPixel:   $include("SharedCode.template.hlsl")
//                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
// Packages/com.unity.shadergraph/Editor/Generation/Templates

    SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */



    output.TangentSpaceNormal =                         float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif



#if defined(UNITY_UIE_INCLUDED)
    output.uv0 =                                        float4(input.texCoord0.x, input.texCoord0.y, 0, 0);
#else
    output.uv0 =                                        input.texCoord0;
#endif
    

    
    
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

    return output;
}

    // --------------------------------------------------
    // Build Surface Data

    void GetSurfaceData(Varyings input, float4 positionCS, float angleFadeFactor, out DecalSurfaceData surfaceData)
    {
        #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
            half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
            half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
            float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
            float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
            input.texCoord0.xy = input.texCoord0.xy * scale + offset;
            half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
            half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
            half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
            half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
            input.normalWS.xyz = normalWS;
            input.tangentWS.xyzw = half4(tangentWS, sign);
        #else
            #if defined(LOD_FADE_CROSSFADE) && USE_UNITY_CROSSFADE
                LODFadeCrossFade(positionCS);
            #endif

            half fadeFactor = half(1.0);
        #endif

        SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
        SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

        // setup defaults -- these are used if the graph doesn't output a value
        ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
        surfaceData.occlusion = half(1.0);
        surfaceData.smoothness = half(0);

        #ifdef _MATERIAL_AFFECTS_NORMAL
            surfaceData.normalWS.w = half(1.0);
        #else
            surfaceData.normalWS.w = half(0.0);
        #endif


        // copy across graph values, if defined
        surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
        surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);

        #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
            #if defined(_MATERIAL_AFFECTS_NORMAL)
                surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
            #else
                surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
            #endif
        #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
            #if defined(_MATERIAL_AFFECTS_NORMAL)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

                // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
            #else
                surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
            #endif
        #endif

        surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;

        // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
    }

    // --------------------------------------------------
    // Main

    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#define DECAL_PSX_LIT
#include "Assets/Art/Decals/Shaders/ShaderPassDecalMultiply.hlsl"

    ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

    // Render State
    Cull Back

    // Debug
    // <None>

    // --------------------------------------------------
    // Pass

    HLSLPROGRAM

    // Pragmas
    #pragma target 2.0
#pragma multi_compile_instancing
#pragma editor_sync_compilation
#pragma vertex Vert
#pragma fragment Frag

    // Keywords
    // PassKeywords: <None>
    // GraphKeywords: <None>

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    // Defines

    #define HAVE_MESH_MODIFICATION


    #define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENEPICKINGPASS 1

    // Includes
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"

    // -- Properties used by ScenePickingPass
    #ifdef SCENEPICKINGPASS
    float4 _SelectionID;
    #endif
    #if _RENDER_PASS_ENABLED
    #define GBUFFER3 0
    FRAMEBUFFER_INPUT_X_FLOAT(GBUFFER3);
    #define GBUFFER4 1
    FRAMEBUFFER_INPUT_X_UINT(GBUFFER4);
    #endif

    // --------------------------------------------------
    // Structs and Packing

    struct Attributes
{
 float3 positionOS : POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

    PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
float4 Base_Map_TexelSize;
float4 Normal_Map_TexelSize;
float Normal_Blend;
float4 _BloodColor;
float _BloodStrength;
float _DrawOrder;
float _DecalMeshBiasType;
float _DecalMeshDepthBias;
float _DecalMeshViewBias;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(Base_Map);
SAMPLER(samplerBase_Map);
TEXTURE2D(Normal_Map);
SAMPLER(samplerNormal_Map);

    // Graph Includes
    // GraphIncludes: <None>

    // Graph Functions
    // GraphFunctions: <None>

    // Graph Vertex
    struct VertexDescription
{
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
return description;
}

    // Graph Pixel
    struct SurfaceDescription
{
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
return surface;
}

    // --------------------------------------------------
    // Build Graph Inputs

    
//     $features.graphVertex:  $include("VertexAnimation.template.hlsl")
//                                       ^ ERROR: $include cannot find file : VertexAnimation.template.hlsl. Looked into:
// Packages/com.unity.shadergraph/Editor/Generation/Templates

    
//     $features.graphPixel:   $include("SharedCode.template.hlsl")
//                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
// Packages/com.unity.shadergraph/Editor/Generation/Templates

    SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif



#if defined(UNITY_UIE_INCLUDED)
#else
#endif
    

    
    
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

    return output;
}

    // --------------------------------------------------
    // Build Surface Data

    void GetSurfaceData(Varyings input, float4 positionCS, float angleFadeFactor, out DecalSurfaceData surfaceData)
    {
        #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
            half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
            half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
            float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
            float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
            half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
            half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
            half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
            half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
        #else
            #if defined(LOD_FADE_CROSSFADE) && USE_UNITY_CROSSFADE
                LODFadeCrossFade(positionCS);
            #endif

            half fadeFactor = half(1.0);
        #endif

        SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
        SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

        // setup defaults -- these are used if the graph doesn't output a value
        ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
        surfaceData.occlusion = half(1.0);
        surfaceData.smoothness = half(0);

        #ifdef _MATERIAL_AFFECTS_NORMAL
            surfaceData.normalWS.w = half(1.0);
        #else
            surfaceData.normalWS.w = half(0.0);
        #endif


        // copy across graph values, if defined

        #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
            #if defined(_MATERIAL_AFFECTS_NORMAL)
            #else
                surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
            #endif
        #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
            #if defined(_MATERIAL_AFFECTS_NORMAL)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

                // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
            #else
                surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
            #endif
        #endif


        // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
    }

    // --------------------------------------------------
    // Main

    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#define DECAL_PSX_LIT
#include "Assets/Art/Decals/Shaders/ShaderPassDecalMultiply.hlsl"

    ENDHLSL
}
}
FallBack "Hidden/Shader Graph/FallbackError"
}