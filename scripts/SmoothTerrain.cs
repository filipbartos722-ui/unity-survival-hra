using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using TerrainWater = SurvivalWorld.Generated.WaterSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-120)]
public partial class SmoothTerrain : MonoBehaviour
{
    public enum BiomeType { Mountains, Taiga, Desert, Swamp, Plains }
    public enum SkyWeather { Automatic, Clear, Rain, Snow }

    [Serializable]
    public class BiomePrefab
    {
        public GameObject prefab;
        [Range(0f,1f)] public float spawnChance = .35f;
        [Range(0,64)] public int attemptsPerChunk = 5;
        public Vector2 randomScale = Vector2.one;
        public bool alignToGround;
        [Range(0f,1f)] public float maxSlope = .35f;
        [Min(0f)] public float minSpacing;
        public Vector2 heightOffset;
        public bool affectedByWind;
    }

    [Header("Svět • rozměry a seed")]
    public Transform player;
    public bool randomSeed = true;
    public int seed = 12345;
    [Range(8,40)] public int chunkSize = 16;
    [Range(2,12)] public int chunksY = 6;
    [Min(.1f)] public float cellSize = 1f;
    [Range(1,24)] public int renderDistance = 7, maxActiveRenderDistance = 10;
    [Range(0,12)] public int colliderDistance = 2;
    [Header("Výkon • rozpočet na snímek")]
    public bool backgroundGeneration = true;
    [Range(1,4)] public int workerCount = 2;
    [Range(.25f,12f)] public float msPerFrame = 3f;
    [Range(.2f,8f)] public float colliderBudgetMs = 1.5f;
    [Range(1,4)] public int maxColumnsPerFrame = 1, maxCollidersPerFrame = 1;
    [Range(1,64)] public int decorationStepsPerFrame = 12;

    [Header("Načtení hráče")]
    public bool showInitialLoadingScreen = true;
    [Range(1,128)] public int initialColumnsToPrepare = 5;
    public string loadingTitle = "Připravuji krajinu";
    public Behaviour playerMovementDuringLoading;
    public Behaviour[] additionalScriptsToDisableDuringLoading;
    public bool pauseGameDuringInitialLoading;
    public bool liftBuriedPlayerOnStart = true;
    [Tooltip("Při teleportu podrží pohyb hráče, než bude k dispozici kolize nového sloupce.")]
    public bool holdMovementForMissingCollision = true;
    [Range(0,120)] public int loadingSettleFrames = 2;
    [Range(0f,30f)] public float loadingSettleSeconds = .1f;
    public bool initialLoadingFinished { get; private set; }
    public float initialLoadingProgress { get; private set; }

    [Header("Reliéf • údolí, hřebeny a vrstvy")]
    public float baseHeight = 40f, minimumTerrainHeight = 7f;
    [Min(0f)] public float hillHeight = 18f, mountainRelief = 26f, fineRelief = 1.4f;
    [Min(.0001f)] public float noiseScale = .022f;
    [Range(1,7)] public int octaves = 4;
    [Min(32f)] public float biomeSize = 240f;
    [Range(0f,.45f)] public float biomeBlend = .18f, biomeWarp = .24f;
    [Min(.1f)] public float grassDepth = 1.5f, dirtDepth = 4f;
    [Min(1)] public int bedrockThickness = 4;
    public Color dirtColor = new Color(.42f,.285f,.18f), stoneColor = new Color(.43f,.46f,.48f);
    public Color mountainSurface = new Color(.45f,.50f,.44f), taigaSurface = new Color(.22f,.40f,.29f);
    public Color desertSurface = new Color(.82f,.67f,.40f), swampSurface = new Color(.32f,.40f,.22f);
    public Color plainsSurface = new Color(.43f,.61f,.28f), snowColor = new Color(.87f,.93f,.97f);
    [Range(.2f,.98f)] public float snowLine = .73f;
    [Range(.01f,.35f)] public float snowTransition = .16f;
    [Tooltip("Volitelný neprůhledný vertex-color materiál. Prázdné pole použije osvětlenou paletu.")]
    public Material material;

    [Header("Jeskyně • chodby, rozšíření a vchody")]
    public bool generateCaves = true;
    [Min(16)] public int regionSize = 128;
    [Range(0,60)] public int cavesPerRegion = 12;
    public Vector2 caveRadius = new Vector2(2.3f,4.1f);
    public Vector2Int caveLength = new Vector2Int(50,110);
    [Range(0f,1f)] public float entranceChance = .32f;
    [Min(0f)] public float chamberExpansion = 2.8f;

    [Header("Voda • hladina, koryta a břehy (v buňkách)")]
    public bool generateWater = true;
    public float waterLevel = 34f;
    [Min(24f)] public float riverSpacing = 144f;
    [Min(0f)] public float riverWidth = 13f, riverDepth = 3.2f;
    public Material waterMaterial;
    public Color waterColor = new Color(.16f,.47f,.56f,.76f);

    [Header("Podrost a stromy • vlastní modely podle biomu")]
    public BiomePrefab[] mountainPrefabs, taigaPrefabs, desertPrefabs, swampPrefabs, plainsPrefabs;
    public bool proceduralModels = true;
    [Range(0,12)] public int decorationDistance = 3;
    [Range(0,64)] public int maxDecorationsPerChunk = 12;
    [Range(0f,1f)] public float treeChance = .62f, plantChance = .76f;
    [Min(0f)] public float treeSpacing = 3.2f;
    public Vector2 treeScaleRange = new Vector2(.78f,1.28f), plantScaleRange = new Vector2(.78f,1.28f);
    [Header("Tráva • trsy, výška a textury")]
    public bool enableGrassPatches = true;
    public Texture2D[] grassTextures = new Texture2D[0];
    public Color grassColor = new Color(.35f,.58f,.24f);
    [Range(0,24)] public int grassPatchesPerChunk = 4;
    [Range(0,120)] public int grassBladesPerPatch = 18;
    [Range(0f,1f)] public float emptyGrassChunkChance = .12f, grassMaxSlope = .28f;
    [Min(.1f)] public float grassPatchRadius = 4f;
    public Vector2 grassHeightRange = new Vector2(.34f,.92f);
    [Range(0,500)] public int maxGrassPerChunk = 72, maxGrassTuftsPerChunkPerformance = 60;
    [Header("Stavby • povrch a podzemí")]
    public GameObject[] surfaceStructures, undergroundStructures;
    [Range(0f,1f)] public float surfaceStructureChance = .09f, caveStructureChance = .22f;
    [Min(.1f)] public float structureFootprint = 2.1f, caveStructureClearance = 3.3f;
    [Range(0f,1f)] public float structureMinUp = .91f;

    [Header("Rudy • spodní strana modelu na stěně jeskyně")]
    public bool enableIntegratedOres = true;
    [Range(0,12)] public int oreGenerationDistance = 2;
    public OreNode coalOrePrefab, ironOrePrefab, goldOrePrefab, rubyOrePrefab;
    [Range(0,30)] public int coalPerChunk = 5, ironPerChunk = 3, goldPerChunk = 1, rubyPerChunk = 1;
    public Vector2 coalHeightRange = new Vector2(12,52), ironHeightRange = new Vector2(10,42);
    public Vector2 goldHeightRange = new Vector2(6,33), rubyHeightRange = new Vector2(5,24);
    [Min(0f)] public float oreMinimumDepthBelowSurface = 3f, oreFloorInset = .055f, oreSpacing = 1.1f;
    [Range(0f,.85f)] public float oreWallMaxUp = .62f;
    public Vector2 oreScaleRange = new Vector2(.75f,1.2f);

    [Header("Vítr • tráva a koruny")]
    public bool enableIntegratedWind = true;
    public Vector2 windDirection = new Vector2(.9f,.35f);
    [Range(0f,3f)] public float windStrength = .55f, windGustStrength = .28f, windGustFrequency = .16f;
    [Range(0f,12f)] public float treeBendDegrees = 2.1f;
    [Range(0f,1f)] public float grassBend = .28f;
    [Header("Počasí • déšť a sníh")]
    public SkyWeather weather = SkyWeather.Automatic;
    public Vector2 weatherDurationSeconds = new Vector2(90f,220f);
    [Range(0f,1f)] public float precipitationChance = .5f, lowlandSnowChance = .12f;
    [Min(.1f)] public float weatherFadeSeconds = 6f, precipitationRadius = 12f, precipitationHeight = 10f;
    [Min(0f)] public float rainRate = 360f, snowRate = 95f;
    [Min(.1f)] public float rainSpeed = 14f, snowSpeed = 2.1f;
    [Range(32,5000)] public int particleLimit = 1400;
    public Vector2 raindropSize = new Vector2(.035f,.066f), snowflakeSize = new Vector2(.045f,.13f);
    public Color rainColor = new Color(.62f,.80f,.89f,.65f), snowflakeColor = new Color(.92f,.95f,1f,.90f);
    public bool shelterStopsWeather = true;
    public LayerMask shelterMask = ~0;
    [Header("Obloha")]
    public bool createDayNightCycle = true;

    sealed class View
    {
        internal Vector2Int key;
        internal GameObject root, habitat, ores;
        internal Mesh ground, water, grass;
        internal MeshCollider collider;
        internal SurvivalSites sites;
        internal int revision;
    }
    sealed class Build
    {
        internal Vector2Int key;
        internal int revision;
        internal SurvivalColumnBuilder builder;
        internal Task task;
        internal volatile bool stop;
        internal bool ready;
        internal Exception failure;
        internal void Run() { try { while (!stop && !builder.Step()) { } } catch (Exception e) { failure = e; } }
    }
    sealed class DetailWork
    {
        internal GameObject lifetime;
        internal IEnumerator steps;
    }
    readonly Dictionary<Vector2Int,View> live = new Dictionary<Vector2Int,View>();
    readonly Dictionary<Vector2Int,View> staged = new Dictionary<Vector2Int,View>();
    readonly HashSet<Vector2Int> editGroup = new HashSet<Vector2Int>();
    readonly Dictionary<Vector2Int,List<SurvivalBrush>> edits = new Dictionary<Vector2Int,List<SurvivalBrush>>();
    readonly List<Build> jobs = new List<Build>();
    readonly Stack<SurvivalColumnBuilder.Buffers> availableBuffers = new Stack<SurvivalColumnBuilder.Buffers>();
    static readonly Unity.Profiling.ProfilerMarker uploadMarker = new Unity.Profiling.ProfilerMarker("SurvivalWorld.Upload");
    static readonly Unity.Profiling.ProfilerMarker physicsMarker = new Unity.Profiling.ProfilerMarker("SurvivalWorld.Collider");
    readonly List<Vector2Int> wanted = new List<Vector2Int>(), trim = new List<Vector2Int>();
    readonly Queue<View> physicsQueue = new Queue<View>();
    readonly HashSet<Collider> terrainColliders = new HashSet<Collider>();
    readonly Queue<DetailWork> detailQueue = new Queue<DetailWork>();
    readonly Dictionary<Behaviour,bool> locked = new Dictionary<Behaviour,bool>();
    readonly HashSet<string> mined = new HashSet<string>(), chopped = new HashSet<string>();
    static readonly SurvivalBrush[] NoEdits = new SurvivalBrush[0];
    readonly Stopwatch timer = new Stopwatch();
    SurvivalWorldField field;
    SurvivalHabitatModels art;
    TerrainWater waterSystem;
    internal GameObject CreateGeneratedWaterCellVisual(Vector2Int key,Vector3Int cell)
    {
        var go=AddMeshChild(live[key].root.transform,"Vylitá voda",art.Get(SurvivalHabitatModels.Form.WaterCell),liquidMaterial);
        go.transform.localPosition=new Vector3(cell.x-key.x*Size,cell.y,cell.z-key.y*Size);
        return go;
    }
    Material groundMaterial, liquidMaterial;
    Texture2D palette;
    Vector2Int centre;
    int nextWanted, settleFrame = -1, radiusLast = -1;
    float settleTime, savedTimeScale;
    bool started, refresh = true, frozeTime;
    [SerializeField, HideInInspector] int terrainFormat;
    int Radius { get { return Mathf.Clamp(Mathf.Min(renderDistance,maxActiveRenderDistance),1,24); } }
    int Dist(Vector2Int a) { return (a - centre).sqrMagnitude; }
    bool Near(Vector2Int k) { int r = Mathf.Max(0,colliderDistance); return Dist(k) <= r*r; }
    int Version(Vector2Int k) { return edits.TryGetValue(k,out var list) ? list.Count : 0; }

    void Start()
    {
        if (terrainFormat == 0 && Mathf.Approximately(minimumTerrainHeight,50f) &&
            Mathf.Approximately(baseHeight,40f) && Mathf.Approximately(waterLevel,34f))
            minimumTerrainHeight = 7f;
        terrainFormat = 1;
        cellSize = Mathf.Max(.1f,cellSize);
        if (randomSeed) seed = UnityEngine.Random.Range(0,int.MaxValue);
        if (player == null && Camera.main != null) player = Camera.main.transform;
        field = new SurvivalWorldField(this);
        groundMaterial = material;
        if (groundMaterial == null || groundMaterial.renderQueue > (int)RenderQueue.GeometryLast || !groundMaterial.shader.isSupported)
        {
            groundMaterial = SurvivalHabitatModels.Lit("Krajina");
            palette = new Texture2D(65,65,TextureFormat.RGBA32,false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[65*65];
            for (int y=0;y<65;y++) for (int x=0;x<65;x++) pixels[y*65+x] = field.PaletteColor(x/64f,y/64f);
            palette.SetPixels32(pixels); palette.Apply(false,true);
            SurvivalHabitatModels.Albedo(groundMaterial,palette);
        }
        art = new SurvivalHabitatModels(this);
        liquidMaterial = waterMaterial != null ? waterMaterial : art.WaterMaterial;
        waterSystem = GetComponent<TerrainWater>();
        if (waterSystem == null) waterSystem = gameObject.AddComponent<TerrainWater>();
        waterSystem.Bind(this);
        if (createDayNightCycle && FindObjectOfType<DayNightCycle>() == null) gameObject.AddComponent<DayNightCycle>();
        InitWeather();
        started = true;
        LockLoading();
    }

    void LockLoading()
    {
        if (playerMovementDuringLoading == null && player != null)
            playerMovementDuringLoading = player.GetComponentInParent<playermovement>();
        Lock(playerMovementDuringLoading);
        if (additionalScriptsToDisableDuringLoading != null)
            foreach (var b in additionalScriptsToDisableDuringLoading) Lock(b);
        if (pauseGameDuringInitialLoading && !frozeTime)
        { savedTimeScale = Time.timeScale; Time.timeScale = 0f; frozeTime = true; }
    }
    void Lock(Behaviour b)
    {
        if (b == null || b == this || locked.ContainsKey(b)) return;
        locked.Add(b,b.enabled); b.enabled = false;
    }
    void Unlock()
    {
        foreach (var pair in locked) if (pair.Key != null) pair.Key.enabled = pair.Value;
        locked.Clear();
        if (frozeTime) { Time.timeScale = savedTimeScale; frozeTime = false; }
    }
    void OnEnable() { if (started && !initialLoadingFinished) LockLoading(); refresh = true; }
    void OnDisable()
    {
        foreach (var j in jobs) j.stop = true;
        jobs.Clear(); Unlock(); StopWeather(); refresh = true;
    }

    void Update()
    {
        if (!started) return;
        Vector2Int key = Column(GridPoint(player != null ? player.position : transform.position));
        if (refresh || key != centre || radiusLast != Radius) Reschedule(key);
        timer.Restart();
        RetireOne();
        PumpJobs();
        double physicsDeadline = timer.Elapsed.TotalMilliseconds + Mathf.Max(.2f,colliderBudgetMs);
        for (int i=0; physicsQueue.Count>0 && i<Mathf.Max(1,maxCollidersPerFrame) && timer.Elapsed.TotalMilliseconds<physicsDeadline; )
        {
            var v = physicsQueue.Dequeue();
            if (v.root == null || !live.TryGetValue(v.key,out var current) || current != v) continue;
            if (Near(v.key) && v.collider == null) { AddPhysics(v); i++; }
        }
        int steps = 0;
        while (detailQueue.Count > 0 && steps++ < Mathf.Max(1,decorationStepsPerFrame) && timer.Elapsed.TotalMilliseconds < msPerFrame)
        {
            var work = detailQueue.Dequeue();
            if (work.lifetime != null && work.lifetime.activeInHierarchy && work.steps.MoveNext()) detailQueue.Enqueue(work);
        }
        if (!initialLoadingFinished) CheckLoading();
        else
        {
            if (holdMovementForMissingCollision && playerMovementDuringLoading!=null &&
                (!live.TryGetValue(centre,out var underfoot) || underfoot.collider==null)) Lock(playerMovementDuringLoading);
            else if (locked.Count>0) Unlock();
        }
        UpdateAtmosphere();
    }

    void Reschedule(Vector2Int key)
    {
        centre = key; radiusLast = Radius; refresh = false; wanted.Clear(); nextWanted = 0;
        int r = Radius;
        for (int x=-r;x<=r;x++) for (int z=-r;z<=r;z++)
            if (x*x+z*z<=r*r) wanted.Add(key+new Vector2Int(x,z));
        wanted.Sort((a,b) => { int d = Dist(a).CompareTo(Dist(b)); return d!=0 ? d : (a.x!=b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y)); });
        trim.Clear(); physicsQueue.Clear();
        foreach (var pair in live)
        {
            View v = pair.Value;
            if (Dist(pair.Key) > (r+1)*(r+1)) { trim.Add(pair.Key); continue; }
            if (!Near(pair.Key) && v.collider != null) { terrainColliders.Remove(v.collider); v.collider.enabled = false; Destroy(v.collider); v.collider = null; }
            DetailRange(v);
        }
        foreach (var k in wanted) if (Near(k) && live.TryGetValue(k,out var v) && v.collider == null) physicsQueue.Enqueue(v);
        foreach (var j in jobs) if (Dist(j.key)>r*r && !editGroup.Contains(j.key)) j.stop = true;
        if (!initialLoadingFinished) settleFrame = -1;
    }
    void RetireOne()
    {
        if (trim.Count == 0) return;
        int i = trim.Count-1; var k = trim[i]; trim.RemoveAt(i);
        if (live.TryGetValue(k,out var v)) { DisposeView(v); live.Remove(k); }
        if (staged.TryGetValue(k,out var s)) { DisposeView(s); staged.Remove(k); }
        editGroup.Remove(k);
    }
    bool IsBuilding(Vector2Int k) { foreach (var j in jobs) if (j.key==k) return true; return false; }
    bool Next(out Vector2Int key)
    {
        foreach (var k in editGroup)
            if (!staged.ContainsKey(k) && !IsBuilding(k)) { key=k; return true; }
        while (nextWanted<wanted.Count)
        {
            key=wanted[nextWanted++];
            if (!live.ContainsKey(key) && !IsBuilding(key)) return true;
        }
        key=default(Vector2Int); return false;
    }
    void PumpJobs()
    {
        int uploads = 0;
        for (int i=0;i<jobs.Count && uploads<Mathf.Max(1,maxColumnsPerFrame); )
        {
            var j = jobs[i];
            if (j.task != null && !j.task.IsCompleted) { i++; continue; }
            if (!j.stop && j.task == null && !j.ready && timer.Elapsed.TotalMilliseconds < msPerFrame) j.ready = j.builder.Step();
            if (!j.stop && j.task == null && !j.ready) { i++; continue; }
            jobs.RemoveAt(i);
            // A completed task has released its lattice; new jobs can reuse these large arrays.
            availableBuffers.Push(j.builder.buffers);
            if (j.failure != null) { Debug.LogException(j.failure,this); enabled=false; return; }
            if (j.stop || j.revision != Version(j.key))
            { if (!live.ContainsKey(j.key)) refresh=true; continue; }
            if (Dist(j.key) > Radius*Radius && !editGroup.Contains(j.key)) continue;
            View built;
            using (uploadMarker.Auto()) { built = CreateView(j); }
            if (editGroup.Contains(j.key)) staged.Add(j.key,built);
            else Activate(built);
            uploads++;
            if (timer.Elapsed.TotalMilliseconds >= msPerFrame) break;
        }
        if (editGroup.Count > 0 && staged.Count == editGroup.Count)
        {
            // All touching columns switch geometry together. Old colliders remain valid until this point.
            foreach (var pair in staged) Activate(pair.Value);
            staged.Clear(); editGroup.Clear();
        }
        bool threaded = backgroundGeneration;
#if UNITY_WEBGL && !UNITY_EDITOR
        threaded = false;
#endif
        int capacity = threaded ? Mathf.Clamp(workerCount,1,4) : 1;
        while (jobs.Count<capacity && Next(out var k))
        {
            SurvivalBrush[] snapshot = edits.TryGetValue(k,out var list) ? list.ToArray() : NoEdits;
            var buffers=availableBuffers.Count>0?availableBuffers.Pop():null;
            var j = new Build { key=k, revision=snapshot.Length, builder=new SurvivalColumnBuilder(field,k,snapshot,buffers) };
            jobs.Add(j);
            if (threaded) j.task=Task.Run((Action)j.Run);
        }
        if (!threaded && jobs.Count>0)
        {
            var j=jobs[0];
            while (!j.ready && !j.stop && timer.Elapsed.TotalMilliseconds<msPerFrame) j.ready=j.builder.Step();
        }
    }

    View CreateView(Build job)
    {
        var v = new View { key=job.key, revision=job.revision, sites=job.builder.sites };
        v.root=new GameObject("Krajina " + v.key.x + ", " + v.key.y);
        v.root.SetActive(false); v.root.layer=gameObject.layer;
        var t=v.root.transform; t.SetParent(transform,false);
        t.localPosition=new Vector3(v.key.x*field.size,0,v.key.y*field.size)*cellSize;
        t.localScale=Vector3.one*cellSize;
        v.ground=job.builder.ground.Upload("Terén");
        if (v.ground!=null)
        {
            v.root.AddComponent<MeshFilter>().sharedMesh=v.ground;
            v.root.AddComponent<MeshRenderer>().sharedMaterial=groundMaterial;
            if (Near(v.key) && (editGroup.Contains(v.key) || v.key==centre)) AddPhysics(v);
        }
        v.water=job.builder.water.Upload("Vodní hladina");
        if (v.water!=null) AddMeshChild(t,"Voda",v.water,liquidMaterial);
        return v;
    }
    static GameObject AddMeshChild(Transform parent,string label,Mesh mesh,Material mat)
    {
        var go=new GameObject(label); go.transform.SetParent(parent,false);
        go.layer=parent.gameObject.layer; go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=mat; return go;
    }
    void AddPhysics(View v)
    {
        if (v.ground==null || v.collider!=null) return;
        using (physicsMarker.Auto())
        {
            v.collider=v.root.AddComponent<MeshCollider>();
            if (v.collider.sharedMesh!=v.ground) v.collider.sharedMesh=v.ground;
        }
        terrainColliders.Add(v.collider);
    }
    void Activate(View v)
    {
        if (live.TryGetValue(v.key,out var previous)) DisposeView(previous);
        live[v.key]=v; v.root.SetActive(true);
        if (Near(v.key) && v.collider==null) physicsQueue.Enqueue(v);
        DetailRange(v); waterSystem.Show(v.key,v.root.transform);
    }
    void DisposeView(View v)
    {
        if (v.collider!=null) terrainColliders.Remove(v.collider);
        if (v.root!=null) { v.root.SetActive(false); Destroy(v.root); }
        if (v.ground!=null) Destroy(v.ground);
        if (v.water!=null) Destroy(v.water);
        if (v.grass!=null) Destroy(v.grass);
    }
    void DetailRange(View v)
    {
        int d=Dist(v.key);
        SetDetail(v,false,d<=decorationDistance*decorationDistance);
        SetDetail(v,true,enableIntegratedOres && d<=oreGenerationDistance*oreGenerationDistance);
    }
    void SetDetail(View v,bool ore,bool on)
    {
        GameObject go=ore?v.ores:v.habitat;
        if (on == (go!=null)) return;
        if (on)
        {
            go=new GameObject(ore?"Rudy":"Vegetace a stavby"); go.layer=v.root.layer; go.transform.SetParent(v.root.transform,false);
            if (ore) v.ores=go; else v.habitat=go;
            detailQueue.Enqueue(new DetailWork { lifetime=go, steps=ore?PlaceOres(v,go.transform):PlaceHabitat(v,go.transform) });
        }
        else
        {
            go.SetActive(false); Destroy(go);
            if (ore) v.ores=null; else { v.habitat=null; if(v.grass!=null) Destroy(v.grass); v.grass=null; }
        }
    }
    void CheckLoading()
    {
        int target=Mathf.Clamp(initialColumnsToPrepare,1,wanted.Count), ready=0;
        for (int i=0;i<target;i++)
            if (live.TryGetValue(wanted[i],out var v) && (!Near(v.key) || v.collider!=null)) ready++;
        initialLoadingProgress=.96f*ready/target;
        if (ready!=target) { settleFrame=-1; return; }
        if (settleFrame<0) { settleFrame=Time.frameCount; settleTime=Time.unscaledTime; }
        if (Time.frameCount-settleFrame<loadingSettleFrames || Time.unscaledTime-settleTime<loadingSettleSeconds) return;
        if (liftBuriedPlayerOnStart && player!=null)
        {
            var controller=player.GetComponentInParent<CharacterController>();
            if (controller!=null && DensityAtWorld(controller.bounds.center)<0f)
            {
                Vector3 foot=controller.bounds.center-Vector3.up*controller.bounds.extents.y;
                float y=TopWorldAt(foot)+.12f;
                if (y>foot.y)
                {
                    bool was=controller.enabled; controller.enabled=false;
                    controller.transform.position+=Vector3.up*(y-foot.y); controller.enabled=was;
                }
            }
        }
        initialLoadingFinished=true; initialLoadingProgress=1f; Unlock();
    }
    void OnGUI()
    {
        if (!showInitialLoadingScreen || initialLoadingFinished) return;
        float width=Mathf.Min(Screen.width*.72f,400f), x=(Screen.width-width)*.5f, y=Screen.height*.56f;
        Color old=GUI.color;
        GUI.color=new Color(.06f,.09f,.095f,1); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
        GUI.color=Color.white; GUI.Label(new Rect(x,y-35,width,28),loadingTitle+"   "+Mathf.RoundToInt(initialLoadingProgress*100)+" %");
        GUI.color=new Color(.23f,.29f,.28f); GUI.DrawTexture(new Rect(x,y,width,5),Texture2D.whiteTexture);
        GUI.color=new Color(.63f,.81f,.53f); GUI.DrawTexture(new Rect(x,y,width*initialLoadingProgress,5),Texture2D.whiteTexture);
        GUI.color=old;
    }

    internal Vector3 GridPoint(Vector3 world) { return transform.InverseTransformPoint(world)/Mathf.Max(.1f,cellSize); }
    internal Vector2Int Column(Vector3 grid)
    {
        int s=field!=null?field.size:Mathf.Max(1,chunkSize);
        return new Vector2Int(Mathf.FloorToInt(grid.x/s),Mathf.FloorToInt(grid.z/s));
    }
    internal float Sea { get { return field!=null?field.sea:waterLevel; } }
    internal int Size { get { return field!=null?field.size:chunkSize; } }
    public BiomeType GetBiome(float x,float z) { return field==null?BiomeType.Plains:SurvivalWorldField.Biome(field.Surface(x,z).climate); }
    public float SurfaceWorldHeightAt(Vector3 worldPosition)
    {
        var p=GridPoint(worldPosition); if (field==null) return transform.position.y+baseHeight*cellSize;
        p.y=field.Surface(p.x,p.z).surface; return transform.TransformPoint(p*cellSize).y;
    }
    internal bool Loaded(Vector2Int key) { return live.ContainsKey(key); }
    internal float Top(Vector3 p)
    {
        var k=Column(p);
        return live.TryGetValue(k,out var v)?v.sites.Top(p.x-k.x*Size,p.z-k.y*Size):
               field!=null?field.Surface(p.x,p.z).surface:baseHeight;
    }
    internal float TopWorldAt(Vector3 world)
    { var p=GridPoint(world); p.y=Top(p); return transform.TransformPoint(p*cellSize).y; }
    float Density(Vector3 p)
    {
        if (field==null) return p.y-baseHeight;
        float d=field.Density(field.Sample(p.x,p.z),p.y);
        if (p.y>field.bedrock && p.y<field.height && edits.TryGetValue(Column(p),out var list))
            foreach (var brush in list) d=brush.Apply(p,d);
        return d;
    }
    internal float DensityAtWorld(Vector3 p) { return Density(GridPoint(p)); }
    public bool IsBedrockAt(Vector3 p) { return GridPoint(p).y <= (field!=null?field.bedrock:bedrockThickness)+.5f; }
    public bool IsDirtAt(Vector3 p)
    {
        if (field==null || IsBedrockAt(p)) return false;
        var q=GridPoint(p); return field.Surface(q.x,q.z).surface-q.y<=field.grassDepth+field.dirtDepth;
    }
    public bool IsTerrainCollider(Collider c)
    {
        return c!=null && terrainColliders.Contains(c);
    }
    public void MarkIntegratedOreMined(string key) { if (!string.IsNullOrEmpty(key)) mined.Add(key); }
    internal void RememberTree(string key) { if (!string.IsNullOrEmpty(key)) chopped.Add(key); }
    public void Modify(Vector3 worldPos,float radius,bool dig) { Edit(worldPos,worldPos,radius,dig); }
    public void Tunnel(Vector3 worldStart,Vector3 direction,float length,float radius)
    { Edit(worldStart,worldStart+direction.normalized*Mathf.Max(0f,length),radius,true); }
    void Edit(Vector3 a,Vector3 b,float radius,bool dig)
    {
        if (field==null || radius<=0f) return;
        var brush=new SurvivalBrush { start=GridPoint(a),end=GridPoint(b),radius=radius/Mathf.Max(.001f,cellSize*Mathf.Abs(transform.lossyScale.x)),dig=dig };
        Vector3 padding=Vector3.one*(brush.radius+3f);
        var min=Column(Vector3.Min(brush.start,brush.end)-padding); var max=Column(Vector3.Max(brush.start,brush.end)+padding);
        for (int x=min.x;x<=max.x;x++) for (int z=min.y;z<=max.y;z++)
        {
            var k=new Vector2Int(x,z);
            if (!edits.TryGetValue(k,out var list)) edits.Add(k,list=new List<SurvivalBrush>());
            list.Add(brush);
            if (live.ContainsKey(k)) editGroup.Add(k);
            if (staged.TryGetValue(k,out var stale)) { DisposeView(stale); staged.Remove(k); }
            foreach (var j in jobs) if (j.key==k) j.stop=true;
        }
    }
    void OnDestroy()
    {
        foreach(var j in jobs) j.stop=true;
        Unlock(); DisposeAtmosphere();
        foreach(var v in live.Values) DisposeView(v);
        foreach(var v in staged.Values) DisposeView(v);
        if (art!=null) art.Dispose();
        if (groundMaterial!=null && groundMaterial!=material) Destroy(groundMaterial);
        if (palette!=null) Destroy(palette);
    }
}
