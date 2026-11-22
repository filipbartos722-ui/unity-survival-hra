using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Plains", menuName = "World Generation/Biome")]
public class BiomeData : ScriptableObject
{
    public string biomeName = "Plains";
    public bool enabled = true;
    [Header("Terrain")]
    public float heightOffset = 0f;
    [Min(0f)] public float heightMultiplier = 0.55f;
    public Color surfaceColor = new Color(.38f, .68f, .23f);

    [Serializable]
    public class SpawnEntry
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float spawnChance = .35f;
        [Min(0)] public int attemptsPerChunk = 5;
        public Vector2 scaleRange = Vector2.one;
        public Vector2 heightOffset = Vector2.zero;
        [Range(0f, 1f)] public float maxSlope = .35f;
        [Min(0f)] public float minSpacing = 0f;
        public bool alignToGround;
    }
    [Header("Surface Objects")]
    public List<SpawnEntry> surfaceObjects = new List<SpawnEntry>();
    [Header("Structures")]
    public List<SpawnEntry> structures = new List<SpawnEntry>();

    [Serializable]
    public class CaveSettings
    {
        public bool allowCaves = true;
        public List<SpawnEntry> orePrefabs = new List<SpawnEntry>();
        public List<SpawnEntry> structures = new List<SpawnEntry>();
        public List<SpawnEntry> mobs = new List<SpawnEntry>();
    }
    [Header("Caves - content lists reserved for next implementation")]
    public CaveSettings caves = new CaveSettings();
    [Header("Mobs - spawn logic reserved for next implementation")]
    public List<SpawnEntry> mobs = new List<SpawnEntry>();

    [Serializable]
    public class WaterSettings
    {
        public bool generateWater = true;
        public float waterLevel = 34f;
        public Material material;
        public Color color = new Color(.12f, .42f, .72f, .68f);
    }
    [Header("Water")]
    public WaterSettings water = new WaterSettings();
}
