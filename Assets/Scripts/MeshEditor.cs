using UnityEngine;
using UnityEditor;
using System.IO;

public class CircleMeshCreator : EditorWindow
{
    // 圆形参数
    private float radius = 1f;
    private int segments = 32; // 分段数（越多越圆）
    private string savePath = "Assets/CircleMeshes/"; // 保存路径

    // 添加编辑器窗口菜单
    [MenuItem("Tools/圆形Mesh生成工具")]
    public static void ShowWindow()
    {
        GetWindow<CircleMeshCreator>("圆形Mesh生成");
    }

    private void OnGUI()
    {
        // 绘制参数面板
        GUILayout.Label("圆形参数设置", EditorStyles.boldLabel);
        radius = EditorGUILayout.FloatField("半径", radius);
        segments = EditorGUILayout.IntField("分段数", segments);
        savePath = EditorGUILayout.TextField("保存路径", savePath);

        // 生成并保存按钮
        if (GUILayout.Button("生成并保存圆形Mesh"))
        {
            CreateAndSaveCircleMesh();
        }
    }

    // 生成圆形Mesh并保存
    private void CreateAndSaveCircleMesh()
    {
        // 1. 验证参数
        if (radius <= 0 || segments < 3)
        {
            EditorUtility.DisplayDialog("错误", "半径需大于0，分段数至少为3！", "确定");
            return;
        }

        // 2. 创建圆形Mesh
        Mesh circleMesh = GenerateCircleMesh(radius, segments);
        circleMesh.name = $"Circle_R{radius}_S{segments}";

        // 3. 确保保存路径存在
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        // 4. 保存Mesh为.asset文件
        string assetPath = $"{savePath}{circleMesh.name}.asset";
        AssetDatabase.CreateAsset(circleMesh, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 提示成功
        EditorUtility.DisplayDialog("成功", $"圆形Mesh已保存至：{assetPath}", "确定");
    }

    // 生成平放的圆形Mesh数据（XY平面，圆心在原点）
    private Mesh GenerateCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CircleMesh";

        // 顶点数组：圆心 + 圆周顶点
        Vector3[] vertices = new Vector3[segments + 1];
        vertices[0] = Vector3.zero; // 圆心顶点

        // 计算圆周顶点（XY平面平放）
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            vertices[i + 1] = new Vector3(x, y, 0); // Z轴为0，平放
        }

        // 三角形索引：每个三角形由圆心+两个相邻圆周顶点组成
        int[] triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            int triIndex = i * 3;
            triangles[triIndex] = 0; // 圆心
            triangles[triIndex + 1] = i + 1; // 当前圆周顶点
            triangles[triIndex + 2] = (i + 1) % segments + 1; // 下一个圆周顶点（闭合）
        }

        // UV坐标（可选，用于贴图）
        Vector2[] uv = new Vector2[vertices.Length];
        uv[0] = new Vector2(0.5f, 0.5f); // 圆心UV居中
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float u = (Mathf.Cos(angle) + 1) * 0.5f; // 转换为0-1范围
            float v = (Mathf.Sin(angle) + 1) * 0.5f;
            uv[i + 1] = new Vector2(u, v);
        }

        // 赋值Mesh数据
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals(); // 自动计算法线（Z轴方向）
        mesh.RecalculateBounds();

        return mesh;
    }
}