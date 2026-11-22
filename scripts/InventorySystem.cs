using UnityEngine;

// Item IDs: 0 Empty, 1 Stone Pickaxe, 2 Stone Axe, 3 Wood, 4 Stone, 5 Work Bench, 6 Jukebox.
public class InventorySystem : MonoBehaviour
{
    public enum ItemCategory { Material, Tool, Placeable, Bundle, Backpack, Other }

    [System.Serializable]
    public class ItemDefinition
    {
        [Min(6)] public int itemId = 6;
        public string displayName = "New Item";
        public Texture2D icon;
        public ItemCategory category = ItemCategory.Material;
        [Range(1, 69)] public int maxStack = 69;
        public GameObject droppedPrefab;
        public GameObject heldPrefab;
        public GameObject placedPrefab; // Building prefab used for placement and preview.
    }

    [Header("References")]
    public Camera playerCamera;
    public GameObject stonePickaxePrefab;
    public GameObject stoneAxePrefab;
    public GameObject droppedPickaxePrefab;
    public GameObject droppedAxePrefab;
    public GameObject droppedWoodPrefab;
    public GameObject droppedStonePrefab;
    public GameObject craftingTablePrefab;
    [Header("Campfire (Item ID 31)")]
    public GameObject campfirePrefab;
    [Header("Furnace (Item ID 42)")]
    public GameObject furnacePrefab;
    public Texture2D campfireIcon;
    [Header("Jukebox (Item ID 6)")]
    public GameObject jukeboxPrefab;
    public Texture2D jukeboxIcon;

    [Header("Survival Items (IDs 24-29)")]
    public Texture2D rawBeefIcon, steakIcon, spearIcon, backpackIcon, bundleIcon, bearClawIcon, leatherIcon;
    public GameObject rawBeefHeldPrefab, steakHeldPrefab, spearHeldPrefab, backpackHeldPrefab, bundleHeldPrefab;
    public GameObject rawBeefDropPrefab, steakDropPrefab, spearDropPrefab, backpackDropPrefab, bundleDropPrefab, leatherDropPrefab;
    [Header("Food & Spear")]
    public KeyCode eatKey = KeyCode.Mouse1;
    [Min(0f)] public float rawBeefHunger = 10f;
    [Min(0f)] public float rawBeefHealth = 5f;
    [Min(0f)] public float steakHunger = 30f;
    [Min(0f)] public float steakHealth = 10f;
    public float spearDamage = 25f;
    public float spearRange = 3.5f;
    [Header("Spear Attack Timing")]
    [Min(0.1f)] public float spearCooldown = 1f;
    [Range(0.05f, 0.95f)] public float spearHitMoment = 0.5f;
    [Min(0f)] public float spearThrustDistance = 0.35f;
    [Range(0f, 90f)] public float spearThrustAngle = 35f;
    private float spearAttackStart = -100f;
    private bool spearHitApplied;
    float nextSpearHit;
    [Header("Spear Cooldown UI")]
    public bool showSpearCooldownUI = true;
    public bool showSpearReadyText = true;
    [Header("Eating Countdown")]
    [Min(0.1f)] public float eatingDuration = 2f;
    private float eatingProgress;
    private int eatingSlot = -1;
    private int eatingItemId;


    [Header("Held Item View")]
    public Vector3 heldLocalPosition = new Vector3(0.48f, -0.42f, 0.85f);
    public Vector3 heldLocalRotation = new Vector3(8f, -25f, 15f);
    public Vector3 heldLocalScale = Vector3.one;
    [Header("Held Model Auto Fit")]
    public bool autoFitHeldModels = true;
    [Min(0.05f)] public float maxHeldModelSize = 0.43f;
    [Min(0.05f)] public float minHeldModelSize = 0.10f;
    [Range(0f, 1f)] public float heldPositionSmoothing = 0.18f;
    [Min(0f)] public float heldIdleBobbing = 0.008f;
    [Min(0.1f)] public float swingSpeed = 7f;
    [Range(0f, 85f)] public float swingAngle = 42f;
    [Min(0f)] public float swingMoveDistance = 0.08f;
    [Min(0.1f)] public float swingReturnSpeed = 14f;
    public bool animateOnlyWhenMining = true;

    [Header("Building Preview")]
    public bool showPlacementPreview = true;
    [Range(0.05f, 0.85f)] public float previewOpacity = 0.5f;
    public Color validPreviewColor = new Color(0.25f, 1f, 0.4f, 1f);
    public Color invalidPreviewColor = new Color(1f, 0.25f, 0.25f, 1f);
    public KeyCode rotateBuildingKey = KeyCode.T;
    [Min(1f)] public float rotationStep = 90f;
    [Min(0f)] public float placementSurfaceOffset = 0.02f;

    [Header("Foundation Ground Depth (only IDs 7 and 17)")]
    [Tooltip("Maximum amount a foundation can sink into the ground (metres). Other buildings stay unchanged.")]
    [Min(0f)] public float foundationMaxSinkDepth = 1.5f;
    [Tooltip("Current foundation sink depth (metres). 0 = sits on the surface.")]
    [Min(0f)] public float foundationSinkDepth = 0f;
    [Tooltip("Change foundation depth while previewing using PageDown (deeper) and PageUp (higher).")]
    [Min(0.01f)] public float foundationDepthStep = 0.1f;
    public KeyCode foundationLowerKey = KeyCode.PageDown;
    public KeyCode foundationRaiseKey = KeyCode.PageUp;

    GameObject placementPreview;
    readonly System.Collections.Generic.List<Material> previewMaterials = new System.Collections.Generic.List<Material>();
    int previewItemId = -1;
    bool previewValid;
    Vector3 previewPosition;
    Quaternion previewRotation;
    float buildingYaw;

    [Header("Inventory Appearance")]
    public Texture2D inventoryBackground;
    public Texture2D slotBackground;
    public Texture2D selectedSlotBackground;
    public Texture2D stonePickaxeIcon;
    public Texture2D stoneAxeIcon;
    public Texture2D woodIcon;
    public Texture2D stoneIcon;
    public Texture2D craftingTableIcon;
    public float slotSize = 64f;
    [Header("Custom Items")]
    public ItemDefinition[] customItems = new ItemDefinition[0];
    [Header("Built-in Building Parts (IDs 7-23)")]
    [Tooltip("Assign Placed Prefab and optionally Held Prefab/Icon for each part. These 17 entries are available automatically; no Custom Items entry needed. Do not reuse their IDs in Custom Items.")]
    public ItemDefinition[] buildingParts = CreateDefaultBuildingParts();

    static ItemDefinition[] CreateDefaultBuildingParts()
    {
        string[] names = {
            "Wood Foundation", "Wood Wall", "Wood Door Frame", "Wood Door",
            "Wood Window Wall", "Wood Stairs", "Wood Floor", "Wood Roof",
            "Wood Pillar", "Wood Fence", "Stone Foundation", "Stone Wall",
            "Stone Door Frame", "Stone Window Wall", "Stone Floor",
            "Stone Roof", "Stone Pillar"
        };
        ItemDefinition[] result = new ItemDefinition[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            result[i] = new ItemDefinition();
            result[i].itemId = i + 7;
            result[i].displayName = names[i];
            result[i].category = ItemCategory.Placeable;
            result[i].maxStack = 69;
        }
        return result;
    }

    [Header("Camera Look - Disabled While UI Is Open")]
    public Behaviour[] cameraLookScripts;
    [Header("Bag Equipment")]
    public int bagItemId;
    public int[] extraSlots = new int[8];
    public int[] extraCounts = new int[8];
    int cursorItemId;
    int cursorCount;
    bool wasMenuOpen;
    Quaternion lockedPlayerRotation;
    Quaternion lockedCameraRotation;
    bool lookRotationLocked;
    int dragSource = -1;
    bool dragActive;
    bool[] lookScriptOriginalStates;
    Texture2D highlightTexture;
    public int ExtraCapacity => GetBagCategory(bagItemId) == ItemCategory.Backpack ? 8 : GetBagCategory(bagItemId) == ItemCategory.Bundle ? 3 : 0;
    public bool IsCameraLockedByUI => IsAnyMenuOpen;
    public int CursorItemId => cursorItemId;
    public int CursorCount => cursorCount;


    [Header("Lantern - Off Hand (ID 46; ID 45 is Dirt)")]
    public int lanternItemId = 46;
    public GameObject lanternHeldPrefab;
    [Tooltip("World prefab with the lantern standing upright; required for placement with R.")]
    public GameObject lanternPlacedPrefab;
    [Tooltip("Allow placing a lantern selected in the hotbar with the building placement key.")]
    public bool allowLanternPlacement = true;
    public Texture2D lanternIcon;
    [Min(0f)] public float lanternLightIntensity = 2.4f;
    [Min(0f)] public float lanternLightRange = 13f;
    public Color lanternLightColor = new Color(1f, .75f, .40f);
    public bool lanternShadows = false; // Real-time point-light shadows are expensive.
    [Range(0f, .25f)] public float lanternFlicker = .035f;
    public Vector3 lanternLeftPosition = new Vector3(-.42f, -.43f, .72f);
    public Vector3 lanternLeftRotation = new Vector3(8f, 25f, -10f);
    [SerializeField] int lanternSlotItemId; // only 0 or 46; cannot drop with Q
    GameObject offHandLantern;
    Light offHandLight;
    Light hotbarLanternLight;
    public bool HasLanternInOffHand => lanternSlotItemId == lanternItemId;

    void RefreshLanternOffHand()
    {
        if (offHandLantern != null) Destroy(offHandLantern);
        offHandLantern = null;
        offHandLight = null;
        if (!HasLanternInOffHand || playerCamera == null) return;
        offHandLantern = lanternHeldPrefab != null ? Instantiate(lanternHeldPrefab, playerCamera.transform) : new GameObject("Lantern Off Hand");
        offHandLantern.transform.SetParent(playerCamera.transform, false);
        offHandLantern.transform.localPosition = lanternLeftPosition;
        offHandLantern.transform.localRotation = Quaternion.Euler(lanternLeftRotation);
        // Match the right-hand lantern: identical base scale and identical auto-fit.
        offHandLantern.transform.localScale = heldLocalScale;
        if (lanternHeldPrefab != null && autoFitHeldModels) FitHeldModel(offHandLantern);
        foreach (Transform t in offHandLantern.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        foreach (Collider c in offHandLantern.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Rigidbody rb in offHandLantern.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }
        foreach (MonoBehaviour b in offHandLantern.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        offHandLight = offHandLantern.GetComponentInChildren<Light>();
        if (offHandLight == null) offHandLight = offHandLantern.AddComponent<Light>();
        ConfigureLanternLight(offHandLight);
    }

    void ConfigureLanternLight(Light lightSource)
    {
        if (lightSource == null) return;
        lightSource.type = LightType.Point;
        lightSource.range = lanternLightRange;
        lightSource.color = lanternLightColor;
        lightSource.shadows = lanternShadows ? LightShadows.Soft : LightShadows.None;
        lightSource.intensity = lanternLightIntensity;
        lightSource.enabled = true;
    }

    void DrawLanternSlot(Rect r)
    {
        DrawSlot(r, lanternSlotItemId, lanternSlotItemId == 0 ? 0 : 1, false);
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return;
        if (cursorItemId == 0 && HasLanternInOffHand)
        {
            cursorItemId = lanternSlotItemId; cursorCount = 1; lanternSlotItemId = 0;
            RefreshLanternOffHand();
        }
        else if (cursorItemId == lanternItemId && cursorCount == 1 && lanternSlotItemId == 0)
        {
            lanternSlotItemId = cursorItemId; cursorItemId = 0; cursorCount = 0;
            RefreshLanternOffHand();
        }
        e.Use();
    }

    [Header("Controls")]
    public KeyCode inventoryKey = KeyCode.I;
    public KeyCode dropKey = KeyCode.Q;
    public KeyCode placeKey = KeyCode.R;
    public int selectedSlot;
    public bool inventoryOpen;
    public float pickupRadius = 1.4f;
    public float pickupDelay = 0.8f;
    public float dropDistance = 1.6f;
    public float dropForce = 3f;
    public float interactionRange = 5f;
    public LayerMask interactionMask = ~0;

    [Header("Items (8 Slots)")]
    public int[] slots = new int[8];
    public int[] counts = new int[8];
    public bool giveStarterPickaxe = true;

    public bool HasPickaxeEquipped => SelectedItemId == 1;
    public bool HasAxeEquipped => SelectedItemId == 2;
    public int SelectedItemId => slots != null && selectedSlot >= 0 && selectedSlot < slots.Length ? slots[selectedSlot] : 0;
    public bool IsAnyMenuOpen => inventoryOpen || (crafting != null && crafting.IsOpen) || (openCampfire != null && openCampfire.IsOpen) || (openFurnace != null && openFurnace.IsOpen) || JukeboxPlayer.IsAnyOpen;
    CampfireCooking openCampfire;
    FurnaceStation openFurnace;

    GameObject heldObject;
    float swingPhase;
    float heldIdleTime;
    float swingWeight;
    TerrainMiner terrainMiner;
    int lastSlot = -1;
    int lastItem = -1;
    CraftingTable crafting;
    int movingSlot = -1;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        terrainMiner = GetComponent<TerrainMiner>();
        if (terrainMiner == null) terrainMiner = GetComponentInChildren<TerrainMiner>();
        if (slots == null || slots.Length != 8) slots = new int[8];
        if (counts == null || counts.Length != 8) counts = new int[8];
        if (extraSlots == null || extraSlots.Length != 8) extraSlots = new int[8];
        if (extraCounts == null || extraCounts.Length != 8) extraCounts = new int[8];
        highlightTexture = new Texture2D(1, 1);
        highlightTexture.SetPixel(0, 0, new Color(0.85f, 0.85f, 0.85f, 0.7f));
        highlightTexture.Apply();
        CacheLookScriptStates();
        if (lanternSlotItemId != 0 && lanternSlotItemId != lanternItemId) lanternSlotItemId = 0;
        bool empty = true;
        for (int i = 0; i < 8; i++)
        {
            if (slots[i] != 0) { empty = false; if (counts[i] <= 0) counts[i] = 1; }
            else counts[i] = 0;
        }
        if (empty && giveStarterPickaxe) { slots[0] = 1; counts[0] = 1; }
    }

    void Update()
    {
        if (HasLanternInOffHand && offHandLantern == null) RefreshLanternOffHand();
        if (offHandLight != null) { ConfigureLanternLight(offHandLight); offHandLight.intensity *= 1f + lanternFlicker * (Mathf.PerlinNoise(Time.time * 3f, 4f) * 2f - 1f); }
        if (hotbarLanternLight != null) { ConfigureLanternLight(hotbarLanternLight); hotbarLanternLight.intensity *= 1f + lanternFlicker * (Mathf.PerlinNoise(Time.time * 3f, 7f) * 2f - 1f); }
        if (heldObject == null && lastSlot < 0) RefreshHeldItem();
        if (Input.GetKeyDown(KeyCode.Escape) && inventoryOpen) CloseInventory();
        else if (Input.GetKeyDown(inventoryKey))
        {
            if (openCampfire != null && openCampfire.IsOpen) openCampfire.Close();
            else if (crafting != null && crafting.IsOpen) crafting.Close();
            else { if (inventoryOpen) CloseInventory(); else { inventoryOpen = true; SetCursor(true); LockLookRotation(); wasMenuOpen = true; } }
        }
        for (int i = 0; i < 8; i++)
            if (!IsAnyMenuOpen && Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) selectedSlot = i;
        if (!IsAnyMenuOpen)
        {
            if (Input.GetKeyDown(rotateBuildingKey) && IsBuildable(SelectedItemId)) buildingYaw += rotationStep;
            if (Input.GetKeyDown(dropKey)) DropSelectedItem();
            if (Input.GetKeyDown(placeKey)) PlaceSelectedObject();
            if (Input.GetKeyDown(KeyCode.E)) TryPickUpPlacedObject();
            UpdateEatingCountdown();
            if (SelectedItemId == 26 && Input.GetMouseButtonDown(0)) StartSpearAttack();
            UpdateSpearAttack();
        }
        if (IsAnyMenuOpen) CancelEating();
        UpdatePlacementPreview();
        TryPickUpNearbyItems();
        if (!IsAnyMenuOpen && cursorItemId != 0) ReturnCursorItems();
        bool menu = IsAnyMenuOpen;
        if (menu != wasMenuOpen) { if (menu) LockLookRotation(); else UnlockLookRotation(); wasMenuOpen = menu; }
        if (lastSlot != selectedSlot || lastItem != SelectedItemId)
        {
            lastSlot = selectedSlot;
            lastItem = SelectedItemId;
            RefreshHeldItem();
        }
    }

    ItemDefinition FindItem(int id)
    {
        if (customItems != null)
            foreach (ItemDefinition item in customItems)
                if (item != null && item.itemId == id) return item;
        if (buildingParts != null)
            foreach (ItemDefinition item in buildingParts)
                if (item != null && item.itemId == id) return item;
        return null;
    }

    ItemCategory GetBagCategory(int id)
    {
        ItemDefinition item = FindItem(id);
        if (id == 27) return ItemCategory.Backpack;
        if (id == 28) return ItemCategory.Bundle;
        return item == null ? ItemCategory.Other : item.category;
    }

    int MaxStack(int id)
    {
        ItemDefinition item = id == 6 ? null : FindItem(id);
        return id == lanternItemId ? 1 : item != null ? Mathf.Clamp(item.maxStack, 1, 69) : (id == 1 || id == 2 || id == 5 || id == 6 || id == 26 || id == 27 || id == 28 ? 1 : 69);
    }

    public string GetDisplayName(int id)
    {
        ItemDefinition item = id == 6 ? null : FindItem(id);
        return item != null ? item.displayName : GetItemName(id);
    }

    void CacheLookScriptStates()
    {
        if (cameraLookScripts == null) return;
        lookScriptOriginalStates = new bool[cameraLookScripts.Length];
        for (int i = 0; i < cameraLookScripts.Length; i++)
            lookScriptOriginalStates[i] = cameraLookScripts[i] != null && cameraLookScripts[i].enabled;
    }

    void SetCameraLookEnabled(bool enabled)
    {
        if (cameraLookScripts == null) return;
        if (lookScriptOriginalStates == null || lookScriptOriginalStates.Length != cameraLookScripts.Length)
            CacheLookScriptStates();
        if (lanternSlotItemId != 0 && lanternSlotItemId != lanternItemId) lanternSlotItemId = 0;
        for (int i = 0; i < cameraLookScripts.Length; i++)
        {
            Behaviour script = cameraLookScripts[i];
            if (script != null && script != this)
                script.enabled = enabled ? lookScriptOriginalStates[i] : false;
        }
    }

    void CloseInventory()
    {
        if (!ReturnCursorItems()) return; // Never silently delete a held stack.
        inventoryOpen = false;
        SetCursor(false);
        UnlockLookRotation();
        wasMenuOpen = false;
    }

    bool ReturnCursorItems()
    {
        if (cursorItemId == 0) return true;
        int id = cursorItemId, remaining = cursorCount;
        for (int pass = 0; pass < 2; pass++)
        for (int i = 0; i < 8 + ExtraCapacity && remaining > 0; i++)
        {
            int existing = SlotId(i);
            if ((pass == 0 && existing != id) || (pass == 1 && existing != 0)) continue;
            int space = MaxStack(id) - SlotCount(i);
            int n = Mathf.Min(space, remaining);
            if (n > 0) { SetSlot(i, id, SlotCount(i) + n); remaining -= n; }
        }
        cursorCount = remaining;
        if (remaining == 0) cursorItemId = 0;
        return remaining == 0;
    }

    void LateUpdate()
    {
        UpdateHeldItemAnimationLate();
        // Restore rotations after mouse-look scripts have run, including scripts
        // that do not respect the cursor lock state.
        if (!IsAnyMenuOpen || !lookRotationLocked) return;
        transform.rotation = lockedPlayerRotation;
        if (playerCamera != null) playerCamera.transform.rotation = lockedCameraRotation;
    }

    void LockLookRotation()
    {
        lockedPlayerRotation = transform.rotation;
        if (playerCamera != null) lockedCameraRotation = playerCamera.transform.rotation;
        lookRotationLocked = true;
        SetCameraLookEnabled(false);
    }

    void UnlockLookRotation()
    {
        lookRotationLocked = false;
        SetCameraLookEnabled(true);
    }

    void OnDisable()
    {
        ReturnCursorItems();
        UnlockLookRotation();
    }
    void OnDestroy()
    {
        ClearPlacementPreview();
        if (highlightTexture != null) Destroy(highlightTexture);
        if (offHandLantern != null) Destroy(offHandLantern);
    }

    public static string GetItemName(int id)
    {
        switch (id)
        {
            case 1: return "Stone Pickaxe";
            case 2: return "Stone Axe";
            case 3: return "Wood";
            case 4: return "Stone";
            case 5: return "Work Bench";
            case 6: return "Jukebox";
            case 24: return "Raw Beef";
            case 25: return "Steak";
            case 26: return "Spear";
            case 27: return "Backpack";
            case 28: return "Bundle";
            case 29: return "Bear Claw";
            case 30: return "Leather";
            case 31: return "Campfire";
            case 42: return "Furnace";
            case 43: return "Stone Carving Station";
            case 44: return "Anvil";
            case 45: return "Dirt";
            case 46: return "Lantern";
            default: return id == 0 ? "Empty" : "Item " + id;
        }
    }

    public Texture2D GetIcon(int id)
    {
        ItemDefinition custom = id == 6 ? null : FindItem(id);
        if (custom != null) return custom.icon;
        switch (id)
        {
            case 46: return lanternIcon;
            case 1: return stonePickaxeIcon;
            case 2: return stoneAxeIcon;
            case 3: return woodIcon;
            case 4: return stoneIcon;
            case 5: return craftingTableIcon;
            case 6: return jukeboxIcon;
            case 24: return rawBeefIcon;
            case 25: return steakIcon;
            case 26: return spearIcon;
            case 27: return backpackIcon;
            case 28: return bundleIcon;
            case 29: return bearClawIcon;
            case 30: return leatherIcon;
            case 31: return campfireIcon;
            default: return null;
        }
    }

    int SlotId(int i) => i < 8 ? slots[i] : extraSlots[i - 8];
    int SlotCount(int i) => i < 8 ? counts[i] : extraCounts[i - 8];
    void SetSlot(int i, int id, int count)
    {
        if (i < 8) { slots[i] = id; counts[i] = count; }
        else { extraSlots[i - 8] = id; extraCounts[i - 8] = count; }
    }

    public bool CanAddItem(int id, int amount = 1)
    {
        if (id <= 0 || amount <= 0) return false;
        int room = 0;
        for (int i = 0; i < 8 + ExtraCapacity; i++)
            room += SlotId(i) == 0 ? MaxStack(id) : SlotId(i) == id ? Mathf.Max(0, MaxStack(id) - SlotCount(i)) : 0;
        return room >= amount;
    }

    public bool TryAddItem(int id, int amount = 1)
    {
        if (!CanAddItem(id, amount)) return false;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < 8 + ExtraCapacity && amount > 0; i++)
            {
                int current = SlotId(i);
                if (pass == 0 && current != id || pass == 1 && current != 0) continue;
                int n = Mathf.Min(amount, MaxStack(id) - SlotCount(i));
                if (n > 0) { SetSlot(i, id, SlotCount(i) + n); amount -= n; }
            }
        return true;
    }

    public bool RemoveFromSlot(int index, int amount = 1)
    {
        if (index < 0 || index >= 8 + ExtraCapacity || SlotId(index) == 0 || SlotCount(index) < amount || amount <= 0) return false;
        int remaining = SlotCount(index) - amount;
        SetSlot(index, remaining == 0 ? 0 : SlotId(index), remaining);
        return true;
    }

    public void SwapSlots(int a, int b)
    {
        if (a < 0 || a >= 8 + ExtraCapacity || b < 0 || b >= 8 + ExtraCapacity) return;
        int id = SlotId(a), count = SlotCount(a);
        SetSlot(a, SlotId(b), SlotCount(b)); SetSlot(b, id, count);
    }

    public void DropSelectedItem()
    {
        int id = SelectedItemId;
        if (id == 0) return;
        Transform t = playerCamera != null ? playerCamera.transform : transform;
        SpawnDroppedItem(id, 1, t.position + t.forward * dropDistance, t.forward * dropForce);
        RemoveFromSlot(selectedSlot);
    }

    public void SpawnDroppedItem(int id, int amount, Vector3 position, Vector3 velocity)
    {
        if (id <= 0 || amount <= 0) return;
        GameObject prefab = null;
        ItemDefinition definition = FindItem(id);
        if (definition != null) prefab = definition.droppedPrefab;
        switch (id)
        {
            case 1: prefab = droppedPickaxePrefab; break;
            case 2: prefab = droppedAxePrefab; break;
            case 3: prefab = droppedWoodPrefab; break;
            case 4: prefab = droppedStonePrefab; break;
            case 24: prefab = rawBeefDropPrefab; break;
            case 25: prefab = steakDropPrefab; break;
            case 26: prefab = spearDropPrefab; break;
            case 27: prefab = backpackDropPrefab; break;
            case 28: prefab = bundleDropPrefab; break;
            case 30: prefab = leatherDropPrefab; break;
        }
        GameObject obj = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.transform.position = position;
        if (prefab == null) obj.transform.localScale = Vector3.one * 0.35f;
        obj.name = "Dropped " + GetDisplayName(id) + " x" + amount;
        if (obj.GetComponentInChildren<Collider>() == null) obj.AddComponent<BoxCollider>();
        DroppedItem dropped = obj.GetComponent<DroppedItem>();
        if (dropped == null) dropped = obj.AddComponent<DroppedItem>();
        dropped.Initialize(id, amount, pickupDelay);
        Rigidbody body = obj.GetComponent<Rigidbody>();
        if (body == null) body = obj.AddComponent<Rigidbody>();
        body.isKinematic = false; body.useGravity = true;
        body.AddForce(velocity, ForceMode.VelocityChange);
    }

    void TryPickUpNearbyItems()
    {
        foreach (Collider c in Physics.OverlapSphere(transform.position, pickupRadius, ~0, QueryTriggerInteraction.Collide))
        {
            DroppedItem drop = c.GetComponentInParent<DroppedItem>();
            if (drop != null && drop.CanPickUp && TryAddItem(drop.itemId, drop.amount))
            {
                drop.MarkCollected(); Destroy(drop.gameObject);
            }
        }
    }

    GameObject GetPlaceablePrefab(int id)
    {
        if (id == lanternItemId && allowLanternPlacement) return lanternPlacedPrefab;
        if (id == 5) return craftingTablePrefab;
        if (id == 6) return jukeboxPrefab;
        if (id == 31) return campfirePrefab;
        if (id == 42 && furnacePrefab != null) return furnacePrefab;
        ItemDefinition item = FindItem(id);
        return item != null ? item.placedPrefab : null;
    }

    bool IsBuildable(int id) => GetPlaceablePrefab(id) != null;

    void ClearPlacementPreview()
    {
        if (placementPreview != null) Destroy(placementPreview);
        foreach (Material material in previewMaterials) if (material != null) Destroy(material);
        previewMaterials.Clear();
        placementPreview = null;
        previewItemId = -1;
        previewValid = false;
    }


    void UpdatePlacementPreview()
    {
        int id = !IsAnyMenuOpen && showPlacementPreview ? SelectedItemId : 0;
        GameObject prefab = GetPlaceablePrefab(id);
        if (prefab == null)
        {
            if (placementPreview != null) ClearPlacementPreview();
            return;
        }
        if (placementPreview == null || previewItemId != id)
        {
            ClearPlacementPreview();
            placementPreview = Instantiate(prefab);
            placementPreview.name = "Building Preview (" + GetDisplayName(id) + ")";
            previewItemId = id;
            // Remove all gameplay behaviour from the ghost. Renderers are kept.
            foreach (MonoBehaviour component in placementPreview.GetComponentsInChildren<MonoBehaviour>(true))
                component.enabled = false;
            foreach (Collider collider in placementPreview.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Rigidbody body in placementPreview.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            // Keep original material textures and colours, changing only transparency.
            foreach (Renderer renderer in placementPreview.GetComponentsInChildren<Renderer>(true))
            {
                Material[] originals = renderer.sharedMaterials;
                Material[] ghosts = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    Material original = originals[i];
                    if (original == null) continue;
                    Material ghost = new Material(original);
                    Shader standard = Shader.Find("Standard");
                    // Built-in render pipeline: Standard supports transparent mode.
                    if (ghost.HasProperty("_Mode"))
                    {
                        ghost.SetFloat("_Mode", 3f);
                        ghost.SetOverrideTag("RenderType", "Transparent");
                        ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        ghost.SetInt("_ZWrite", 0);
                        ghost.DisableKeyword("_ALPHATEST_ON");
                        ghost.DisableKeyword("_ALPHABLEND_ON");
                        ghost.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    }
                    else if (ghost.HasProperty("_Surface"))
                    {
                        ghost.SetFloat("_Surface", 1f);
                        ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        ghost.SetInt("_ZWrite", 0);
                        ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }
                    else if (standard != null)
                    {
                        // Unknown opaque shader: Standard retains main texture and tint.
                        Texture main = original.HasProperty("_MainTex") ? original.mainTexture : null;
                        Color tint = original.HasProperty("_Color") ? original.color : Color.white;
                        ghost.shader = standard;
                        ghost.mainTexture = main;
                        ghost.color = tint;
                        ghost.SetFloat("_Mode", 3f);
                        ghost.SetOverrideTag("RenderType", "Transparent");
                        ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        ghost.SetInt("_ZWrite", 0);
                        ghost.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    }
                    ghost.renderQueue = 3000;
                    ghosts[i] = ghost;
                    previewMaterials.Add(ghost);
                }
                renderer.sharedMaterials = ghosts;
            }
        }

        Transform view = playerCamera != null ? playerCamera.transform : transform;
        previewValid = Physics.Raycast(view.position, view.forward, out RaycastHit hit,
            interactionRange, interactionMask, QueryTriggerInteraction.Ignore) &&
            Vector3.Dot(hit.normal, Vector3.up) >= 0.6f;
        placementPreview.SetActive(previewValid);
        if (!previewValid) return;

        previewRotation = Quaternion.Euler(0f, buildingYaw, 0f);
        // Place the lowest visible point on the hit surface, not the prefab pivot.
        placementPreview.transform.SetPositionAndRotation(Vector3.zero, previewRotation);
        float bottom = 0f;
        bool hasBounds = false;
        foreach (Renderer renderer in placementPreview.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds || renderer.bounds.min.y < bottom) bottom = renderer.bounds.min.y;
            hasBounds = true;
        }
        // Foundations only: adjust how far they are embedded into the terrain.
        // Clamp Inspector values too, so a foundation cannot sink deeper than allowed.
        if (id == 7 || id == 17)
        {
            foundationMaxSinkDepth = Mathf.Max(0f, foundationMaxSinkDepth);
            float step = Mathf.Max(0.01f, foundationDepthStep);
            if (Input.GetKeyDown(foundationLowerKey)) foundationSinkDepth += step;
            if (Input.GetKeyDown(foundationRaiseKey)) foundationSinkDepth -= step;
            foundationSinkDepth = Mathf.Clamp(foundationSinkDepth, 0f, foundationMaxSinkDepth);
        }
        float sink = (id == 7 || id == 17) ? foundationSinkDepth : 0f;
        previewPosition = hit.point + Vector3.up * (placementSurfaceOffset - (hasBounds ? bottom : 0f) - sink);
        placementPreview.transform.SetPositionAndRotation(previewPosition, previewRotation);
        foreach (Material material in previewMaterials)
        {
            if (material == null) continue;
            if (material.HasProperty("_Color"))
            {
                Color color = material.color;
                color.a = previewOpacity;
                material.color = color;
            }
            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor");
                color.a = previewOpacity;
                material.SetColor("_BaseColor", color);
            }
        }
    }

    void PlaceSelectedObject()
    {
        int id = SelectedItemId;
        GameObject prefab = GetPlaceablePrefab(id);
        if (prefab == null) return;
        // Use the same position/rotation as the transparent ghost.
        if (placementPreview == null || previewItemId != id || !previewValid) return;
        GameObject placed = Instantiate(prefab, previewPosition, previewRotation);
        if (placed.GetComponentInChildren<Collider>() == null) placed.AddComponent<BoxCollider>();
        if (id == lanternItemId)
        {
            placed.name = "Placed Lantern";
            LanternPlacedLight marker = placed.GetComponent<LanternPlacedLight>();
            if (marker == null) marker = placed.AddComponent<LanternPlacedLight>();
            marker.itemId = lanternItemId;
            Light worldLight = placed.GetComponentInChildren<Light>();
            if (worldLight == null) worldLight = placed.AddComponent<Light>();
            ConfigureLanternLight(worldLight);
            worldLight.enabled = true;
        }
        if (id == 5)
        {
            if (placed.GetComponent<CraftingTable>() == null) placed.AddComponent<CraftingTable>();
            placed.name = "Work Bench";
        }
        else if (id == 31)
        {
            if (placed.GetComponent<CampfireCooking>() == null) placed.AddComponent<CampfireCooking>();
            placed.name = "Campfire";
        }
        else if (id == 6)
        {
            if (placed.GetComponent<JukeboxPlayer>() == null) placed.AddComponent<JukeboxPlayer>();
            placed.name = "Jukebox";
        }
        // Wire station scripts on placed prefabs (references are not serialized across players).
        FurnaceStation furnace = placed.GetComponentInChildren<FurnaceStation>();
        if (id == 42 && furnace == null) furnace = placed.AddComponent<FurnaceStation>();
        if (furnace != null) { furnace.inventory = this; furnace.viewCamera = playerCamera; }
        Station16x16 station = placed.GetComponent<Station16x16>();
        if (station != null) { station.user = this; station.viewCamera = playerCamera; }
        RemoveFromSlot(selectedSlot);
        UpdatePlacementPreview();
    }

    void TryPickUpPlacedObject()
    {
        Transform view = playerCamera != null ? playerCamera.transform : transform;
        // A lantern may have a small/offset collider. Check a narrow area around
        // the crosshair before the normal station pickup raycast.
        RaycastHit[] nearbyHits = Physics.SphereCastAll(view.position, 0.24f, view.forward,
            interactionRange, interactionMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(nearbyHits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit candidate in nearbyHits)
        {
            if (candidate.collider == null) continue;
            if (candidate.collider.transform.IsChildOf(transform)) continue;
            LanternPlacedLight nearbyLantern = candidate.collider.GetComponentInParent<LanternPlacedLight>();
            if (nearbyLantern != null)
            {
                if (TryAddItem(nearbyLantern.itemId, 1))
                {
                    Destroy(nearbyLantern.gameObject);
                    ClearPlacementPreview();
                }
                return;
            }
            // A wall or other object blocks pickup through it. Ignore terrain
            // when it is merely the floor at the edge of the sphere cast.
            if (candidate.distance > 0.4f && candidate.normal.y < 0.6f) break;
        }
        if (!Physics.Raycast(view.position, view.forward, out RaycastHit hit, interactionRange, interactionMask, QueryTriggerInteraction.Ignore)) return;
        CampfireCooking fire = hit.collider.GetComponentInParent<CampfireCooking>();
        if (fire != null)
        {
            if (fire.CanPickUp && TryAddItem(31)) Destroy(fire.gameObject);
            return;
        }
        CraftingTable bench = hit.collider.GetComponentInParent<CraftingTable>();
        if (bench != null && !bench.IsOpen && bench.CanPickUp)
        {
            if (TryAddItem(5)) Destroy(bench.gameObject);
            return;
        }
        FurnaceStation furnace = hit.collider.GetComponentInParent<FurnaceStation>();
        if (furnace != null)
        {
            if (!furnace.IsOpen && furnace.CanPickUp && TryAddItem(42)) Destroy(furnace.gameObject);
            return;
        }
        JukeboxPlayer jukebox = hit.collider.GetComponentInParent<JukeboxPlayer>();
        if (jukebox != null && TryAddItem(6)) Destroy(jukebox.gameObject);
    }

    public bool OpenCampfire(CampfireCooking fire)
    {
        if (fire == null || IsAnyMenuOpen || !ReturnCursorItems()) return false;
        inventoryOpen = false;
        openCampfire = fire;
        fire.Open(this);
        SetCursor(true);
        LockLookRotation();
        wasMenuOpen = true;
        return true;
    }

    public void CampfireClosed(CampfireCooking fire)
    {
        if (openCampfire == fire) openCampfire = null;
        if (!inventoryOpen) SetCursor(false);
        UnlockLookRotation();
        wasMenuOpen = false;
    }

    public bool OpenFurnace(FurnaceStation furnace)
    {
        if (furnace == null || IsAnyMenuOpen || !ReturnCursorItems()) return false;
        inventoryOpen = false;
        openFurnace = furnace;
        furnace.Open(this);
        SetCursor(true);
        LockLookRotation();
        wasMenuOpen = true;
        return true;
    }

    public void FurnaceClosed(FurnaceStation furnace)
    {
        if (openFurnace == furnace) openFurnace = null;
        if (!inventoryOpen) SetCursor(false);
        UnlockLookRotation();
        wasMenuOpen = false;
    }

    public int MoveCursorToCampfire(int itemId, int capacity)
    {
        if (cursorItemId != itemId || cursorCount <= 0) return 0;
        int amount = Mathf.Min(cursorCount, Mathf.Max(0, capacity));
        cursorCount -= amount;
        if (cursorCount == 0) cursorItemId = 0;
        return amount;
    }

    public bool TakeCampfireOutput(int itemId, int amount)
    {
        if (amount <= 0) return false;
        if (cursorItemId == 0 || cursorItemId == itemId)
        {
            int room = MaxStack(itemId) - cursorCount;
            if (room >= amount)
            {
                cursorItemId = itemId;
                cursorCount += amount;
                return true;
            }
        }
        return TryAddItem(itemId, amount);
    }

    public void OpenCrafting(CraftingTable table)
    {
        if (!ReturnCursorItems()) return;
        inventoryOpen = false; movingSlot = -1; crafting = table;
        table.Open(this); SetCursor(true);
        LockLookRotation(); wasMenuOpen = true;
    }

    public void CraftingClosed(CraftingTable table)
    {
        if (crafting == table) crafting = null;
        if (!inventoryOpen) SetCursor(false);
        UnlockLookRotation(); wasMenuOpen = false;
    }

    void SetCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    void RefreshHeldItem()
    {
        if (heldObject != null) Destroy(heldObject);
        hotbarLanternLight = null;
        heldObject = null;
        swingPhase = 0f;
        swingWeight = 0f;
        spearAttackStart = -100f;
        spearHitApplied = true;
        if (playerCamera == null) return;

        GameObject prefab = null;
        if (SelectedItemId == lanternItemId) prefab = lanternHeldPrefab;
        else if (SelectedItemId == 1) prefab = stonePickaxePrefab;
        else if (SelectedItemId == 2) prefab = stoneAxePrefab;
        else if (SelectedItemId == 5) prefab = craftingTablePrefab;
        else if (SelectedItemId == 6) prefab = jukeboxPrefab;
        else if (SelectedItemId == 24) prefab = rawBeefHeldPrefab;
        else if (SelectedItemId == 25) prefab = steakHeldPrefab;
        else if (SelectedItemId == 26) prefab = spearHeldPrefab;
        else if (SelectedItemId == 27) prefab = backpackHeldPrefab;
        else if (SelectedItemId == 28) prefab = bundleHeldPrefab;
        else
        {
            ItemDefinition item = SelectedItemId == 6 ? null : FindItem(SelectedItemId);
            if (item != null) prefab = item.heldPrefab != null ? item.heldPrefab : item.placedPrefab;
        }
        if (prefab == null) return;

        heldObject = Instantiate(prefab, playerCamera.transform);
        heldObject.name = "Held Item (" + GetDisplayName(SelectedItemId) + ")";
        heldObject.transform.localPosition = heldLocalPosition;
        heldObject.transform.localRotation = Quaternion.Euler(heldLocalRotation);
        heldObject.transform.localScale = heldLocalScale;
        if (autoFitHeldModels) FitHeldModel(heldObject);

        // Prevent the first-person model from blocking mining raycasts or interacting with physics.
        foreach (Transform child in heldObject.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 2; // Ignore Raycast
        foreach (Collider collider in heldObject.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in heldObject.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }
        foreach (CraftingTable bench in heldObject.GetComponentsInChildren<CraftingTable>(true))
            bench.enabled = false;
        foreach (MonoBehaviour behaviour in heldObject.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null) behaviour.enabled = false;
        if (SelectedItemId == lanternItemId)
        {
            hotbarLanternLight = heldObject.GetComponentInChildren<Light>();
            if (hotbarLanternLight == null) hotbarLanternLight = heldObject.AddComponent<Light>();
            ConfigureLanternLight(hotbarLanternLight);
        }
    }

    void UpdateHeldItemAnimationLate()
    {
        if (heldObject == null) return;

        bool toolEquipped = HasPickaxeEquipped || HasAxeEquipped;
        bool swinging = SelectedItemId != 26 && toolEquipped && !IsAnyMenuOpen && Input.GetMouseButton(0);
        if (swinging && animateOnlyWhenMining && SelectedItemId != 26)
        {
            swinging = false;
            if (playerCamera != null && terrainMiner != null &&
                Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                    out RaycastHit hit, terrainMiner.range, terrainMiner.mask, QueryTriggerInteraction.Ignore))
            {
                TreeResource tree = hit.collider.GetComponentInParent<TreeResource>();
                SmoothTerrain terrain = tree == null ? hit.collider.GetComponentInParent<SmoothTerrain>() : null;
                swinging = (tree != null && HasAxeEquipped) || (terrain != null && HasPickaxeEquipped);
            }
        }

        swingWeight = Mathf.MoveTowards(swingWeight, swinging ? 1f : 0f,
            Time.deltaTime * swingReturnSpeed);
        if (swinging) swingPhase += Time.deltaTime * swingSpeed * Mathf.PI * 2f;
        else if (swingWeight <= 0f) swingPhase = 0f;

        float wave = Mathf.Sin(swingPhase) * swingWeight;
        heldIdleTime += Time.deltaTime;
        float idle = Mathf.Sin(heldIdleTime * 1.9f) * heldIdleBobbing;
        Vector3 target = heldLocalPosition + new Vector3(0f, idle - Mathf.Abs(wave) * swingMoveDistance, -Mathf.Abs(wave) * swingMoveDistance);
        Quaternion rotation = Quaternion.Euler(heldLocalRotation) *
            Quaternion.Euler(wave * swingAngle, 0f, -wave * swingAngle * 0.2f);

        // Spear uses its own single thrust animation, never the mining swing.
        if (SelectedItemId == 26)
        {
            float progress = (Time.time - spearAttackStart) / Mathf.Max(0.1f, spearCooldown);
            if (progress >= 0f && progress < 1f)
            {
                float hitAt = Mathf.Clamp(spearHitMoment, 0.05f, 0.95f);
                float phase = progress <= hitAt ? progress / hitAt : (1f - progress) / (1f - hitAt);
                float thrust = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase));
                // Peak of the thrust and the damage occur at the same time.
                target = heldLocalPosition + new Vector3(0f, -thrust * 0.025f, thrust * spearThrustDistance);
                rotation = Quaternion.Euler(heldLocalRotation) * Quaternion.Euler(-thrust * spearThrustAngle, 0f, 0f);
            }
            else
            {
                // Keep spear steady between attacks (no mining or idle bobbing).
                target = heldLocalPosition;
                rotation = Quaternion.Euler(heldLocalRotation);
            }
        }
        heldObject.transform.localPosition = Vector3.Lerp(heldObject.transform.localPosition,
            target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, heldPositionSmoothing)));
        heldObject.transform.localRotation = rotation;
    }

    void FitHeldModel(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largest < 0.0001f) return;
        float target = Mathf.Clamp(maxHeldModelSize, minHeldModelSize, 2f);
        float scaleFactor = target / largest;
        model.transform.localScale *= scaleFactor;
        // Center geometry around the held object's transform without changing the prefab.
        Vector3 offset = model.transform.position - bounds.center;
        foreach (Transform child in model.transform)
            child.position += offset;
    }

    void CancelEating()
    {
        eatingProgress = 0f;
        eatingSlot = -1;
        eatingItemId = 0;
    }

    // Hold right mouse button to eat. Releasing, switching items or opening a menu cancels it.
    void UpdateEatingCountdown()
    {
        int id = SelectedItemId;
        if (!Input.GetKey(eatKey) || (id != 24 && id != 25))
        {
            CancelEating();
            return;
        }
        SurvivalVitals vitals = GetComponent<SurvivalVitals>();
        if (vitals == null) vitals = GetComponentInParent<SurvivalVitals>();
        if (vitals == null || (vitals.Hunger >= vitals.maxHunger - 0.01f && vitals.Health >= vitals.maxHealth - 0.01f))
        {
            CancelEating();
            return;
        }
        if (eatingSlot != selectedSlot || eatingItemId != id)
        {
            eatingProgress = 0f;
            eatingSlot = selectedSlot;
            eatingItemId = id;
        }
        eatingProgress += Time.deltaTime;
        if (eatingProgress >= Mathf.Max(0.1f, eatingDuration))
        {
            vitals.Eat(id == 25 ? steakHunger : rawBeefHunger);
            vitals.Heal(id == 25 ? steakHealth : rawBeefHealth);
            RemoveFromSlot(selectedSlot);
            CancelEating();
        }
    }

    void StartSpearAttack()
    {
        if (playerCamera == null || Time.time < nextSpearHit) return;
        spearAttackStart = Time.time;
        spearHitApplied = false;
        nextSpearHit = Time.time + Mathf.Max(0.1f, spearCooldown);
    }

    void UpdateSpearAttack()
    {
        if (spearHitApplied || SelectedItemId != 26 || IsAnyMenuOpen) return;
        float duration = Mathf.Max(0.1f, spearCooldown);
        if (Time.time - spearAttackStart < duration * spearHitMoment) return;
        spearHitApplied = true;
        TrySpearAttack();
    }

    void TrySpearAttack()
    {
        if (playerCamera == null) return;
        RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward,
            Mathf.Max(0.5f, spearRange), ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            MobAI mob = hit.collider.GetComponentInParent<MobAI>();
            if (mob != null) mob.TakeDamage(spearDamage, transform);
            break; // A wall or other obstacle blocks the hit.
        }
    }

    public void DrawSlot(Rect rect, int id, int count, bool selected)
    {
        // Change only the slot background; the item icon remains unchanged.
        Texture2D background = selected && selectedSlotBackground != null
            ? selectedSlotBackground
            : slotBackground;
        if (background != null) GUI.DrawTexture(rect, background, ScaleMode.StretchToFill);
        else GUI.Box(rect, GUIContent.none);
        if (id == 0) return;
        Texture2D icon = GetIcon(id);
        if (icon != null) GUI.DrawTexture(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, rect.height - 20), icon, ScaleMode.ScaleToFit);
        else GUI.Label(new Rect(rect.x + 3, rect.y + 8, rect.width - 6, rect.height - 12), GetDisplayName(id));
        if (count > 1) GUI.Label(new Rect(rect.x + rect.width - 24, rect.y + rect.height - 20, 25, 20), count.ToString());
        GUI.Label(rect, new GUIContent("", GetDisplayName(id) + " (ID: " + id + ")"));
    }

    void ClickSlot(int index, int button)
    {
        if (index < 0 || index >= 8 + ExtraCapacity) return;
        int id = SlotId(index), count = SlotCount(index);
        if (cursorItemId == 0)
        {
            if (id == 0) return;
            int take = button == 1 ? (count + 1) / 2 : count;
            cursorItemId = id; cursorCount = take;
            SetSlot(index, count == take ? 0 : id, count - take);
        }
        else if (id == 0 || id == cursorItemId)
        {
            int room = MaxStack(cursorItemId) - count;
            int put = Mathf.Min(room, button == 1 ? 1 : cursorCount);
            if (put <= 0) return;
            SetSlot(index, cursorItemId, count + put);
            cursorCount -= put;
            if (cursorCount == 0) cursorItemId = 0;
        }
        else if (button == 0 && cursorCount <= MaxStack(cursorItemId))
        {
            SetSlot(index, cursorItemId, cursorCount);
            cursorItemId = id; cursorCount = count;
        }
    }

    // A mouse-down picks up a stack; a mouse-up over another slot places it.
    // A later click also places it, matching the familiar inventory interaction.
    void DrawInteractiveSlot(Rect r, int index, bool selected)
    {
        DrawSlot(r, SlotId(index), SlotCount(index), selected);
        Event e = Event.current;
        if (!r.Contains(e.mousePosition)) return;
        if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            ClickSlot(index, e.button);
            dragSource = e.button == 0 ? index : -1;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && dragSource >= 0 && dragSource != index)
        {
            ClickSlot(index, 0);
            dragSource = -1;
            e.Use();
        }
    }

    public void DrawInventorySlots(float x, float y, float size, bool interactive)
    {
        for (int i = 0; i < 8; i++)
        {
            Rect r = new Rect(x + i * (size + 4), y, size, size);
            if (interactive) DrawInteractiveSlot(r, i, false);
            else DrawSlot(r, slots[i], counts[i], selectedSlot == i);
        }
    }

    public void DrawExtraSlots(float x, float y, float size)
    {
        for (int i = 0; i < ExtraCapacity; i++)
            DrawInteractiveSlot(new Rect(x + i * (size + 4), y, size, size), i + 8, false);
    }

    public bool TakeOneFromCursor(out int itemId)
    {
        itemId = cursorItemId;
        if (cursorItemId == 0 || cursorCount <= 0) return false;
        cursorCount--;
        if (cursorCount == 0) cursorItemId = 0;
        return true;
    }

    public bool GiveOneToCursor(int itemId)
    {
        if (itemId <= 0 || (cursorItemId != 0 && cursorItemId != itemId) || cursorCount >= MaxStack(itemId)) return false;
        cursorItemId = itemId;
        cursorCount++;
        return true;
    }

    bool CanUnequipBag()
    {
        for (int i = 0; i < 8; i++) if (extraSlots[i] != 0) return false;
        return true;
    }

    void DrawBagSlot(Rect r)
    {
        DrawSlot(r, bagItemId, bagItemId == 0 ? 0 : 1, false);
        Event e = Event.current;
        if (e.type != EventType.MouseDown || !r.Contains(e.mousePosition) || e.button != 0) return;
        if (cursorItemId == 0 && bagItemId != 0 && CanUnequipBag())
        {
            cursorItemId = bagItemId; cursorCount = 1; bagItemId = 0;
        }
        else if (cursorItemId != 0 && (GetBagCategory(cursorItemId) == ItemCategory.Bundle || GetBagCategory(cursorItemId) == ItemCategory.Backpack))
        {
            int newCapacity = GetBagCategory(cursorItemId) == ItemCategory.Backpack ? 8 : 3;
            bool safe = true;
            for (int i = newCapacity; i < 8; i++) if (extraSlots[i] != 0) safe = false;
            if (safe && cursorCount == 1)
            {
                int old = bagItemId; bagItemId = cursorItemId;
                cursorItemId = old; cursorCount = old == 0 ? 0 : 1;
            }
        }
        e.Use();
    }

    void OnGUI()
    {
        float size = Mathf.Clamp(slotSize, 42f, 80f);
        float width = 8 * (size + 4);
        DrawInventorySlots((Screen.width - width) / 2f, Screen.height - size - 12, size, false);
        if (eatingSlot >= 0 && eatingProgress > 0f && !IsAnyMenuOpen)
        {
            float remaining = Mathf.Max(0f, eatingDuration - eatingProgress);
            float fraction = Mathf.Clamp01(eatingProgress / Mathf.Max(0.1f, eatingDuration));
            Rect labelRect = new Rect((Screen.width - 200f) * 0.5f, Screen.height - size - 95f, 200f, 24f);
            GUI.Label(labelRect, "Eating... " + remaining.ToString("0.0") + "s");
            Rect bar = new Rect((Screen.width - 200f) * 0.5f, Screen.height - size - 68f, 200f, 12f);
            GUI.Box(bar, GUIContent.none);
            GUI.Box(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * fraction, bar.height - 4f), GUIContent.none);
        }
        if (SelectedItemId == 26 && showSpearCooldownUI && !IsAnyMenuOpen)
        {
            float duration = Mathf.Max(0.1f, spearCooldown);
            float remaining = Mathf.Max(0f, nextSpearHit - Time.time);
            float fraction = 1f - Mathf.Clamp01(remaining / duration);
            const float barWidth = 200f;
            float x = (Screen.width - barWidth) * 0.5f;
            float y = Screen.height - size - 95f;
            string label = remaining > 0f ? "Spear: " + remaining.ToString("0.0") + "s" : "Spear: READY";
            if (remaining > 0f || showSpearReadyText)
                GUI.Label(new Rect(x, y, barWidth, 24f), label);
            GUI.Box(new Rect(x, y + 27f, barWidth, 14f), GUIContent.none);
            GUI.Box(new Rect(x + 2f, y + 29f, (barWidth - 4f) * fraction, 10f), GUIContent.none);
        }
        if (!IsAnyMenuOpen) return;
        if (inventoryOpen)
        {
            float height = 305;
            Rect panel = new Rect((Screen.width - width - 32) / 2f, (Screen.height - height) / 2f, width + 32, height);
            if (inventoryBackground != null) GUI.DrawTexture(panel, inventoryBackground);
            else GUI.Box(panel, GUIContent.none);
            DrawInventorySlots(panel.x + 16, panel.y + 52, size, true);
            GUI.Label(new Rect(panel.x + 16, panel.y + 128, 200, 22), "BAG SLOT");
            DrawBagSlot(new Rect(panel.x + 16, panel.y + 150, size, size));
            GUI.Label(new Rect(panel.x + 105, panel.y + 128, 210, 22), "LANTERN SLOT (OFF HAND)");
            DrawLanternSlot(new Rect(panel.x + 105, panel.y + 150, size, size));
            DrawExtraSlots(panel.x + 16, panel.y + 222, size);
        }
        if (Event.current.type == EventType.MouseUp && Event.current.button == 0) dragSource = -1;
        if (cursorItemId != 0)
        {
            Vector2 mouse = Event.current.mousePosition;
            DrawSlot(new Rect(mouse.x + 12, mouse.y + 12, size, size), cursorItemId, cursorCount, false);
        }
        if (!string.IsNullOrEmpty(GUI.tooltip)) GUI.Label(new Rect(Event.current.mousePosition.x + 14, Event.current.mousePosition.y - 24, 240, 24), GUI.tooltip);
    }
}
