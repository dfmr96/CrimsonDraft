// Lo-Fi Stroke: TextMeshPro bitmap shader for indexed fonts baked by CrimsonDraft > Fonts > Lo-Fi Stroke Baker.
// The atlas stores a palette index per pixel (alpha = index / 15): 0 background, 1-3 fill, 4-7 smoothing, 8 border.
// Exact ramps: if the vertex colour matches a key colour of _PaletteTex (column 0 of each row, gamma values),
// that row's 8 hand-picked colours (columns 1-8) are used. Otherwise the index is looked up in the _Ramp1-8
// ramp and multiplied by the vertex colour (tint), so any <color> tag or Yarn markup colour still recolours
// the whole ramp, border included. Based on TextMesh Pro's TMP_Bitmap.shader.
Shader "CrimsonDraft/UI/TMP Lo-Fi Stroke" {

Properties {
	_MainTex		    ("Font Atlas (indices)", 2D) = "white" {}
	_FaceColor	        ("Text Color", Color) = (1,1,1,1)

	_Ramp1              ("Ramp 1 (fill light)", Color)  = (0.973, 0.973, 0.925, 1)
	_Ramp2              ("Ramp 2 (fill)", Color)        = (0.910, 0.910, 0.863, 1)
	_Ramp3              ("Ramp 3 (fill dark)", Color)   = (0.784, 0.784, 0.737, 1)
	_Ramp4              ("Ramp 4 (smoothing)", Color)   = (0.627, 0.627, 0.588, 1)
	_Ramp5              ("Ramp 5 (smoothing)", Color)   = (0.502, 0.502, 0.471, 1)
	_Ramp6              ("Ramp 6 (smoothing)", Color)   = (0.361, 0.361, 0.345, 1)
	_Ramp7              ("Ramp 7 (smoothing)", Color)   = (0.251, 0.251, 0.251, 1)
	_Ramp8              ("Ramp 8 (border)", Color)      = (0.220, 0.188, 0.282, 1)

	_PaletteTex         ("Exact ramps (9x16, linear, col 0 = key)", 2D) = "black" {}
	_PaletteRows        ("Exact ramp rows", Float) = 0
	_KeyTolerance       ("Key tolerance (0-1, gamma)", Float) = 0.006

	_VertexOffsetX	    ("Vertex OffsetX", float) = 0
	_VertexOffsetY	    ("Vertex OffsetY", float) = 0
	_MaskSoftnessX	    ("Mask SoftnessX", float) = 0
	_MaskSoftnessY	    ("Mask SoftnessY", float) = 0

	_ClipRect           ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)

	_StencilComp        ("Stencil Comparison", Float) = 8
	_Stencil            ("Stencil ID", Float) = 0
	_StencilOp          ("Stencil Operation", Float) = 0
	_StencilWriteMask   ("Stencil Write Mask", Float) = 255
	_StencilReadMask    ("Stencil Read Mask", Float) = 255

	_CullMode           ("Cull Mode", Float) = 0
	_ColorMask          ("Color Mask", Float) = 15
}

SubShader{

	Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

	Stencil
	{
		Ref[_Stencil]
		Comp[_StencilComp]
		Pass[_StencilOp]
		ReadMask[_StencilReadMask]
		WriteMask[_StencilWriteMask]
	}

	Lighting Off
	Cull [_CullMode]
	ZTest [unity_GUIZTestMode]
	ZWrite Off
	Fog { Mode Off }
	Blend SrcAlpha OneMinusSrcAlpha
	ColorMask[_ColorMask]

	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0

		#pragma multi_compile __ UNITY_UI_CLIP_RECT
		#pragma multi_compile __ UNITY_UI_ALPHACLIP

		#include "UnityCG.cginc"
		#include "UnityUI.cginc"

		struct appdata_t
		{
			float4 vertex		: POSITION;
			fixed4 color		: COLOR;
			float4 texcoord0	: TEXCOORD0;
		};

		struct v2f
		{
			float4	vertex		: SV_POSITION;
			fixed4	color		: COLOR;
			float2	texcoord0	: TEXCOORD0;
			float4	mask		: TEXCOORD2;
			float	paletteRow	: TEXCOORD3;
		};

		#define PALETTE_COLUMNS 9.0
		#define PALETTE_HEIGHT  16.0

		uniform	sampler2D 	_MainTex;
		uniform	fixed4		_FaceColor;
		uniform fixed4		_Ramp1, _Ramp2, _Ramp3, _Ramp4, _Ramp5, _Ramp6, _Ramp7, _Ramp8;
		uniform sampler2D	_PaletteTex;
		uniform float		_PaletteRows;
		uniform float		_KeyTolerance;

		// Exact sRGB transfer functions (UnityCG's GammaToLinearSpace is an approximation, off by up to ~3/255).
		float3 SrgbToLinear(float3 c)
		{
			return lerp(pow((c + 0.055) / 1.055, 2.4), c / 12.92, step(c, 0.04045));
		}

		float3 LinearToSrgb(float3 c)
		{
			return lerp(1.055 * pow(max(c, 0), 1.0 / 2.4) - 0.055, c * 12.92, step(c, 0.0031308));
		}

		// Row of _PaletteTex whose key colour (column 0) matches the vertex colour, or -1.
		float FindPaletteRow(float3 linearColor)
		{
			float3 gammaColor = IsGammaSpace() ? linearColor : LinearToSrgb(linearColor);
			for (int row = 0; row < 16; row++)
			{
				if (row >= _PaletteRows) break;
				float3 key = tex2Dlod(_PaletteTex, float4(0.5 / PALETTE_COLUMNS, (row + 0.5) / PALETTE_HEIGHT, 0, 0)).rgb;
				if (all(abs(key - gammaColor) <= _KeyTolerance)) return row;
			}
			return -1;
		}

		uniform float		_VertexOffsetX;
		uniform float		_VertexOffsetY;
		uniform float4		_ClipRect;
		uniform float		_MaskSoftnessX;
		uniform float		_MaskSoftnessY;
		uniform float		_UIMaskSoftnessX;
		uniform float		_UIMaskSoftnessY;
		uniform int			_UIVertexColorAlwaysGammaSpace;

		v2f vert (appdata_t v)
		{
			float4 vert = v.vertex;
			vert.x += _VertexOffsetX;
			vert.y += _VertexOffsetY;

			vert.xy += (vert.w * 0.5) / _ScreenParams.xy;

			float4 vPosition = UnityPixelSnap(UnityObjectToClipPos(vert));

			if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
			{
				v.color.rgb = UIGammaToLinear(v.color.rgb);
			}

			v2f OUT;
			OUT.vertex = vPosition;
			OUT.color = v.color * _FaceColor;
			OUT.texcoord0 = v.texcoord0;
			OUT.paletteRow = FindPaletteRow(v.color.rgb);
			float2 pixelSize = vPosition.w;
			pixelSize /= abs(float2(_ScreenParams.x * UNITY_MATRIX_P[0][0], _ScreenParams.y * UNITY_MATRIX_P[1][1]));

			const float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _MaskSoftnessX), max(_UIMaskSoftnessY, _MaskSoftnessY));
			OUT.mask = float4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + pixelSize.xy));

			return OUT;
		}

		fixed4 frag (v2f IN) : SV_Target
		{
			int index = (int)round(tex2D(_MainTex, IN.texcoord0).a * 15.0);

			fixed3 ramp[9] = { fixed3(0, 0, 0), _Ramp1.rgb, _Ramp2.rgb, _Ramp3.rgb, _Ramp4.rgb,
			                   _Ramp5.rgb, _Ramp6.rgb, _Ramp7.rgb, _Ramp8.rgb };
			int i = clamp(index, 0, 8);
			fixed4 color = fixed4(ramp[i] * IN.color.rgb, index > 0 ? IN.color.a : 0);

			// Exact ramp: every vertex of a character carries the same row, so rounding is enough.
			float row = round(IN.paletteRow);
			if (row >= 0 && i > 0)
			{
				float3 exact = tex2D(_PaletteTex, float2((i + 0.5) / PALETTE_COLUMNS, (row + 0.5) / PALETTE_HEIGHT)).rgb;
				color.rgb = IsGammaSpace() ? exact : SrgbToLinear(exact);
			}

			#if UNITY_UI_CLIP_RECT
				half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
				color *= m.x * m.y;
			#endif

			#if UNITY_UI_ALPHACLIP
				clip(color.a - 0.001);
			#endif

			return color;
		}
		ENDCG
	}
}

}
