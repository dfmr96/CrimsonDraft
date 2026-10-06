Shader "HorrorEngine/StatusLine"
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
        _Tiling("Tiling", Float) = 1
        _Interval("Interval", Float) = 1
        _SpeedMultiplier("SpeedMultiplier", Float) = 1
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
                #include "UnitySprites.cginc"

                float _Tiling;
                float _Interval;
                float _SpeedMultiplier;
                float _UnescaledTime;

                fixed4 SpriteFragB(v2f IN) : SV_Target
                {
                    float t = _UnescaledTime * _SpeedMultiplier;
                    float tmod = fmod(t, _Interval * _Tiling);
                    float visibleInt = 0.1 * _Tiling;
                    float2 uv = saturate(IN.texcoord);
                    uv.x *= _Tiling;

                     float interval = uv.x < (tmod + visibleInt) && uv.x > (tmod - visibleInt);
                    interval *= 1-saturate(abs(uv.x - tmod) / visibleInt);

                    fixed4 c = SampleSpriteTexture(uv) * IN.color;
                    c.rgb = IN.color * interval * c.a;
                    c.a = c.a * interval;
                    return c;
                }
           
            ENDCG
            }
        }
}