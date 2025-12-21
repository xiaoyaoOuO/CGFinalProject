Shader "Custom/SnowWithStep"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_NoiseTex ("Noise", 2D) = "white" {}
		_StepBump ("StepBump", 2D) = "bump" {}
		_BumpScale ("BumpScale", Range(0,5)) = 1
		_SnowTint ("Snow Tint", Color) = (0.92, 0.95, 1.0, 1)
		_ShadowTint ("Shadow Tint", Color) = (0.55, 0.65, 0.85, 1)
		_SnowAmount ("Snow Amount", Range(0,1)) = 0.8
		_StepStrength ("Footprint Strength", Range(0,4)) = 2
		_StepNormalStrength ("Footprint Normal Strength", Range(0,20)) = 12
		_StepDarken ("Footprint Darken", Range(0,1)) = 0.25
		_StepUVScale ("Footprint UV Scale", Range(0.001,0.05)) = 0.02
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase"}
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityPBSLighting.cginc"
			#include "AutoLight.cginc"
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 tangent : TEXCOORD1;
				float3 bitangent : TEXCOORD2;
				float3 normal : TEXCOORD3;
				float3 worldPos : TEXCOORD4;
			};

			sampler2D _MainTex;
			sampler2D _NoiseTex;
			sampler2D _StepBump;
			float4 _MainTex_ST;
			float _BumpScale;
			float3 _PlayerPos;
			float4 _SnowTint;
			float4 _ShadowTint;
			float _SnowAmount;
			float _StepStrength;
			float _StepNormalStrength;
			float _StepDarken;
			float _StepUVScale;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.tangent = UnityObjectToWorldDir(v.tangent.xyz);
				float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				o.bitangent = cross(o.normal, o.tangent) * tangentSign;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);

				float3x3 tangentToWorld = float3x3(normalize(i.tangent), normalize(i.bitangent), normalize(i.normal));

				// Tangent-space normal (start from flat normal)
				float3 normalLocal = float3(0, 0, 1);
				float2 stepUV = (i.worldPos.xz - _PlayerPos.xz) * _StepUVScale + 0.5;
				float4 stepNormalCol = tex2D(_StepBump, stepUV);
				float3 stepNormal = stepNormalCol.rgb * 2 - 1;

				stepNormal.xy *= _StepNormalStrength;
				stepNormal = normalize(stepNormal);
				float stepMask = saturate(stepNormalCol.a * _StepStrength);

				normalLocal = lerp(normalLocal, stepNormal, stepMask);

				normalLocal.xy *= _BumpScale;
				normalLocal = normalize(normalLocal);

				float3 normal = normalize(mul(normalLocal, tangentToWorld));

				float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
				float ndl = saturate(dot(normal, lightDir));

				// Push albedo towards a snowy white tint
				float3 baseSnow = lerp(col.rgb, _SnowTint.rgb, _SnowAmount);
				baseSnow *= (1.0 - stepMask * _StepDarken);

				// Cold shadow -> white highlight ramp
				float3 shadowCol = _ShadowTint.rgb * baseSnow;
				float3 highlightCol = baseSnow;
				float3 diffuseCol = lerp(shadowCol, highlightCol, pow(ndl, 1.2));

				// Simple ambient + subtle sparkle
				float3 ambient = ShadeSH9(float4(normal, 1)) * baseSnow * 0.1; //减少环境光的影响
				float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
				float3 halfDir = normalize(lightDir + viewDir);
				float spec = pow(saturate(dot(normal, halfDir)), 64) * 0.15;

				col.rgb = ambient + diffuseCol * _LightColor0.rgb + spec;

				return col;
			}
			ENDCG
		}
	}
	Fallback "VertexLit"
}
