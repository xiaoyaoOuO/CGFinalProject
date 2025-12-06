Shader "Custom/DayNightSkybox"
{
    Properties
    {
        _DayTex("Day Texture (lat-long)", 2D) = "white" {}
        _NightTex("Night Texture (lat-long)", 2D) = "black" {}
        _CloudTex("Cloud Texture (tiling)", 2D) = "white" {}
        _CloudSpeed("Cloud Speed", Float) = 0.1
        _CloudTexNear("Cloud Texture Near", 2D) = "white" {}
        _CloudTexFar("Cloud Texture Far", 2D) = "white" {}
        _CloudNearScale("Cloud Near Scale", Float) = 4.0
        _CloudFarScale("Cloud Far Scale", Float) = 1.2
        _CloudNearOpacity("Cloud Near Opacity", Range(0,1)) = 0.6
        _CloudFarOpacity("Cloud Far Opacity", Range(0,1)) = 0.35
        _StarsIntensity("Stars Intensity", Float) = 1.5
        _DayNightBlend("Day/Night Blend", Range(0,1)) = 0
        _CloudOpacity("Cloud Opacity", Range(0,1)) = 0.3
        _CloudDayDarken("Cloud Day Darken", Range(0,2)) = 1.0
        _CloudNightDarken("Cloud Night Darken", Range(0,2)) = 0.5
        _CloudDayBrightness("Cloud Day Brightness", Range(0,2)) = 1.0
        _CloudNightBrightness("Cloud Night Brightness", Range(0,2)) = 0.7
        _CloudDayOpacity("Cloud Day Opacity", Range(0,1)) = 1.0
        _CloudNightOpacity("Cloud Night Opacity", Range(0,1)) = 0.6
        // 日间程序化动画配置（若希望使用贴图，仍可通过 _DayTex 提供）
        _DayTopColor("Day Top Color", Color) = (0.45,0.7,1,1)
        _DayHorizonColor("Day Horizon Color", Color) = (1,0.95,0.85,1)
        _DayAnimSpeed("Day Animation Speed", Float) = 0.06
        _DayAnimIntensity("Day Animation Intensity", Float) = 0.06
        _SunDir("Sun Direction", Vector) = (0,1,0,0)
        _SunColor("Sun Color", Color) = (1,0.95,0.8,1)
        _SunSize("Sun Size (deg)", Float) = 3.0
        _SunIntensity("Sun Intensity", Float) = 1.0
        // 月亮（由着色器绘制，优先于场景中的球体以便与云层/星空一致融合）
        _MoonTex("Moon Texture (RGBA)", 2D) = "white" {}
        _MoonSize("Moon Size (deg)", Float) = 4.5
        _MoonIntensity("Moon Intensity", Float) = 1.2
        _MoonSoftness("Moon Edge Softness", Range(0,1)) = 0.25
        _MoonGlowColor("Moon Glow Color", Color) = (1,0.95,0.85,1)
        _MoonGlowIntensity("Moon Glow Intensity", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _DayTex;
            sampler2D _NightTex;
            sampler2D _CloudTex;
            sampler2D _CloudTexNear;
            sampler2D _CloudTexFar;
            sampler2D _MoonTex;
            float _CloudSpeed;
            float _CloudNearScale;
            float _CloudFarScale;
            float _CloudNearOpacity;
            float _CloudFarOpacity;
            float _StarsIntensity;
            float _DayNightBlend;
            float _CloudOpacity;
            float _CloudDarken;
            float _CloudBrightness;
            float _CloudDayDarken;
            float _CloudNightDarken;
            float _CloudDayBrightness;
            float _CloudNightBrightness;
            float _CloudDayOpacity;
            float _CloudNightOpacity;
            float4 _DayTopColor;
            float4 _DayHorizonColor;
            float _DayAnimSpeed;
            float _DayAnimIntensity;
            float4 _SunDir;
            float4 _SunColor;
            float _SunSize;
            float _SunIntensity;
            float _MoonSize;
            float _MoonIntensity;
            float _MoonSoftness;
            float4 _MoonGlowColor;
            float _MoonGlowIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0; // world-space direction
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // world-space position of the vertex
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                // direction from camera to that position
                float3 camPos = _WorldSpaceCameraPos.xyz;
                o.dir = normalize(worldPos - camPos);
                return o;
            }

            // convert direction vector to lat-long UV (equirectangular)
            float2 DirToLatLongUV(float3 dir)
            {
                        float u = 0.5 + atan2(dir.x, dir.z) / (6.28318530718); // 2*pi
                        // 注意：不要用 saturate() 截断 dir.y 到 [0,1]，那会把下半球映射为同一 v
                        float v = 0.5 - asin(clamp(dir.y, -1.0, 1.0)) / 3.14159265359; // pi
                return float2(u, v);
            }

            

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float2 uv = DirToLatLongUV(dir);

                // day / night base
                // 日间使用程序化动画：垂直渐变（地平线 -> 天顶）并带有微弱随时间变化的色彩抖动
                float t = saturate((dir.y + 1.0) * 0.5); // 0 at nadir, 1 at zenith
                float grad = pow(t, 1.35);
                float3 proceduralDay = lerp(_DayHorizonColor.rgb, _DayTopColor.rgb, grad);
                // 小幅度时间和方向相关抖动，增加天空动态感
                float anim = sin(_Time.y * _DayAnimSpeed + dir.x * 6.28318) * 0.5 + 0.5;
                proceduralDay += _DayAnimIntensity * (anim - 0.5);
                fixed4 dayCol = fixed4(proceduralDay, 1.0);

                // 夜间仍使用夜空贴图（包含星星），并由脚本的 _StarsIntensity 调整亮度
                fixed4 nightCol = tex2D(_NightTex, uv) * _StarsIntensity;

                // 太阳盘：基于太阳方向在天空盒上绘制一个光盘（以角度为单位）
                float3 sunDir = normalize(_SunDir.xyz);
                float sunDot = dot(dir, sunDir);
                sunDot = saturate(sunDot);
                float ang = acos(sunDot); // radians
                float sizeRad = radians(max(_SunSize, 0.01));
                // 平滑遮罩：中心清晰，边缘渐隐
                float sunMask = 1.0 - smoothstep(sizeRad * 0.7, sizeRad, ang);
                float3 sunCol = _SunColor.rgb * _SunIntensity * sunMask;

                // 月亮盘：使用与太阳相反方向作为月心（场景控制器也将月亮放在太阳对面）
                float3 moonDir = normalize(-_SunDir.xyz);
                float moonDot = dot(dir, moonDir);
                moonDot = saturate(moonDot);
                float moonAng = acos(moonDot);
                float moonSizeRad = radians(max(_MoonSize, 0.01));
                float moonMask = 1.0 - smoothstep(moonSizeRad * (1.0 - _MoonSoftness), moonSizeRad * (1.0 + _MoonSoftness), moonAng);

                // 计算局部切线空间以用于月球纹理映射（仅用于小角度贴图，因此近似成立）
                float3 upRef = abs(moonDir.y) > 0.99 ? float3(1,0,0) : float3(0,1,0);
                float3 right = normalize(cross(upRef, moonDir));
                float3 up = cross(moonDir, right);
                float localX = dot(dir, right);
                float localY = dot(dir, up);
                float denom = sin(moonAng);
                denom = (denom == 0) ? 1e-6 : denom;
                float2 moonUV = float2(0.5 + localX / (2.0 * sin(moonSizeRad)), 0.5 + localY / (2.0 * sin(moonSizeRad)));
                fixed4 moonSample = tex2D(_MoonTex, moonUV);
                float2 moonLocal = float2(localX, localY);
                float distFromCenter = length(moonLocal) / (sin(moonSizeRad) + 1e-6);
                // 边缘暗化（近边缘稍微暗下来），增加真实感
                float rim = smoothstep(0.6, 1.0, distFromCenter);
                float3 moonSurface = moonSample.rgb * lerp(1.0, 0.7, rim);
                // 月亮光晕（柔和）
                float glowMask = 1.0 - smoothstep(moonSizeRad, moonSizeRad * 3.0, moonAng);

                // clouds: sample two layers (far/near) with different scales and offsets
                    float2 cloudUVFar = uv * _CloudFarScale + float2(_Time.y * _CloudSpeed * 0.6, 0);
                    fixed4 cloudFarCol = tex2D(_CloudTexFar, frac(cloudUVFar));

                    float2 cloudUVNear = uv * _CloudNearScale + float2(_Time.y * _CloudSpeed, 0);
                    fixed4 cloudNearCol = tex2D(_CloudTexNear, frac(cloudUVNear));

                // blend day/night: _DayNightBlend == 0 -> day, 1 -> night
                fixed4 baseCol = lerp(dayCol, nightCol, saturate(_DayNightBlend));

                // 在日间把太阳盘加到最终颜色上（只在白天明显）
                // 使用 (1 - _DayNightBlend) 来降低夜间太阳可见性
                float dayFactor = 1.0 - saturate(_DayNightBlend);
                baseCol.rgb += sunCol * dayFactor;

                // 夜间把月亮加入天空（并在后续由云层遮挡）
                float nightFactor = saturate(_DayNightBlend);
                baseCol.rgb += moonSurface * moonMask * _MoonIntensity * nightFactor;
                baseCol.rgb += _MoonGlowColor.rgb * _MoonGlowIntensity * glowMask * nightFactor;

                // apply far layer then near layer (far in back, near on top)
                float cloudFarAlpha = cloudFarCol.a * _CloudFarOpacity * _CloudOpacity;
                float cloudNearAlpha = cloudNearCol.a * _CloudNearOpacity * _CloudOpacity;

                // 合并云层 alpha 与颜色（近/远合成）
                float combinedAlpha = saturate(cloudFarAlpha + cloudNearAlpha - cloudFarAlpha * cloudNearAlpha);
                float3 combinedCloudColor = float3(0,0,0);
                float alphaDenom = cloudFarAlpha + cloudNearAlpha + 1e-6;
                combinedCloudColor = (cloudFarCol.rgb * cloudFarAlpha + cloudNearCol.rgb * cloudNearAlpha) / alphaDenom;

                // 根据日夜混合因子选择白天/夜间参数，然后应用到云的渲染
                float blend = saturate(_DayNightBlend); // 0 day, 1 night

                float cloudDarkenFactor = lerp(_CloudDayDarken, _CloudNightDarken, blend);
                float cloudBrightness = lerp(_CloudDayBrightness, _CloudNightBrightness, blend);
                float cloudLayerOpacity = lerp(_CloudDayOpacity, _CloudNightOpacity, blend);

                // 将云的 alpha 乘以日夜不透明度因子（以便白天/夜间能分别控制强度）
                combinedAlpha = combinedAlpha * cloudLayerOpacity;

                // 在云层区域暗化天空并增强云亮度
                float3 darkenedSky = baseCol.rgb * (1.0 - combinedAlpha * saturate(cloudDarkenFactor));
                float3 brightCloud = combinedCloudColor * cloudBrightness;
                baseCol.rgb = lerp(darkenedSky, brightCloud, combinedAlpha);

                baseCol.a = 1.0;
                return baseCol;
            }
            ENDCG
        }
    }
    FallBack "Skybox/Procedural"
}
