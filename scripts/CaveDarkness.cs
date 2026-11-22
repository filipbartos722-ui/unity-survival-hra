using UnityEngine;

/// <summary>
/// Nastav na kameru hrace. Pouziva hloubku pod povrchem a plynule fog.
/// Built-in/URP fog je globalni: pri pohledu z jeskyne ven ztmavi i vzdalenou oblohu.
/// </summary>
public class CaveDarkness : MonoBehaviour
{
    public SmoothTerrain terrain;
    [Min(0f)] public float startDepth = 5f;
    [Min(1f)] public float fullDarkDepth = 35f;
    [Range(0f, 0.2f)] public float maxFogDensity = 0.095f;
    [Min(0.1f)] public float transitionSpeed = 2.5f;
    public Color caveFogColor = new Color(0.015f, 0.02f, 0.03f, 1f);
    bool originalFog;
    FogMode originalMode;
    Color originalColor;
    float originalDensity;
    float current;

    void Start()
    {
        if (terrain == null) terrain = FindObjectOfType<SmoothTerrain>();
        originalFog = RenderSettings.fog;
        originalMode = RenderSettings.fogMode;
        originalColor = RenderSettings.fogColor;
        originalDensity = RenderSettings.fogDensity;
    }

    void LateUpdate()
    {
        if (terrain == null) return;
        // Povrch zjistime z terenu, nikoliv z absolutni vysky Y.
        float surface = terrain.SurfaceWorldHeightAt(transform.position);
        float depth = Mathf.Max(0f, surface - transform.position.y);
        float target = Mathf.InverseLerp(startDepth, Mathf.Max(startDepth + 0.01f, fullDarkDepth), depth);
        current = Mathf.MoveTowards(current, target, Time.deltaTime * transitionSpeed);
        if (current < 0.001f)
        {
            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalMode;
            RenderSettings.fogColor = originalColor;
            RenderSettings.fogDensity = originalDensity;
            return;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(originalColor, caveFogColor, current);
        RenderSettings.fogDensity = Mathf.Lerp(originalFog ? originalDensity : 0f, maxFogDensity, current);
    }

    void OnDisable()
    {
        RenderSettings.fog = originalFog;
        RenderSettings.fogMode = originalMode;
        RenderSettings.fogColor = originalColor;
        RenderSettings.fogDensity = originalDensity;
    }
}
