Shader "CrimsonDraft/UI/KnobOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (world units)", Range(0, 0.02)) = 0.0017
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+1" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            // Nudges the outline's depth toward the camera so it wins against nearby geometry
            // (e.g. the pointer cube sitting right at the rim) without breaking the technique:
            // the ring itself still relies on the real mesh occluding the extruded shell's center.
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Extrude in WORLD space along a properly re-normalized world normal, not object
                // space: these knobs/buttons carry very non-uniform scale (parented under a
                // heavily-scaled Radio), so a fixed object-space offset came out a wildly
                // different apparent thickness depending on which local axis a given face's
                // normal happened to point along -- thin to the point of vanishing on some faces,
                // which read as "the side facing the camera has no outline".
                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS, true));
                posWS += normalWS * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
