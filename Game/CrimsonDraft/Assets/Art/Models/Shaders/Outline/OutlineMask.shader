Shader "Hidden/CrimsonDraft/OutlineMask"
{
    // Rendered as the override material of an extra RendererList draw of whatever sits on the
    // "Outline" layer (see OutlinePass), into its own screen-space texture -- not into the
    // camera color. Plain solid white; OutlineComposite.shader turns this silhouette mask into
    // an edge-detected rim, the same "selection mask -> screen-space edge detect" technique
    // Unity's own editor selection outline uses, so it traces the real mesh silhouette from any
    // camera angle regardless of how many sub-meshes the object has (unlike the KnobOutline
    // inverted-hull technique, which needs one contiguous mesh per target).
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
