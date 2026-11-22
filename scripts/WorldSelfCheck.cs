#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Runs in Edit mode from Tools. It creates only a temporary inactive test object.
internal static class SurvivalWorldSelfCheck
{
    struct Edge : IEquatable<Edge>
    {
        internal Vector3Int a,b;
        public bool Equals(Edge e) { return a==e.a && b==e.b; }
        public override bool Equals(object obj) { return obj is Edge e && Equals(e); }
        public override int GetHashCode() { unchecked { return a.GetHashCode()*397 ^ b.GetHashCode(); } }
    }
    static int Compare(Vector3Int a,Vector3Int b)
    { return a.x!=b.x?a.x.CompareTo(b.x):a.y!=b.y?a.y.CompareTo(b.y):a.z.CompareTo(b.z); }
    static void EdgeCount(Dictionary<Edge,int> edges,Vector3Int a,Vector3Int b)
    {
        int sign=Compare(a,b); if(sign==0) return;
        var key=sign<0?new Edge{a=a,b=b}:new Edge{a=b,b=a};
        edges.TryGetValue(key,out var count); edges[key]=count+(sign<0?1:-1);
    }
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }

    [MenuItem("Tools/Survival World/Prověřit generátor")]
    static void Run()
    {
        var host=new GameObject("Kontrola krajiny"); host.hideFlags=HideFlags.HideAndDontSave; host.SetActive(false);
        try
        {
            var settings=host.AddComponent<SmoothTerrain>(); settings.seed=12345; settings.randomSeed=false;
            var f=new SurvivalWorldField(settings);
            var p=f.Sample(0,0);
            var stroke=new SurvivalBrush { start=new Vector3(-6,p.surface-1,0),end=new Vector3(6,p.surface-5,1),radius=3.1f,dig=true };
            CheckBorders(f,new SurvivalBrush[0]);
            CheckBorders(f,new[]{stroke});
            float low=float.MaxValue, high=float.MinValue; int wet=0, mouths=0;
            for(int x=-192;x<=192;x+=3) for(int z=-192;z<=192;z+=3)
            {
                var profile=f.Sample(x,z);
                low=Mathf.Min(low,profile.surface); high=Mathf.Max(high,profile.surface);
                if(profile.surface<f.sea) wet++;
                if(profile.entrance) mouths++;
                Require(f.Density(profile,f.bedrock)<0,"Podloží musí být pevné.");
            }
            Require(high-low>12f && wet>0 && mouths>0,"Chybí očekávaný reliéf, voda nebo vchod.");
            Require(Mathf.Abs(DayNightCycle.Orbit(.5f,.5f)-90f)<.001f,"Poledne musí být v polovině cyklu.");
            foreach(var s in new[]{"Grass","Water","Precipitation","PaletteFallback"})
            {
                var shader=Resources.Load<Shader>("SurvivalWorld/"+s);
                Require(shader!=null && shader.isSupported && !ShaderUtil.ShaderHasError(shader),"Shader: "+s);
            }
            Debug.Log("Kontrola prošla: hrany i po kopání, podloží, vchody, hladina, poledne a čtyři shadery. Rozsah výšek "
                      +low.ToString("F1")+" až "+high.ToString("F1")+" buněk.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
    static void CheckBorders(SurvivalWorldField f,SurvivalBrush[] edits)
    {
        var tally=new Dictionary<Edge,int>();
        const int snap=4096;
        for(int x=-1;x<=0;x++) for(int z=-1;z<=0;z++)
        {
            var b=new SurvivalColumnBuilder(f,new Vector2Int(x,z),edits);
            while(!b.Step()) { }
            var m=b.ground;
            Vector3 off=new Vector3(x*f.size,0,z*f.size);
            var pts=new Vector3Int[m.vertices.Count];
            for(int i=0;i<pts.Length;i++) pts[i]=Vector3Int.RoundToInt((m.vertices[i]+off)*snap);
            for(int i=0;i<m.triangles.Count;i+=3)
            {
                var a=pts[m.triangles[i]]; var c=pts[m.triangles[i+1]]; var d=pts[m.triangles[i+2]];
                EdgeCount(tally,a,c); EdgeCount(tally,c,d); EdgeCount(tally,d,a);
            }
        }
        foreach(var e in tally)
        {
            if(e.Value==0) continue;
            var a=e.Key.a; var b=e.Key.b;
            int min=-f.size*snap, max=(f.size-1)*snap;
            bool outer=(a.x<=min && b.x<=min)||(a.z<=min && b.z<=min)
                    ||(a.x>=max && b.x>=max)||(a.z>=max && b.z>=max);
            Require(outer,"Otevřená vnitřní hrana: "+a+" → "+b);
        }
    }
}
#endif
