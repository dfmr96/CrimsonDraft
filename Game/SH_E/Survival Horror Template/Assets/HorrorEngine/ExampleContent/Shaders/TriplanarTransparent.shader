Shader "HorrorEngine/Tri-Planar World Transparent" {
	Properties{
		  _Side("Side", 2D) = "white" {}
		  _Top("Top", 2D) = "white" {}
		  _Bottom("Bottom", 2D) = "white" {}
		  _SideScale("Side Scale", Float) = 2
		  _TopScale("Top Scale", Float) = 2
		  _BottomScale("Bottom Scale", Float) = 2
	      _Opacity("Opacity", Range(0,1)) = 1
	}

		SubShader
		{
			Tags {
				"Queue" = "Transparent"
				"RenderType" = "Transparent"
			}

			Cull Off
			ZWrite Off

			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma surface surf Lambert fullforwardshadows alpha:blend
			#pragma exclude_renderers flash

			sampler2D _Side, _Top, _Bottom;
			float _SideScale, _TopScale, _BottomScale, _Opacity;

			struct Input {
				float3 worldPos;
				float3 worldNormal;
			};

			void surf(Input IN, inout SurfaceOutput o) {
				float3 projNormal = saturate(pow(IN.worldNormal * 1.4, 4));

				// SIDE X
				float4 x = tex2D(_Side, frac(IN.worldPos.zy * _SideScale)) * abs(IN.worldNormal.x);

				// TOP / BOTTOM
				float4 y = 0;
				if (IN.worldNormal.y > 0) {
					y = tex2D(_Top, frac(IN.worldPos.zx * _TopScale)) * abs(IN.worldNormal.y);
				}
				else {
					y = tex2D(_Bottom, frac(IN.worldPos.zx * _BottomScale)) * abs(IN.worldNormal.y);
				}

				// SIDE Z	
				float4 z = tex2D(_Side, frac(IN.worldPos.xy * _SideScale)) * abs(IN.worldNormal.z);

				float4 finalCol = z;
				finalCol = lerp(finalCol, x, projNormal.x);
				finalCol = lerp(finalCol, y, projNormal.y);

				o.Albedo = finalCol.rgb;
				o.Alpha = finalCol.a * _Opacity;
			}
			ENDCG
		  }
			  Fallback "Diffuse"
}