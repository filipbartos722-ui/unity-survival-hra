using System.Collections.Generic;
using UnityEngine;

// Attach to the same GameObject as SmoothTerrain. Uses deterministic positions.
// Current version places ore on exposed terrain; underground cave-wall placement is a later extension.
public class OreSpawner : MonoBehaviour
{
    [System.NonSerialized] public OreSpawner owner;
    [System.Serializable]
    public class OreEntry
    {
        public OreNode prefab;
        [Range(0, 20)] public int attemptsPerColumn = 2;
        public float minHeight = 0;
        public float maxHeight = 100;
    }
    public bool enableOres = true;
    public OreEntry[] ores = new OreEntry[4];
    public SmoothTerrain terrain;
    readonly Dictionary<Vector2Int, GameObject> roots = new Dictionary<Vector2Int, GameObject>();
    readonly HashSet<string> mined = new HashSet<string>();
    public void MarkMined(string key) { if (!string.IsNullOrEmpty(key)) mined.Add(key); }
    public void SpawnColumn(Vector2Int column)
    {
        if (!enableOres || roots.ContainsKey(column) || terrain == null) return;
        var root = new GameObject("Ores " + column.x + "," + column.y);
        root.transform.SetParent(transform, false);
        roots[column] = root;
        for (int e = 0; e < ores.Length; e++)
        {
            OreEntry entry = ores[e];
            if (entry == null || entry.prefab == null) continue;
            var rng = new System.Random(unchecked(terrain.seed * 16777619 ^ column.x * 73856093 ^ column.y * 19349663 ^ e * 83492791));
            for (int i = 0; i < Mathf.Clamp(entry.attemptsPerColumn, 0, 20); i++)
            {
                string key = column.x + ":" + column.y + ":" + e + ":" + i;
                if (mined.Contains(key)) continue;
                float x = (column.x * terrain.chunkSize + (float)rng.NextDouble() * terrain.chunkSize) * terrain.cellSize;
                float z = (column.y * terrain.chunkSize + (float)rng.NextDouble() * terrain.chunkSize) * terrain.cellSize;
                Vector3 start = terrain.transform.position + new Vector3(x, terrain.chunksY * terrain.chunkSize * terrain.cellSize + 10f, z);
                if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, terrain.chunksY * terrain.chunkSize * terrain.cellSize + 20f)) continue;
                if (!hit.collider.transform.IsChildOf(terrain.transform)) continue;
                float h = hit.point.y - terrain.transform.position.y;
                if (h < entry.minHeight || h > entry.maxHeight) continue;
                OreNode node = Instantiate(entry.prefab, hit.point + Vector3.up * .15f, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), root.transform);
                node.owner = this;
                node.persistentKey = key;
            }
        }
    }
    public void UnloadColumn(Vector2Int column)
    {
        if (roots.TryGetValue(column, out GameObject root)) { Destroy(root); roots.Remove(column); }
    }
}
