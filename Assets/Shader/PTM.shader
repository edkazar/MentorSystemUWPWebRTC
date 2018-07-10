﻿Shader "Custom/PTM" {
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Lambert
		#pragma target 3.0

		sampler2D _MainTex;
		float4x4 mainCamera;

		sampler2D _YTex;
		sampler2D _UTex;
		sampler2D _VTex;
		int width, height;
		float fx, fy;
		float cx, cy;
		float4x4 camera;

		struct Input {
			float3 worldPos;
		};

		UNITY_INSTANCING_BUFFER_START(Props)
		UNITY_INSTANCING_BUFFER_END(Props)

		bool proj(float4x4 camera, float3 pos, inout half3 P) {
			P = mul(camera, half4(pos, 1.0));
			P.x = (P.x * fx / P.z + cx) / width;
			P.y = (P.y * fy / P.z + cy) / height;
			return P.z > 0.0 && P.x > 0.0 && P.x < 1.0 && P.y > 0.0 && P.y < 1.0;
		}

		void surf(Input IN, inout SurfaceOutput o) {
			half3 p;
			if (proj(camera, IN.worldPos, p)) {
				half2 t = half2(p.x, 1.0 - p.y);
				half y = tex2D(_YTex, t).a;
				half u = tex2D(_UTex, t).a;
				half v = tex2D(_VTex, t).a;
				half r = 1.164 * (y - 0.0625) + 0.000 * (u - 0.5) + 1.596 * (v - 0.5);
				half g = 1.164 * (y - 0.0625) - 0.392 * (u - 0.5) - 0.813 * (v - 0.5);
				half b = 1.164 * (y - 0.0625) + 2.017 * (u - 0.5) + 0.000 * (v - 0.5);
				o.Albedo = half3(b, g, r);
			} else if (proj(mainCamera, IN.worldPos, p)) {
				half a = tex2D(_MainTex, half2(p.x, 1.0 - p.y)).a;
				o.Albedo = half3(a, a, a);
			} else {
				o.Albedo = half3(1.0, 1.0, 1.0);
			}
			o.Alpha = 1.0;
		}

		ENDCG
	}
	FallBack "Diffuse"
}
