using UnityEngine;

// Hold left mouse to mine terrain or chop a tree. Keeps crafting table interaction.
public class TerrainMiner : MonoBehaviour
{
    public Camera cam;
    public InventorySystem inventory;
    public float range = 6f;
    public float radius = 1.5f;
    public float minRadius = 0.7f;
    public float maxRadius = 4f;
    public float tunnelLength = 20f;
    public KeyCode tunnelKey = KeyCode.E;
    public LayerMask mask = ~0;

    [Header("Timed Mining")]
    public float terrainMiningDuration = 3f;
    public float treeMiningDuration = 3f;
    [Header("Ore Mining")]
    public float secondsPerOreHit = 0.75f;
    public int stonePickaxeId = 1;
    public int ironPickaxeId = 39;
    public int stoneDropAmount = 4;
    public int stoneItemId = 4;
    [Header("Dirt Drops")]
    public int dirtItemId = 45;
    public int dirtDropAmount = 1;
    public bool allowHandDirtMining = true;
    public float dropHeight = 0.5f;

    [Header("Crosshair")]
    public bool showCrosshair = true;
    public float crosshairSize = 18f;
    public Color crosshairColor = Color.white;
    public Color interactColor = Color.green;

    [Header("Progress Bar")]
    public bool showProgressBar = true;
    public float progressBarWidth = 90f;
    public float progressBarHeight = 8f;
    public float progressBarOffsetY = 25f;
    public Color progressColor = Color.green;
    public Color progressBackgroundColor = new Color(0f, 0f, 0f, 0.7f);

    private bool aimingAtTable;
    private bool mining;
    private float elapsed;
    private Collider targetCollider;
    private TreeResource targetTree;
    private OreNode targetOre;
    private SmoothTerrain targetTerrain;
    private Vector3 targetPoint;
    private Vector3 targetNormal;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (inventory == null) inventory = GetComponentInParent<InventorySystem>();
    }

    void Update()
    {
        aimingAtTable = false;
        if (cam == null || inventory == null || inventory.IsAnyMenuOpen)
        {
            ResetMining();
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            ResetMining();
        }

        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
        {
            ResetMining();
            return;
        }

        CraftingTable table = hit.collider.GetComponentInParent<CraftingTable>();
        if (table != null && Vector3.Distance(cam.transform.position, table.transform.position) <= table.useDistance + 1f)
        {
            aimingAtTable = true;
            ResetMining();
            if (Input.GetMouseButtonDown(0)) inventory.OpenCrafting(table);
            return;
        }

        radius = Mathf.Clamp(radius + Input.mouseScrollDelta.y * 0.25f, minRadius, maxRadius);

        OreNode ore = hit.collider.GetComponentInParent<OreNode>();
        TreeResource tree = ore == null ? hit.collider.GetComponentInParent<TreeResource>() : null;
        SmoothTerrain terrain = tree == null && ore == null ? hit.collider.GetComponentInParent<SmoothTerrain>() : null;
        // Ruins and vegetation also belong to the streamed column hierarchy.
        if (terrain != null && !terrain.IsTerrainCollider(hit.collider)) terrain = null;

        // Right-click never modifies terrain. This prevents accidental terrain creation.
        // Tunnel shortcut remains available only when a pickaxe is equipped.
        if (terrain != null && (inventory.HasPickaxeEquipped || inventory.SelectedItemId == ironPickaxeId)
            && (Input.GetKeyDown(tunnelKey) || Input.GetMouseButtonDown(2)))
            terrain.Tunnel(hit.point, cam.transform.forward, tunnelLength, radius);

        int pickaxeTier = inventory.SelectedItemId == ironPickaxeId ? 2 : inventory.SelectedItemId == stonePickaxeId ? 1 : 0;
        bool validOre = ore != null && pickaxeTier >= ore.minimumPickaxeTier && pickaxeTier > 0;
        bool validTree = tree != null && inventory.HasAxeEquipped;
        bool terrainIsDirt = terrain != null && terrain.IsDirtAt(hit.point - hit.normal * 0.12f);
        bool terrainIsBedrock = terrain != null && terrain.IsBedrockAt(hit.point - hit.normal * 0.12f);
        bool validTerrain = terrain != null && !terrainIsBedrock &&
            (inventory.HasPickaxeEquipped || pickaxeTier == 2 ||
             (allowHandDirtMining && inventory.SelectedItemId == 0 && terrainIsDirt));
        if (!Input.GetMouseButton(0) || (!validTree && !validTerrain && !validOre))
        {
            if (!validTree && !validTerrain && !validOre) ResetMining();
            return;
        }

        // Keep mining while held. When a terrain chunk collider is rebuilt after
        // digging, do not force the player to release the mouse button.
        bool sameTerrainChain = mining && terrain != null && targetTerrain == terrain && targetOre == null && targetTree == null;
        bool sameOreChain = mining && ore != null && targetOre == ore;
        bool sameTreeChain = mining && tree != null && targetTree == tree;
        if (!mining || (!sameTerrainChain && !sameOreChain && !sameTreeChain))
        {
            mining = true;
            elapsed = 0f;
            targetCollider = hit.collider;
            targetTree = tree;
            targetOre = ore;
            targetTerrain = terrain;
        }

        targetPoint = hit.point;
        targetNormal = hit.normal;
        elapsed += Time.deltaTime;
        float duration = GetCurrentMiningDuration();
        if (elapsed < duration) return;

        if (validOre)
        {
            // Finish only after holding the button for the full duration.
            int hits = Mathf.Max(1, ore.hitsRequired);
            for (int i = 0; i < hits; i++)
            {
                if (ore == null) break;
                ore.Hit(pickaxeTier, inventory);
            }
        }
        else if (validTree)
        {
            // One completed hold chops the entire tree; drops are handled by TreeResource.
            tree.Chop(inventory);
        }
        else
        {
            // Bedrock is never rewarded, even if an old collider remains after a mesh update.
            Vector3 samplePoint = targetPoint - targetNormal * 0.12f;
            if (!terrain.IsBedrockAt(samplePoint))
            {
                bool dirt = terrain.IsDirtAt(samplePoint);
                terrain.Modify(targetPoint - targetNormal * (radius * 0.4f), radius, true);
                int dropId = dirt ? dirtItemId : stoneItemId;
                int dropCount = dirt ? dirtDropAmount : stoneDropAmount;
                if (dropId > 0 && dropCount > 0)
                    inventory.SpawnDroppedItem(dropId, dropCount,
                        targetPoint + Vector3.up * dropHeight, Vector3.up * 1.5f);
            }
        }
        // Do not latch completion until mouse release: the next terrain section
        // or ore node can be mined without another click. One operation per frame.
        // A new ore/tree is acquired on the next Update after destruction.
        if (validTerrain)
        {
            // Preserve excess time, but start a fresh cycle on the next frame.
            elapsed = Mathf.Max(0f, elapsed - duration);
            targetCollider = null;
        }
        else
        {
            ResetMining();
        }
    }

    void ResetMining()
    {
        mining = false;
        elapsed = 0f;
        targetCollider = null;
        targetTree = null;
        targetOre = null;
        targetTerrain = null;
    }

    // One identical progress bar for terrain, trees and ores.
    // It uses the same position, size, background and fill colors for every target.
    void OnGUI()
    {
        if (inventory == null || inventory.IsAnyMenuOpen) return;

        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;
        Color previous = GUI.color;

        if (showCrosshair)
        {
            float half = crosshairSize * 0.5f;
            GUI.color = aimingAtTable ? interactColor : crosshairColor;
            GUI.DrawTexture(new Rect(x - half, y - 1f, crosshairSize, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y - half, 2f, crosshairSize), Texture2D.whiteTexture);
        }

        // IMPORTANT: this bar is not dependent on showCrosshair.
        if (showProgressBar && mining)
        {
            float duration = GetCurrentMiningDuration();
            float fraction = Mathf.Clamp01(elapsed / duration);
            Rect background = new Rect(x - progressBarWidth * 0.5f,
                y + progressBarOffsetY, progressBarWidth, progressBarHeight);
            GUI.color = progressBackgroundColor;
            GUI.DrawTexture(background, Texture2D.whiteTexture);
            GUI.color = progressColor;
            GUI.DrawTexture(new Rect(background.x, background.y,
                background.width * fraction, background.height), Texture2D.whiteTexture);
        }
        GUI.color = previous;
    }

    float GetCurrentMiningDuration()
    {
        if (targetOre != null)
            return Mathf.Max(0.1f, targetOre.hitsRequired * secondsPerOreHit);
        if (targetTree != null)
            return Mathf.Max(0.1f, treeMiningDuration);
        return Mathf.Max(0.1f, terrainMiningDuration);
    }
}
