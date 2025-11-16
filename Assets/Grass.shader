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
    }

	CGINCLUDE
	#include "UnityCG.cginc"
	#include "Autolight.cginc"
	#include "Shaders/CustomTessellation.cginc"

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
	};

	geometryOutput VertexOutput(float3 pos,float2 uv)
	{
		geometryOutput o;
		o.pos = UnityObjectToClipPos(pos);
		o.uv = uv;
		return o;
	}

	[maxvertexcount(3)]
	void geo(triangle vertexOutput IN[3], inout TriangleStream<geometryOutput> triStream)
	{
		float3 pos = IN[0].vertex;

		float3 vNormal = IN[0].normal;
		float4 vTangent = IN[0].tangent;

		float3 vBinormal = cross(vNormal, vTangent) * vTangent.w;

		float3x3 tbn = float3x3(
			vTangent.x, vBinormal.x, vNormal.x,
			vTangent.y, vBinormal.y, vNormal.y,
			vTangent.z, vBinormal.z, vNormal.z
		);

		float height = (rand(pos.zyx) * 2 - 1) * _BladeHeightRandom + _BladeHeight;
		float width = (rand(pos.xzy) * 2 - 1) * _BladeWidthRandom + _BladeWidth;

		//随机朝向
		float3x3 RotateMatrix = AngleAxis3x3(rand(pos) * UNITY_TWO_PI, float3(0,0,1));

		float3x3 BlendMatrix = AngleAxis3x3(rand(pos.zzx) * UNITY_PI * _BlendRotationRandom * 0.5, float3(-1,0,0));

		float3x3 TransformMatrix = mul(mul(tbn, RotateMatrix), BlendMatrix);

		geometryOutput o;
		triStream.Append(VertexOutput(pos + mul(TransformMatrix, float3(width,0,0)),float2(0.0,0.0)));
		triStream.Append(VertexOutput(pos + mul(TransformMatrix, float3(-width,0,0)),float2(1.0,0)));
		triStream.Append(VertexOutput(pos + mul(TransformMatrix, float3(0,0,height)),float2(0.5,1)));
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
            
			#include "Lighting.cginc"

			float4 _TopColor;
			float4 _BottomColor;
			float _TranslucentGain;

			float4 frag (geometryOutput i, fixed facing : VFACE) : SV_Target
            {	
				return lerp(_BottomColor,_TopColor,i.uv.y);
            }
            ENDCG
        }
    }
}