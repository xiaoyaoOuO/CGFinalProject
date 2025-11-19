using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

[ExecuteInEditMode]
public class RandomSpawner : MonoBehaviour
{
    public GameObject prefab;
    public GameObject GrassRoot;
    public int totalCount = 100;
    public int currentCount = 0;
    public float spawnRadius = 10f; // 圆形区域半径
    public float minDistance = 1f; // 物体间最小距离（避免重叠）
    private List<Vector3> spawnedPositions = new List<Vector3>();
    private List<GameObject> spawnedObjects = new List<GameObject>();

    //编辑时点击按钮生成物体
    [ContextMenu("Spawn Objects")]
    void SpawnObjects()
    {
        int attempts = 0; // 防止无限循环
        while (currentCount < totalCount && attempts < 1000)
        {
            // 在圆形区域内生成随机位置
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPos = new Vector3(
                randomCircle.x,
                0,
                randomCircle.y
            );
            randomPos += transform.position;

            // 检查与已生成物体的距离
            bool isOverlap = false;
            foreach (var pos in spawnedPositions)
            {
                if (Vector3.Distance(randomPos, pos) < minDistance)
                {
                    isOverlap = true;
                    break;
                }
            }

            if (!isOverlap)
            {
                GameObject InstantiatedObject = Instantiate(prefab, randomPos, prefab.transform.rotation);
                InstantiatedObject.transform.parent = GrassRoot.transform;
                spawnedPositions.Add(randomPos);
                spawnedObjects.Add(InstantiatedObject);
                currentCount++;
            }
            attempts++;
        }
        totalCount = spawnedPositions.Count;
        currentCount = 0;
    }

    //编辑时点击按钮清除已生成物体
    [ContextMenu("Clear All")]
    void ClearAll()
    {
        foreach (var obj in spawnedObjects)
        {
            GameObject.DestroyImmediate(obj);
        }
        spawnedObjects.Clear();
        spawnedPositions.Clear();
        currentCount = 0;
    }

    // 在Scene视图中绘制圆形区域Gizmo
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}