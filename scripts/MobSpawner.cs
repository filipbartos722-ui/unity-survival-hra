using UnityEngine;
using System.Collections.Generic;

// Lightweight proximity spawning. Use separate spawners for cows and bears.
// Put a spawner inside a biome region or attach it to a fixed world object.
public class MobSpawner : MonoBehaviour
{
    public GameObject mobPrefab;
    public Transform player;
    public int maxAlive = 5;
    public float spawnRadius = 35f;
    public float minimumDistanceFromPlayer = 12f;
    public float activePlayerRange = 90f;
    public float spawnInterval = 9f;
    public float groundProbeHeight = 100f;
    public LayerMask groundMask = ~0;
    public float maxSlope = 35f;
    readonly List<GameObject> alive = new List<GameObject>();
    float nextSpawn;

    void Update()
    {
        if (player == null)
        {
            SurvivalVitals vitals = FindObjectOfType<SurvivalVitals>();
            if (vitals != null) player = vitals.transform;
        }
        if (player == null || mobPrefab == null || Time.time < nextSpawn) return;
        nextSpawn = Time.time + Mathf.Max(1f, spawnInterval);
        if (Vector3.Distance(player.position, transform.position) > activePlayerRange) return;
        alive.RemoveAll(x => x == null);
        if (alive.Count >= Mathf.Max(0, maxAlive)) return;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 probe = transform.position + new Vector3(circle.x, groundProbeHeight, circle.y);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, groundProbeHeight * 2f, groundMask, QueryTriggerInteraction.Ignore)) continue;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope) continue;
            if (Vector3.Distance(hit.point, player.position) < minimumDistanceFromPlayer) continue;
            if (hit.collider.GetComponentInParent<MobAI>() != null) continue;
            GameObject mob = Instantiate(mobPrefab, hit.point + Vector3.up * 0.1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            alive.Add(mob);
            break;
        }
    }
}
