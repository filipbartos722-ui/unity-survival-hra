using UnityEngine;

// Empty and full buckets are separate inventory IDs. Right-click to interact.
public class BucketTool : MonoBehaviour
{
    public InventorySystem inventory;
    public Camera viewCamera;
    public WaterSystem water;
    public SurvivalWorld.Generated.WaterSystem terrainWater;
    public int emptyBucketId = 40;
    public int waterBucketId = 41;
    public float reach = 5f;
    void Start()
    {
        if (water == null) water = FindObjectOfType<WaterSystem>();
        if (terrainWater == null) terrainWater = FindObjectOfType<SurvivalWorld.Generated.WaterSystem>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (inventory == null) inventory = GetComponentInParent<InventorySystem>();
    }
    void Update()
    {
        if (inventory == null || viewCamera == null || inventory.IsAnyMenuOpen || !Input.GetMouseButtonDown(1)) return;
        if (terrainWater == null) terrainWater = FindObjectOfType<SurvivalWorld.Generated.WaterSystem>();
        if (water == null && terrainWater == null) return;
        int selected = inventory.SelectedItemId;
        if (selected != emptyBucketId && selected != waterBucketId) return;
        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        bool solid = Physics.Raycast(ray, out RaycastHit hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        int result = selected == emptyBucketId ? waterBucketId : emptyBucketId;
        if (inventory.counts[inventory.selectedSlot] > 1 && !inventory.CanAddItem(result)) return;
        if (selected == emptyBucketId)
        {
            float end = solid ? Mathf.Max(0f, hit.distance - .002f) : reach;
            float size = terrainWater != null ? terrainWater.cellSize : water.cellSize;
            if (water != null) size = Mathf.Min(size, water.cellSize);
            float step = Mathf.Max(.025f, size * .16f);
            for (float d = 0f; ; d = Mathf.Min(end, d + step))
            {
                Vector3 point = ray.GetPoint(d);
                if (terrainWater != null && terrainWater.TakeAt(point)) break;
                if (water != null)
                {
                    Vector3Int cell = water.WorldToCell(point);
                    if (water.HasWater(cell) && water.TakeWater(cell)) break;
                }
                if (d >= end) return;
            }
        }
        else
        {
            if (!solid) return;
            if (water != null)
            {
                Vector3Int cell = water.WorldToCell(hit.point + hit.normal * water.cellSize * .55f);
                if (!water.AddWater(cell)) return;
            }
            else
            {
                Vector3Int cell = terrainWater.WorldToCell(hit.point + hit.normal * terrainWater.cellSize * .55f);
                if (!terrainWater.AddWater(cell)) return;
            }
        }
        inventory.RemoveFromSlot(inventory.selectedSlot);
        inventory.TryAddItem(result);
    }
}
