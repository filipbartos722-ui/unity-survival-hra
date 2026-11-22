using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class SurvivalMeshData
{
    internal readonly List<Vector3> vertices = new List<Vector3>();
    internal readonly List<Vector3> normals = new List<Vector3>();
    internal readonly List<Vector2> uv = new List<Vector2>();
    internal readonly List<Color32> colors = new List<Color32>();
    internal readonly List<int> triangles = new List<int>();

    // Only called by the main thread.
    internal Mesh Upload(string name)
    {
        if (triangles.Count == 0) return null;
        Mesh mesh = new Mesh { name = name };
        if (vertices.Count > 65534) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        if (normals.Count == vertices.Count) mesh.SetNormals(normals);
        if (uv.Count == vertices.Count) mesh.SetUVs(0, uv);
        if (colors.Count == vertices.Count) mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        return mesh;
    }
}

internal struct SurvivalSite
{
    internal Vector3 a, b, c, normal;
    internal float depth, climate;
    internal Vector3 Centre { get { return (a + b + c) / 3f; } }
    internal Vector3 Point(System.Random random)
    {
        float u = (float)random.NextDouble(), v = (float)random.NextDouble();
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return a + u * (b - a) + v * (c - a);
    }
}

internal sealed class SurvivalSites
{
    internal readonly List<SurvivalSite> surface = new List<SurvivalSite>();
    internal readonly List<SurvivalSite> floors = new List<SurvivalSite>();
    internal readonly List<SurvivalSite> walls = new List<SurvivalSite>();
    internal readonly float[] bed;
    readonly int side;
    internal SurvivalSites(int size) { side = size + 1; bed = new float[side * side]; }

    // Same diagonal as shoreline clipping; fractional water levels work as well.
    internal float Top(float x, float z)
    {
        x = Mathf.Clamp(x, 0f, side - 1f); z = Mathf.Clamp(z, 0f, side - 1f);
        int ix = Mathf.Min(side - 2, Mathf.FloorToInt(x)), iz = Mathf.Min(side - 2, Mathf.FloorToInt(z));
        float u = x - ix, v = z - iz;
        int i = ix * side + iz;
        float a = bed[i], b = bed[i + side], c = bed[i + 1], d = bed[i + side + 1];
        return u + v <= 1f ? a + u * (b - a) + v * (c - a)
                           : d + (1f - u) * (c - d) + (1f - v) * (b - d);
    }
}

// Surface Nets. Every oriented lattice edge has one owner. Every column has a
// negative-side halo, so its boundary vertices and normals use identical samples.
internal sealed class SurvivalColumnBuilder
{
    internal sealed class Buffers
    {
        internal readonly float[] density;
        internal readonly int[] cellVertices;
        internal readonly SurvivalWorldField.Profile[] profiles;
        internal Buffers(int side,int height)
        {
            density=new float[side*(height+2)*side]; cellVertices=new int[density.Length];
            profiles=new SurvivalWorldField.Profile[side*side];
        }
    }
    internal readonly Buffers buffers;
    internal readonly SurvivalMeshData ground = new SurvivalMeshData(), water = new SurvivalMeshData();
    internal readonly SurvivalSites sites;
    internal int caveColumns, entranceColumns;
    readonly SurvivalWorldField field;
    readonly SurvivalBrush[] edits;
    readonly int size, height, side, sx, ox, oz;
    readonly float[] density;
    readonly int[] ids, offsets;
    readonly SurvivalWorldField.Profile[] profiles;
    readonly float[] corner = new float[8];
    readonly Vector3[] shore = new Vector3[4];
    static readonly int[] edgeA = { 0,2,4,6, 0,1,4,5, 0,1,2,3 };
    static readonly int[] edgeB = { 1,3,5,7, 2,3,6,7, 4,5,6,7 };
    int phase, row;

    internal SurvivalColumnBuilder(SurvivalWorldField field, Vector2Int key, SurvivalBrush[] edits, Buffers reuse=null)
    {
        this.field = field; this.edits = edits;
        size = field.size; height = field.height; side = size + 2; sx = (height + 2) * side;
        ox = key.x * size; oz = key.y * size;
        buffers = reuse ?? new Buffers(side,height);
        density = buffers.density; ids = buffers.cellVertices;
        offsets = new[] { 0, sx, side, sx + side, 1, sx + 1, side + 1, sx + side + 1 };
        profiles = buffers.profiles;
        sites = new SurvivalSites(size);
    }

    // One X slice per step. Also the single-threaded/WebGL path.
    internal bool Step()
    {
        switch (phase)
        {
            case 0: SampleRow(row); if (++row == side) { row = 0; phase++; } break;
            case 1: VertexRow(row); if (++row == size + 1) { row = 0; phase++; } break;
            case 2: FaceRow(row); if (++row == size) { phase++; } break;
        }
        return phase == 3;
    }

    void SampleRow(int x)
    {
        for (int z = 0; z < side; z++)
        {
            int gx = ox + x - 1, gz = oz + z - 1;
            var p = field.Sample(gx, gz);
            profiles[x * side + z] = p;
            if (p.entrance) entranceColumns = 1;
            if (p.tunnelVertical > 0f || p.crossVertical > 0f) caveColumns = 1;
            for (int y = 0, i = x * sx + z; y <= height + 1; y++, i += side)
                density[i] = field.Density(p, y - 1);
            // Replay only the bounded spans touched by each edit, in the order of play.
            for (int b = 0; b < edits.Length; b++)
            {
                var edit = edits[b]; float pad = edit.radius + 2f;
                if (gx < Mathf.Min(edit.start.x, edit.end.x)-pad || gx > Mathf.Max(edit.start.x, edit.end.x)+pad ||
                    gz < Mathf.Min(edit.start.z, edit.end.z)-pad || gz > Mathf.Max(edit.start.z, edit.end.z)+pad) continue;
                int low = Mathf.Max(field.bedrock+1, Mathf.FloorToInt(Mathf.Min(edit.start.y, edit.end.y)-pad));
                int high = Mathf.Min(height-1, Mathf.CeilToInt(Mathf.Max(edit.start.y, edit.end.y)+pad));
                for (int gy=low, i=x*sx+(low+1)*side+z; gy<=high; gy++,i+=side)
                    density[i]=edit.Apply(new Vector3(gx,gy,gz),density[i]);
            }
            if (x > 0 && z > 0)
            {
                int i = x*sx + (height+1)*side + z;
                while (density[i-side]>=0f) i-=side;
                int gy=(i-x*sx-z)/side-1;
                sites.bed[(x-1)*(size+1)+z-1]=gy-1+density[i-side]/(density[i-side]-density[i]);
            }
        }
    }

    void VertexRow(int x)
    {
        for (int z = 0; z <= size; z++)
        for (int y = 0, i = x * sx + z; y <= height; y++, i += side)
        {
            bool sign = density[i] < 0f;
            int k = 1;
            while (k < 8 && (density[i + offsets[k]] < 0f) == sign) k++;
            if (k == 8) { ids[i]=0; continue; }
            for (k = 0; k < 8; k++) corner[k] = density[i + offsets[k]];
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int e = 0; e < 12; e++)
            {
                int a = edgeA[e], b = edgeB[e];
                if ((corner[a] < 0f) == (corner[b] < 0f)) continue;
                float t = corner[a] / (corner[a] - corner[b]);
                sum.x += (a & 1) + (e < 4 ? t : 0f);
                sum.y += ((a >> 1) & 1) + (e >= 4 && e < 8 ? t : 0f);
                sum.z += ((a >> 2) & 1) + (e >= 8 ? t : 0f);
                count++;
            }
            Vector3 off = sum / count;
            // Binary fractions remain identical on either side of translated chunk origins.
            off.x = Mathf.Round(off.x * 4096f) / 4096f;
            off.y = Mathf.Round(off.y * 4096f) / 4096f;
            off.z = Mathf.Round(off.z * 4096f) / 4096f;
            Vector3 p = new Vector3(x - 1, y - 1, z - 1) + off;
            Vector3 n = new Vector3(
                corner[1]+corner[3]+corner[5]+corner[7]-corner[0]-corner[2]-corner[4]-corner[6],
                corner[2]+corner[3]+corner[6]+corner[7]-corner[0]-corner[1]-corner[4]-corner[5],
                corner[4]+corner[5]+corner[6]+corner[7]-corner[0]-corner[1]-corner[2]-corner[3]);
            n = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
            int h = x * side + z;
            float surface = Mathf.Lerp(Mathf.Lerp(profiles[h].surface, profiles[h+side].surface, off.x),
                                       Mathf.Lerp(profiles[h+1].surface, profiles[h+side+1].surface, off.x), off.z);
            float climate = Mathf.Lerp(Mathf.Lerp(profiles[h].climate, profiles[h+side].climate, off.x),
                                       Mathf.Lerp(profiles[h+1].climate, profiles[h+side+1].climate, off.x), off.z);
            Vector2 uv = field.PaletteUV(climate, surface - p.y, n.y, p.y);
            ids[i] = ground.vertices.Count + 1;
            ground.vertices.Add(p); ground.normals.Add(n); ground.uv.Add(uv);
            ground.colors.Add(field.PaletteColor(uv.x, uv.y));
        }
    }

    void FaceRow(int x)
    {
        for (int z = 0; z < size; z++)
        {
            int h = (x + 1) * side + z + 1;
            for (int y = 0, i = (x + 1) * sx + side + z + 1; y < height; y++, i += side)
            {
                bool inside = density[i] < 0f;
                if (inside != (density[i + sx] < 0f)) Quad(i, side, 1, inside, profiles[h]);
                if (inside != (density[i + side] < 0f)) Quad(i, 1, sx, inside, profiles[h]);
                if (inside != (density[i + 1] < 0f)) Quad(i, sx, side, inside, profiles[h]);
            }
            if (field.water) WaterCell(x, z);
        }
    }

    void Quad(int i, int u, int v, bool inside, SurvivalWorldField.Profile p)
    {
        int a = ids[i-u-v]-1, b = ids[i-v]-1, c = ids[i]-1, d = ids[i-u]-1;
        if (!inside) { int swap = b; b = d; d = swap; }
        ground.triangles.Add(a); ground.triangles.Add(b); ground.triangles.Add(c);
        ground.triangles.Add(a); ground.triangles.Add(c); ground.triangles.Add(d);
        var face = new SurvivalSite { a = ground.vertices[a], b = ground.vertices[b], c = ground.vertices[c] };
        Vector3 cross = Vector3.Cross(face.b - face.a, face.c - face.a);
        if (cross.sqrMagnitude < 1e-9f) return;
        face.normal = cross.normalized;
        Vector3 centre = face.Centre;
        face.depth = p.surface - centre.y; face.climate = p.climate;
        if (face.depth < 2.2f && face.normal.y > .28f) sites.surface.Add(face);
        else if (face.depth >= 2f && centre.y > field.bedrock)
        {
            if (face.normal.y > .58f) sites.floors.Add(face);
            if (Mathf.Abs(face.normal.y) < .85f) sites.walls.Add(face);
        }
    }

    void WaterCell(int x, int z)
    {
        int n = size + 1, i = x * n + z;
        Vector3 a = new Vector3(x, sites.bed[i], z), b = new Vector3(x + 1, sites.bed[i+n], z);
        Vector3 c = new Vector3(x, sites.bed[i+1], z+1), d = new Vector3(x+1, sites.bed[i+n+1], z+1);
        ClipWater(a, c, b);
        ClipWater(b, c, d);
    }

    void ClipWater(Vector3 a, Vector3 b, Vector3 c)
    {
        if (a.y >= field.sea && b.y >= field.sea && c.y >= field.sea) return;
        int count = 0;
        Vector3 prev = c;
        for (int i = 0; i < 3; i++)
        {
            Vector3 next = i == 0 ? a : i == 1 ? b : c;
            bool inA = prev.y < field.sea, inB = next.y < field.sea;
            if (inA != inB)
                shore[count++] = Vector3.Lerp(prev, next, (field.sea - prev.y) / (next.y - prev.y));
            if (inB) shore[count++] = next;
            prev = next;
        }
        int first = water.vertices.Count;
        for (int j = 0; j < count; j++)
        {
            Vector3 v = shore[j]; v.y = field.sea;
            water.vertices.Add(v); water.normals.Add(Vector3.up);
            water.uv.Add(new Vector2(v.x + ox, v.z + oz));
        }
        for (int j = 1; j + 1 < count; j++)
        {
            water.triangles.Add(first); water.triangles.Add(first + j); water.triangles.Add(first + j + 1);
        }
    }
}
