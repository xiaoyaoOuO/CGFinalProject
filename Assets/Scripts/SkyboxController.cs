using UnityEngine;
using UnityEngine.Rendering;

// DayNightController：太阳与月亮（简洁版）
// - 月亮总是位于太阳的对面
// - 夜间根据太阳高度角判定，启用月亮显示并切换环境光
// - 提供 Inspector 参数用于调整行为
public class DayNightController : MonoBehaviour
{
    [Header("时间参数")]
    public float dayDurationInSeconds = 120f;
    [Range(0f, 1f)]
    public float currentTimeOfDay = 0.25f; // 0..1

    [Header("主光源（太阳）")]
    public Light sunLight; // 场景中的太阳（方向光）

    [Header("天空盒")]
    public Material skyboxMaterial;

    [Header("月亮（视觉）")]
    public GameObject moonPrefab;
    public Material moonMaterial;
    public float moonDistance = 50f;
    public float moonSize = 5f;
    public float minMoonHeight = 10f; // 相对于脚本挂载点的最低高度，避免出现在地下
    [Tooltip("If true, the skybox shader will draw the moon disk. The scene 'Moon' GameObject will be hidden when enabled.")]
    public bool useShaderMoon = true;
    [Header("Shader Moon Settings")]
    public Texture2D moonTexture;
    [Tooltip("Size of shader-drawn moon in degrees on the sky dome")] public float moonSizeDeg = 4.5f;
    [Tooltip("Brightness multiplier for the shader-drawn moon")] public float moonIntensity = 1.2f;
    [Tooltip("Softness of the moon edge (0..1)")] [Range(0f,1f)] public float moonSoftness = 0.25f;
    public Color moonGlowColor = new Color(1f, 0.95f, 0.85f);
    public float moonGlowIntensity = 0.6f;

    [Header("夜间环境")]
    public float nightAmbientIntensity = 0.22f;
    public Color nightAmbientColor = new Color(0.06f, 0.08f, 0.12f);

    [Header("月光（可选，影响全局光照）")]
    public bool enableMoonDirectionalLight = true;
    public float moonDirectionalIntensity = 0.35f; // 夜间方向光强度（比太阳弱）
    public Color moonDirectionalColor = new Color(0.85f, 0.9f, 1f);
    public float lightTransitionDuration = 2f; // 淡入/淡出持续时间（秒）

    [Header("昼夜判定")]
    public float nightElevationThreshold = -6f; // 当太阳高度角低于此值（度）时视为夜晚

    [Header("天空盒纹理")]
    public Texture2D daySkyTexture;
    public Texture2D nightSkyTexture; // 包含星星的纹理
    public Texture2D cloudTexture;
    public Texture2D cloudTextureNear;
    public Texture2D cloudTextureFar;
    public float cloudSpeed = 0.1f;
    public float starsIntensity = 1.5f;
    public float cloudOpacity = 0.9f;
    // 用于控制 shader 分层云参数（近/远）
    public float cloudNearScale = 4.0f;
    public float cloudFarScale = 1.2f;
    public float cloudNearOpacity = 1.0f;
    public float cloudFarOpacity = 0.6f;

    [Header("云层 日/夜 调整")]
    public float cloudDayDarken = 1.0f;
    public float cloudNightDarken = 0.5f;
    public float cloudDayBrightness = 1.0f;
    public float cloudNightBrightness = 0.7f;
    public float cloudDayOpacity = 1.0f;
    public float cloudNightOpacity = 0.6f;

    [Header("调试")]
    public bool pauseTime = false;
    public bool debugForceBrightMoon = false;
    public bool debugMoonLightLogs = false; // 在控制台打印月光/太阳状态以便排查


    // （已移除星星粒子与自动纹理生成功能，保持控制器简洁）

    GameObject moonInstance;
    Light moonDirectional;

    // 用于恢复白天的环境设置
    Color originalAmbientLight;
    float originalAmbientIntensity;
    AmbientMode originalAmbientMode;

    void Start()
    {
        // 缓存原始环境设置
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientLight = RenderSettings.ambientLight;
        originalAmbientIntensity = RenderSettings.ambientIntensity;

        if (sunLight == null)
            sunLight = RenderSettings.sun;

        if (skyboxMaterial != null)
            RenderSettings.skybox = skyboxMaterial;

        // 未启用自动纹理生成功能（如需纹理，请在场景中添加 TextureGenerator 并手动设置天空盒）

        CreateMoon();
        SetupMoonDirectionalLight();

        UpdateLightingImmediate();
    }

    void SetupMoonDirectionalLight()
    {
        if (!enableMoonDirectionalLight) return;

        if (moonDirectional == null)
        {
            var go = new GameObject("MoonDirectionalLight");
            go.transform.SetParent(transform, false);
            moonDirectional = go.AddComponent<Light>();
            moonDirectional.type = LightType.Directional;
            moonDirectional.color = moonDirectionalColor;
            moonDirectional.intensity = 0f;
            moonDirectional.shadows = LightShadows.None;
        }
    }

    // 纹理生成功能已移除 — 若需要可在独立组件中处理并将结果赋给天空盒

    void Update()
    {
        if (!pauseTime)
        {
            currentTimeOfDay += Time.deltaTime / dayDurationInSeconds;
            if (currentTimeOfDay >= 1f) currentTimeOfDay -= 1f;
        }

        UpdateLightingImmediate();
    }

    void CreateMoon()
    {
        if (moonInstance != null) return;

        if (moonPrefab != null)
        {
            moonInstance = Instantiate(moonPrefab, transform);
        }
        else
        {
            moonInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(moonInstance.GetComponent<Collider>());
            moonInstance.transform.SetParent(transform, false);
        }

        moonInstance.name = "Moon";
        moonInstance.transform.localScale = Vector3.one * moonSize;

        var rend = moonInstance.GetComponent<Renderer>();
        if (rend != null)
        {
            if (moonMaterial != null)
            {
                rend.sharedMaterial = moonMaterial;
            }
            else
            {
                // 尽量使用 Unlit/Color 保证黑夜也能看见
                Shader sh = Shader.Find("Unlit/Color");
                if (sh != null)
                {
                    var m = new Material(sh);
                    m.SetColor("_Color", new Color(0.96f, 0.96f, 1f));
                    rend.sharedMaterial = m;
                }
                else
                {
                    var m = new Material(Shader.Find("Standard"));
                    m.SetColor("_Color", new Color(0.9f, 0.9f, 0.95f));
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.12f, 0.12f, 0.14f));
                    rend.sharedMaterial = m;
                }
            }
        }
    }

    void UpdateLightingImmediate()
    {
        // 计算太阳在天空中的位置方向（从场景中心指向太阳）
        // currentTimeOfDay 映射到角度：0 -> -90deg（午夜），0.5 -> 90deg（中午）
        float sunAngle = currentTimeOfDay * 360f - 90f;
        // 使用 Vector3.up 作为基准，使得角度旋转更直观（绕 X 轴抬高或降落）
        Vector3 sunPositionDir = Quaternion.Euler(sunAngle, 0f, 0f) * Vector3.up;

        if (sunLight != null)
        {
            // 方向光应当朝向场景中心（即从太阳指向地面）
            sunLight.transform.rotation = Quaternion.LookRotation(-sunPositionDir);
            // 根据太阳高度调整强度（使用 sunPositionDir 的 y 分量）
            float elevation = Mathf.Clamp01((sunPositionDir.y + 0.1f) / 1.1f);
            sunLight.intensity = Mathf.Lerp(0.05f, 1f, elevation);
        }

        // 将月亮放在太阳的对面
        Vector3 moonDir = -sunPositionDir.normalized;
        Vector3 moonPos = transform.position + moonDir * moonDistance;
        if (moonInstance != null)
        {

            // 最小高度约束，防止出现在地下
            float minY = transform.position.y + minMoonHeight;
            if (moonPos.y < minY)
            {
                moonPos.y = minY;
                // 维持距离但调整水平分量以保持大致朝向
                Vector3 toMoon = (moonPos - transform.position).normalized;
                moonPos = transform.position + toMoon * moonDistance;
                if (moonPos.y < minY) moonPos.y = minY;
            }

            moonInstance.transform.position = moonPos;
            // 朝向场景中心（或朝向相机），使纹理朝向更自然
            moonInstance.transform.rotation = Quaternion.LookRotation((transform.position - moonInstance.transform.position).normalized, Vector3.up);
        }

        // 更新月光（方向光）方向与强度目标
        if (enableMoonDirectionalLight && moonDirectional != null)
        {
            // 方向光朝向场景中心（从月亮指向地面）
            moonDirectional.transform.rotation = Quaternion.LookRotation(-moonDir);
        }

        // 根据太阳高度判断是否为夜晚
        float sunElevationDeg = Mathf.Asin(Mathf.Clamp(sunPositionDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        bool isNight = sunElevationDeg < nightElevationThreshold;

        // 切换环境光
        if (isNight)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = nightAmbientColor * nightAmbientIntensity;
            RenderSettings.ambientIntensity = 1f;
        }
        else
        {
            // 恢复原始的白天设置
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientLight;
            RenderSettings.ambientIntensity = originalAmbientIntensity;
        }

        // 平滑过渡月光强度
        if (enableMoonDirectionalLight && moonDirectional != null)
        {
            float target = isNight ? moonDirectionalIntensity : 0f;
            if (lightTransitionDuration <= 0f)
                moonDirectional.intensity = target;
            else
                moonDirectional.intensity = Mathf.MoveTowards(moonDirectional.intensity, target, Time.deltaTime * (Mathf.Abs(moonDirectional.intensity - target) / lightTransitionDuration));
            // 同步颜色（允许实时调整）
            moonDirectional.color = moonDirectionalColor;
        }

        // 月亮只在夜间显示（可用 debug 强制显示）
        if (moonInstance != null)
        {
            // 若 shader 负责绘制月亮，则隐藏场景中的月亮球体以免重复
            bool sceneMoonVisible = !useShaderMoon && (isNight || debugForceBrightMoon);
            moonInstance.SetActive(sceneMoonVisible);
        }

        // 可选的运行时调试输出，打印月光与太阳当前状态
        if (debugMoonLightLogs)
        {
            string bakeType = moonDirectional != null ? moonDirectional.lightmapBakeType.ToString() : "null";
            string moonRot = moonDirectional != null ? moonDirectional.transform.rotation.eulerAngles.ToString() : "null";
            string moonPosStr = moonInstance != null ? moonInstance.transform.position.ToString() : "null";
            string moonEnabled = moonDirectional != null ? moonDirectional.enabled.ToString() : "null";
            string moonInt = moonDirectional != null ? moonDirectional.intensity.ToString() : "null";
            string sunInt = sunLight != null ? sunLight.intensity.ToString() : "null";

            Debug.Log($"[MoonDbg] moonEnabled={moonEnabled} moonIntensity={moonInt} bakeType={bakeType} moonRot={moonRot} moonPos={moonPosStr} sunDir={sunPositionDir} sunElev={sunElevationDeg:F2} isNight={isNight} sunLightInt={sunInt} moonActive={(moonInstance!=null?moonInstance.activeSelf.ToString():"null")} ");
        }

            // 更新天空盒材质参数
        if (skyboxMaterial != null)
        {
            // 根据太阳高度计算日夜混合因子（使用上面已计算的 sunElevationDeg）
            float dayNightBlend = Mathf.Clamp01((nightElevationThreshold - sunElevationDeg) / 20f);
            
            // 设置着色器参数
            if (daySkyTexture != null)
                skyboxMaterial.SetTexture("_DayTex", daySkyTexture);
            if (nightSkyTexture != null)
                skyboxMaterial.SetTexture("_NightTex", nightSkyTexture);
            if (cloudTexture != null)
                skyboxMaterial.SetTexture("_CloudTex", cloudTexture);
            if (cloudTextureNear != null)
                skyboxMaterial.SetTexture("_CloudTexNear", cloudTextureNear);
            if (cloudTextureFar != null)
                skyboxMaterial.SetTexture("_CloudTexFar", cloudTextureFar);
            // 月亮纹理与参数（用于着色器内绘制月盘）
            if (moonTexture != null)
                skyboxMaterial.SetTexture("_MoonTex", moonTexture);
            skyboxMaterial.SetFloat("_MoonSize", moonSizeDeg);
            skyboxMaterial.SetFloat("_MoonIntensity", moonIntensity);
            skyboxMaterial.SetFloat("_MoonSoftness", moonSoftness);
            skyboxMaterial.SetColor("_MoonGlowColor", moonGlowColor);
            skyboxMaterial.SetFloat("_MoonGlowIntensity", moonGlowIntensity);
                
            skyboxMaterial.SetFloat("_CloudSpeed", cloudSpeed);
            skyboxMaterial.SetFloat("_StarsIntensity", starsIntensity);
            skyboxMaterial.SetFloat("_DayNightBlend", dayNightBlend);
            skyboxMaterial.SetFloat("_CloudOpacity", cloudOpacity);
            skyboxMaterial.SetFloat("_CloudNearScale", cloudNearScale);
            skyboxMaterial.SetFloat("_CloudFarScale", cloudFarScale);
            skyboxMaterial.SetFloat("_CloudNearOpacity", cloudNearOpacity);
            skyboxMaterial.SetFloat("_CloudFarOpacity", cloudFarOpacity);
            // 白天/夜间云参数（分别传入以便在 shader 中混合）
            skyboxMaterial.SetFloat("_CloudDayDarken", cloudDayDarken);
            skyboxMaterial.SetFloat("_CloudNightDarken", cloudNightDarken);
            skyboxMaterial.SetFloat("_CloudDayBrightness", cloudDayBrightness);
            skyboxMaterial.SetFloat("_CloudNightBrightness", cloudNightBrightness);
            skyboxMaterial.SetFloat("_CloudDayOpacity", cloudDayOpacity);
            skyboxMaterial.SetFloat("_CloudNightOpacity", cloudNightOpacity);
            // 传递太阳参数给天空盒，以便 shader 绘制太阳盘
            Vector3 sunDirVec = sunPositionDir.normalized;
            skyboxMaterial.SetVector("_SunDir", new Vector4(sunDirVec.x, sunDirVec.y, sunDirVec.z, 0f));
            Color sunColor = sunLight != null ? sunLight.color : Color.white;
            skyboxMaterial.SetColor("_SunColor", sunColor);
            // 太阳尺寸与强度，可根据需要在 Inspector 暴露为参数
            skyboxMaterial.SetFloat("_SunSize", 3.0f);
            skyboxMaterial.SetFloat("_SunIntensity", sunLight != null ? sunLight.intensity : 1.0f);
            
            // 动态关键字，用于优化
            if (dayNightBlend > 0.5f)
                skyboxMaterial.EnableKeyword("NIGHT_MODE");
            else
                skyboxMaterial.DisableKeyword("NIGHT_MODE");
        }
    }

    [ContextMenu("Force Midnight")]
    public void ForceMidnight()
    {
        currentTimeOfDay = 0f;
        UpdateLightingImmediate();
    }

    [ContextMenu("Force Noon")]
    public void ForceNoon()
    {
        currentTimeOfDay = 0.5f;
        UpdateLightingImmediate();
    }
}