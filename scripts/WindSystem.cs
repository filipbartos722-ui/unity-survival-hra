using UnityEngine;

// One global wind source. Shader-based foliage can read these global properties.
public class WindSystem : MonoBehaviour
{
    public bool enableWind = true;
    [Range(0, 2)] public float strength = .5f;
    public Vector2 direction = Vector2.right;
    public float gustFrequency = .15f;
    public float gustStrength = .3f;
    public bool enableGusts = true;
    public float CurrentStrength { get; private set; }
    void Update()
    {
        float gust = enableGusts ? (Mathf.PerlinNoise(Time.time * Mathf.Max(0, gustFrequency), 3.1f) * 2 - 1) * gustStrength : 0;
        CurrentStrength = enableWind ? Mathf.Max(0, strength + gust) : 0;
        Vector2 dir = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
        Shader.SetGlobalVector("_SurvivalWind", new Vector4(dir.x, 0, dir.y, CurrentStrength));
        Shader.SetGlobalFloat("_SurvivalWindTime", Time.time);
    }
}
