Shader "Custom/Snow" 
{
	Properties
	{
		_Tess("Tessellation", Range(1,64)) = 4

		_MainTex("Top Tex (RGB)", 2D) = "white" {}
		_MainTex2("Bottom Tex (RGB)", 2D) = "white" {}

		_DispTex("Displacement Texture", 2D) = "white" {}
		_ImprintTex("Imprint Texture", 2D) = "white" {}

		_Displacement("Displacement", Range(0, 1.0)) = 0.3

		_TopColor("Top Color", color) = (1,1,1,0)
		_BotColor("Bottom Color", color) = (1,1,1,0)
	}
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 300

		CGPROGRAM
		#pragma surface surf BlinnPhong addshadow fullforwardshadows vertex:disp tessellate:tessDistance nolightmap
		#pragma target 4.6
		#include "Tessellation.cginc"

		struct appdata
		{
			float4 vertex  : POSITION;
			float3 normal  : NORMAL;
			float4 tangent : TANGENT;
			float2 texcoord : TEXCOORD0;
		};

		sampler2D _MainTex;
		sampler2D _MainTex2;
		sampler2D _DispTex;
		sampler2D _ImprintTex;

		float _Displacement;
		float _Tess;
		float4 _TopColor;
		float4 _BotColor;

		float3 FindNormal(sampler2D tex, float2 uv,float u)
		{
			float2 offset[4];
			offset[0] = float2(-u, 0);
			offset[1] = float2(u, 0);
			offset[2] = float2(0, -u);
			offset[3] = float2(0, u);

			float hit[4];
			hit[0] = tex2D(tex, uv + offset[0]).r;
			hit[1] = tex2D(tex, uv + offset[1]).r;
			hit[2] = tex2D(tex, uv + offset[2]).r;
			hit[3] = tex2D(tex, uv + offset[3]).r;

			float3 normal;
			float2 step = float2(1, 0);
			float3 uvA = float3(step.xy, hit[1] - hit[0]);
			float3 uvB = float3(step.yx, hit[3] - hit[2]);
			return normalize(cross(uvA, uvB)).rbg;
		}

		float4 tessDistance(appdata v0, appdata v1, appdata v2) 
		{
			float minDist = 10.0;
			float maxDist = 25.0;
			return UnityDistanceBasedTess(v0.vertex, v1.vertex, v2.vertex, minDist, maxDist, _Tess);
		}

		void disp(inout appdata v)
		{
			float d = tex2Dlod(_ImprintTex, float4(1 - v.texcoord.x, v.texcoord.y, 0,0)).r * _Displacement;
			d *= 1 - tex2Dlod(_DispTex, float4(v.texcoord,0,0)) *.5f;
			v.vertex.xyz += v.normal * d;
		}

		struct Input
		{
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutput o)
		{
			half4 c = lerp(
				tex2D(_MainTex, IN.uv_MainTex) * _TopColor,
				tex2D(_MainTex2, IN.uv_MainTex) * _BotColor,
				1 - tex2D(_ImprintTex, float2(1 - IN.uv_MainTex.x, IN.uv_MainTex.y)).r
			);
			o.Albedo = c.rgb;
			o.Specular = .2;
			o.Gloss = 1.0;
			//calculate normal based on earlier function
			o.Normal = FindNormal(_DispTex, IN.uv_MainTex, .00025f);
		}
		ENDCG
	}
	Fallback "Diffuse"
}