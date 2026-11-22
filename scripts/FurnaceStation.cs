using UnityEngine;

// Furnace UI uses the same inventory/cursor workflow as CampfireCooking.
public class FurnaceStation : MonoBehaviour
{
    [Header("Player References (optional)")]
    public InventorySystem inventory;
    public Camera viewCamera;

    [Header("Interaction")]
    public KeyCode openKey = KeyCode.F;
    [Min(0.5f)] public float useDistance = 5f;
    [Header("Smelting")]
    [Min(0.1f)] public float smeltSeconds = 5f;
    [Range(1, 69)] public int maxStack = 69;
    public int ironOreId = 33, goldOreId = 34, coalId = 32, woodId = 3;
    public int ironIngotId = 36, goldIngotId = 37;
    [SerializeField] int inputId, inputCount, fuelId, fuelCount, fuelRemaining, outputId, outputCount;
    [SerializeField] float progress;
    InventorySystem user;
    public bool IsOpen { get; private set; }
    public bool CanPickUp => !IsOpen && inputCount == 0 && fuelCount == 0 && fuelRemaining == 0 && outputCount == 0;

    int Product(int id) { return id == ironOreId ? ironIngotId : id == goldOreId ? goldIngotId : 0; }

    void Update()
    {
        if (inputCount > 0 && (fuelCount > 0 || fuelRemaining > 0) && outputCount < maxStack &&
            (outputCount == 0 || outputId == Product(inputId)) && Product(inputId) != 0)
        {
            progress += Time.deltaTime;
            float duration = Mathf.Max(0.1f, smeltSeconds);
            while (progress >= duration && inputCount > 0 && (fuelCount > 0 || fuelRemaining > 0) && outputCount < maxStack)
            {
                progress -= duration;
                int result = Product(inputId);
                if (outputCount > 0 && outputId != result) break;
                if (fuelRemaining == 0)
                {
                    fuelCount--;
                    fuelRemaining = fuelId == coalId ? 6 : 1;
                    if (fuelCount == 0) fuelId = 0;
                }
                fuelRemaining--;
                inputCount--; outputId = result; outputCount++;
                if (inputCount == 0) inputId = 0;
            }
        }
        else if (inputCount == 0) progress = 0f;

        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || user == null ||
                Vector3.Distance(user.transform.position, transform.position) > useDistance + 1f) Close();
            return;
        }
        if (!Input.GetKeyDown(openKey)) return;
        InventorySystem inv = inventory != null ? inventory : FindObjectOfType<InventorySystem>();
        Camera activeCamera = viewCamera != null ? viewCamera : (inv != null ? inv.playerCamera : null);
        if (inv == null || inv.IsAnyMenuOpen || activeCamera == null) return;
        if (Vector3.Distance(inv.transform.position, transform.position) > useDistance) return;
        Transform cam = activeCamera.transform;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, useDistance + 1f,
            ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<FurnaceStation>() == this)
            inv.OpenFurnace(this);
    }

    public void Open(InventorySystem inv) { user = inv; IsOpen = true; }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        InventorySystem previous = user;
        user = null;
        if (previous != null) previous.FurnaceClosed(this);
    }
    void OnDisable() { if (IsOpen) Close(); }

    void OnGUI()
    {
        if (!IsOpen || user == null) return;

        float slot = Mathf.Clamp(user.slotSize, 42f, 60f);
        float width = Mathf.Max(8 * (slot + 4f) + 32f, 560f);
        float height = user.ExtraCapacity > 0 ? 410f : 315f;
        Rect panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        if (user.inventoryBackground != null) GUI.DrawTexture(panel, user.inventoryBackground);
        else GUI.Box(panel, GUIContent.none);

        GUI.Label(new Rect(panel.x + 20, panel.y + 12, 150, 24), "FURNACE");
        if (GUI.Button(new Rect(panel.xMax - 84, panel.y + 10, 68, 26), "CLOSE"))
        {
            Close();
            return;
        }

        float top = panel.y + 64f;
        Rect ore = new Rect(panel.x + 60f, top, slot, slot);
        Rect fuel = new Rect(panel.x + 60f, top + slot + 24f, slot, slot);
        Rect output = new Rect(panel.xMax - 60f - slot, top, slot, slot);
        Rect bar = new Rect(ore.xMax + 28f, top + slot * 0.5f - 7f,
            output.x - ore.xMax - 56f, 14f);

        GUI.Label(new Rect(ore.x, ore.y - 23f, 110f, 21f), "ORE");
        GUI.Label(new Rect(fuel.x, fuel.y - 23f, 110f, 21f), "FUEL");
        GUI.Label(new Rect(output.x, output.y - 23f, 110f, 21f), "OUTPUT");

        user.DrawSlot(ore, inputId, inputCount, false);
        user.DrawSlot(fuel, fuelId, fuelCount, false);
        user.DrawSlot(output, outputId, outputCount, false);

        if (GUI.Button(ore, GUIContent.none, GUIStyle.none))
        {
            if (Event.current.button == 1 && inputCount > 0 && user.GiveOneToCursor(inputId))
            {
                inputCount--;
                if (inputCount == 0) inputId = 0;
            }
            else if (Event.current.button == 0 && Product(user.CursorItemId) != 0 &&
                     (inputCount == 0 || inputId == user.CursorItemId))
            {
                int oreType = user.CursorItemId;
                int moved = user.MoveCursorToCampfire(oreType, maxStack - inputCount);
                if (moved > 0)
                {
                    if (inputCount == 0) inputId = oreType;
                    inputCount += moved;
                }
            }
        }

        if (GUI.Button(fuel, GUIContent.none, GUIStyle.none))
        {
            if (Event.current.button == 1 && fuelCount > 0 && user.GiveOneToCursor(fuelId))
            {
                fuelCount--;
                if (fuelCount == 0) fuelId = 0;
            }
            else if (Event.current.button == 0 &&
                     (user.CursorItemId == coalId || user.CursorItemId == woodId) &&
                     (fuelCount == 0 || fuelId == user.CursorItemId))
            {
                int selectedFuel = user.CursorItemId;
                int moved = user.MoveCursorToCampfire(selectedFuel, maxStack - fuelCount);
                if (moved > 0)
                {
                    if (fuelCount == 0) fuelId = selectedFuel;
                    fuelCount += moved;
                }
            }
        }

        if (GUI.Button(output, GUIContent.none, GUIStyle.none) && outputCount > 0 &&
            user.TakeCampfireOutput(outputId, outputCount))
        {
            outputCount = 0;
            outputId = 0;
        }

        float fill = inputCount > 0 && (fuelCount > 0 || fuelRemaining > 0) && outputCount < maxStack
            ? Mathf.Clamp01(progress / Mathf.Max(0.1f, smeltSeconds)) : 0f;
        GUI.Box(bar, GUIContent.none);
        if (fill > 0f)
            GUI.Box(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * fill, bar.height - 4f), GUIContent.none);
        GUI.Label(new Rect(bar.x, bar.y - 26f, bar.width + 20f, 22f),
            inputCount == 0 ? "ADD ORE" :
            fuelCount == 0 && fuelRemaining == 0 ? "ADD FUEL" :
            outputCount >= maxStack ? "OUTPUT FULL" :
            Mathf.Max(0f, smeltSeconds - progress).ToString("0.0") + "s");
        GUI.Label(new Rect(bar.x, bar.yMax + 8f, bar.width + 80f, 22f),
            "Wood: 1  |  Coal: 6");

        float inventoryY = fuel.yMax + 36f;
        GUI.Label(new Rect(panel.x + 16f, inventoryY - 24f, 120f, 22f), "INVENTORY");
        user.DrawInventorySlots(panel.x + 16f, inventoryY, slot, true);
        if (user.ExtraCapacity > 0)
        {
            GUI.Label(new Rect(panel.x + 16f, inventoryY + slot + 12f, 120f, 22f), "BAG");
            user.DrawExtraSlots(panel.x + 16f, inventoryY + slot + 34f, slot);
        }

        if (user.CursorItemId != 0)
        {
            Vector2 mouse = Event.current.mousePosition;
            user.DrawSlot(new Rect(mouse.x + 12f, mouse.y + 12f, slot, slot),
                user.CursorItemId, user.CursorCount, false);
        }
    }
}
