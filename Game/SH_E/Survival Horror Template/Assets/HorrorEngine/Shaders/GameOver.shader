Shader "HorrorEngine/GameOver"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha("Enable External Alpha", Float) = 0

         _MaxFadeInProgress("Max FadeIn Progress", Float) = 5
         _FadeInProgressSpeed("FadeIn Progress Speed", Float) = 1
        _MinFrequency("MinFrequency", Float) = 0
        _MaxFrequency("MaxFrequency", Float) = 0
         _MinDisplacement("Displacement", Float) = 0
         _MaxDisplacement("Displacement", Float) = 0
         _Speed("Speed", Float) = 1
    }

        SubShader
        {
            Tags
            {
                "Queue" = "Transparent"
                "IgnoreProjector" = "True"
                "RenderType" = "Transparent"
                "PreviewType" = "Plane"
                "CanUseSpriteAtlas" = "True"
            }

            Cull Off
            Lighting Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            Pass
            {
            CGPROGRAM
                #pragma vertex SpriteVert
                #pragma fragment SpriteFragB
                #pragma target 2.0
                #pragma multi_compile_instancing
                #pragma multi_compile_local _ PIXELSNAP_ON
                #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
                #include "UnitySprites.cginc"

                float _MaxFadeInProgress;
                float _FadeInProgressSpeed;
                float _MinDisplacement;
                float _MinFrequency;
                float _MaxDisplacement;
                float _MaxFrequency;
                float _Speed;
                float _ActivationTime;

                fixed4 SpriteFragB(v2f IN) : SV_Target
                {
                    

                    float2 dir = (IN.texcoord - 0.5) * 2;
                    float distanceFromCenter = length(dir);

                    float displacement = lerp(_MinDisplacement, _MaxDisplacement, distanceFromCenter);
                    float freq = lerp(_MinFrequency, _MaxFrequency, distanceFromCenter);
                    
                    fixed4 c = SampleSpriteTexture(IN.texcoord + dir * sin(distanceFromCenter * freq - _Time.z* _Speed) * displacement);
                    
                    float progress = (_Time.y - _ActivationTime) * _FadeInProgressSpeed;
                    progress = clamp(progress, 0, _MaxFadeInProgress);
                    float mask = clamp((1 - distanceFromCenter) * progress, 0, _MaxFadeInProgress);

                    c.rgb = c.rgb*c.a*IN.color.rgb* mask;
                    c.a = c.a * mask;
                    return c;
                }
        ENDCG
        }
    }
}
