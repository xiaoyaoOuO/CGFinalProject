Shader "Roystan/Grass"
{
    Properties
    {
		[Header(Shading)]
        _TopColor("Top Color", Color) = (1,1,1,1)
		_BottomColor("Bottom Color", Color) = (1,1,1,1)
		_BlendRotationRandom("Blend Rotation", Range(0,1)) = 0.2
		_TranslucentGain("Translucent Gain", Range(0,1)) = 0.5
		_BladeWidth("Blade Width", Float) = 0.05
		_BladeWidthRandom("Blade Width Random", Float) = 0.02
		_BladeHeight("Blade Height", Float) = 0.5
		_BladeHeightRandom("Blade Height Random", Float) = 0.3
		_TessellationUniform("Tessellation", Range(1,64)) = 16
		_WindDistortionMap("Wind Distortion Map", 2D) = "white" {}
		_WindFrequency("Wind Frequency", Vector) = (0.05, 0.05, 0, 0)
		_WindStrength("Wind Strength", Float) = 1
		_BladeForward("Blade Forward Amount", Float) = 0.38
		_BladeCurve("Blade Curvature Amount", Range(1, 4)) = 2	
		_InteractorRadius("Interactor Radius", Float) = 0.3
		_InteractorStrength("Interactor Strength", Float) = 1
		_MinFadeDistance("Min Fade Distance", Float) = 20
		_MaxFadeDistance("Max Fade Distance", Float) = 50
    }

	CGINCLUDE
	#include "UnityCG.cginc"
	#include "Autolight.cginc"
	#include "CustomTessellation.cginc"
	#define BLADE_SEGMENTS 3

	// Simple noise function, sourced from http://answers.unity.com/answers/624136/view.html
	// Extended discussion on this function can be found at the following link:
	// https://forum.unity.com/threads/am-i-over-complicating-this-random-function.454887/#post-2949326
	// Returns a number in the 0...1 range.
	float rand(float3 co)
	{
		return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
	}

	// Construct a rotation matrix that rotates around the provided axis, sourced from:
	// https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
	float3x3 AngleAxis3x3(float angle, float3 axis)
	{
		float c, s;
		sincos(angle, s, c);

		float t = 1 - c;
		float x = axis.x;
		float y = axis.y;
		float z = axis.z;

		return float3x3(
			t * x * x + c, t * x * y - s * z, t * x * z + s * y,
			t * x * y + s * z, t * y * y + c, t * y * z - s * x,
			t * x * z - s * y, t * y * z + s * x, t * z * z + c
			);
	}
	//变量
	float _BlendRotationRandom;
	float _BladeHeight;
	float _BladeHeightRandom;	
	float _BladeWidth;
	float _BladeWidthRandom;
	sampler2D _WindDistortionMap;
	float4 _WindDistortionMap_ST;
	float2 _WindFrequency;
	float _WindStrength;
	float _BladeForward;
	float _BladeCurve;
	float _InteractorRadius;
	float _InteractorStrength;
	float _MinFadeDistance;
	float _MaxFadeDistance;
		
	uniform float3 _PositionMoving;  // 交互器世界位置

	//和曲面细分冲突，这里可以注释掉
	// struct vertexInput
	// {
	// 	float4 vertex : POSITION;
	// 	float3 normal : NORMAL;
	// 	float4 tangent : TANGENT;
	// };

	// struct vertexOutput
	// {
	// 	float4 vertex : SV_POSITION;
	// 	float3 normal : NORMAL;
	// 	float4 tangent : TANGENT;
	// };

	
	// vertexOutput vert(vertexInput v)
	// {
	// 	vertexOutput o;
	// 	o.vertex = v.vertex;
	// 	o.normal = v.normal;
	// 	o.tangent = v.tangent;
	// 	return o;
	// }

	struct geometryOutput
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
		float3 normal : NORMAL;
		unityShadowCoord4 _ShadowCoord : TEXCOORD1;
	};

	geometryOutput VertexOutput(float3 pos,float2 uv,float3 normal)
	{
		geometryOutput o;
		o.pos = UnityObjectToClipPos(pos);
		o.uv = uv;
		o.normal = UnityObjectToWorldNormal(normal);
		o._ShadowCoord = ComputeScreenPos(o.pos); 	//接收阴影
		#if UNITY_PASS_SHADOWCASTER
			o.pos = UnityApplyLinearShadowBias(o.pos);
		#endif
		return o;
	}
	//封装顶点函数，用于生成草的顶点
	// vertexPosition: 顶点位置
	// width: 草的变换宽度
	// height: 草的变换高度
	// uv: 顶点的uv坐标
	// TransformMatrix: 变换矩阵
	geometryOutput GenerateGrassVertex(float3 vertexPosition,float width, float height, float forward, float2 uv, float3x3 TransformMatrix)
	{
		float3 tangentPoint = float3(width, forward, height);

		float3 localPosition = vertexPosition + mul(TransformMatrix, tangentPoint);

		//光照
		float3 tangentNormal = normalize(float3(0,-1,forward)); //根据曲率变化
		float3 localNormal = mul(TransformMatrix, tangentNormal);
		return VertexOutput(localPosition, uv, localNormal);
	}

	[maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
	void geo(triangle vertexOutput IN[3], inout TriangleStream<geometryOutput> triStream)
	{
		float3 pos = IN[0].vertex;

		float3 worldPos = mul(unity_ObjectToWorld, float4(pos, 1)).xyz;
		float distanceToCamera = distance(worldPos, _WorldSpaceCameraPos);

		float FadeRate = 1 - saturate((distanceToCamera - _MinFadeDistance) / (_MaxFadeDistance - _MinFadeDistance));

		//与物体交互
		// Interactivity
		float dis = distance(_PositionMoving, worldPos);
		float radius = 1 - saturate(dis / _InteractorRadius);
		// in world radius based on objects interaction radius
		float3 sphereDisp = worldPos - _PositionMoving; // position comparison
		sphereDisp *= radius; // position multiplied by radius for falloff
		// increase strength
		sphereDisp = clamp(sphereDisp.xyz * _InteractorStrength, -0.8, 0.8);

		//wind
		float2 uv = worldPos.xz * _WindDistortionMap_ST.xy + _WindDistortionMap_ST.zw + _WindFrequency * _Time.y;
		float2 windSample = (tex2Dlod(_WindDistortionMap, float4(uv, 0, 0)).xy * 2 - 1) * _WindStrength;
		float3 wind = normalize(float3(windSample.x, windSample.y, 0));//Wind Vector
		float3x3 windRotation = AngleAxis3x3(UNITY_PI * windSample, wind);

		//Tangent空间转换
		float3 vNormal = IN[0].normal;
		float4 vTangent = IN[0].tangent;
		float3 vBinormal = cross(vNormal, vTangent) * vTangent.w;

		float3x3 tbn = float3x3(
			vTangent.x, vBinormal.x, vNormal.x,
			vTangent.y, vBinormal.y, vNormal.y,
			vTangent.z, vBinormal.z, vNormal.z
		);

		float height = abs((rand(pos.zyx) * 2 - 1) * _BladeHeightRandom + _BladeHeight);
		float width  = abs((rand(pos.xzy) * 2 - 1) * _BladeWidthRandom + _BladeWidth);

		//随机朝向
		float3x3 RotateMatrix = AngleAxis3x3(rand(pos) * UNITY_TWO_PI, float3(0,0,1));
		//随机弯曲
		float3x3 BlendMatrix = AngleAxis3x3(rand(pos.zzx) * UNITY_PI * _BlendRotationRandom * 0.5, float3(-1,0,0));

		float3x3 TransformMatrix = mul(mul(mul(tbn, RotateMatrix), BlendMatrix),windRotation);

		//根部不动
		float3x3 transformationFacingMatrix = mul(tbn, RotateMatrix);  

		//曲率
		float forward = rand(pos.yyz) * _BladeForward;

		geometryOutput o;
		//增加一个草的三角形
		for (int i = 0; i < BLADE_SEGMENTS * FadeRate; i++)
		{
			float t = i / (float)BLADE_SEGMENTS;
			float segmentHeight = height * t;
			float segmentWidth = width * (1 - t);
			float segmentForward = pow(t, _BladeCurve) * forward;
			//底部顶点使用transformationFacingMatrix
			float3x3 transfromMatrix = i == 0? transformationFacingMatrix : TransformMatrix;
			pos = i == 0? pos : pos + sphereDisp * t;
			triStream.Append(GenerateGrassVertex(pos, segmentWidth, segmentHeight, segmentForward, float2(0,t), transfromMatrix));
			triStream.Append(GenerateGrassVertex(pos, -segmentWidth, segmentHeight, segmentForward, float2(1,t), transfromMatrix));
		}
		// triStream.Append(GenerateGrassVertex(pos, width  ,0      , float2(0.0,0.0), transformationFacingMatrix));
		// triStream.Append(GenerateGrassVertex(pos, -width ,0      , float2(1.0,0)  , transformationFacingMatrix));
		triStream.Append(GenerateGrassVertex(pos,0,height,forward,float2(0.5,1),TransformMatrix));
	}
	ENDCG

    SubShader
    {
		Cull Off

        Pass
        {
			Tags
			{
				"RenderType" = "Opaque"
				"LightMode" = "ForwardBase"
			}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma target 4.6
			#pragma geometry geo
			#pragma hull hull
			#pragma domain domain
			#pragma multi_compile_fwdbase
            
			#include "Lighting.cginc"

			float4 _TopColor;
			float4 _BottomColor;
			float _TranslucentGain;

			float4 frag (geometryOutput i, fixed facing : VFACE) : SV_Target
            {	
				float3 normal = facing > 0 ? i.normal : -i.normal;

				float3 shadow = SHADOW_ATTENUATION(i);
				float NdotL = saturate(saturate(dot(normal, _WorldSpaceLightPos0.xyz)) + _TranslucentGain) *shadow;
				float3 ambient = ShadeSH9(float4(normal, 1));
				float4 lightIntensity = NdotL * _LightColor0 + float4(ambient, 1);
				float4 col = lerp(_BottomColor, _TopColor * lightIntensity, i.uv.y);

				return col;
            }
            ENDCG
        }

		Pass
        {
			Tags
			{
				"LightMode" = "ShadowCaster"
			}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma target 4.6
			#pragma geometry geo
			#pragma hull hull
			#pragma domain domain
			#pragma multi_compile_shadowcaster
            
			float4 frag (geometryOutput i) : SV_Target
            {	
				SHADOW_CASTER_FRAGMENT(i);
            }
            ENDCG
        }
    }
}