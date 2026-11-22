using UnityEngine;

// Campfire: F to open, E to pick up when empty and closed.
// Raw Beef (24) -> Steak (25). Whole stacks, one item per cookTime.
public class CampfireCooking : MonoBehaviour
{
    [Header("Interaction")]
    public KeyCode openKey = KeyCode.F;
    [Min(0.5f)] public float useDistance = 4f;
    [Header("Cooking")]
    [Min(0.1f)] public float cookTime = 5f;
    [Range(1, 69)] public int maxStack = 69;
    public int rawBeefId = 24;
    public int steakId = 25;
    [SerializeField] int rawCount;
    [SerializeField] int cookedCount;
    [SerializeField] float progress;
    InventorySystem user;
    public bool IsOpen { get; private set; }
    public bool CanPickUp => !IsOpen && rawCount == 0 && cookedCount == 0;

    void Update()
    {
        if (rawCount > 0 && cookedCount < maxStack)
        {
            progress += Time.deltaTime;
            float duration = Mathf.Max(0.1f, cookTime);
            while (progress >= duration && rawCount > 0 && cookedCount < maxStack)
            {
                progress -= duration;
                rawCount--;
                cookedCount++;
            }
        }
        else if (rawCount == 0) progress = 0f;

        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || user == null ||
                Vector3.Distance(user.transform.position, transform.position) > useDistance + 1f) Close();
            return;
        }
        if (!Input.GetKeyDown(openKey)) return;
        InventorySystem inventory = FindObjectOfType<InventorySystem>();
        if (inventory == null || inventory.IsAnyMenuOpen || inventory.playerCamera == null) return;
        if (Vector3.Distance(inventory.transform.position, transform.position) > useDistance) return;
        Transform cameraTransform = inventory.playerCamera.transform;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit,
            useDistance + 1f, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<CampfireCooking>() == this)
            inventory.OpenCampfire(this);
    }

    public void Open(InventorySystem inventory) { user = inventory; IsOpen = true; }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        InventorySystem old = user;
        user = null;
        if (old != null) old.CampfireClosed(this);
    }

    void OnDisable() { if (IsOpen) Close(); }

    void OnGUI()
    {
        if (!IsOpen || user == null) return;
        float slot = Mathf.Clamp(user.slotSize, 42f, 64f);
        float width = Mathf.Max(8 * (slot + 4f) + 28f, 580f);
        float height = user.ExtraCapacity > 0 ? 350f : 260f;
        Rect panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        if (user.inventoryBackground != null) GUI.DrawTexture(panel, user.inventoryBackground);
        else GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 16, panel.y + 10, 260, 25), "CAMPFIRE");
        if (GUI.Button(new Rect(panel.x + width - 78, panel.y + 8, 64, 25), "CLOSE")) { Close(); return; }

        float left = panel.x + 55f;
        float row = panel.y + 65f;
        Rect rawRect = new Rect(left, row, slot, slot);
        Rect cookedRect = new Rect(panel.x + width - slot - 55f, row, slot, slot);
        Rect bar = new Rect(rawRect.xMax + 28f, row + slot / 2f - 8f,
            cookedRect.x - rawRect.xMax - 56f, 16f);

        GUI.Label(new Rect(rawRect.x - 10, row - 25, 120, 23), "RAW BEEF");
        user.DrawSlot(rawRect, rawCount > 0 ? rawBeefId : 0, rawCount, false);
        if (GUI.Button(rawRect, GUIContent.none, GUIStyle.none))
        {
            int moved = user.MoveCursorToCampfire(rawBeefId, maxStack - rawCount);
            rawCount += moved;
        }
        GUI.Box(bar, GUIContent.none);
        float fill = rawCount > 0 && cookedCount < maxStack ? Mathf.Clamp01(progress / Mathf.Max(0.1f, cookTime)) : 0f;
        GUI.Box(new Rect(bar.x + 2, bar.y + 2, Mathf.Max(0f, (bar.width - 4) * fill), bar.height - 4), GUIContent.none);
        string status = cookedCount >= maxStack ? "OUTPUT FULL" : rawCount == 0 ? "WAITING FOR RAW BEEF" :
            Mathf.Max(0f, cookTime - progress).ToString("0.0") + "s / steak";
        GUI.Label(new Rect(bar.x, bar.y - 27, bar.width, 23), status);
        GUI.Label(new Rect(cookedRect.x - 4, row - 25, 120, 23), "STEAK");
        user.DrawSlot(cookedRect, cookedCount > 0 ? steakId : 0, cookedCount, false);
        if (GUI.Button(cookedRect, GUIContent.none, GUIStyle.none) && cookedCount > 0)
        {
            if (user.TakeCampfireOutput(steakId, cookedCount)) cookedCount = 0;
        }

        float inventoryY = row + slot + 42f;
        GUI.Label(new Rect(panel.x + 16, inventoryY - 25, width - 30, 23), "INVENTORY - Click Raw Beef stack, then RAW BEEF slot");
        user.DrawInventorySlots(panel.x + 16, inventoryY, slot, true);
        if (user.ExtraCapacity > 0)
        {
            GUI.Label(new Rect(panel.x + 16, inventoryY + slot + 4, 220, 22), "BAG STORAGE");
            user.DrawExtraSlots(panel.x + 16, inventoryY + slot + 25, slot);
        }
        if (user.CursorItemId != 0)
        {
            Vector2 mouse = Event.current.mousePosition;
            user.DrawSlot(new Rect(mouse.x + 12, mouse.y + 12, slot, slot), user.CursorItemId, user.CursorCount, false);
        }
    }
}
