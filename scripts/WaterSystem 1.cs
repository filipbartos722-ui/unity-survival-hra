using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaterSystem : MonoBehaviour
{
    public SmoothTerrain terrain;
    readonly Dictionary<Vector2Int,Dictionary<Vector3Int,GameObject>> poured = new Dictionary<Vector2Int,Dictionary<Vector3Int,GameObject>>();
    public float cellSize { get { return terrain==null ? 1f : Mathf.Max(.01f,terrain.cellSize*Mathf.Abs(terrain.transform.lossyScale.x)); } }
    void Awake() { if(terrain==null) terrain=GetComponent<SmoothTerrain>(); }
    internal void Bind(SmoothTerrain source) { terrain=source; }
    public Vector3Int WorldToCell(Vector3 p) { return Vector3Int.FloorToInt(terrain!=null?terrain.GridPoint(p):p); }
    bool Poured(Vector3Int p)
    { return poured.TryGetValue(terrain.Column((Vector3)p+Vector3.one*.5f),out var bucket) && bucket.ContainsKey(p); }
    bool Natural(Vector3 local)
    {
        return terrain.generateWater && local.y<=terrain.Sea && local.y>terrain.Top(local);
    }
    public bool ContainsWorld(Vector3 world)
    {
        if(terrain==null) return false;
        var p=terrain.GridPoint(world);
        return terrain.Loaded(terrain.Column(p)) && (Poured(Vector3Int.FloorToInt(p)) || Natural(p));
    }
    public bool HasWater(Vector3Int p)
    {
        if(terrain==null) return false;
        Vector3 centre=(Vector3)p+Vector3.one*.5f;
        if(!terrain.Loaded(terrain.Column(centre))) return false;
        if(Poured(p)) return true;
        float bed=terrain.Top(centre);
        return terrain.generateWater && bed<terrain.Sea && p.y<terrain.Sea && p.y+1f>bed;
    }
    // Natural lakes and rivers are renewable. Poured cells have finite volume.
    public bool TakeWater(Vector3Int p)
    {
        if(!HasWater(p)) return false;
        var key=terrain.Column((Vector3)p+Vector3.one*.5f);
        if(poured.TryGetValue(key,out var cells) && cells.TryGetValue(p,out var go))
        {
            if(go!=null) Destroy(go); cells.Remove(p);
            if(cells.Count==0) poured.Remove(key);
        }
        return true;
    }
    internal bool TakeAt(Vector3 world)
    {
        if(!ContainsWorld(world)) return false;
        var cell=WorldToCell(world);
        return Poured(cell)?TakeWater(cell):true;
    }
    public bool AddWater(Vector3Int p)
    {
        if(terrain==null || HasWater(p)) return false;
        Vector3 centre=(Vector3)p+Vector3.one*.5f;
        var key=terrain.Column(centre);
        if(!terrain.Loaded(key) || terrain.DensityAtWorld(terrain.transform.TransformPoint(centre*terrain.cellSize))<=0f) return false;
        if(!poured.TryGetValue(key,out var cells)) poured.Add(key,cells=new Dictionary<Vector3Int,GameObject>());
        cells.Add(p,terrain.PouredWater(key,p)); return true;
    }
    internal void Show(Vector2Int key,Transform column)
    {
        if(!poured.TryGetValue(key,out var cells)) return;
        var keys=new List<Vector3Int>(cells.Keys);
        foreach(var p in keys) cells[p]=terrain.PouredWater(key,p);
    }
}

public partial class SmoothTerrain
{
    internal GameObject PouredWater(Vector2Int key,Vector3Int cell)
    {
        var go=AddMeshChild(live[key].root.transform,"Vylitá voda",art.Get(SurvivalHabitatModels.Form.WaterCell),liquidMaterial);
        go.transform.localPosition=new Vector3(cell.x-key.x*Size,cell.y,cell.z-key.y*Size);
        return go;
    }
}
