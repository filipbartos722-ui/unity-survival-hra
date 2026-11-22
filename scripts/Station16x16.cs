using UnityEngine;

// Attach to an Anvil or Stone Carving Station prefab with a collider.
// F opens when player looks at station; 16x16 grid, scrollable 8x8 viewport.
// Stone recipes intentionally empty. Anvil supports a configurable iron pickaxe pattern.
public class Station16x16 : MonoBehaviour
{
    public enum StationType { Anvil, StoneCarving }
    public StationType type;
    public int ironIngotId = 36;
    public int woodId = 3;
    public int ironPickaxeId = 39;
    public float useDistance = 5f;
    public int[] grid = new int[256];
    public InventorySystem user;
    public Camera viewCamera;
    bool opened;
    Vector2 scroll;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { opened = false; return; }
        if (Input.GetKeyDown(KeyCode.F) && user != null && viewCamera != null)
        {
            if (opened) { opened = false; return; }
            if (user.IsAnyMenuOpen) return;
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out RaycastHit hit, useDistance)
                && hit.collider.GetComponentInParent<Station16x16>() == this) opened = true;
        }
    }
    void Cell(int index)
    {
        if (grid[index] != 0)
        {
            if (user.GiveOneToCursor(grid[index]) || user.TryAddItem(grid[index])) grid[index] = 0;
        }
        else if (user.TakeOneFromCursor(out int item)) grid[index] = item;
    }
    bool MatchesIronPickaxe()
    {
        if (type != StationType.Anvil) return false;
        // Proposed 3x3 recipe: III / .W. / .W.
        for (int r0 = 0; r0 <= 13; r0++) for (int c0 = 0; c0 <= 13; c0++)
        {
            bool match = true;
            for (int r = 0; r < 16 && match; r++) for (int c = 0; c < 16; c++)
            {
                int expected = 0;
                if (r == r0 && c >= c0 && c < c0 + 3) expected = ironIngotId;
                if ((r == r0 + 1 || r == r0 + 2) && c == c0 + 1) expected = woodId;
                if (grid[r * 16 + c] != expected) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }
    void Craft()
    {
        if (!MatchesIronPickaxe() || !user.TryAddItem(ironPickaxeId)) return;
        for (int i = 0; i < grid.Length; i++) grid[i] = 0;
    }
    void OnGUI()
    {
        if (!opened || user == null) return;
        Rect panel = new Rect(Mathf.Max(5, (Screen.width - 520) / 2), Mathf.Max(5, (Screen.height - 580) / 2), Mathf.Min(520, Screen.width - 10), Mathf.Min(580, Screen.height - 10));
        GUI.Box(panel, type == StationType.Anvil ? "ANVIL 16x16" : "STONE CARVING 16x16");
        GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 25, panel.width - 20, panel.height - 35));
        GUILayout.Label("Cursor: " + user.CursorItemId + " x " + user.CursorCount + " | Click to place/take 1");
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(100, panel.height - 160)));
        for (int r = 0; r < 16; r++)
        {
            GUILayout.BeginHorizontal();
            for (int c = 0; c < 16; c++)
            {
                int index = r * 16 + c;
                if (GUILayout.Button(grid[index] == 0 ? "." : grid[index].ToString(), GUILayout.Width(34), GUILayout.Height(34))) Cell(index);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        if (type == StationType.Anvil && GUILayout.Button(MatchesIronPickaxe() ? "CRAFT IRON PICKAXE" : "Iron Pickaxe: III / .W. / .W.", GUILayout.Height(34))) Craft();
        if (GUILayout.Button("CLOSE (ESC)", GUILayout.Height(28))) opened = false;
        GUILayout.EndArea();
    }
}
