using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class SmoothTerrain
{
    struct Sway { internal Transform graphic; internal Quaternion rest; internal float phase; }
    readonly List<Sway> sway=new List<Sway>();
    ParticleSystem rain, snow;
    System.Random weatherRng;
    SkyWeather targetWeather;
    float nextWeather, nextShelter, rainMix, snowMix;
    bool covered;
    static readonly int WindId=Shader.PropertyToID("_SW_Wind"), TimeId=Shader.PropertyToID("_SW_Clock"), LightId=Shader.PropertyToID("_SW_Light");
    static readonly int SunDirId=Shader.PropertyToID("_SW_SunDirection");
    void RegisterWind(GameObject go)
    {
        var resource=go.GetComponentInChildren<TreeResource>();
        Transform pivot=resource!=null?resource.windPivot:null;
        if(pivot==null) pivot=go.transform.Find("WindPivot");
        if(pivot==null) pivot=go.transform.Find("Visual");
        if(pivot==null)
        {
            foreach(var render in go.GetComponentsInChildren<MeshRenderer>())
            {
                if(render.transform==go.transform || render.GetComponentInChildren<Collider>()!=null) continue;
                pivot=render.transform; break;
            }
        }
        if(pivot==null)
        {
            var filter=go.GetComponent<MeshFilter>(); var render=go.GetComponent<MeshRenderer>();
            if(filter!=null && filter.sharedMesh!=null && render!=null && render.enabled && go.GetComponent<LODGroup>()==null)
            {
                var copy=AddMeshChild(go.transform,"WindPivot",filter.sharedMesh,render.sharedMaterial);
                var graphics=copy.GetComponent<MeshRenderer>();
                graphics.sharedMaterials=render.sharedMaterials; graphics.receiveShadows=render.receiveShadows;
                graphics.shadowCastingMode=render.shadowCastingMode; render.enabled=false; pivot=copy.transform;
            }
        }
        if(pivot!=null)
            sway.Add(new Sway { graphic=pivot,rest=pivot.localRotation,phase=go.transform.position.x*.097f+go.transform.position.z*.08f });
    }
    void InitWeather()
    {
        weatherRng=new System.Random(seed^48013); targetWeather=SkyWeather.Clear;
        nextWeather=Time.time+RollInterval();
        rain=MakePrecipitation(false); snow=MakePrecipitation(true);
    }
    float RollInterval() { return Mathf.Max(1f,Between(weatherRng,weatherDurationSeconds)); }
    ParticleSystem MakePrecipitation(bool snowing)
    {
        var go=new GameObject(snowing?"Sníh":"Déšť"); go.transform.SetParent(transform,false);
        var ps=go.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.loop=true; main.playOnAwake=false; main.startSpeed=0f;
        main.simulationSpace=ParticleSystemSimulationSpace.World; main.maxParticles=Mathf.Max(32,particleLimit);
        Vector2 sizes=snowing?snowflakeSize:raindropSize;
        main.startSize=new ParticleSystem.MinMaxCurve(Mathf.Max(.001f,Mathf.Min(sizes.x,sizes.y)),Mathf.Max(.001f,Mathf.Max(sizes.x,sizes.y)));
        main.startColor=snowing?snowflakeColor:rainColor;
        main.startLifetime=(precipitationHeight+3.5f)/Mathf.Max(.1f,snowing?snowSpeed:rainSpeed);
        var shape=ps.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(2f*precipitationRadius,.4f,2f*precipitationRadius);
        var v=ps.velocityOverLifetime; v.enabled=true; v.space=ParticleSystemSimulationSpace.World;
        v.x=0f; v.y=-(snowing?snowSpeed:rainSpeed); v.z=0f;
        var e=ps.emission; e.rateOverTime=0f;
        if(snowing) { var n=ps.noise; n.enabled=true; n.strength=.32f; n.frequency=.34f; n.scrollSpeed=.28f; }
        var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=art.Precipitation;
        renderer.renderMode=snowing?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;
        renderer.lengthScale=snowing?1f:2.6f; renderer.velocityScale=snowing?0f:.033f;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        return ps;
    }
    void UpdateAtmosphere()
    {
        float t=Time.time; Vector3 point=player!=null?player.position:transform.position;
        if(weather!=SkyWeather.Automatic) targetWeather=weather;
        else if(t>=nextWeather)
        {
            nextWeather=t+RollInterval(); Vector3 p=GridPoint(point);
            bool alpine=GetBiome(p.x,p.z)==BiomeType.Mountains || p.y>field.height*snowLine;
            targetWeather=weatherRng.NextDouble()>=precipitationChance?SkyWeather.Clear:
                (alpine || weatherRng.NextDouble()<lowlandSnowChance?SkyWeather.Snow:SkyWeather.Rain);
        }
        float fade=Time.deltaTime/Mathf.Max(.1f,weatherFadeSeconds);
        rainMix=Mathf.MoveTowards(rainMix,targetWeather==SkyWeather.Rain?1f:0f,fade);
        snowMix=Mathf.MoveTowards(snowMix,targetWeather==SkyWeather.Snow?1f:0f,fade);
        Vector2 d=windDirection.sqrMagnitude>.0001f?windDirection.normalized:Vector2.right;
        float wave=.55f*Mathf.Sin(t*windGustFrequency*6.283f)+.45f*Mathf.Sin(t*windGustFrequency*2.83f+1.4f);
        float strength=enableIntegratedWind?Mathf.Max(0f,windStrength+wave*windGustStrength)*(1f+.42f*rainMix):0f;
        Vector3 breeze=new Vector3(d.x,0,d.y)*strength;
        Shader.SetGlobalVector(WindId,new Vector4(breeze.x,0,breeze.z,0)); Shader.SetGlobalFloat(TimeId,t);
        Light sun=RenderSettings.sun;
        float light=sun==null?1f:Mathf.Clamp01(sun.enabled?sun.intensity:0f);
        Vector3 sunDir=sun!=null ? -sun.transform.forward : new Vector3(.33f,.88f,.35f);
        Shader.SetGlobalVector(SunDirId,new Vector4(sunDir.x,sunDir.y,sunDir.z,0));
        Shader.SetGlobalFloat(LightId,Mathf.Lerp(.22f,1f,light));
        Vector3 axis=Vector3.Cross(Vector3.up,new Vector3(d.x,0,d.y));
        for(int i=sway.Count-1;i>=0;i--)
        {
            var s=sway[i];
            if(s.graphic==null) { int last=sway.Count-1; sway[i]=sway[last]; sway.RemoveAt(last); continue; }
            float bend=treeBendDegrees*strength*(.55f+.45f*Mathf.Sin(t*1.7f+s.phase));
            Vector3 localAxis=s.graphic.parent!=null?s.graphic.parent.InverseTransformDirection(axis):axis;
            s.graphic.localRotation=Quaternion.AngleAxis(bend,localAxis)*s.rest;
        }
        if(player==null || !initialLoadingFinished) return;
        if(t>=nextShelter)
        {
            nextShelter=t+.24f;
            bool inside=shelterStopsWeather && (TopWorldAt(point)>point.y+.65f ||
                Physics.Raycast(point+Vector3.up*.12f,Vector3.up,precipitationHeight+2f,shelterMask,QueryTriggerInteraction.Ignore));
            if(inside && !covered) { rain.Clear(); snow.Clear(); }
            covered=inside;
        }
        Vector3 origin=point+Vector3.up*precipitationHeight;
        SetRain(rain,origin,breeze,rainSpeed,covered?0f:rainRate*rainMix);
        SetRain(snow,origin,breeze,snowSpeed,covered?0f:snowRate*snowMix);
    }
    static void SetRain(ParticleSystem ps,Vector3 origin,Vector3 wind,float speed,float rate)
    {
        ps.transform.position=origin;
        var v=ps.velocityOverLifetime; v.x=wind.x; v.y=-Mathf.Max(.1f,speed); v.z=wind.z;
        var e=ps.emission; e.rateOverTime=Mathf.Max(0f,rate);
        if(rate>.01f && !ps.isPlaying) ps.Play();
        else if(rate<=.01f && ps.isEmitting) ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
    }
    void StopWeather()
    {
        if(rain!=null) rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(snow!=null) snow.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach(var s in sway) if(s.graphic!=null) s.graphic.localRotation=s.rest;
        if(started) Shader.SetGlobalVector(WindId,Vector4.zero);
    }
    void DisposeAtmosphere()
    {
        if(rain!=null) Destroy(rain.gameObject);
        if(snow!=null) Destroy(snow.gameObject);
    }
}
