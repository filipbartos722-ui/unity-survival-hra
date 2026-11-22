using UnityEngine;

// Attach to a cow, bear or boss prefab with a Collider. No NavMesh package required.
public class MobAI : MonoBehaviour
{
    public enum MobType { Cow, Bear, Boss }
    [Header("Mob")]
    public MobType mobType = MobType.Cow;
    public float maxHealth = 65f;
    public float moveSpeed = 1.5f;
    public float detectionRange = 13f;
    public float attackRange = 2f;
    public float attackDamage = 12f;
    public float attackCooldown = 1.5f;
    public float wanderRadius = 9f;
    public float turnSpeed = 7f;
    [Header("Drops")]
    public int dropItemId = 24;
    public int minDrop = 1;
    public int maxDrop = 3;
    public GameObject dropPrefab;
    public int secondaryDropItemId = 30;
    public int secondaryDropAmount = 1;
    [Header("Optional Animation")]
    public Animator animator;
    public string moveBool = "Moving";
    public string attackTrigger = "Attack";
    public string hurtTrigger = "Hurt";
    [Header("Ground")]
    public LayerMask groundMask = ~0;
    public float groundProbeHeight = 3f;
    public float maxGroundSlope = 40f;
    public float gravity = 15f;

    [Header("Movement Fixes")]
    public bool disableAnimatorRootMotion = true;
    [Min(0f)] public float groundClearance = 0.03f;
    [Min(0f)] public float maximumStepUp = 0.45f;

    Rigidbody body;
    Collider ownCollider;
    float health, nextAttack, nextWander;
    Vector3 destination;
    Transform player;
    CharacterController controller;
    float verticalVelocity;
    bool provoked;
    Vector3 horizontalDirection;
    bool wantsToMove;

    void Awake()
    {
        health = Mathf.Max(1f, maxHealth);
        controller = GetComponent<CharacterController>();
        body = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && disableAnimatorRootMotion) animator.applyRootMotion = false;
        // Physics must control vertical motion. The previous version made the
        // Rigidbody kinematic and disabled gravity, causing animals to float.
        if (body != null && controller == null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints &= ~RigidbodyConstraints.FreezePositionY;
            body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        destination = transform.position;
        FindPlayer();
    }

    void FindPlayer()
    {
        SurvivalVitals vitals = FindObjectOfType<SurvivalVitals>();
        if (vitals != null) player = vitals.transform;
    }

    void Update()
    {
        if (player == null) FindPlayer();
        // Gravity still works even when the player has not spawned yet.
        if (player == null) { wantsToMove = false; return; }
        float distance = Vector3.Distance(transform.position, player.position);
        // Cows never attack, even after being hit.
        bool aggressive = mobType != MobType.Cow;
        bool chasing = aggressive && distance <= detectionRange;
        if (chasing)
        {
            destination = player.position;
            if (distance <= attackRange)
            {
                if (Time.time >= nextAttack)
                {
                    SurvivalVitals vitals = player.GetComponent<SurvivalVitals>();
                    if (vitals != null && !vitals.IsDead && attackDamage > 0f) vitals.TakeDamage(attackDamage);
                    if (animator != null && HasParameter(attackTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(attackTrigger);
                    nextAttack = Time.time + Mathf.Max(0.2f, attackCooldown);
                }
                wantsToMove = false;
                if (animator != null && HasParameter(moveBool, AnimatorControllerParameterType.Bool)) animator.SetBool(moveBool, false);
                return;
            }
        }
        else if (Time.time >= nextWander || Vector3.Distance(transform.position, destination) < 1f)
        {
            Vector2 point = Random.insideUnitCircle * Mathf.Max(1f, wanderRadius);
            destination = transform.position + new Vector3(point.x, 0f, point.y);
            nextWander = Time.time + Random.Range(3f, 7f);
        }
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.25f)
        {
            wantsToMove = false;
            if (animator != null && HasParameter(moveBool, AnimatorControllerParameterType.Bool)) animator.SetBool(moveBool, false);
            return;
        }
        direction.Normalize();
        if (animator != null && HasParameter(moveBool, AnimatorControllerParameterType.Bool)) animator.SetBool(moveBool, true);
        // Only move horizontally while standing on a walkable surface.
        // Never snap the animal's Y coordinate to a raycast hit.
        horizontalDirection = direction;
        wantsToMove = true;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        if (controller != null)
        {
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity - gravity * Time.deltaTime;
            controller.Move(direction * moveSpeed * Time.deltaTime +
                Vector3.up * verticalVelocity * Time.deltaTime);
        }
        else if (body == null)
        {
            // A Rigidbody or CharacterController is recommended for gravity.
            transform.position += direction * moveSpeed * Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        if (body == null || controller != null || body.isKinematic) return;

        // Keep the physics engine responsible for gravity and falling.
        // A downward ray checks for ground but does not reposition the mob.
        if (!wantsToMove || !TryFindGround(transform.position, out RaycastHit ground) ||
            Vector3.Angle(ground.normal, Vector3.up) > maxGroundSlope)
        {
            Vector3 stopped = body.velocity;
            stopped.x = 0f;
            stopped.z = 0f;
            body.velocity = stopped;
            return;
        }

        // MovePosition on a dynamic Rigidbody can interfere with gravity.
        // Instead, only adjust horizontal velocity, preserving vertical velocity.
        Vector3 velocity = body.velocity;
        velocity.x = horizontalDirection.x * moveSpeed;
        velocity.z = horizontalDirection.z * moveSpeed;
        body.velocity = velocity;
    }

    bool TryFindGround(Vector3 position, out RaycastHit ground)
    {
        Vector3 origin = position + Vector3.up * Mathf.Max(groundProbeHeight, 1f);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down,
            Mathf.Max(groundProbeHeight, 1f) + 8f, groundMask,
            QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<MobAI>() != null) continue;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundSlope) continue;
            ground = hit;
            return true;
        }
        ground = default(RaycastHit);
        return false;
    }

    public void TakeDamage(float damage, Transform attacker = null)
    {
        if (damage <= 0f || health <= 0f) return;
        health -= damage;
        BossEncounter boss = GetComponent<BossEncounter>();
        if (boss != null) boss.SetHealth(health, maxHealth);
        if (animator != null && HasParameter(hurtTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hurtTrigger);
        if (attacker != null && mobType != MobType.Cow) { player = attacker; provoked = true; }
        if (health <= 0f) Die();
    }

    bool HasParameter(string parameter, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(parameter)) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == parameter && p.type == type) return true;
        return false;
    }

    void Die()
    {
        InventorySystem inventory = FindObjectOfType<InventorySystem>();
        if (inventory != null)
        {
            int amount = Random.Range(Mathf.Max(0, minDrop), Mathf.Max(minDrop, maxDrop) + 1);
            if (amount > 0) inventory.SpawnDroppedItem(dropItemId, amount, transform.position + Vector3.up, Vector3.zero);
            if (secondaryDropAmount > 0 && secondaryDropItemId > 0)
                inventory.SpawnDroppedItem(secondaryDropItemId, secondaryDropAmount, transform.position + Vector3.up, Vector3.zero);
        }
        Destroy(gameObject);
    }
}
