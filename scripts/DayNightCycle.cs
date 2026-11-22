using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class DayNightCycle : MonoBehaviour
{
    [Header("Čas • délka celého dne i noci dohromady")]
    [Min(.05f)] public float cycleMinutes = 20f;
    [Range(0f,24f)] public float startHour = 8f;
    [Range(.1f,.9f)] public float daylightFraction = .5f;
    public bool useUnscaledTime;
    [Header("Slunce a měsíc")]
    public Light sun, moon;
    public bool createMoon = true;
    [Range(0f,360f)] public float azimuth = 125f;
    [Min(0f)] public float sunIntensity = 1.12f, moonIntensity = .16f;
    public Color noonColor = new Color(1f,.95f,.83f), horizonColor = new Color(1f,.53f,.28f);
    public Color moonColor = new Color(.53f,.67f,.95f);
    [Header("Barvy oblohy a okolního světla")]
    public bool animateAmbient = true, animateSkyboxExposure = true;
    public Color daySky = new Color(.67f,.78f,.93f), nightSky = new Color(.065f,.09f,.16f);
    public Color dayHorizon = new Color(.50f,.55f,.53f), nightHorizon = new Color(.035f,.05f,.085f);
    public Color dayGround = new Color(.28f,.28f,.23f), nightGround = new Color(.023f,.028f,.038f);
    [Min(0f)] public float dayExposure = 1.08f, nightExposure = .13f;
    public float Hour { get { return (float)(phase*24.0); } }
    double phase;
    bool initialized, ownsSun, ownsMoon, captured, wroteAmbient;
    AmbientMode oldMode;
    Color oldSky, oldHorizon, oldGround;
    Material oldSkybox, liveSkybox;
    Light previousSun;
    struct LightState
    {
        internal Quaternion rotation; internal float intensity; internal Color color; internal bool enabled;
        internal LightState(Light l)
        { rotation=l.transform.rotation; intensity=l.intensity; color=l.color; enabled=l.enabled; }
        internal void Restore(Light l)
        { if(l!=null) { l.transform.rotation=rotation; l.intensity=intensity; l.color=color; l.enabled=enabled; } }
    }
    LightState sunBefore, moonBefore;
    Light NewLight(string label)
    {
        var go=new GameObject(label); go.transform.SetParent(transform,false);
        var l=go.AddComponent<Light>(); l.type=LightType.Directional; l.shadows=LightShadows.None; return l;
    }
    void OnEnable()
    {
        if(!initialized) { phase=Mathf.Repeat(startHour,24f)/24.0; initialized=true; }
        previousSun=RenderSettings.sun;
        if(sun==null && previousSun!=null && previousSun.type==LightType.Directional) sun=previousSun;
        if(sun==null)
        {
            foreach(var l in FindObjectsOfType<Light>())
                if(l.type==LightType.Directional && l!=moon && l.enabled) { sun=l; break; }
        }
        if(sun==null) { sun=NewLight("Slunce"); ownsSun=true; sun.shadows=LightShadows.Soft; }
        if(moon==sun) moon=null;
        if(moon==null && createMoon) { moon=NewLight("Měsíc"); ownsMoon=true; }
        sunBefore=new LightState(sun); if(moon!=null) moonBefore=new LightState(moon);
        oldMode=RenderSettings.ambientMode; oldSky=RenderSettings.ambientSkyColor;
        oldHorizon=RenderSettings.ambientEquatorColor; oldGround=RenderSettings.ambientGroundColor;
        oldSkybox=RenderSettings.skybox;
        if(animateSkyboxExposure && oldSkybox!=null && oldSkybox.HasProperty("_Exposure"))
        { liveSkybox=new Material(oldSkybox); RenderSettings.skybox=liveSkybox; }
        RenderSettings.sun=sun; captured=true; Apply();
    }
    public void SetHour(float hour) { phase=Mathf.Repeat(hour,24f)/24.0; if(captured) Apply(); }
    void Update()
    {
        phase+=(useUnscaledTime?Time.unscaledDeltaTime:Time.deltaTime)/(60.0*Mathf.Max(.05f,cycleMinutes));
        phase-=System.Math.Floor(phase); Apply();
    }
    internal static float Orbit(float time,float dayShare)
    {
        float day=Mathf.Clamp(dayShare,.1f,.9f), sinceDawn=Mathf.Repeat(time-(.5f-day*.5f),1f);
        return sinceDawn<day?180f*sinceDawn/day:180f+180f*(sinceDawn-day)/(1f-day);
    }
    void Apply()
    {
        float angle=Orbit((float)phase,daylightFraction);
        float height=Mathf.Sin(angle*Mathf.Deg2Rad);
        float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.13f,.30f,height));
        if(sun!=null)
        {
            sun.transform.rotation=Quaternion.Euler(angle,azimuth,0);
            sun.color=Color.Lerp(horizonColor,noonColor,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.65f,height)));
            sun.intensity=sunIntensity*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.04f,.42f,height));
            sun.enabled=sun.intensity>.001f;
        }
        if(moon!=null)
        {
            moon.transform.rotation=Quaternion.Euler(angle+180f,azimuth,0);
            moon.color=moonColor;
            moon.intensity=moonIntensity*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,-.40f,height));
            moon.enabled=moon.intensity>.001f;
        }
        if(animateAmbient)
        {
            wroteAmbient=true;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(nightSky,daySky,daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(nightHorizon,dayHorizon,daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(nightGround,dayGround,daylight);
        }
        if(liveSkybox!=null) liveSkybox.SetFloat("_Exposure",Mathf.Lerp(nightExposure,dayExposure,daylight));
    }
    void OnDisable()
    {
        if(!captured) return; captured=false;
        if(wroteAmbient)
        {
            wroteAmbient=false;
            RenderSettings.ambientMode=oldMode; RenderSettings.ambientSkyColor=oldSky;
            RenderSettings.ambientEquatorColor=oldHorizon; RenderSettings.ambientGroundColor=oldGround;
        }
        if(liveSkybox!=null)
        { if(RenderSettings.skybox==liveSkybox) RenderSettings.skybox=oldSkybox; Destroy(liveSkybox); liveSkybox=null; }
        if(RenderSettings.sun==sun) RenderSettings.sun=previousSun;
        if(ownsSun && sun!=null) { sun.enabled=false; Destroy(sun.gameObject); sun=null; ownsSun=false; } else sunBefore.Restore(sun);
        if(ownsMoon && moon!=null) { moon.enabled=false; Destroy(moon.gameObject); moon=null; ownsMoon=false; } else moonBefore.Restore(moon);
    }
}
