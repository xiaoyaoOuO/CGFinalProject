Shader"Custom/Leaves"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.0
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.1
        _WindFrequency ("Wind Frequency", Range(0, 10)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" }
        
Cull off

        CGPROGRAM
        // 添加顶点修改函数
        #pragma surface surf Lambert addshadow vertex:vert

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

sampler2D _MainTex;
float _WindSpeed;
float _WindStrength;
float _WindFrequency;

struct Input
{
    float2 uv_MainTex;
};

fixed4 _Color;

        // 飘动效果函数
void vert(inout appdata_full v, out Input o)
{
    UNITY_INITIALIZE_OUTPUT(Input, o);
            
            // 获取世界空间位置
    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            
            // 使用时间和位置创建风波动画
    float windTime = _Time.y * _WindSpeed;
    float windWave = sin(windTime + worldPos.x * _WindFrequency) * cos(windTime * 0.5 + worldPos.z * _WindFrequency);
            
            // 应用风效果 - 主要影响顶点y和x轴
    float windEffect = windWave * _WindStrength * v.texcoord.y; // 使用texcoord.y让树叶顶部摆动更大
            
            // 在局部空间应用变形
    v.vertex.x += windEffect * 0.5;
    v.vertex.y += windEffect;
            
            // 添加一些随机旋转效果
    float rotation = windWave * _WindStrength * 0.2;
    float3 pivot = float3(0, 0, 0); // 旋转中心点
            
            // 创建旋转矩阵
    float s = sin(rotation);
    float c = cos(rotation);
    float3x3 rotMatrix = float3x3(
                c, 0, s,
                0, 1, 0,
                -s, 0, c
            );
            
            // 应用旋转
    v.vertex.xyz = mul(rotMatrix, v.vertex.xyz - pivot) + pivot;
}

void surf(Input IN, inout SurfaceOutput o)
{
            // Albedo comes from a texture tinted by color
    fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
    o.Albedo = c.rgb;
    o.Alpha = c.a;
    clip(o.Alpha - 0.5);
}
        ENDCG
    }
FallBack"Diffuse"
}