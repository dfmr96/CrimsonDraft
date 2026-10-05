Shader "HorrorEngine/ZombieAddOn/TextureColorAdjustHSL"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Hue("Hue", Range(-1.0, 1.0)) = 0.0
        _Saturation("Saturation", Range(0.0, 2.0)) = 1.0
        _Lightness("Lightness", Range(0.0, 2.0)) = 1.0
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            LOD 200

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct appdata_t
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };

                sampler2D _MainTex;
                float _Hue;
                float _Saturation;
                float _Lightness;

                v2f vert(appdata_t v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                float3 RGBToHSL(float3 color)
                {
                    float3 hsl; // init to 0 to avoid warnings ? (and reverse if + remove first part)

                    float fmin = min(min(color.r, color.g), color.b);    //Min. value of RGB
                    float fmax = max(max(color.r, color.g), color.b);    //Max. value of RGB
                    float delta = fmax - fmin;             //Delta RGB value

                    hsl.z = (fmax + fmin) / 2.0; // Luminance

                    if (delta == 0.0)		//This is a gray, no chroma...
                    {
                        hsl.x = 0.0;	// Hue
                        hsl.y = 0.0;	// Saturation
                    }
                    else                                    //Chromatic data...
                    {
                        if (hsl.z < 0.5)
                            hsl.y = delta / (fmax + fmin); // Saturation
                        else
                            hsl.y = delta / (2.0 - fmax - fmin); // Saturation

                        float deltaR = (((fmax - color.r) / 6.0) + (delta / 2.0)) / delta;
                        float deltaG = (((fmax - color.g) / 6.0) + (delta / 2.0)) / delta;
                        float deltaB = (((fmax - color.b) / 6.0) + (delta / 2.0)) / delta;

                        if (color.r == fmax)
                            hsl.x = deltaB - deltaG; // Hue
                        else if (color.g == fmax)
                            hsl.x = (1.0 / 3.0) + deltaR - deltaB; // Hue
                        else if (color.b == fmax)
                            hsl.x = (2.0 / 3.0) + deltaG - deltaR; // Hue

                        if (hsl.x < 0.0)
                            hsl.x += 1.0; // Hue
                        else if (hsl.x > 1.0)
                            hsl.x -= 1.0; // Hue
                    }

                    return hsl;
                }

                float HueToRGB(float f1, float f2, float hue)
                {
                    if (hue < 0.0)
                        hue += 1.0;
                    else if (hue > 1.0)
                        hue -= 1.0;
                    float res;
                    if ((6.0 * hue) < 1.0)
                        res = f1 + (f2 - f1) * 6.0 * hue;
                    else if ((2.0 * hue) < 1.0)
                        res = f2;
                    else if ((3.0 * hue) < 2.0)
                        res = f1 + (f2 - f1) * ((2.0 / 3.0) - hue) * 6.0;
                    else
                        res = f1;
                    return res;
                }

                float3 HSLToRGB(float3 hsl)
                {
                    float3 rgb;

                    if (hsl.y == 0.0)
                        rgb = float3(hsl.z, hsl.z, hsl.z); // Luminance
                    else
                    {
                        float f2;

                        if (hsl.z < 0.5)
                            f2 = hsl.z * (1.0 + hsl.y);
                        else
                            f2 = (hsl.z + hsl.y) - (hsl.y * hsl.z);

                        float f1 = 2.0 * hsl.z - f2;

                        rgb.r = HueToRGB(f1, f2, hsl.x + (1.0 / 3.0));
                        rgb.g = HueToRGB(f1, f2, hsl.x);
                        rgb.b = HueToRGB(f1, f2, hsl.x - (1.0 / 3.0));
                    }

                    return rgb;
                }


                fixed4 frag(v2f i) : SV_Target
                {
                    // Sample the texture
                    float4 color = tex2D(_MainTex, i.uv);

                    // Convert RGB to HSL
                    float3 hsl = RGBToHSL(color.rgb);

                    // Adjust the hue
                    hsl.x += _Hue;
                    hsl.x = frac(hsl.x); // Wrap hue if it goes out of range [0, 1]

                    // Adjust the saturation
                    hsl.y *= _Saturation;

                    // Adjust the lightness
                    hsl.z = lerp(0.0, 1.0, hsl.z) * _Lightness;

                    // Convert back to RGB
                    color.rgb = HSLToRGB(hsl);

                    return color;
                }
                ENDCG
            }
        }
            FallBack "Diffuse"
}
