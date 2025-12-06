using UnityEngine;
using System.Collections;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TextureGenerator : MonoBehaviour
{
    [Header("纹理设置")]
    public int textureSize = 512;
    public bool generateOnStart = false; // 改为默认不自动生成
    
    [Header("星星纹理设置")]
    public int starCount = 300;
    
    [Header("云朵纹理设置")]
    public float cloudScale = 4.0f;
    public int cloudOctaves = 4;
    public float cloudPersistence = 0.5f;
    // 风格化云参数
    public float cloudContrast = 1.8f; // 对比度，增大会使云更有边界
    public float cloudThreshold = 0.12f; // 阈值，低于此值部分将变透明（降低可增大云覆盖）
    public float cloudBaseOpacity = 0.85f; // 最终云层 alpha 基础值（整体透明度控制）
    // 远近云层设置（用于生成两张不同尺度/透明度的云图）
    public float cloudFarScale = 1.2f;
    public int cloudFarOctaves = 3;
    public float cloudFarPersistence = 0.5f;
    public float cloudFarContrast = 1.2f;
    public float cloudFarThreshold = 0.12f;
    public float cloudFarBaseOpacity = 0.75f;
    // 旧的中心掩码参数已移除 — 使用新的风格化云参数
    public float cloudEdgeSoftness = 0.15f; // 仍用于边缘柔和性（但不做中心掩码）
    // 细节与光照控制（用于风格化效果）
    public float cloudDetailScale = 6.0f; // 细节噪声的缩放（越大细节越细腻）
    public float cloudNoiseMix = 0.68f; // 基础云与细节的混合比例
    public float cloudLightIntensity = 0.55f; // 顶部高光强度
    public float cloudBottomDarkness = 0.5f; // 底部暗部强度
    [Header("消除对称性 / 扰动设置")]
    [Tooltip("启用基于噪声的域扭曲以打破云的对称性/放射状伪像")]
    public bool enableDomainWarp = true;
    [Tooltip("域扭曲强度（0关闭，0.1~1常用）")]
    [Range(0f, 2f)]
    public float domainWarpStrength = 0.45f;
    [Tooltip("是否在生成时对 UV 进行整体旋转，辅助破坏对称性")]
    public bool enableRotation = false;
    [Tooltip("预设旋转角度（度），若为0且 enableRotation=true 则使用随机种子生成角度")]
    public float rotationAngleDeg = 0f;
    [Tooltip("用于生成随机旋转/扰动的种子（0 表示使用时间随机）")]
    public int randomSeed = 0;
    [Header("边缘填充（用于隐藏拼接/边缘）")]
    public bool fillEdgeWithColor = false;
    public Color edgeFillColor = new Color(1f, 1f, 1f, 0f); // 默认为透明
    [Range(0f, 0.5f)]
    public float edgeMargin = 0.05f; // 边缘占纹理尺寸的比例
    
    [Header("输出纹理")]
    public Texture2D starTexture;
    public Texture2D cloudTexture;
    public Texture2D cloudTextureNear;
    public Texture2D cloudTextureFar;
    
    [Header("保存路径")]
    public string saveFolder = "GeneratedTextures";
    
    void Start()
    {
        if (generateOnStart)
        {
            GenerateAndSaveAllTextures();
        }
    }
    
    [ContextMenu("生成并保存所有纹理")]
    public void GenerateAndSaveAllTextures()
    {
        Debug.Log("开始生成纹理...");
        
        // 生成纹理
        starTexture = GenerateStarTexture();
        // 生成近/远两层云图
        cloudTextureNear = GenerateCloudTextureVariant(cloudScale, cloudOctaves, cloudPersistence, cloudContrast, cloudThreshold, cloudBaseOpacity);
        cloudTextureFar = GenerateCloudTextureVariant(cloudFarScale, cloudFarOctaves, cloudFarPersistence, cloudFarContrast, cloudFarThreshold, cloudFarBaseOpacity);
        // 主 cloudTexture 保持兼容（指向 near 层）
        cloudTexture = cloudTextureNear;
        
        // 保存纹理
        if (starTexture != null)
        {
            SaveTextureToFile(starTexture, "StarTexture.png");
            Debug.Log("星星纹理生成并保存完成");
        }
        
        if (cloudTextureNear != null)
        {
            SaveTextureToFile(cloudTextureNear, "CloudTexture_Near.png");
            Debug.Log("近云纹理生成并保存完成");
        }
        if (cloudTextureFar != null)
        {
            SaveTextureToFile(cloudTextureFar, "CloudTexture_Far.png");
            Debug.Log("远云纹理生成并保存完成");
        }

        // 生成并保存一个白天用的完全透明纹理（白色，alpha=0），便于在场景里仅显示星空/云层效果
        Texture2D dayTransparentTex = GenerateDayTransparentTexture();
        if (dayTransparentTex != null)
        {
            SaveTextureToFile(dayTransparentTex, "DayTransparent.png");
            Debug.Log("白天透明纹理生成并保存完成");
        }

        // 应用到当前场景的天空盒
        ApplyToCurrentScene();
    }
    
    [ContextMenu("仅生成星星纹理")]
    public void GenerateStarTextureOnly()
    {
        starTexture = GenerateStarTexture();
        if (starTexture != null)
        {
            SaveTextureToFile(starTexture, "StarTexture.png");
            Debug.Log("星星纹理生成并保存完成");
            ApplyToCurrentScene();
        }
    }
    
    [ContextMenu("仅生成云朵纹理")]
    public void GenerateCloudTextureOnly()
    {
        cloudTextureNear = GenerateCloudTextureVariant(cloudScale, cloudOctaves, cloudPersistence, cloudContrast, cloudThreshold, cloudBaseOpacity);
        cloudTextureFar = GenerateCloudTextureVariant(cloudFarScale, cloudFarOctaves, cloudFarPersistence, cloudFarContrast, cloudFarThreshold, cloudFarBaseOpacity);
        cloudTexture = cloudTextureNear;
        if (cloudTextureNear != null)
        {
            SaveTextureToFile(cloudTextureNear, "CloudTexture_Near.png");
            Debug.Log("近云纹理生成并保存完成");
        }
        if (cloudTextureFar != null)
        {
            SaveTextureToFile(cloudTextureFar, "CloudTexture_Far.png");
            Debug.Log("远云纹理生成并保存完成");
        }
        ApplyToCurrentScene();
    }
    
    public Texture2D GenerateStarTexture()
    {
        try
        {
            Debug.Log("生成星星纹理...");
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            
            // 填充透明背景（黑色但 alpha 为 0）以便导出为带透明通道的星点贴图
            Color[] pixels = new Color[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            }
            
            // 生成星星
            for (int i = 0; i < starCount; i++)
            {
                int x = Random.Range(0, textureSize);
                int y = Random.Range(0, textureSize);
                
                float brightness = Random.Range(0.4f, 1.0f);
                // 缩小默认星点尺寸以得到更细小的高光
                float size = Random.Range(0.4f, 1.4f);
                
                // 小概率生成亮星
                if (Random.value < 0.1f)
                {
                    brightness = Random.Range(0.8f, 1.2f);
                    // 亮星也缩小到更合理的范围，但仍比普通星点大
                    size = Random.Range(1.0f, 2.0f);
                }
                
                DrawStar(pixels, x, y, brightness, size);
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            
            Debug.Log("星星纹理生成成功");
            return texture;
        }
        catch (System.Exception e)
        {
            Debug.LogError("生成星星纹理时出错: " + e.Message);
            return null;
        }
    }
    
    void DrawStar(Color[] pixels, int centerX, int centerY, float brightness, float size)
    {
        int radius = Mathf.CeilToInt(size);
        
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int pixelX = centerX + x;
                int pixelY = centerY + y;
                
                if (pixelX >= 0 && pixelX < textureSize && pixelY >= 0 && pixelY < textureSize)
                {
                    float distance = Mathf.Sqrt(x * x + y * y);
                        if (distance <= radius)
                        {
                            // 使用更小的分母以让高光更集中，视觉上更小
                            float intensity = Mathf.Exp(-distance * distance / (size * size * 0.35f));
                            intensity *= brightness;

                            int index = pixelY * textureSize + pixelX;
                            Color currentColor = pixels[index];

                            // 将星星作为白色点叠加到当前像素上，alpha 表示不透明度
                            float newAlpha = Mathf.Clamp01(currentColor.a + intensity);
                            float newChannel = Mathf.Clamp01(currentColor.r + intensity);
                            pixels[index] = new Color(newChannel, newChannel, newChannel, newAlpha);
                        }
                }
            }
        }
    }
    
    public Texture2D GenerateCloudTexture()
    {
        return GenerateCloudTextureVariant(cloudScale, cloudOctaves, cloudPersistence, cloudContrast, cloudThreshold, cloudBaseOpacity);
    }

    // 通用云层生成器（可用于生成近/远不同参数的云图）
    public Texture2D GenerateCloudTextureVariant(float scale, int octaves, float persistence, float contrast, float threshold, float baseOpacity)
    {
        try
        {
            Debug.Log($"生成云朵纹理（scale={scale}, octaves={octaves}）...");
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

            Color[] pixels = new Color[textureSize * textureSize];

            for (int x = 0; x < textureSize; x++)
            {
                for (int y = 0; y < textureSize; y++)
                {
                    // normalized uv [0,1]
                    float uNorm = ((float)x + 0.5f) / textureSize;
                    float vNorm = ((float)y + 0.5f) / textureSize;

                    // 可选：整体旋转以打破明显对称（围绕中心 0.5,0.5）
                    if (enableRotation)
                    {
                        float angleRad = rotationAngleDeg * Mathf.Deg2Rad;
                        if (Mathf.Approximately(rotationAngleDeg, 0f))
                        {
                            // 若角度为 0 且提供了种子，则用种子生成随机角度
                            System.Random rnd = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random(System.Environment.TickCount);
                            angleRad = ((float)rnd.NextDouble() * 360f) * Mathf.Deg2Rad;
                        }
                        float cx = 0.5f, cy = 0.5f;
                        float dx = uNorm - cx;
                        float dy = vNorm - cy;
                        float cosA = Mathf.Cos(angleRad);
                        float sinA = Mathf.Sin(angleRad);
                        float rx = dx * cosA - dy * sinA;
                        float ry = dx * sinA + dy * cosA;
                        uNorm = rx + cx;
                        vNorm = ry + cy;
                    }

                    // 域扭曲（domain warp）：在采样前用低频 FBM/噪声对坐标进行扰动
                    if (enableDomainWarp && domainWarpStrength > 0f)
                    {
                        // 使用低频的 FBM 作为位移场
                        float warpScale = Mathf.Max(1f, cloudDetailScale * 0.5f);
                        float wx = FractalBrownianMotion(uNorm * warpScale + 12.34f, vNorm * warpScale + 45.67f, 3, 0.5f) - 0.5f;
                        float wy = FractalBrownianMotion(uNorm * warpScale + 98.76f, vNorm * warpScale + 11.11f, 3, 0.5f) - 0.5f;
                        uNorm += wx * domainWarpStrength;
                        vNorm += wy * domainWarpStrength;
                    }

                    // 最终采样坐标（缩放到 scale 范围）
                    float xCoord = uNorm * scale;
                    float yCoord = vNorm * scale;

                    // 多层无缝 FBM 合成：基础低频云 + 高频细节（使用 tileable FBM 避免瓦片重复感）
                    float baseNoise = TileableFBM(xCoord, yCoord, octaves, persistence, scale);
                    float detailNoise = TileableFBM(xCoord * cloudDetailScale / scale, yCoord * cloudDetailScale / scale, Mathf.Max(1, octaves - 1), Mathf.Max(0.3f, persistence), scale * cloudDetailScale);
                    // 合并基础与细节
                    float combined = Mathf.Lerp(baseNoise, detailNoise, cloudNoiseMix);
                    // 增强对比并阈值化以得到云块形状
                    combined = Mathf.Pow(Mathf.Clamp01(combined), 1.15f);
                    float shaped = Mathf.Clamp01((combined - threshold) * contrast);

                    // 基于高度进行光照与阴影：顶端更亮，底部更暗
                    float v = (float)y / textureSize; // 0..1 bottom->top
                    // 计算亮度修正：靠近顶端提升亮度，靠近底部降低
                    float topLight = Mathf.Lerp(1.0f, 1.0f + cloudLightIntensity, Mathf.Pow(Mathf.Clamp01((v - 0.45f) * 2.0f), 1.4f));
                    float bottomShade = Mathf.Lerp(1.0f, cloudBottomDarkness, Mathf.Clamp01((0.5f - v) * 2.0f));
                    float shade = topLight * bottomShade;

                    // 最终 alpha（注意：alpha 越大云越密）
                    float alpha = shaped * baseOpacity;
                    // 颜色基于 shade 与少量噪声用于变色
                    float colorNoise = Mathf.Lerp(0.95f, 1.05f, FractalBrownianMotion(xCoord * 0.5f, yCoord * 0.5f, 2, 0.5f));
                    float finalShade = shade * colorNoise;
                    Color cloudColor = alpha > 0.0005f ? new Color(finalShade, finalShade, finalShade, alpha) : new Color(0f, 0f, 0f, 0f);
                    // 如果开启边缘填充，则对边缘区域做平滑渐隐（从 edgeFillColor -> cloudColor）以隐藏拼接
                    int marginPx = Mathf.RoundToInt(edgeMargin * textureSize);
                    if (fillEdgeWithColor && marginPx > 0)
                    {
                        int nx = Mathf.Min(x, textureSize - 1 - x);
                        int ny = Mathf.Min(y, textureSize - 1 - y);
                        int dist = Mathf.Min(nx, ny);
                        float t = Mathf.Clamp01((float)dist / (float)marginPx); // 0 at edge, 1 at inside
                        float fade = Mathf.SmoothStep(0f, 1f, t);
                        // fade==0 -> edgeFillColor, fade==1 -> cloudColor
                        pixels[y * textureSize + x] = Color.Lerp(edgeFillColor, cloudColor, fade);
                    }
                    else
                    {
                        pixels[y * textureSize + x] = cloudColor;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Debug.Log("云朵纹理生成成功");
            return texture;
        }
        catch (System.Exception e)
        {
            Debug.LogError("生成云朵纹理时出错: " + e.Message);
            return null;
        }
    }
    
    float FractalBrownianMotion(float x, float y, int octaves, float persistence)
    {
        float total = 0f;
        float frequency = 1f;
        float amplitude = 1f;
        float maxValue = 0f;
        
        for (int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }
        
        return total / maxValue;
    }

    // Tileable Perlin: 使用四个偏移采样并双线性插值来保证 period 区间内的无缝性
    // x,y: 任意坐标； period: 在相同坐标尺度下的重复周期长度
    float TileablePerlin(float x, float y, float period)
    {
        if (period <= 0.0001f) return Mathf.PerlinNoise(x, y);

        float fx = (x / period) - Mathf.Floor(x / period);
        float fy = (y / period) - Mathf.Floor(y / period);

        float v00 = Mathf.PerlinNoise(x, y);
        float v10 = Mathf.PerlinNoise(x + period, y);
        float v01 = Mathf.PerlinNoise(x, y + period);
        float v11 = Mathf.PerlinNoise(x + period, y + period);

        float i1 = Mathf.Lerp(v00, v10, fx);
        float i2 = Mathf.Lerp(v01, v11, fx);
        return Mathf.Lerp(i1, i2, fy);
    }

    // Tileable FBM: 结合多个频率的 TileablePerlin，保证在给定 period 下无缝
    float TileableFBM(float x, float y, int octaves, float persistence, float basePeriod)
    {
        float total = 0f;
        float frequency = 1f;
        float amplitude = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float period = basePeriod / frequency; // period scales inversely with frequency
            total += TileablePerlin(x * frequency, y * frequency, period) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }

        return total / maxValue;
    }
    
    void SaveTextureToFile(Texture2D texture, string fileName)
    {
        if (texture == null)
        {
            Debug.LogError("无法保存空纹理");
            return;
        }
        
        try
        {
            // 创建保存目录
            string folderPath = Path.Combine(Application.dataPath, saveFolder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            
            // 保存纹理
            string fullPath = Path.Combine(folderPath, fileName);
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);
            
            Debug.Log("纹理已保存: " + fullPath);
            
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            #endif
        }
        catch (System.Exception e)
        {
            Debug.LogError("保存纹理时出错: " + e.Message);
        }
    }
    
    void ApplyToCurrentScene()
    {
        #if UNITY_EDITOR
        // 在编辑器中尝试把刚生成的纹理赋值回场景中的 DayNightController 与当前天空盒材质
        string basePath = "Assets/" + saveFolder + "/";
        string starPath = basePath + "StarTexture.png";
        string nearPath = basePath + "CloudTexture_Near.png";
        string farPath = basePath + "CloudTexture_Far.png";

        AssetDatabase.Refresh();

        Texture2D loadedStar = AssetDatabase.LoadAssetAtPath<Texture2D>(starPath);
        Texture2D loadedNear = AssetDatabase.LoadAssetAtPath<Texture2D>(nearPath);
        Texture2D loadedFar = AssetDatabase.LoadAssetAtPath<Texture2D>(farPath);

        DayNightController controller = FindObjectOfType<DayNightController>();

        if (controller != null)
        {
            Debug.Log("将生成的纹理应用到场景的 DayNightController（编辑器模式）...");
            if (loadedStar != null) controller.nightSkyTexture = loadedStar;
            if (loadedNear != null) controller.cloudTextureNear = loadedNear;
            if (loadedFar != null) controller.cloudTextureFar = loadedFar;

            // 若控制器持有天空盒材质，也同时写入材质
            if (controller.skyboxMaterial != null)
            {
                if (loadedStar != null) controller.skyboxMaterial.SetTexture("_NightTex", loadedStar);
                if (loadedNear != null) controller.skyboxMaterial.SetTexture("_CloudTexNear", loadedNear);
                if (loadedFar != null) controller.skyboxMaterial.SetTexture("_CloudTexFar", loadedFar);
                RenderSettings.skybox = controller.skyboxMaterial;
            }

            EditorUtility.SetDirty(controller);
            if (controller.skyboxMaterial != null) EditorUtility.SetDirty(controller.skyboxMaterial);

            Debug.Log("纹理已应用（若找到了 DayNightController）。请在 Inspector 中检查并手动微调参数以达到最佳视觉效果。");
            return;
        }

        // 若找不到 DayNightController，尝试直接写入当前 RenderSettings.skybox
        if (RenderSettings.skybox != null)
        {
            Debug.Log("未找到 DayNightController；将纹理直接应用到当前的 RenderSettings.skybox...");
            if (loadedStar != null) RenderSettings.skybox.SetTexture("_NightTex", loadedStar);
            if (loadedNear != null) RenderSettings.skybox.SetTexture("_CloudTexNear", loadedNear);
            if (loadedFar != null) RenderSettings.skybox.SetTexture("_CloudTexFar", loadedFar);
            EditorUtility.SetDirty(RenderSettings.skybox);
            Debug.Log("纹理已应用到当前天空盒材质。请在 Inspector 中检查并手动微调参数以达到最佳视觉效果。");
            return;
        }

        Debug.LogWarning("未找到 DayNightController 且当前 RenderSettings.skybox 为空，无法自动应用生成的纹理。请手动将生成的纹理分配到场景的天空盒或 DayNightController 中。");
        #endif
    }

    // 生成白天全透明纹理（白色但 alpha=0），并返回 Texture2D
    public Texture2D GenerateDayTransparentTexture()
    {
        try
        {
            Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[textureSize * textureSize];
            // 填充白色但 alpha=0
            Color fill = new Color(1f, 1f, 1f, 0f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;

            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
        catch (System.Exception e)
        {
            Debug.LogError("生成白天透明纹理失败: " + e.Message);
            return null;
        }
    }
    
    // 公共方法供其他脚本调用
    public Texture2D GetStarTexture() { return starTexture; }
    public Texture2D GetCloudTexture() { return cloudTexture; }
}