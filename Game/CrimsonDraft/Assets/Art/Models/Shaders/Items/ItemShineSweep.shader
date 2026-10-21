Shader "CrimsonDraft/Items/ItemShineSweep"
{
    // Drawn by a second renderer that duplicates a pickup's own mesh (see ItemShineOverlay) --
    // never assigned as the item's actual material. Additively redraws a soft white band that
    // sweeps once across the mesh every few seconds and then sits idle off-mesh, the same
    // "periodic glint" read as the RE1 Remake item shine. _SweepMin/_SweepMax/_SweepDir are set
    // per-instance from the source mesh's own bounds (via MaterialPropertyBlock) so one shared
    // material works on any prop without per-item tuning, and _TimeOffset staggers multiple
    // items so they don't all glint in lockstep.
    Properties
    {
        _SweepColor    ("Sweep Color", Color) = (1, 1, 1, 1)
        _Intensity     ("Intensity", Range(0, 4)) = 1.5
        _SweepWidth    ("Sweep Width (object units)", Range(0.01, 2)) = 0.15
        _SweepInterval ("Seconds Between Sweeps", Range(0.5, 20)) = 3.5
        _SweepDuration ("Sweep Duration (seconds)", Range(0.05, 5)) = 0.7
        _SweepDir      ("Sweep Direction (object space)", Vector) = (0.35, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ItemShineSweep"
            Cull Back
            ZWrite Off
            ZTest LEqual
            // Coincident with the source mesh's own depth (same transform, same vertices) --
            // a tiny bias avoids depth-fighting flicker at grazing angles without visibly
            // detaching the sweep from the surface.
            Offset -1, -1
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // _SweepMin/_SweepMax/_TimeOffset are set per-renderer via MaterialPropertyBlock
            // (ItemShineOverlay) -- plain UnityPerMaterial works fine for that (SRP Batcher
            // handles per-object CBUFFER overrides without needing GPU instancing here).
            CBUFFER_START(UnityPerMaterial)
                half4  _SweepColor;
                float  _Intensity;
                float  _SweepWidth;
                float  _SweepInterval;
                float  _SweepDuration;
                float4 _SweepDir;
                float  _SweepMin;
                float  _SweepMax;
                float  _TimeOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float  sweepCoord  : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.sweepCoord  = dot(IN.positionOS.xyz, normalize(_SweepDir.xyz));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float cycle     = max(_SweepInterval, 0.001);
                float duration  = max(_SweepDuration, 0.001);
                float localTime = fmod(_Time.y + _TimeOffset, cycle);
                float sweepT    = saturate(localTime / duration);

                // Travels from just-before the mesh to just-past it, then parks off-mesh
                // (band contributes nothing) for the rest of the cycle.
                float bandPos = lerp(_SweepMin - _SweepWidth, _SweepMax + _SweepWidth, sweepT);

                float dist = abs(IN.sweepCoord - bandPos);
                float band = saturate(1.0 - dist / max(_SweepWidth, 0.0001));
                band *= band;

                half3 col = _SweepColor.rgb * band * _Intensity;
                return half4(col, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
