using System;
using UnityEngine;

// A numeric snapshot. Jobs only read this object; scene objects stay on the main thread.
internal sealed class SurvivalWorldField
{
    internal readonly int size, height, bedrock, seed;
    internal readonly float sea, grassDepth, dirtDepth;
    internal readonly bool water;
    readonly bool caves;
    readonly int octaves;
    readonly float low, datum, relief, scale, climateSize, climateBlend, warp;
    readonly float riverSpacing, riverWidth, riverDepth, mountainLift, fineRelief;
    readonly float snowLine, snowTransition;
    readonly float caveSpacing, bendLength, radiusLow, radiusHigh, portalChance, chamberGain;
    readonly Color[] land;
    readonly Color dirt, rock, frost;
    const float Band = 2f;

    internal SurvivalWorldField(SmoothTerrain s)
    {
        size = Mathf.Clamp(s.chunkSize, 8, 40);
        height = size * Mathf.Clamp(s.chunksY, 2, 12);
        bedrock = Mathf.Clamp(s.bedrockThickness, 1, height - 9);
        seed = s.seed;
        sea = Mathf.Clamp(s.waterLevel, 1f, height - 2f);
        water = s.generateWater;
        low = Mathf.Clamp(s.minimumTerrainHeight, bedrock + 2, height - 5);
        datum = s.baseHeight; relief = Mathf.Max(0f, s.hillHeight);
        scale = Mathf.Max(.0001f, s.noiseScale); octaves = Mathf.Clamp(s.octaves, 1, 7);
        climateSize = Mathf.Max(32f, s.biomeSize); climateBlend = Mathf.Clamp01(s.biomeBlend * 2f);
        warp = Mathf.Clamp(s.biomeWarp, 0f, .45f);
        riverSpacing = Mathf.Max(24f, s.riverSpacing);
        riverWidth = Mathf.Clamp(s.riverWidth, 0f, riverSpacing * .42f);
        riverDepth = Mathf.Max(0f, s.riverDepth);
        mountainLift = Mathf.Max(0f, s.mountainRelief); fineRelief = Mathf.Max(0f, s.fineRelief);
        snowLine = Mathf.Clamp01(s.snowLine); snowTransition = Mathf.Max(.01f,s.snowTransition);
        caves = s.generateCaves && s.cavesPerRegion > 0;
        caveSpacing = Mathf.Max(14f, s.regionSize / Mathf.Sqrt(Mathf.Max(1, s.cavesPerRegion)));
        bendLength = Mathf.Max(16f, .5f * (s.caveLength.x + s.caveLength.y));
        radiusLow = Mathf.Clamp(Mathf.Min(s.caveRadius.x, s.caveRadius.y), .9f, caveSpacing * .3f);
        radiusHigh = Mathf.Clamp(Mathf.Max(s.caveRadius.x, s.caveRadius.y), radiusLow, caveSpacing * .4f);
        portalChance = Mathf.Clamp01(s.entranceChance); chamberGain = Mathf.Max(0f, s.chamberExpansion);
        grassDepth = Mathf.Max(.1f, s.grassDepth); dirtDepth = Mathf.Max(.1f, s.dirtDepth);
        land = new[] { s.desertSurface, s.plainsSurface, s.swampSurface, s.taigaSurface, s.mountainSurface };
        dirt = s.dirtColor; rock = s.stoneColor; frost = s.snowColor;
    }

    internal struct Profile
    {
        public float surface, climate, river;
        public float tunnelY, crossY, tunnelVertical, crossVertical;
        public bool entrance;
    }

    internal static uint Hash(int seed, int x, int z)
    {
        unchecked
        {
            uint v = (uint)seed ^ (uint)x * 0x9e3779b1u ^ (uint)z * 0x85ebca77u;
            v ^= v >> 16; v *= 0x7feb352du;
            v ^= v >> 15; v *= 0x846ca68bu;
            return v ^ (v >> 16);
        }
    }

    static float Unit(uint v) { return (v >> 8) * (1f / 16777216f); }
    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    float Noise(float x, float z, int salt)
    {
        int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
        float tx = Smooth(x - ix), tz = Smooth(z - iz);
        int key = seed ^ salt;
        float a = Mathf.Lerp(Unit(Hash(key, ix, iz)), Unit(Hash(key, ix + 1, iz)), tx);
        float b = Mathf.Lerp(Unit(Hash(key, ix, iz + 1)), Unit(Hash(key, ix + 1, iz + 1)), tx);
        return 2f * Mathf.Lerp(a, b, tz) - 1f;
    }

    float Fbm(float x, float z)
    {
        float sum = 0f, weight = 1f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += weight * Noise(x, z, 3271 + 137 * i);
            float nextX = 1.74f * x + 1.02f * z + 17.1f;
            z = 1.74f * z - 1.02f * x - 8.3f; x = nextX;
            total += weight; weight *= .48f;
        }
        return sum / total;
    }

    // Smoothly approaches both bounds. A low basin or a high massif keeps its relief.
    static float Bounded(float value, float floor, float ceiling)
    {
        float knee = Mathf.Min(4f, (ceiling - floor) * .25f);
        if (value < floor + knee) return floor + knee * knee / (floor + 2f * knee - value);
        if (value > ceiling - knee) return ceiling - knee * knee / (value - ceiling + 2f * knee);
        return value;
    }

    internal Profile Surface(float x, float z)
    {
        float cx = x / climateSize, cz = z / climateSize;
        float wx = Noise(cx * 2f, cz * 2f, 73), wz = Noise(cx * 2f, cz * 2f, 137);
        float climate = Mathf.Clamp(2f + 3.1f * Noise(cx + wx * warp, cz + wz * warp, 405), 0f, 4f);
        int band = Mathf.FloorToInt(climate);
        float blend = Smooth(Mathf.InverseLerp(.5f - Mathf.Max(.005f, climateBlend),
                                               .5f + Mathf.Max(.005f, climateBlend), climate - band));
        float palette = Mathf.Min(4f, band + blend);
        float broad = Noise(x * scale * .23f, z * scale * .23f, 219);
        float hills = Fbm(x * scale + .63f * wx, z * scale + .63f * wz);
        float ridge = 1f - Mathf.Abs(Noise(x * scale * .67f + wx, z * scale * .67f + wz, 549));
        float alpine = Smooth((climate - 2.8f) / 1.2f);
        float wetland = Smooth(1f - Mathf.Abs(climate - 2f) * 2.2f);
        float terrain = datum + relief * (.86f * broad + 1.1f * hills)
                      + mountainLift * (.14f + .86f * alpine) * ridge * ridge
                      + fineRelief * Noise(x * scale * 4.3f, z * scale * 4.3f, 315);
        terrain = Mathf.Lerp(terrain, sea + 1.8f + hills * relief * .52f, wetland * .78f);
        float meander = z / riverSpacing + .52f * Noise(x / riverSpacing, z / riverSpacing * .38f, 901)
                         + .11f * Noise(x * 3f / riverSpacing, z / riverSpacing, 902);
        float distance = Mathf.Abs(meander - Mathf.Floor(meander + .5f)) * riverSpacing;
        float river = riverWidth > 0f ? 1f - Smooth(distance / riverWidth) : 0f;
        if (river > 0f) terrain = Mathf.Lerp(terrain, Mathf.Min(terrain, sea - riverDepth), river);
        float surface = Bounded(terrain, low, height - 1.5f);
        return new Profile { surface = surface, climate = palette, river = river };
    }

    internal Profile Sample(float x, float z)
    {
        Profile p = Surface(x, z);
        if (!caves) return p;
        float along = z / caveSpacing + .86f * Noise(x / bendLength, z / bendLength, 1103);
        float across = x / caveSpacing + .72f * Noise(x / bendLength, z / bendLength, 777);
        int row = Mathf.FloorToInt(along + .5f), lane = Mathf.FloorToInt(across + .5f);
        float da = Mathf.Abs(along - row) * caveSpacing, db = Mathf.Abs(across - lane) * caveSpacing;
        float chamber = Smooth(1f - Mathf.Min(1f, Mathf.Max(da, db) / (radiusHigh * 3f)));
        float radius = Mathf.Lerp(radiusLow, radiusHigh, .5f + .5f * Noise(x / 17f, z / 17f, 1889));
        radius += chamber * chamberGain;
        float r2 = radius * .88f;
        p.tunnelVertical = HalfOpening(da, radius);
        p.crossVertical = HalfOpening(db, r2);
        float floor = bedrock + 2f + radius;
        float roof = p.surface - radius - 4f;
        if (roof < floor) { p.tunnelVertical = 0f; p.crossVertical = 0f; return p; }
        p.tunnelY = Mathf.Lerp(floor, roof, .42f + .28f * Noise(x / bendLength, z / bendLength, 2741));
        p.crossY = Mathf.Lerp(floor, roof, .30f + .25f * Noise(x / bendLength, z / bendLength, 3823));
        if (p.tunnelVertical > 0f)
        {
            int section = Mathf.FloorToInt(x / bendLength);
            uint h = Hash(seed ^ 5119, section, row);
            if (Unit(h) < portalChance)
            {
                float centre = .32f + .34f * Unit(Hash(seed ^ 8127, section, row));
                float alongRamp = 1f - Smooth(Mathf.Abs(x / bendLength - section - centre) / .30f);
                p.tunnelY = Mathf.Lerp(p.tunnelY, p.surface + radius * .45f, alongRamp);
                p.entrance = p.tunnelY + p.tunnelVertical > p.surface;
            }
        }
        return p;
    }

    static float HalfOpening(float distance, float radius)
    {
        return distance < radius ? Mathf.Sqrt(radius * radius - distance * distance) * .88f : 0f;
    }

    internal float Density(Profile p, float y)
    {
        if (y <= bedrock) return -Band;
        if (y >= height) return Band;
        float d = y - p.surface;
        if (p.tunnelVertical > 0f) d = Mathf.Max(d, p.tunnelVertical - Mathf.Abs(y - p.tunnelY));
        if (p.crossVertical > 0f) d = Mathf.Max(d, p.crossVertical - Mathf.Abs(y - p.crossY));
        return Mathf.Clamp(d, -Band, Band);
    }

    internal static SmoothTerrain.BiomeType Biome(float climate)
    {
        switch (Mathf.RoundToInt(climate))
        {
            case 0: return SmoothTerrain.BiomeType.Desert;
            case 1: return SmoothTerrain.BiomeType.Plains;
            case 2: return SmoothTerrain.BiomeType.Swamp;
            case 3: return SmoothTerrain.BiomeType.Taiga;
            default: return SmoothTerrain.BiomeType.Mountains;
        }
    }

    internal Vector2 PaletteUV(float climate, float depth, float up, float y)
    {
        float layer;
        if (depth > grassDepth)
            layer = .25f * (1f - Smooth((depth - grassDepth) / dirtDepth));
        else if (depth > .25f)
            layer = .5f - .25f * Smooth((depth - .25f) / Mathf.Max(.1f, grassDepth - .25f));
        else
        {
            float cliff = Smooth((.84f - up) / .46f);
            float snow = Smooth((y - height * snowLine) / (height * snowTransition)) * Smooth((up - .50f) / .35f);
            layer = Mathf.Lerp(.5f + .25f * cliff, 1f, snow);
        }
        return new Vector2(Mathf.Clamp01(climate * .25f), layer);
    }

    internal Color PaletteColor(float u, float v)
    {
        float x = Mathf.Clamp01(u) * 4f;
        int band = Mathf.Min(3, Mathf.FloorToInt(x));
        Color top = Color.Lerp(land[band], land[band + 1], x - band);
        if (v < .25f) return Color.Lerp(rock, dirt, v * 4f);
        if (v < .50f) return Color.Lerp(dirt, top, (v - .25f) * 4f);
        if (v < .75f) return Color.Lerp(top, rock, (v - .50f) * 4f);
        return Color.Lerp(rock, frost, (v - .75f) * 4f);
    }
}

internal struct SurvivalBrush
{
    internal Vector3 start, end;
    internal float radius;
    internal bool dig;

    internal float Apply(Vector3 p, float density)
    {
        Vector3 axis = end - start;
        float lengthSq = axis.sqrMagnitude;
        float t = lengthSq < 1e-8f ? 0f : Mathf.Clamp01(Vector3.Dot(p - start, axis) / lengthSq);
        float signedDistance = Mathf.Clamp(radius - (p - (start + axis * t)).magnitude, -2f, 2f);
        return dig ? Mathf.Max(density, signedDistance) : Mathf.Min(density, -signedDistance);
    }
}
