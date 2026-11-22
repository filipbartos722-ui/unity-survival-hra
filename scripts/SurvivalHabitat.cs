using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Form = SurvivalHabitatModels.Form;
using Rng = System.Random;

public partial class SmoothTerrain
{
    Rng RandomFor(Vector2Int k,int salt) { return new Rng(unchecked((int)SurvivalWorldField.Hash(seed^salt,k.x,k.y))); }
    static float Between(Rng r,Vector2 range) { return Mathf.Lerp(range.x,range.y,(float)r.NextDouble()); }
    static bool Apart(List<Vector3> points,Vector3 p,float space)
    {
        float sq=space*space;
        foreach(var q in points) if ((p-q).sqrMagnitude<sq) return false;
        return true;
    }
    bool Dry(Vector3 p) { return !field.water || p.y>field.sea+.12f; }
    // Stable XZ targets keep intact trees in place when another part of a column is mined.
    bool PickSurface(View v,Rng rng,out SurvivalSite site)
    {
        float x=(float)rng.NextDouble()*Size, z=(float)rng.NextDouble()*Size;
        float best=2.25f; site=default(SurvivalSite); bool found=false;
        foreach(var f in v.sites.surface)
        {
            var p=f.Centre; float dx=p.x-x, dz=p.z-z, distance=dx*dx+dz*dz;
            if(distance>=best) continue;
            best=distance; site=f; found=true;
        }
        return found;
    }
    static string SlotKey(Vector2Int k,int group,int slot) { return k.x+"/"+k.y+"/"+group+"/"+slot; }
    BiomePrefab[] Entries(BiomeType b)
    {
        switch(b)
        {
            case BiomeType.Mountains: return mountainPrefabs;
            case BiomeType.Taiga: return taigaPrefabs;
            case BiomeType.Desert: return desertPrefabs;
            case BiomeType.Swamp: return swampPrefabs;
            default: return plainsPrefabs;
        }
    }
    GameObject Model(Transform parent,string label,Form form,Vector3 p,Quaternion q,float scale)
    {
        var root=new GameObject(label); root.layer=parent.gameObject.layer; root.transform.SetParent(parent,false);
        root.transform.localPosition=p; root.transform.localRotation=q; root.transform.localScale=Vector3.one*scale;
        AddMeshChild(root.transform,"Visual",art.Get(form),art.solids);
        return root;
    }
    static GameObject Pick(GameObject[] items,Rng r)
    {
        if(items==null || items.Length==0) return null;
        int start=r.Next(items.Length);
        for(int i=0;i<items.Length;i++) { var go=items[(i+start)%items.Length]; if(go!=null) return go; }
        return null;
    }
    static GameObject PlacePrefab(GameObject prefab,Transform parent,Vector3 pos,Quaternion rotation,float scale)
    {
        var go=Instantiate(prefab,parent,false);
        go.transform.localPosition=pos; go.transform.localRotation=rotation;
        go.transform.localScale=prefab.transform.localScale*scale;
        return go;
    }
    // Renderer bounds are evaluated in root space so rotated or scaled child meshes work too.
    static Bounds ModelBounds(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>(true);
        Bounds bounds=new Bounds(Vector3.zero,Vector3.zero); bool any=false;
        foreach(var r in renderers)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            var b=r.localBounds;
            Matrix4x4 matrix=go.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
            for(int i=0;i<8;i++)
            {
                Vector3 p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(any) bounds.Encapsulate(p); else { bounds=new Bounds(p,Vector3.zero); any=true; }
            }
        }
        return any?bounds:new Bounds(Vector3.up*.25f,new Vector3(.5f,.5f,.5f));
    }
    static void AddBoxIfMissing(GameObject go,Bounds b)
    {
        if(go.GetComponentInChildren<Collider>()!=null) return;
        var box=go.AddComponent<BoxCollider>(); box.center=b.center; box.size=b.size;
    }
    void BindTree(GameObject go,string key,bool sway)
    {
        var tree=go.GetComponentInChildren<TreeResource>();
        if(tree!=null) { tree.BindGenerated(this,key,go); AddBoxIfMissing(tree.gameObject,ModelBounds(tree.gameObject)); }
        if(sway) RegisterWind(go);
    }

    IEnumerator PlaceHabitat(View v,Transform parent)
    {
        var surface=v.sites.surface;
        var occupied=new List<Vector3>(); int count=0;
        if(surface.Count>0)
        {
            for(int b=0;b<5;b++)
            {
                var entries=Entries((BiomeType)b); if(entries==null) continue;
                for(int e=0;e<entries.Length && count<maxDecorationsPerChunk;e++)
                {
                    var entry=entries[e]; if(entry==null || entry.prefab==null) continue;
                    for(int j=0;j<Mathf.Clamp(entry.attemptsPerChunk,0,64) && count<maxDecorationsPerChunk;j++)
                    {
                        yield return null;
                        var rng=RandomFor(v.key,190481+b*3181+e*17417+j*4133);
                        SurvivalSite site;
                        if(!PickSurface(v,rng,out site)) continue;
                        Vector3 p=site.Point(rng); string key=SlotKey(v.key,10+b,e*128+j);
                        if(SurvivalWorldField.Biome(site.climate)!=(BiomeType)b || rng.NextDouble()>=entry.spawnChance ||
                           !Dry(p) || 1f-site.normal.y>entry.maxSlope || !Apart(occupied,p,entry.minSpacing)) continue;
                        if(chopped.Contains(key)) { occupied.Add(p); count++; continue; }
                        Quaternion q=Quaternion.Euler(0,(float)rng.NextDouble()*360f,0);
                        if(entry.alignToGround) q=Quaternion.FromToRotation(Vector3.up,site.normal)*q;
                        var go=PlacePrefab(entry.prefab,parent,p+Vector3.up*Between(rng,entry.heightOffset),q,
                                           Mathf.Max(.01f,Between(rng,entry.randomScale)));
                        BindTree(go,key,entry.affectedByWind || go.GetComponentInChildren<TreeResource>()!=null);
                        occupied.Add(p); count++;
                    }
                }
            }
            if(proceduralModels)
            {
                for(int j=0;j<maxDecorationsPerChunk*2 && count<maxDecorationsPerChunk;j++)
                {
                    yield return null;
                    var rng=RandomFor(v.key,60103+j*7151);
                    SurvivalSite site;
                    if(!PickSurface(v,rng,out site)) continue;
                    var p=site.Point(rng);
                    var biome=SurvivalWorldField.Biome(site.climate);
                    bool tree=j%3==0;
                    float habitat=biome==BiomeType.Taiga ? 1f : biome==BiomeType.Desert ? .24f : biome==BiomeType.Mountains ? .42f : .70f;
                    if(!Dry(p) || site.normal.y<(tree ? .79f : .70f) || rng.NextDouble()>=(tree?treeChance:plantChance)*habitat ||
                       !Apart(occupied,p,tree?treeSpacing:.52f)) continue;
                    string key=SlotKey(v.key,39,j);
                    if(chopped.Contains(key)) { occupied.Add(p); count++; continue; }
                    float scale=Mathf.Max(.02f,Between(rng,tree?treeScaleRange:plantScaleRange));
                    Quaternion q=Quaternion.Euler(0,(float)rng.NextDouble()*360f,0);
                    if(tree)
                    {
                        var form=biome==BiomeType.Desert?Form.Acacia:biome==BiomeType.Taiga || biome==BiomeType.Mountains?Form.Spruce:Form.Birch;
                        var go=Model(parent,form.ToString(),form,p,q,scale);
                        var trunk=go.AddComponent<CapsuleCollider>(); trunk.center=Vector3.up*1.65f; trunk.height=3.3f; trunk.radius=.22f;
                        go.AddComponent<TreeResource>().BindGenerated(this,key); RegisterWind(go);
                    }
                    else
                    {
                        var form=biome==BiomeType.Swamp?Form.Reed:biome==BiomeType.Desert?Form.Aloe:j%2==0?Form.Bloom:Form.Fern;
                        Model(parent,form.ToString(),form,p,q,scale);
                    }
                    occupied.Add(p); count++;
                }
            }
        }
        var surfaceWork=PlaceStructure(v,parent,false,occupied);
        while(surfaceWork.MoveNext()) yield return null;
        var caveWork=PlaceStructure(v,parent,true,occupied);
        while(caveWork.MoveNext()) yield return null;
        if(enableGrassPatches && surface.Count>0)
        {
            var grassWork=BuildGrass(v,parent);
            while(grassWork.MoveNext()) yield return null;
        }
    }

    IEnumerator PlaceStructure(View v,Transform root,bool cave,List<Vector3> occupied)
    {
        var rng=RandomFor(v.key,cave?38083:8161);
        var sites=cave?v.sites.floors:v.sites.surface;
        if(sites.Count==0 || rng.NextDouble()>=(cave?caveStructureChance:surfaceStructureChance)) yield break;
        var prefab=Pick(cave?undergroundStructures:surfaceStructures,rng);
        if(prefab==null && !proceduralModels) yield break;
        for(int j=0;j<18;j++)
        {
            yield return null;
            SurvivalSite site;
            if(cave) site=sites[rng.Next(sites.Count)];
            else if(!PickSurface(v,rng,out site)) continue;
            Vector3 p=site.Centre;
            if(site.normal.y<structureMinUp || (!cave && !Dry(p)) || !Apart(occupied,p,structureFootprint)) continue;
            if(p.x<structureFootprint || p.z<structureFootprint ||
               p.x>Size-structureFootprint || p.z>Size-structureFootprint) continue;
            Vector3 grid=p+new Vector3(v.key.x*Size,0,v.key.y*Size);
            var q=Quaternion.Euler(0,(float)rng.NextDouble()*360f,0);
            bool fits=true;
            for(int k=0;k<5 && fits;k++)
            {
                Vector3 test=grid+q*(k==0?Vector3.zero:new Vector3((k%2==0?1:-1)*.65f,0,(k>2?1:-1)*.46f)*structureFootprint);
                if(Density(test-Vector3.up*.65f)>0f || Density(test+Vector3.up*.8f)<0f ||
                   (cave && (Density(test+Vector3.up*caveStructureClearance)<0f ||
                             Density(test+Vector3.up*(.5f*caveStructureClearance))<0f))) fits=false;
            }
            if(!fits) continue;
            var go=prefab!=null?PlacePrefab(prefab,root,p,q,1f):Model(root,cave?"Důlní výdřeva":"Kamenná brána",cave?Form.Gallery:Form.Ruin,p,q,1f);
            if(prefab!=null)
            {
                var b=ModelBounds(go);
                go.transform.position+=root.TransformPoint(p)-go.transform.TransformPoint(new Vector3(b.center.x,b.min.y,b.center.z));
            }
            if(prefab==null)
            {
                // Keep the opening of the arch walkable.
                for(int k=-1;k<=1;k+=2)
                {
                    var foot=go.AddComponent<BoxCollider>();
                    foot.center=new Vector3(k*.95f,1.1f,0); foot.size=new Vector3(cave ? .33f : .68f,2.2f,cave ? .38f : .7f);
                }
                var lintel=go.AddComponent<BoxCollider>(); lintel.center=Vector3.up*(cave?2.32f:2.48f);
                lintel.size=new Vector3(cave ? 2.27f : 2.68f,cave ? .29f : .45f,cave ? .35f : .78f);
            }
            occupied.Add(p); yield break;
        }
    }

    IEnumerator PlaceOres(View v,Transform parent)
    {
        var walls=v.sites.walls; if(walls.Count==0) yield break;
        OreNode[] prefabs={coalOrePrefab,ironOrePrefab,goldOrePrefab,rubyOrePrefab};
        Vector2[] ranges={coalHeightRange,ironHeightRange,goldHeightRange,rubyHeightRange};
        int[] counts={coalPerChunk,ironPerChunk,goldPerChunk,rubyPerChunk};
        var occupied=new List<Vector3>();
        for(int t=0;t<4;t++)
        for(int j=0;j<Mathf.Clamp(counts[t],0,30);j++)
        {
            yield return null;
            string key=SlotKey(v.key,70+t,j);
            if(mined.Contains(key) || (prefabs[t]==null && !proceduralModels)) continue;
            var rng=RandomFor(v.key,7807+t*4919+j*8191);
            Vector3 probe=new Vector3((float)rng.NextDouble()*Size,Between(rng,ranges[t]),(float)rng.NextDouble()*Size);
            int best=-1; float nearest=float.PositiveInfinity;
            for(int k=0;k<walls.Count;k++)
            {
                if((k&127)==127) yield return null;
                var face=walls[k]; Vector3 point=face.Centre;
                float distance=(point-probe).sqrMagnitude;
                if(distance>=nearest || Mathf.Abs(face.normal.y)>oreWallMaxUp || point.y<Mathf.Min(ranges[t].x,ranges[t].y) ||
                   point.y>Mathf.Max(ranges[t].x,ranges[t].y) || face.depth<oreMinimumDepthBelowSurface ||
                   !Apart(occupied,point,oreSpacing)) continue;
                best=k; nearest=distance;
            }
            if(best>=0)
            {
                var site=walls[best]; Vector3 p=site.Centre;
                Quaternion q=Quaternion.FromToRotation(Vector3.up,site.normal)*Quaternion.Euler(0,(float)rng.NextDouble()*360f,0);
                Vector3 contact=parent.TransformPoint(p-site.normal*oreFloorInset);
                if(prefabs[t]!=null)
                {
                    OreNode node=Instantiate(prefabs[t],parent,false);
                    Transform tr=node.transform; tr.localPosition=p; tr.localRotation=q;
                    var anchor=node.mountPoint!=null ? node.mountPoint : tr.Find("MountPoint");
                    if(anchor!=null)
                    {
                        Quaternion delta=Quaternion.Inverse(tr.rotation)*anchor.rotation;
                        tr.rotation=parent.rotation*q*Quaternion.Inverse(delta);
                        tr.position+=contact-anchor.position;
                    }
                    else
                    {
                        var bounds=ModelBounds(node.gameObject);
                        Vector3 bottom=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                        tr.position+=contact-tr.TransformPoint(bottom);
                    }
                    AddBoxIfMissing(node.gameObject,ModelBounds(node.gameObject));
                    node.owner=null; node.terrainOwner=this; node.persistentKey=key;
                    node.gameObject.SetActive(true);
                }
                else
                {
                    var form=(Form)((int)Form.Coal+t);
                    var go=Model(parent,form.ToString(),form,p-site.normal*oreFloorInset,q,Mathf.Max(.02f,Between(rng,oreScaleRange)));
                    var node=go.AddComponent<OreNode>();
                    node.minimumPickaxeTier=t<2?1:2; node.hitsRequired=2+t; node.dropItemId=32+t;
                    AddBoxIfMissing(go,ModelBounds(go));
                    node.terrainOwner=this; node.persistentKey=key;
                }
                occupied.Add(p);
            }
        }
    }

    IEnumerator BuildGrass(View v,Transform parent)
    {
        var rng=RandomFor(v.key,45691);
        if(rng.NextDouble()<emptyGrassChunkChance) yield break;
        int limit=Mathf.Clamp(Mathf.Min(maxGrassPerChunk,maxGrassTuftsPerChunkPerformance),0,500);
        var positions=new List<Vector3>(); var uv=new List<Vector2>(); var colors=new List<Color32>();
        var textures=new List<Texture2D>();
        if(grassTextures!=null) foreach(var tex in grassTextures) if(tex!=null && !textures.Contains(tex)) textures.Add(tex);
        bool cards=textures.Count>0; if(!cards) textures.Add(null);
        var indices=new List<int>[textures.Count]; for(int n=0;n<indices.Length;n++) indices[n]=new List<int>();
        var surfaces=v.sites.surface; int created=0;
        var around=new List<SurvivalSite>();
        for(int patch=0;patch<grassPatchesPerChunk && created<limit;patch++)
        {
            SurvivalSite centreSite;
            if(!PickSurface(v,rng,out centreSite)) continue;
            Vector3 centrePoint=centreSite.Centre;
            around.Clear();
            for(int s=0;s<surfaces.Count;s++)
            {
                if((s&63)==0) yield return null;
                var f=surfaces[s]; if((f.Centre-centrePoint).sqrMagnitude<=grassPatchRadius*grassPatchRadius) around.Add(f);
            }
            for(int t=0;t<grassBladesPerPatch && around.Count>0 && created<limit;t++)
            {
                if((t&3)==0) yield return null;
                var f=around[rng.Next(around.Count)]; Vector3 p=f.Point(rng);
                if(!Dry(p) || 1f-f.normal.y>grassMaxSlope) continue;
                var biome=SurvivalWorldField.Biome(f.climate);
                if((biome==BiomeType.Desert && rng.NextDouble()>.08) || (biome==BiomeType.Mountains && rng.NextDouble()>.45)) continue;
                float h=Mathf.Max(.04f,Between(rng,grassHeightRange)), yaw=(float)rng.NextDouble()*Mathf.PI;
                List<int> tri=indices[rng.Next(textures.Count)];
                Color32 tint=Color.Lerp(new Color(.67f,.79f,.55f),new Color(.98f,1f,.79f),(float)rng.NextDouble());
                for(int blade=0;blade<(cards?2:5);blade++)
                {
                    float a=yaw+blade*(cards?Mathf.PI*.5f:2.39996f);
                    Vector3 dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    Vector3 side=dir*h*(cards ? .32f : .055f);
                    Vector3 root=p+(!cards?dir*h*.10f:Vector3.zero)-Vector3.up*.018f;
                    Vector3 tip=root+Vector3.up*h*(cards?1f:.62f+.38f*(float)rng.NextDouble());
                    if(!cards) tip+=dir*h*.26f;
                    int i=positions.Count;
                    positions.Add(root-side); positions.Add(root+side);
                    uv.Add(Vector2.zero); uv.Add(Vector2.right);
                    if(cards) { positions.Add(tip-side); positions.Add(tip+side); uv.Add(Vector2.up); uv.Add(Vector2.one); }
                    else { positions.Add(tip); uv.Add(new Vector2(.5f,1)); }
                    tri.Add(i); tri.Add(i+2); tri.Add(i+1);
                    if(cards) { tri.Add(i+1); tri.Add(i+2); tri.Add(i+3); }
                    for(int k=i;k<positions.Count;k++) colors.Add(tint);
                }
                created++;
            }
        }
        if(created==0) yield break;
        Mesh m=new Mesh { name="Trsy trávy",subMeshCount=textures.Count };
        m.SetVertices(positions); m.SetUVs(0,uv); m.SetColors(colors);
        var materials=new Material[textures.Count];
        for(int i=0;i<materials.Length;i++) { m.SetTriangles(indices[i],i); materials[i]=art.Grass(textures[i]); }
        var bounds=m.bounds; bounds.Expand(Vector3.one*2f); m.bounds=bounds;
        var patchObject=AddMeshChild(parent,"Tráva",m,materials[0]);
        var renderer=patchObject.GetComponent<MeshRenderer>(); renderer.sharedMaterials=materials;
        renderer.shadowCastingMode=ShadowCastingMode.Off; v.grass=m;
    }
}
