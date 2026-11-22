using System.Collections.Generic;
using UnityEngine;

// Lightweight decoration spawner. Set grass prefab without colliders.
public class GrassSpawner : MonoBehaviour
{
    public bool enableGrass = true;
    public SmoothTerrain terrain;
    public GameObject grassPrefab;
    [Range(0, 32)] public int tuftsPerColumn = 8;
    public float minimumNormalY = .88f;
    public float scaleMin = .7f, scaleMax = 1.2f;
    readonly Dictionary<Vector2Int, GameObject> roots = new Dictionary<Vector2Int, GameObject>();
    public void SpawnColumn(Vector2Int column)
    {
        if (!enableGrass || terrain == null || grassPrefab == null || roots.ContainsKey(column)) return;
        GameObject root = new GameObject("Grass " + column.x + "," + column.y);
        root.transform.SetParent(transform, false);
        roots[column] = root;
        var rng = new System.Random(unchecked(terrain.seed ^ column.x * 912367 ^ column.y * 72839));
        for (int i = 0; i < tuftsPerColumn; i++)
        {
            float x = (column.x * terrain.chunkSize + (float)rng.NextDouble() * terrain.chunkSize) * terrain.cellSize;
            float z = (column.y * terrain.chunkSize + (float)rng.NextDouble() * terrain.chunkSize) * terrain.cellSize;
            Vector3 start = terrain.transform.position + new Vector3(x, terrain.chunksY * terrain.chunkSize * terrain.cellSize + 5, z);
            if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, terrain.chunksY * terrain.chunkSize * terrain.cellSize + 10)) continue;
            if (!hit.collider.transform.IsChildOf(terrain.transform) || hit.normal.y < minimumNormalY) continue;
            Vector3 local = terrain.transform.InverseTransformPoint(hit.point);
            SmoothTerrain.BiomeType biome = terrain.GetBiome(local.x / terrain.cellSize, local.z / terrain.cellSize);
            if (biome == SmoothTerrain.BiomeType.Desert || biome == SmoothTerrain.BiomeType.Mountains) continue;
            GameObject tuft = Instantiate(grassPrefab, hit.point, Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), root.transform);
            tuft.transform.localScale *= Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble());
        }
    }
    public void UnloadColumn(Vector2Int col)
    {
        if (roots.TryGetValue(col, out GameObject root)) { Destroy(root); roots.Remove(col); }
    }
}
