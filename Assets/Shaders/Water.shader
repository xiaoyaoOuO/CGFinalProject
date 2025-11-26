Shader "Custom/Water"
{
    Properties
    {
        _Foam("效果贴图:R深浅(黑浅白深)，G边缘泡沫，B细节扰动", 2D) = "white" {}
        _DeepColor("深水区颜色", Color) = (0,0,0,0)
        _ShalowColor("浅水区颜色", Color) = (1,1,1,0)

        [Space(20)]
        _WaterNormal("波纹法线贴图", 2D) = "bump" {}
        _NormalScale("法线强度", Range(0,1)) = 0.3
        _WaveParams ("水浪偏移速度：xy速度1，zw速度2", vector) = (-0.04,-0.02,-0.02,-0.04)

        [Space(20)]
        _WaterSpecular("高光强度", Range(0,1)) = 0.8
        _WaterSmoothness("高光衰减", Range(0,10)) = 8
        _LightColor ("高光颜色", color) = (1,1,1,1)
        _LightDir("光照方向", vector) = (0, 0, 0, 0)
        _RimPower ("菲涅尔强度", Range(0,20)) = 8

        [Space(20)]
        _FoamColor("泡沫颜色", Color) = (1,1,1,1)
        _FoamDepth("泡沫范围", Range(-2,10)) = 0.5
        _FoamFactor("泡沫衰减",Range(0,10)) = 0.2
        _FoamOffset("XY:泡沫速度,Z:泡沫强度,W:泡沫扰动", vector) = (-0.01,0.01, 2, 0.01)

        [Space(20)]
        _DetailColor("细节颜色", Color) = (1,1,1,1)
        _WaterWave("细节扰动强度",Range(0,0.1)) = 0.02

        [Space(20)]
        _Frequency("波动频率", Range(0,100)) = 10
        _Amplitude("波动幅度", Range(0,1)) = 0.1
        _Speed("波动速度", Range(0,10)) = 1

        [Space(40)]
        _AlphaWidth("边缘透明宽度",Range(-1,1)) = 0

        [Space(20)]
        _RefractionIntensity("折射强度", Range(0,1)) = 0.5
        _ReflectionIntensity("反射扰动", Range(0,1)) = 0.5

        [Space(20)]
        _Wave1("波1：XY方向，Z陡峭度，W波长", vector) = (1,0,0.2,60)
        _Wave2("波2：XY方向，Z陡峭度，W波长", vector) = (0.6,0.8,0.15,31)
        _Wave3("波3：XY方向，Z陡峭度，W波长", vector) = (0.5,0.5,0.1,18)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "true"}
        LOD 500

        GrabPass { "_CameraOpaqueTexture" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            uniform sampler2D _Foam;
            uniform float4 _Foam_ST;
            uniform half4 _DeepColor;
            uniform half4 _ShalowColor;

            uniform sampler2D _WaterNormal;
            uniform float4 _WaterNormal_ST;
            uniform half _NormalScale;
            uniform half4 _WaveParams;

            uniform half _WaterSpecular;
            uniform half _WaterSmoothness;
            uniform half4 _LightDir;
            uniform half4 _LightColor;
            uniform half _RimPower;

            uniform half4 _FoamColor;
            uniform half _FoamDepth;
            uniform half _FoamFactor;
            uniform half4 _FoamOffset;
            uniform sampler2D _CameraDepthTexture;

            uniform half4 _DetailColor;
            uniform half _WaterWave;

            uniform half _Frequency;
            uniform half _Amplitude;
            uniform half _Speed;
            uniform half _AlphaWidth;
            uniform half _RefractionIntensity;

            sampler2D _CameraOpaqueTexture;
            uniform float4 _CameraOpaqueTexture_TexelSize;

            float _ReflectionIntensity;
            uniform sampler2D _ReflectionTexture;

            float4 _Wave1;
            float4 _Wave2;
            float4 _Wave3;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uv_Tex : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float4 TW0:TEXCOORD2;
                float4 TW1:TEXCOORD3;
                float4 TW2:TEXCOORD4;
                float4 screenPos:TEXCOORD5;
                UNITY_FOG_COORDS(6)
            };
            float3 Reflection(half4 screenPos, float3 worldNormal, float3 viewDir)
            {
                float2 reflectUV = screenPos.xy / screenPos.w;
                
                // 应用法线扰动来实现水面波纹效果
                float2 normalDisturb = normalize(worldNormal.xz) * _ReflectionIntensity;
                reflectUV += normalDisturb * 0.05;
                
                // 确保UV在有效范围内
                reflectUV = saturate(reflectUV);
                
                float3 reflectColor = tex2D(_ReflectionTexture, reflectUV).rgb;
                
                // 根据视角调整反射强度（菲涅尔效应）
                float fresnelReflect = pow(1.0 - saturate(dot(worldNormal, viewDir)), 2.0);
                reflectColor *= fresnelReflect;
                
                return reflectColor;
            }

            float2 AlignWithGrabTexel(float2 uv){
               return (floor(uv*_CameraOpaqueTexture_TexelSize.zw)+ 0.5)*abs(_CameraOpaqueTexture_TexelSize.xy);
            }
            float3 Refraction(half eyeDepth,half4 screenPos,float refractionintensity,float3 WorldNormal)
            {
                //最后返回折射颜色
                float3 refractionColor = float3(0,0,0);
                float DepthDiff = eyeDepth - screenPos.w;
                
                //UV扰动
                float2 refractionUVOffset = WorldNormal*0.1*refractionintensity;
                refractionUVOffset *= saturate((DepthDiff)/abs(refractionintensity)+0.001);
                float2 sceneRefractionUVs = (screenPos.xy/screenPos.w)+float4(refractionUVOffset, refractionUVOffset).rg;
                sceneRefractionUVs = AlignWithGrabTexel(sceneRefractionUVs);

                refractionColor = tex2D(_CameraOpaqueTexture, sceneRefractionUVs);
                return refractionColor;
            }

            // Gerstner 波函数
            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 normal)
            {
                float steepness = wave.z;
                float wavelength = wave.w;
                float k = 2.0 * 3.14159 / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * _Time.y);
                float a = steepness / k;

                tangent += float3(
                    -d.x * d.x * (steepness * sin(f)),
                    d.x * (steepness * cos(f)),
                    -d.x * d.y * (steepness * sin(f))
                );
                normal += float3(
                    -d.x * d.y * (steepness * sin(f)),
                    d.y * (steepness * cos(f)),
                    -d.y * d.y * (steepness * sin(f))
                );
                return float3(
                    d.x * a * cos(f),
                    a * sin(f),
                    d.y * a * cos(f)
                );
            }

            v2f vert(appdata_full v)
            {
                //海浪起伏
                float time = _Time.y * _Speed;
                // float waveValue = sin(time + v.vertex.x *_Frequency)* _Amplitude;
                // v.vertex.xyz = float3(v.vertex.x, v.vertex.y + waveValue, v.vertex.z);
                float3 pos = v.vertex.xyz;
                float3 tangent = v.tangent.xyz;
                float3 normal = v.normal;

                // 定义多个波（波向、陡峭度、波长）
                float4 wave1 = _Wave1;
                float4 wave2 = _Wave2;
                float4 wave3 = _Wave3;

                pos += GerstnerWave(wave1, pos, tangent, normal);
                pos += GerstnerWave(wave2, pos, tangent, normal);
                pos += GerstnerWave(wave3, pos, tangent, normal);
                v.vertex.xyz = pos;
                v.normal = normalize(normal);
                v.tangent.xyz = normalize(tangent);

                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv_Tex.xy = TRANSFORM_TEX(v.texcoord, _Foam);
                o.uv_Tex.zw = TRANSFORM_TEX(v.texcoord, _WaterNormal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);

                
                fixed3 worldNormal = UnityObjectToWorldNormal(v.normal);
                fixed3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                fixed tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                fixed3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign;

                o.TW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, o.worldPos.x);
                o.TW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, o.worldPos.y);
                o.TW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, o.worldPos.z);

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_TARGET
            {
                // 计算水波纹法线
                half2 panner1 = ( _Time.y * _WaveParams.xy + i.uv_Tex.zw);
                half2 panner2 = ( _Time.y * _WaveParams.zw + i.uv_Tex.zw);

                half3 worldNormal = BlendNormals(UnpackNormal(tex2D( _WaterNormal, panner1)) 
                    , UnpackNormal(tex2D(_WaterNormal, panner2)));
                worldNormal = lerp(half3(0, 0, 1), worldNormal, _NormalScale);
                worldNormal = normalize(fixed3(
                    dot(i.TW0.xyz, worldNormal), 
                    dot(i.TW1.xyz, worldNormal), 
                    dot(i.TW2.xyz, worldNormal)));
                
                // 计算深度
                half4 screenPos = half4(i.screenPos.xyz,i.screenPos.w);
                half eyeDepth = LinearEyeDepth(UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture,UNITY_PROJ_COORD( screenPos ))));
                half eyeDepthSubScreenPos = abs( eyeDepth - screenPos.w );
                half depthMask = 1-eyeDepthSubScreenPos + _FoamDepth;

                //计算泡沫
                half3 water = tex2D(_Foam,i.uv_Tex.xy/_Foam_ST.xy);
                half3 foam1 = tex2D(_Foam,i.uv_Tex.xy + worldNormal.xy*_FoamOffset.w);
                half3 foam2 = tex2D(_Foam, _Time.y * _FoamOffset.xy + i.uv_Tex.xy + worldNormal.xy*_FoamOffset.w);
                float temp_output = ( saturate( (foam1.g + foam2.g ) * depthMask * water.g  -_FoamFactor));

                //光照
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float NdotV = saturate(dot(worldNormal,viewDir));
                fixed3 worldLightDir = _LightDir.xyz;
                fixed3 halfDir = normalize(worldLightDir + viewDir);

                //细节颜色
                half2 detailplanner = i.uv_Tex.xy / _Foam_ST.xy + worldNormal.xy*_WaterWave;
                half4 detail = tex2D(_Foam, detailplanner).b * _DetailColor;

                //折射
                float3 refractionColor = float3(0,0,0);
                if(eyeDepth - screenPos.w > 0 && eyeDepth - screenPos.w < 6){
                    refractionColor = Refraction(eyeDepth,screenPos,_RefractionIntensity,worldNormal);
                }
                //反射
                float3 reflectionColor = Reflection(screenPos, worldNormal, viewDir);

                half4 diffuse = lerp(_ShalowColor, _DeepColor, water.r);
                diffuse = lerp( diffuse , _FoamColor * _FoamOffset.z , temp_output);
                fixed3 specular = _LightColor.rgb * _WaterSpecular * pow(max(0, dot(worldNormal, halfDir)), _WaterSmoothness*256.0);
                fixed3 rim = pow(1-saturate(NdotV),_RimPower)*_LightColor;
                diffuse = diffuse * (NdotV + detail) * 0.5;
                half alpha = saturate(eyeDepthSubScreenPos-_AlphaWidth);
                
                // 混合反射、折射和基本光照
                float3 finalColor = diffuse.rgb + specular + rim*0.2;
                finalColor += refractionColor * (1.0 - alpha) * 0.5;
                finalColor += reflectionColor;
                fixed4 col = fixed4( finalColor ,alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 400
        PAss
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            uniform sampler2D _Foam;
            uniform float4 _Foam_ST;
            uniform half4 _DeepColor;
            uniform half4 _ShalowColor;

            uniform sampler2D _WaterNormal;
            uniform float4 _WaterNormal_ST;
            uniform half _NormalScale;
            uniform half4 _WaveParams;

            uniform half _WaterSpecular;
            uniform half _WaterSmoothness;
            uniform half4 _LightDir;
            uniform half4 _LightColor;
            uniform half _RimPower;

            uniform half4 _FoamColor;
            uniform half _FoamDepth;
            uniform half _FoamFactor;
            uniform half4 _FoamOffset;
            uniform sampler2D _CameraDepthTexture;

            uniform half4 _DetailColor;
            uniform half _WaterWave;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uv_Tex : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float4 TW0:TEXCOORD2;
                float4 TW1:TEXCOORD3;
                float4 TW2:TEXCOORD4;
                float4 screenPos:TEXCOORD5;
                UNITY_FOG_COORDS(6)
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv_Tex.xy = TRANSFORM_TEX(v.texcoord, _Foam);
                o.uv_Tex.zw = TRANSFORM_TEX(v.texcoord, _WaterNormal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                
                fixed3 worldNormal = UnityObjectToWorldNormal(v.normal);
                fixed3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                fixed tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                fixed3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign;

                o.TW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, o.worldPos.x);
                o.TW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, o.worldPos.y);
                o.TW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, o.worldPos.z);

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_TARGET
            {
                half2 panner1 = ( _Time.y * _WaveParams.xy + i.uv_Tex.zw);
                half2 panner2 = ( _Time.y * _WaveParams.zw + i.uv_Tex.zw);

                half3 worldNormal = BlendNormals(UnpackNormal(tex2D( _WaterNormal, panner1)) 
                    , UnpackNormal(tex2D(_WaterNormal, panner2)));
                worldNormal = lerp(half3(0, 0, 1), worldNormal, _NormalScale);
                worldNormal = normalize(fixed3(
                    dot(i.TW0.xyz, worldNormal), 
                    dot(i.TW1.xyz, worldNormal), 
                    dot(i.TW2.xyz, worldNormal)));
                
                // 计算深度
                half4 screenPos = half4(i.screenPos.xyz,i.screenPos.w);
                half eyeDepth = LinearEyeDepth(UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture,UNITY_PROJ_COORD( screenPos ))));
                half eyeDepthSubScreenPos = abs( eyeDepth - screenPos.w );
                half depthMask = 1-eyeDepthSubScreenPos + _FoamDepth;

                //计算泡沫
                half3 water = tex2D(_Foam,i.uv_Tex.xy/_Foam_ST.xy);
                half3 foam1 = tex2D(_Foam,i.uv_Tex.xy + worldNormal.xy*_FoamOffset.w);
                half3 foam2 = tex2D(_Foam, _Time.y * _FoamOffset.xy + i.uv_Tex.xy + worldNormal.xy*_FoamOffset.w);
                float temp_output = ( saturate( (foam1.g + foam2.g ) * depthMask * water.g  -_FoamFactor));

                //光照
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float NdotV = saturate(dot(worldNormal,viewDir));
                fixed3 worldLightDir = _LightDir.xyz;
                fixed3 halfDir = normalize(worldLightDir + viewDir);

                //细节颜色
                half2 detailplanner = i.uv_Tex.xy / _Foam_ST.xy + worldNormal.xy*_WaterWave;
                half4 detail = tex2D(_Foam, detailplanner).b * _DetailColor;

                half4 diffuse = lerp(_ShalowColor, _DeepColor, water.r);
                diffuse = lerp( diffuse , _FoamColor * _FoamOffset.z , temp_output);
                fixed3 specular = _LightColor.rgb * _WaterSpecular * pow(max(0, dot(worldNormal, halfDir)), _WaterSmoothness*256.0);
                fixed3 rim = pow(1-saturate(NdotV),_RimPower)*_LightColor;
                diffuse.rgb = diffuse.rgb * (NdotV + detail.rgb) * 0.5;
                fixed4 col = fixed4(diffuse.rgb+specular+rim*0.2,1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        PAss
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            uniform sampler2D _Foam;
            uniform float4 _Foam_ST;
            uniform half4 _DeepColor;
            uniform half4 _ShalowColor;

            uniform sampler2D _WaterNormal;
            uniform float4 _WaterNormal_ST;
            uniform half _NormalScale;
            uniform half4 _WaveParams;

            uniform half _WaterSpecular;
            uniform half _WaterSmoothness;
            uniform half4 _LightDir;
            uniform half4 _LightColor;
            uniform half _RimPower;

            uniform half4 _FoamColor;
            uniform half _FoamDepth;
            uniform half _FoamFactor;
            uniform half4 _FoamOffset;
            uniform sampler2D _CameraDepthTexture;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uv_Tex : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float4 TW0:TEXCOORD2;
                float4 TW1:TEXCOORD3;
                float4 TW2:TEXCOORD4;
                float4 screenPos:TEXCOORD5;
                UNITY_FOG_COORDS(6)
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv_Tex.xy = TRANSFORM_TEX(v.texcoord, _Foam);
                o.uv_Tex.zw = TRANSFORM_TEX(v.texcoord, _WaterNormal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                
                fixed3 worldNormal = UnityObjectToWorldNormal(v.normal);
                fixed3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                fixed tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                fixed3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign;

                o.TW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, o.worldPos.x);
                o.TW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, o.worldPos.y);
                o.TW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, o.worldPos.z);

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_TARGET
            {
                half2 panner1 = ( _Time.y * _WaveParams.xy + i.uv_Tex.zw);
                half2 panner2 = ( _Time.y * _WaveParams.zw + i.uv_Tex.zw);

                half3 worldNormal = BlendNormals(UnpackNormal(tex2D( _WaterNormal, panner1)) 
                    , UnpackNormal(tex2D(_WaterNormal, panner2)));
                worldNormal = lerp(half3(0, 0, 1), worldNormal, _NormalScale);
                worldNormal = normalize(fixed3(
                    dot(i.TW0.xyz, worldNormal), 
                    dot(i.TW1.xyz, worldNormal), 
                    dot(i.TW2.xyz, worldNormal)));
                
                // 计算深度
                half4 screenPos = half4(i.screenPos.xyz,i.screenPos.w);
                half eyeDepth = LinearEyeDepth(UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture,UNITY_PROJ_COORD( screenPos ))));
                half eyeDepthSubScreenPos = abs( eyeDepth - screenPos.w );
                half depthMask = 1-eyeDepthSubScreenPos + _FoamDepth;

                //计算泡沫
                half3 water = tex2D(_Foam,i.uv_Tex.xy/_Foam_ST.xy);
                half3 foam1 = tex2D(_Foam,i.uv_Tex.xy + worldNormal.xy*_FoamOffset.w);
                half3 foam2 = tex2D(_Foam, _Time.y * _FoamOffset.xy + i.uv_Tex.xy + worldNormal.xy*_FoamOffset.w);
                float temp_output = ( saturate( (foam1.g + foam2.g ) * depthMask * water.g  -_FoamFactor));

                //光照
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float NdotV = saturate(dot(worldNormal,viewDir));
                fixed3 worldLightDir = _LightDir.xyz;
                fixed3 halfDir = normalize(worldLightDir + viewDir);

                half4 diffuse = lerp(_ShalowColor, _DeepColor, water.r);
                diffuse = lerp( diffuse , _FoamColor * _FoamOffset.z , temp_output);
                fixed3 specular = _LightColor.rgb * _WaterSpecular * pow(max(0, dot(worldNormal, halfDir)), _WaterSmoothness*256.0);
                fixed3 rim = pow(1-saturate(NdotV),_RimPower)*_LightColor;
                diffuse *= NdotV;
                fixed4 col = fixed4(diffuse.rgb+specular+rim*0.2,1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        PAss
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            uniform sampler2D _Foam;
            uniform float4 _Foam_ST;
            uniform half4 _DeepColor;
            uniform half4 _ShalowColor;

            uniform sampler2D _WaterNormal;
            uniform float4 _WaterNormal_ST;
            uniform half _NormalScale;
            uniform half4 _WaveParams;

            uniform half _WaterSpecular;
            uniform half _WaterSmoothness;
            uniform half4 _LightDir;
            uniform half4 _LightColor;
            uniform half _RimPower;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uv_Tex : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float4 TW0:TEXCOORD2;
                float4 TW1:TEXCOORD3;
                float4 TW2:TEXCOORD4;
                UNITY_FOG_COORDS(5)
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv_Tex.xy = TRANSFORM_TEX(v.texcoord, _Foam);
                o.uv_Tex.zw = TRANSFORM_TEX(v.texcoord, _WaterNormal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                fixed3 worldNormal = UnityObjectToWorldNormal(v.normal);
                fixed3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                fixed tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                fixed3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign;

                o.TW0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, o.worldPos.x);
                o.TW1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, o.worldPos.y);
                o.TW2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, o.worldPos.z);

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_TARGET
            {
                half2 panner1 = ( _Time.y * _WaveParams.xy + i.uv_Tex.zw);
                half2 panner2 = ( _Time.y * _WaveParams.zw + i.uv_Tex.zw);

                half3 worldNormal = BlendNormals(UnpackNormal(tex2D( _WaterNormal, panner1)) 
                    , UnpackNormal(tex2D(_WaterNormal, panner2)));
                worldNormal = lerp(half3(0, 0, 1), worldNormal, _NormalScale);
                worldNormal = normalize(fixed3(
                    dot(i.TW0.xyz, worldNormal), 
                    dot(i.TW1.xyz, worldNormal), 
                    dot(i.TW2.xyz, worldNormal)));

                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float NdotV = saturate(dot(worldNormal,viewDir));
                fixed3 worldLightDir = _LightDir.xyz;
                fixed3 halfDir = normalize(worldLightDir + viewDir);
                half degree = tex2D(_Foam,i.uv_Tex.xy/_Foam_ST.xy).r;
                half4 diffuse = lerp(_ShalowColor, _DeepColor, degree);
                diffuse *= NdotV;
                fixed3 specular = _LightColor.rgb * _WaterSpecular * pow(max(0, dot(worldNormal, halfDir)), _WaterSmoothness*256.0);
                fixed3 rim = pow(1-saturate(NdotV),_RimPower)*_LightColor;

                fixed4 col = fixed4(diffuse.rgb+specular+rim*0.2,1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Pass
        {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            uniform sampler2D _Foam;
            uniform float4 _Foam_ST;
            uniform half4 _DeepColor;
            uniform half4 _ShalowColor;

            v2f vert(appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _Foam);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }


            fixed4 frag (v2f IN) : SV_TARGET
            {
                half degree = tex2D(_Foam, IN.uv / _Foam_ST.xy).r;
                float4 diffuse = lerp(_ShalowColor, _DeepColor, degree);
                float4 col = fixed4(diffuse.rgb, 1);
                UNITY_APPLY_FOG(IN.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
