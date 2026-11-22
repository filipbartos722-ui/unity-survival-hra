using UnityEngine;

// Put on a boss prefab together with MobAI. Shows a boss bar while nearby.
public class BossEncounter : MonoBehaviour
{
    public string bossName = "Ancient Bear";
    public float bossMaxHealth = 350f;
    public float displayRange = 35f;
    public Vector2 barSize = new Vector2(440f, 24f);
    public float topMargin = 35f;
    public Color barColor = new Color(0.75f, 0.12f, 0.12f);
    public float currentHealth;
    Transform player;
    Texture2D white;

    void Awake()
    {
        MobAI mob = GetComponent<MobAI>();
        if (mob != null) bossMaxHealth = mob.maxHealth;
        currentHealth = bossMaxHealth;
        white = Texture2D.whiteTexture;
    }

    public void SetHealth(float health, float maxHealth)
    {
        bossMaxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(health, 0f, bossMaxHealth);
    }

    void OnGUI()
    {
        if (player == null)
        {
            SurvivalVitals vitals = FindObjectOfType<SurvivalVitals>();
            if (vitals != null) player = vitals.transform;
        }
        if (player == null || Vector3.Distance(player.position, transform.position) > displayRange) return;
        float width = Mathf.Min(Screen.width - 30f, barSize.x);
        Rect label = new Rect((Screen.width - width) / 2f, topMargin, width, 24f);
        GUI.Label(label, bossName);
        Rect back = new Rect(label.x, label.yMax + 3f, width, barSize.y);
        GUI.DrawTexture(back, white, ScaleMode.StretchToFill, true, 0f, new Color(0.15f, 0.15f, 0.15f), 0f, 0f);
        Rect fill = new Rect(back.x, back.y, width * Mathf.Clamp01(currentHealth / Mathf.Max(1f, bossMaxHealth)), back.height);
        GUI.DrawTexture(fill, white, ScaleMode.StretchToFill, true, 0f, barColor, 0f, 0f);
    }
}
