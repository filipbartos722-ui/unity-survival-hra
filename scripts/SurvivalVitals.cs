using UnityEngine;
using System.Reflection;

/// <summary>Standalone health, hunger and stamina. Attach to the player.</summary>
public class SurvivalVitals : MonoBehaviour
{
    [Header("Health")]
    [Min(1f)] public float maxHealth = 100f;
    [SerializeField] private float health = 100f;
    [Min(0f)] public float starvationDamagePerSecond = 2f;

    [Header("Hunger")]
    [Min(1f)] public float maxHunger = 100f;
    [SerializeField] private float hunger = 100f;
    [Min(0f)] public float hungerLossPerSecond = 0.05f;
    [Range(0f, 1f)] public float lowHungerThreshold = 0.2f;

    [Header("Stamina")]
    [Min(1f)] public float maxStamina = 100f;
    [SerializeField] private float stamina = 100f;
    [Min(0f)] public float sprintCostPerSecond = 18f;
    [Min(0f)] public float staminaRecoveryPerSecond = 14f;
    [Min(0f)] public float recoveryDelay = 0.7f;
    [Range(0f, 1f)] public float hungryStaminaRecoveryMultiplier = 0.5f;
    public KeyCode sprintKey = KeyCode.LeftShift;
    [Range(1f, 3f)] public float sprintSpeedMultiplier = 1.5f;

    [Header("Optional Movement Integration")]
    [Tooltip("Drag your player movement MonoBehaviour here. Leave empty to only track stamina.")]
    public MonoBehaviour movementScript;
    [Tooltip("Name of a public float speed field/property in your movement script, e.g. moveSpeed or speed.")]
    public string movementSpeedMember = "moveSpeed";

    [System.Serializable]
    public class HUDBarSettings
    {
        [Tooltip("Optional icon displayed to the left of this bar.")]
        public Texture2D icon;
        [Min(1f)] public float barWidth = 308f;
        [Min(1f)] public float barHeight = 29f;
        [Min(1f)] public float iconWidth = 38f;
        [Min(1f)] public float iconHeight = 38f;
    }

    [Header("HUD - Bottom Left")]
    public bool showHUD = true;
    [Tooltip("Distance from the left and bottom edges of the screen, in pixels.")]
    public Vector2 hudMargin = new Vector2(18f, 18f);
    [Min(0f)] public float barSpacing = 12f;
    [Min(0f)] public float iconBarSpacing = 8f;
    [Tooltip("If no icon is assigned, the bar uses the full row width without an empty icon gap.")]
    public HUDBarSettings healthHUD = new HUDBarSettings();
    public HUDBarSettings hungerHUD = new HUDBarSettings();
    public HUDBarSettings staminaHUD = new HUDBarSettings();

    [Header("HUD - Smooth Bar Animation")]
    [Tooltip("Smoothly animate all three bars without changing the actual survival values.")]
    public bool smoothHUDValues = true;
    [Tooltip("Higher values make the bars move more slowly and smoothly. Recommended: 0.35 - 0.6.")]
    [Range(0.05f, 2f)] public float hudSmoothTime = 0.45f;
    private float displayedHealth, displayedHunger, displayedStamina;
    private float healthVelocity, hungerVelocity, staminaVelocity;

    public float Health => health;
    public float Hunger => hunger;
    public float Stamina => stamina;
    public bool IsDead => health <= 0f;
    public bool IsSprinting { get; private set; }

    private float lastStaminaUseTime = -100f;
    private FieldInfo speedField;
    private PropertyInfo speedProperty;
    private MonoBehaviour cachedMovement;
    private string cachedMemberName;
    private float baseSpeed;
    private bool speedConnected;
    private bool wasApplyingSprint;

    private void Awake()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        displayedHealth = health;
        displayedHunger = hunger;
        displayedStamina = stamina;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (IsDead)
        {
            IsSprinting = false;
            ApplySprintSpeed(false);
            return;
        }

        hunger = Mathf.Max(0f, hunger - hungerLossPerSecond * dt);
        if (hunger <= 0f && starvationDamagePerSecond > 0f)
            TakeDamage(starvationDamagePerSecond * dt);

        bool moving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
                      Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
        bool wantsSprint = Input.GetKey(sprintKey) && moving;
        IsSprinting = wantsSprint && stamina > 0f && !IsDead;
        if (IsSprinting)
        {
            stamina = Mathf.Max(0f, stamina - sprintCostPerSecond * dt);
            lastStaminaUseTime = Time.time;
        }
        else if (Time.time - lastStaminaUseTime >= recoveryDelay)
        {
            float recovery = staminaRecoveryPerSecond;
            if (hunger <= maxHunger * lowHungerThreshold)
                recovery *= hungryStaminaRecoveryMultiplier;
            stamina = Mathf.Min(maxStamina, stamina + recovery * dt);
        }
        ApplySprintSpeed(IsSprinting);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead) return;
        health = Mathf.Max(0f, health - amount);
        if (health <= 0f) Debug.Log("Player died (SurvivalVitals). Add your own respawn/death screen logic.", this);
    }

    public void Heal(float amount) { if (amount > 0f) health = Mathf.Min(maxHealth, health + amount); }
    public void Eat(float amount) { if (amount > 0f) hunger = Mathf.Min(maxHunger, hunger + amount); }
    public bool TryUseStamina(float amount)
    {
        if (amount < 0f || IsDead || stamina < amount) return false;
        stamina -= amount;
        lastStaminaUseTime = Time.time;
        return true;
    }

    private void ConnectMovement()
    {
        if (cachedMovement == movementScript && cachedMemberName == movementSpeedMember) return;
        RestoreMovementSpeed();
        cachedMovement = movementScript;
        cachedMemberName = movementSpeedMember;
        speedConnected = false;
        speedField = null;
        speedProperty = null;
        if (movementScript == null || string.IsNullOrEmpty(movementSpeedMember)) return;
        var type = movementScript.GetType();
        speedField = type.GetField(movementSpeedMember, BindingFlags.Public | BindingFlags.Instance);
        if (speedField != null && speedField.FieldType != typeof(float)) speedField = null;
        if (speedField == null)
        {
            speedProperty = type.GetProperty(movementSpeedMember, BindingFlags.Public | BindingFlags.Instance);
            if (speedProperty != null && (speedProperty.PropertyType != typeof(float) || !speedProperty.CanRead || !speedProperty.CanWrite)) speedProperty = null;
        }
        if (speedField == null && speedProperty == null)
        {
            Debug.LogWarning("SurvivalVitals: No public float movement speed member named '" + movementSpeedMember + "' on " + type.Name + ". Stamina still works, but sprint speed will not change.", this);
            return;
        }
        baseSpeed = ReadSpeed();
        speedConnected = true;
    }

    private float ReadSpeed() => speedField != null ? (float)speedField.GetValue(cachedMovement) : (float)speedProperty.GetValue(cachedMovement, null);
    private void WriteSpeed(float value)
    {
        if (speedField != null) speedField.SetValue(cachedMovement, value);
        else speedProperty.SetValue(cachedMovement, value, null);
    }

    private void ApplySprintSpeed(bool sprint)
    {
        ConnectMovement();
        if (!speedConnected || cachedMovement == null) return;
        // Do not overwrite normal speed every frame: another movement script may manage it.
        if (sprint && !wasApplyingSprint)
        {
            baseSpeed = ReadSpeed();
            WriteSpeed(baseSpeed * sprintSpeedMultiplier);
            wasApplyingSprint = true;
        }
        else if (!sprint && wasApplyingSprint)
        {
            WriteSpeed(baseSpeed);
            wasApplyingSprint = false;
        }
    }

    private void RestoreMovementSpeed()
    {
        if (wasApplyingSprint && speedConnected && cachedMovement != null)
            WriteSpeed(baseSpeed);
        wasApplyingSprint = false;
    }

    private void OnDisable() { RestoreMovementSpeed(); IsSprinting = false; }
    private void OnDestroy() { RestoreMovementSpeed(); }

    private void LateUpdate()
    {
        if (!smoothHUDValues)
        {
            displayedHealth = health;
            displayedHunger = hunger;
            displayedStamina = stamina;
            healthVelocity = hungerVelocity = staminaVelocity = 0f;
            return;
        }

        // SmoothDamp uses a stable time-based interpolation for both increases and decreases.
        // This changes only the visual fill, not the real gameplay values.
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        float smoothTime = Mathf.Max(0.05f, hudSmoothTime);
        displayedHealth = Mathf.SmoothDamp(displayedHealth, health, ref healthVelocity, smoothTime, Mathf.Infinity, dt);
        displayedHunger = Mathf.SmoothDamp(displayedHunger, hunger, ref hungerVelocity, smoothTime, Mathf.Infinity, dt);
        displayedStamina = Mathf.SmoothDamp(displayedStamina, stamina, ref staminaVelocity, smoothTime, Mathf.Infinity, dt);
        displayedHealth = Mathf.Clamp(displayedHealth, 0f, maxHealth);
        displayedHunger = Mathf.Clamp(displayedHunger, 0f, maxHunger);
        displayedStamina = Mathf.Clamp(displayedStamina, 0f, maxStamina);
    }

    private void OnGUI()
    {
        if (!showHUD) return;

        // IMGUI coordinates start at the top left. Calculate from the bottom
        // so all three rows stay anchored to the bottom-left corner.
        float h0 = RowHeight(healthHUD);
        float h1 = RowHeight(hungerHUD);
        float h2 = RowHeight(staminaHUD);
        float totalHeight = h0 + h1 + h2 + 2f * Mathf.Max(0f, barSpacing);
        float x = Mathf.Max(0f, hudMargin.x);
        float y = Screen.height - Mathf.Max(0f, hudMargin.y) - totalHeight;

        DrawBar(x, y, displayedHealth, maxHealth, new Color(0.85f, 0.22f, 0.24f), healthHUD);
        y += h0 + Mathf.Max(0f, barSpacing);
        DrawBar(x, y, displayedHunger, maxHunger, new Color(0.92f, 0.64f, 0.18f), hungerHUD);
        y += h1 + Mathf.Max(0f, barSpacing);
        DrawBar(x, y, displayedStamina, maxStamina, new Color(0.22f, 0.7f, 0.35f), staminaHUD);
    }

    private static float RowHeight(HUDBarSettings settings)
    {
        if (settings == null) return 29f;
        return Mathf.Max(1f, settings.barHeight,
            settings.icon != null ? Mathf.Max(1f, settings.iconHeight) : 0f);
    }

    private void DrawBar(float x, float y, float value, float maximum, Color fill, HUDBarSettings settings)
    {
        if (settings == null) return;
        float rowHeight = RowHeight(settings);
        float width = Mathf.Max(1f, settings.barWidth);
        float height = Mathf.Max(1f, settings.barHeight);

        if (settings.icon != null)
        {
            float iconWidth = Mathf.Max(1f, settings.iconWidth);
            float iconHeight = Mathf.Max(1f, settings.iconHeight);
            GUI.DrawTexture(new Rect(x, y + (rowHeight - iconHeight) * 0.5f,
                iconWidth, iconHeight), settings.icon, ScaleMode.ScaleToFit, true);
            x += iconWidth + Mathf.Max(0f, iconBarSpacing);
        }

        Rect outer = new Rect(x, y + (rowHeight - height) * 0.5f, width, height);
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(outer, Texture2D.whiteTexture);

        float border = Mathf.Min(2f, (width - 1f) * 0.5f, (height - 1f) * 0.5f);
        float innerWidth = Mathf.Max(0f, width - border * 2f);
        float innerHeight = Mathf.Max(0f, height - border * 2f);
        float fraction = Mathf.Clamp01(value / Mathf.Max(0.001f, maximum));
        if (fraction > 0f && innerWidth > 0f && innerHeight > 0f)
        {
            GUI.color = fill;
            GUI.DrawTexture(new Rect(outer.x + border, outer.y + border,
                innerWidth * fraction, innerHeight), Texture2D.whiteTexture);
        }
        GUI.color = oldColor;
    }
}
