Shader "Custom/SnowAggregation" {
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_Color("Color", color) = (0.1, 0.1, 0.1,0)
		_Speed("Refresh Speed", float) = 50
	}
		SubShader{
			Tags{ "RenderType" = "Opaque" }

			Pass{

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;

			sampler2D _CameraDepthTexture;

			struct v2f {
				float2 uv : TEXCOORD0;
				float4 scrPos:TEXCOORD1;
				float4 pos : SV_POSITION;
			};

			v2f vert(appdata_base v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.scrPos = ComputeScreenPos(o.pos);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				//for some reason, the y position of the depth texture comes out inverted
				return o;
			}

			float _Speed;

			half4 frag(v2f i) : COLOR{
				fixed4 mtex = tex2D(_MainTex, i.uv);
				float depthValue = Linear01Depth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.scrPos)).r);
				//debug depth
				// return half4(depthValue, depthValue, depthValue, 1);
				
				// 使用阈值而不是精确比较
				float depthThreshold = 1;
				float4 c = mtex;
				
				// 如果不是背景（有物体），根据深度添加雪
				if (depthValue < depthThreshold) {	
					float snowAmount = 1.0 - depthValue; // 雪量与深度成反比
					c.rgb += _Color.rgb * snowAmount; // 根据深度添加颜色
				}else{
					if(frac(_Time.y) > 0.8f){
						c.rgb += float3(1,1,1) * _Speed;
					}
				}	
				return c;
			}
			ENDCG
		}
	}
		FallBack "Diffuse"
}