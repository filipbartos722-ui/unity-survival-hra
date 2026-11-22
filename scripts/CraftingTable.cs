using UnityEngine;

// A 4x4 crafting grid. Recipes are shaped and may be shifted within the grid.
// Click an inventory slot, then a grid slot to add one ingredient.
// Click a filled grid slot to return one ingredient to inventory.
public class CraftingTable : MonoBehaviour
{
    public float useDistance = 5f;
    public int[] grid = new int[16];
    public bool IsOpen { get; private set; }

    // The workbench can be picked up only when closed and empty.
    public bool CanPickUp
    {
        get
        {
            if (IsOpen) return false;
            if (grid == null) return true;
            for (int i = 0; i < grid.Length; i++)
                if (grid[i] != 0) return false;
            return true;
        }
    }
    InventorySystem user;
    int sourceSlot = -1;

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Open(InventorySystem inventory)
    {
        user = inventory;
        sourceSlot = -1;
        IsOpen = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        sourceSlot = -1;
        InventorySystem old = user;
        user = null;
        if (old != null) old.CraftingClosed(this);
        // Ingredients remain stored in this crafting table until collected.
    }

    public void InventorySlotClicked(int index)
    {
        if (user == null) return;
        sourceSlot = index >= 0 && index < user.slots.Length && user.slots[index] != 0 ? index : -1;
    }

    void ClickGrid(int index)
    {
        if (user == null) return;
        if (grid[index] != 0)
        {
            if (user.GiveOneToCursor(grid[index]) || user.TryAddItem(grid[index])) grid[index] = 0;
            return;
        }
        if (user.CursorItemId != 0)
        {
            if (user.TakeOneFromCursor(out int itemId)) grid[index] = itemId;
            return;
        }
        if (sourceSlot < 0 || sourceSlot >= user.slots.Length || user.slots[sourceSlot] == 0) return;
        int id = user.slots[sourceSlot];
        if (user.RemoveFromSlot(sourceSlot)) grid[index] = id;
        if (user.slots[sourceSlot] == 0) sourceSlot = -1;
    }

    // Patterns are 3x3. Zero means an empty slot.
    // Pickaxe: S S S / . W . / . W .
    // Axe:     S S . / S W . / . W .
    // Table:   W W . / W W . / . . .
    static readonly int[,] pickaxe = { { 4, 4, 4 }, { 0, 3, 0 }, { 0, 3, 0 } };
    static readonly int[,] axe = { { 4, 4, 0 }, { 4, 3, 0 }, { 0, 3, 0 } };
    static readonly int[,] table = { { 3, 3, 0 }, { 3, 3, 0 }, { 0, 0, 0 } };

    // Building recipes. Each cell contains one ingredient (3 = Wood, 4 = Stone).
    // These recipes require the 4x4 Work Bench: a 2x2 grid has only four cells.
    static readonly int[,] woodFoundation = { { 3, 3, 3, 3 }, { 3, 3, 3, 3 } }; // ID 7, 8x Wood
    static readonly int[,] woodWall = { { 3, 3, 3 }, { 3, 3, 3 } };       // ID 8
    static readonly int[,] woodFloor = { { 3, 3, 3, 3 }, { 0, 3, 0, 0 } }; // ID 13
    static readonly int[,] stoneWall = { { 4, 4, 4 }, { 4, 4, 4 } };      // ID 18
    static readonly int[,] stoneFloor = { { 4, 4, 4, 4 }, { 4, 4, 4, 0 } }; // ID 21

    // Lantern: .II. / IWCI / ISSI / .II. (I=Iron Ingot, W=Wood, C=Coal, S=Stone)
    static readonly int[,] lantern = { {0,36,36,0}, {36,3,32,36}, {36,4,4,36}, {0,36,36,0} }; // ID 46; ID 45 is Dirt

    bool Matches(int[,] pattern)
    {
        // Ignore empty outer rows/columns of the pattern and grid.
        int minR = 3, minC = 3, maxR = -1, maxC = -1;
        for (int r = 0; r < pattern.GetLength(0); r++) for (int c = 0; c < pattern.GetLength(1); c++)
            if (pattern[r, c] != 0)
            { minR = Mathf.Min(minR, r); minC = Mathf.Min(minC, c); maxR = Mathf.Max(maxR, r); maxC = Mathf.Max(maxC, c); }
        int h = maxR - minR + 1, w = maxC - minC + 1;
        for (int offsetR = 0; offsetR <= 4 - h; offsetR++)
        for (int offsetC = 0; offsetC <= 4 - w; offsetC++)
        {
            bool match = true;
            for (int r = 0; r < 4 && match; r++) for (int c = 0; c < 4; c++)
            {
                int pr = r - offsetR + minR, pc = c - offsetC + minC;
                int expected = pr >= minR && pr <= maxR && pc >= minC && pc <= maxC ? pattern[pr, pc] : 0;
                if (grid[r * 4 + c] != expected) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    int Result()
    {
        if (Matches(lantern)) return 46;
        if (Matches(pickaxe)) return 1;
        if (Matches(axe)) return 2;
        if (Matches(table)) return 5;
        if (Matches(woodFoundation)) return 7;
        if (Matches(woodWall)) return 8;
        if (Matches(woodFloor)) return 13;
        if (Matches(stoneWall)) return 18;
        if (Matches(stoneFloor)) return 21;
        return 0;
    }

    void Craft()
    {
        int result = Result();
        if (result == 0 || user == null || !user.CanAddItem(result)) return;
        if (!user.TryAddItem(result)) return;
        for (int i = 0; i < grid.Length; i++) grid[i] = 0;
    }

    void OnGUI()
    {
        if (!IsOpen || user == null) return;
        float size = Mathf.Clamp(user.slotSize, 42f, 64f);
        float gridWidth = 4 * (size + 4);
        float invWidth = 8 * (size + 4);
        float panelWidth = Mathf.Max(invWidth + 28, gridWidth + 150);
        float panelHeight = 4 * (size + 4) + size + (user.ExtraCapacity > 0 ? 225 : 120);
        Rect panel = new Rect((Screen.width - panelWidth) / 2, (Screen.height - panelHeight) / 2, panelWidth, panelHeight);
        if (user.inventoryBackground != null) GUI.DrawTexture(panel, user.inventoryBackground);
        else GUI.Box(panel, "CRAFTING TABLE");
        GUI.Label(new Rect(panel.x + 14, panel.y + 12, 340, 22), "CRAFTING TABLE - 4 x 4");
        float gx = panel.x + 15, gy = panel.y + 40;
        for (int i = 0; i < 16; i++)
        {
            Rect r = new Rect(gx + (i % 4) * (size + 4), gy + (i / 4) * (size + 4), size, size);
            user.DrawSlot(r, grid[i], grid[i] == 0 ? 0 : 1, false);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) ClickGrid(i);
        }
        int output = Result();
        Rect resultRect = new Rect(gx + gridWidth + 25, gy + size, size + 10, size + 10);
        user.DrawSlot(resultRect, output, output == 0 ? 0 : 1, false);
        if (GUI.Button(resultRect, GUIContent.none, GUIStyle.none)) Craft();
        GUI.Label(new Rect(resultRect.x - 8, resultRect.y - 25, 115, 25), "CRAFT OUTPUT");
        float invY = gy + 4 * (size + 4) + 32;
        GUI.Label(new Rect(gx, invY - 24, 440, 24), "INVENTORY - Pick up a stack, then click crafting cell");
        user.DrawInventorySlots(gx, invY, size, true);
        if (user.ExtraCapacity > 0)
        {
            GUI.Label(new Rect(gx, invY + size + 4, 200, 22), "BAG STORAGE");
            user.DrawExtraSlots(gx, invY + size + 25, size);
        }
        if (GUI.Button(new Rect(panel.x + panelWidth - 75, panel.y + 8, 62, 25), "CLOSE")) Close();
        GUI.Label(new Rect(gx, panel.y + panelHeight - 24, panelWidth - 20, 20), "Click a filled crafting cell to return its item. ESC: Close.");
    }
}
