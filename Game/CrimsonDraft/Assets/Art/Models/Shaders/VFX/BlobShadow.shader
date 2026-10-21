Shader "CrimsonDraft/VFX/BlobShadow"
{
    // Fake contact shadow drawn on a flat quad under a character (see BlobShadow). The blob is
    // procedural -- a radial falloff in UV space -- so its softness is tunable here instead of
    // being baked into a texture. The quad itself is scaled wider across the shoulders than
    // front-to-back (BlobShadowSettings.Size), which stretches the circle into an ellipse.
    Properties
    {
        _Color        ("Color", Color) = (0, 0, 0, 1)
        _Opacity      ("Opacity", Range(0, 1)) = 0.75
        _FalloffStart ("Falloff Start (normalized radius)", Range(0, 0.99)) = 0.25
        _FalloffCurve ("Falloff Curve", Range(0.25, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BlobShadow"
            Cull Back
            ZWrite Off
            ZTest LEqual
            // Lies a hair above the floor (BlobShadowSettings.SurfaceBias); the depth bias keeps
            // it from fighting the floor at grazing camera angles.
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _Opacity;
                half  _FalloffStart;
                half  _FalloffCurve;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 centered    : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.centered    = IN.uv * 2.0 - 1.0;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Solid core up to _FalloffStart, then fades to zero at the quad's inscribed edge.
                float radius  = length(IN.centered);
                float fade    = saturate((1.0 - radius) / max(1.0 - _FalloffStart, 0.0001));
                half  alpha   = pow(fade, _FalloffCurve) * _Opacity * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
