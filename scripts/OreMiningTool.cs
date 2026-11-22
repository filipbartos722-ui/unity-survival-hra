using UnityEngine;

// Separate input handler. Add to player. Stone Pickaxe ID=1; Iron Pickaxe ID=39 by default.
public class OreMiningTool : MonoBehaviour
{
    public InventorySystem inventory;
    public Camera viewCamera;
    public int stonePickaxeId = 1;
    public int ironPickaxeId = 39;
    public float reach = 5f;
    public float hitCooldown = .5f;
    public KeyCode mineButton = KeyCode.Mouse0;
    float nextHit;

    void Update()
    {
        if (inventory == null || inventory.IsAnyMenuOpen || viewCamera == null) return;
        if (!Input.GetKey(mineButton) || Time.time < nextHit) return;
        int tier = inventory.SelectedItemId == ironPickaxeId ? 2 : inventory.SelectedItemId == stonePickaxeId ? 1 : 0;
        if (tier == 0) return;
        if (!Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out RaycastHit hit, reach)) return;
        OreNode ore = hit.collider.GetComponentInParent<OreNode>();
        if (ore == null) return;
        nextHit = Time.time + Mathf.Max(.1f, hitCooldown);
        ore.Hit(tier, inventory);
    }
}
