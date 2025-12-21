using UnityEngine;

/// <summary>
/// Generates a mesh that follows a Terrain surface, intended as a render overlay.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class TerrainSnowOverlayMesh : MonoBehaviour
{
    public Terrain terrain;

    [Range(8, 512)]
    public int resolution = 128;

    public float heightOffset = 0.02f;

    public Mesh _mesh;

    void OnEnable()
    {
        Rebuild();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Rebuild();
    }

    public void Rebuild()
    {
        if (terrain == null || terrain.terrainData == null)
            return;

        var td = terrain.terrainData;
        var size = td.size;
        var origin = terrain.transform.position;

        int vertsPerSide = Mathf.Max(2, resolution + 1);
        int vertCount = vertsPerSide * vertsPerSide;

        var vertices = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var normals = new Vector3[vertCount];

        for (int z = 0; z < vertsPerSide; z++)
        {
            float v = z / (float)(vertsPerSide - 1);
            for (int x = 0; x < vertsPerSide; x++)
            {
                float u = x / (float)(vertsPerSide - 1);
                int i = z * vertsPerSide + x;

                float wx = origin.x + u * size.x;
                float wz = origin.z + v * size.z;
                float wy = origin.y + terrain.SampleHeight(new Vector3(wx, 0f, wz)) + heightOffset;

                vertices[i] = new Vector3(wx, wy, wz);
                uvs[i] = new Vector2(u, v);
                normals[i] = Vector3.up;
            }
        }

        int quadCount = resolution * resolution;
        var triangles = new int[quadCount * 6];
        int t = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i0 = z * vertsPerSide + x;
                int i1 = i0 + 1;
                int i2 = i0 + vertsPerSide;
                int i3 = i2 + 1;

                triangles[t++] = i0;
                triangles[t++] = i2;
                triangles[t++] = i1;

                triangles[t++] = i1;
                triangles[t++] = i2;
                triangles[t++] = i3;
            }
        }

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "TerrainSnowOverlayMesh" };
            _mesh.indexFormat = vertCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;

        // Keep this overlay object aligned to world; vertices are already in world space.
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
}
