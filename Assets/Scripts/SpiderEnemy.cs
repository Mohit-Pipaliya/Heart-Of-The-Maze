using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class SpiderEnemy : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  INSPECTOR CONTROLS (All Spider Parameters Fully Tunable In Inspector)
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("─── Health & Durability ─────────────────────────────────────")]
    [Tooltip("Spider ki total max health")]
    public float maxHealth = 1500f;

    [SerializeField, Tooltip("Runtime active health (inspector me live dikhta hai)")]
    private float currentHealth;

    [Tooltip("Damage lagne par spider kitne second stun rahega (seconds)")]
    [Range(0f, 2f)]
    public float hitStunDuration = 0.45f;

    [Tooltip("Goli ya talwar lagne par spider kitna peeche hatega (meters)")]
    [Range(0f, 2f)]
    public float knockbackDistance = 0.35f;

    [Tooltip("Marne ke baad body kitne second baad gayab hogi (seconds)")]
    [Range(0.5f, 20f)]
    public float corpseDespawnDelay = 5.0f;

    [Header("─── AI Ranges & Territory ───────────────────────────────────")]
    [Tooltip("Kitne paas aane par spider attack shuru karega (meters)")]
    public float attackRange = 30f;

    [Tooltip("NavMeshAgent player ke kitne paas aake rukega (meters)")]
    public float stoppingDistance = 8.2f;

    [Tooltip("Player kitni doori pe aane se spider chase shuru karega (meters)")]
    public float aggroDistance = 45f;

    [Tooltip("Territory Trigger: is collider ke andar spider patrol karega (optional)")]
    public Collider territoryTrigger;

    [Tooltip("Territory na ho toh spawn point ke aas paas kitne area me ghumega (meters)")]
    public float patrolRadius = 15f;

    [Header("─── Combat & Attack Damage ──────────────────────────────────")]
    [Tooltip("Ek attack me player ko kitna damage hoga (HP)")]
    public float damageToPlayer = 10f;

    [Tooltip("Do attacks ke beech ka cooldown time (seconds)")]
    [Range(0.1f, 10f)]
    public float attackCooldown = 2.2f;

    [Tooltip("Attack animation start hone ke kitne second baad damage lage (seconds)")]
    [Range(0.05f, 2f)]
    public float damageHitDelay = 0.45f;

    [Tooltip("Attack ke time kitni extra reach me player ho toh damage lage (meters)")]
    public float damageReachTolerance = 2.5f;

    [Header("─── Movement Speeds & Steering ──────────────────────────────")]
    [Tooltip("Patrol karte waqt chalne ki speed (units/sec)")]
    public float walkSpeed = 8f;

    [Tooltip("Player ko chase karte waqt bhagne ki speed (units/sec)")]
    public float runSpeed = 8f;

    [Tooltip("NavMeshAgent ki acceleration (jitna zyada, utna turant speed pakdega)")]
    public float acceleration = 15f;

    [Tooltip("Player ki taraf ghoomne / face karne ki speed")]
    public float rotationSpeed = 8f;

    [Tooltip("NavMeshAgent avoidance radius")]
    public float agentRadius = 4.8f;

    [Tooltip("NavMeshAgent height")]
    public float agentHeight = 4.0f;

    [Header("─── Physical Collision & Body Size ──────────────────────────")]
    [Tooltip("Player spider ke andar na ghus sake")]
    public bool enableAntiPenetration = true;

    [Tooltip("Spider ke solid capsule collider ka world radius (meters)")]
    public float bodyColliderRadius = 6.2f;

    [Tooltip("Spider ke solid capsule collider ki world height (meters)")]
    public float bodyColliderHeight = 6.5f;

    [Tooltip("Spider ke solid capsule collider ka vertical center (meters)")]
    public float bodyColliderCenterY = 2.8f;

    [Tooltip("Player ko spider se kitni door push karna hai (meters)")]
    public float safeDistance = 6.8f;

    [Tooltip("Player ko bahar dhakelne ki force (0-1)")]
    [Range(0.1f, 1f)]
    public float pushPlayerForce = 0.9f;

    [Header("─── HitBox & Targeting ───────────────────────────────────────")]
    [Tooltip("Spider ke 3D mesh ke hisaab se bullet hitbox auto-fit karna")]
    public bool autoFitHitbox = true;

    [Tooltip("Manual HitBox size agar auto-fit off ho")]
    public Vector3 manualHitboxSize = new Vector3(16f, 5.5f, 16f);

    [Tooltip("Manual HitBox center agar auto-fit off ho")]
    public Vector3 manualHitboxCenter = new Vector3(0f, 2.5f, 0f);

    [Header("─── Audio & Visual Effects ──────────────────────────────────")]
    [Tooltip("Attack aur hit sound effects enable/disable")]
    public bool enableAudio = true;

    [Tooltip("Blood burst aur hit markers enable/disable")]
    public bool enableVFX = true;

    [Tooltip("Damage lagne par red flash light enable/disable")]
    public bool enableHitFlash = true;

    [Header("─── Target Reference ─────────────────────────────────────────")]
    [Tooltip("Player transform reference (agar khali ho toh automatically find karega)")]
    public Transform player;

    [Header("─── Scene View Debug Gizmos ─────────────────────────────────")]
    [Tooltip("Scene view me ranges ke rings draw karna")]
    public bool showDebugGizmos = true;
    public Color attackRangeColor = new Color(1f, 0.2f, 0.2f, 0.6f);
    public Color stoppingDistanceColor = new Color(0.2f, 1f, 0.4f, 0.6f);
    public Color aggroDistanceColor = new Color(1f, 0.9f, 0.2f, 0.35f);
    public Color safeDistanceColor = new Color(0.2f, 0.6f, 1f, 0.45f);

    // ═══════════════════════════════════════════════════════════════════════════
    //  INTERNAL RUNTIME STATE
    // ═══════════════════════════════════════════════════════════════════════════

    private NavMeshAgent agent;
    private Animator anim;
    private Vector3 startPos;
    private float lastAttackTime;
    private float _animSpeedTarget = 0f;
    private float _animSpeedCurrent = 0f;

    private bool isDead = false;
    private bool _isAttacking;
    private bool _isChasing;
    private float _hitStunUntil;
    private bool _offMeshJumping;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    // Animator Parameter Hashes (Fast lookup)
    private int hashSpeed;
    private int hashAttack;
    private int hashHit;
    private int hashDie;
    private int hashJump;

    // ═══════════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE & LIVE INSPECTOR SYNC
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnValidate()
    {
        // Enforce valid safe ranges without breaking user desires
        maxHealth = Mathf.Max(1f, maxHealth);
        attackRange = Mathf.Max(0.5f, attackRange);
        aggroDistance = Mathf.Max(attackRange, aggroDistance);
        stoppingDistance = Mathf.Max(0.1f, stoppingDistance);
        walkSpeed = Mathf.Max(0.1f, walkSpeed);
        runSpeed = Mathf.Max(0.1f, runSpeed);
        acceleration = Mathf.Max(0.5f, acceleration);
        rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
        damageToPlayer = Mathf.Max(0f, damageToPlayer);
        attackCooldown = Mathf.Max(0.05f, attackCooldown);
        damageHitDelay = Mathf.Max(0f, damageHitDelay);
        hitStunDuration = Mathf.Max(0f, hitStunDuration);
        knockbackDistance = Mathf.Max(0f, knockbackDistance);
        corpseDespawnDelay = Mathf.Max(0.1f, corpseDespawnDelay);
        safeDistance = Mathf.Max(0.5f, safeDistance);
        bodyColliderRadius = Mathf.Max(0.1f, bodyColliderRadius);
        bodyColliderHeight = Mathf.Max(0.1f, bodyColliderHeight);

        // Live-sync with NavMeshAgent in Play Mode
        if (Application.isPlaying && agent != null && agent.enabled)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.acceleration = acceleration;
            agent.radius = agentRadius;
            agent.height = agentHeight;
            agent.speed = _isChasing ? runSpeed : walkSpeed;
        }

        UpdateCapsuleCollider();
    }

    private void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        // Configure Layer (Layer 23 is "Enemy")
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            gameObject.layer = enemyLayer;
        }

        // Setup CapsuleCollider for physical body collision
        UpdateCapsuleCollider();

        // Broad HitBox across entire carapace & leg span for 100% reliable bullet hits & crosshairs
        SetupHitbox(enemyLayer);
    }

    private void SetupHitbox(int enemyLayer)
    {
        float sx = Mathf.Max(0.001f, transform.lossyScale.x);
        float sy = Mathf.Max(0.001f, transform.lossyScale.y);
        float sz = Mathf.Max(0.001f, transform.lossyScale.z);

        Transform hb = transform.Find("SpiderHitBox");
        if (hb == null)
        {
            GameObject hbObj = new GameObject("SpiderHitBox");
            hbObj.transform.SetParent(transform, false);
            if (enemyLayer >= 0) hbObj.layer = enemyLayer;

            BoxCollider box = hbObj.AddComponent<BoxCollider>();
            box.size = new Vector3(manualHitboxSize.x / sx, manualHitboxSize.y / sy, manualHitboxSize.z / sz);
            box.center = new Vector3(manualHitboxCenter.x / sx, manualHitboxCenter.y / sy, manualHitboxCenter.z / sz);
            box.isTrigger = false;
        }
    }

    public void UpdateCapsuleCollider()
    {
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap == null)
        {
            cap = gameObject.AddComponent<CapsuleCollider>();
        }

        float sx = Mathf.Max(0.001f, transform.lossyScale.x);
        float sy = Mathf.Max(0.001f, transform.lossyScale.y);

        cap.direction = 1; // Y-axis
        cap.radius = bodyColliderRadius / sx;
        cap.height = bodyColliderHeight / sy;
        cap.center = new Vector3(0f, bodyColliderCenterY / sy, 0f);
        cap.isTrigger = false;
    }

    private void Start()
    {
        // NO HARDCODED OVERWRITES! What the user enters in Inspector stays 100% in game!
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        if (anim != null) anim.applyRootMotion = false;

        currentHealth = maxHealth;
        startPos = transform.position;

        // Automatically find player if not assigned
        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = stoppingDistance;
            agent.radius = agentRadius;
            agent.height = agentHeight;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        if (autoFitHitbox)
        {
            FitHitboxToBody();
        }

        EnsurePlayerCollision();

        hashSpeed = Animator.StringToHash("Speed");
        hashAttack = Animator.StringToHash("Attack");
        hashHit = Animator.StringToHash("Hit");
        hashDie = Animator.StringToHash("Die");
        hashJump = Animator.StringToHash("Jump");
    }

    private void FitHitboxToBody()
    {
        Renderer[] renders = GetComponentsInChildren<Renderer>();
        if (renders == null || renders.Length == 0) return;

        Bounds world = renders[0].bounds;
        for (int i = 1; i < renders.Length; i++)
        {
            if (renders[i] != null)
                world.Encapsulate(renders[i].bounds);
        }

        Transform hb = transform.Find("SpiderHitBox");
        if (hb == null) return;
        BoxCollider box = hb.GetComponent<BoxCollider>();
        if (box == null) return;

        float sx = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        float sy = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float sz = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.z));
        box.center = transform.InverseTransformPoint(world.center);
        box.size = new Vector3(world.size.x / sx, world.size.y / sy, world.size.z / sz);
        box.isTrigger = false;
    }

    private void EnsurePlayerCollision()
    {
        if (player == null) return;
        Collider myCol = GetComponent<Collider>();
        if (myCol == null) return;

        Collider[] pCols = player.GetComponentsInChildren<Collider>();
        foreach (var pc in pCols)
        {
            if (pc != null && pc != myCol)
            {
                Physics.IgnoreCollision(myCol, pc, false);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  UPDATE & AI LOGIC
    // ═══════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (isDead) return;

        // Active Anti-Penetration: Player can NEVER walk or clip inside spider!
        if (enableAntiPenetration && player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distToPlayer = toPlayer.magnitude;

            if (distToPlayer < safeDistance)
            {
                Vector3 pushOut = distToPlayer > 0.001f ? (toPlayer / distToPlayer) : transform.forward;
                float overlap = safeDistance - distToPlayer;

                // Push player character controller firmly outward
                CharacterController playerCC = player.GetComponent<CharacterController>();
                if (playerCC != null && playerCC.enabled)
                {
                    playerCC.Move(pushOut * (overlap * pushPlayerForce));
                }
                else
                {
                    player.position += pushOut * (overlap * pushPlayerForce);
                }

                // Push spider slightly away if agent is on NavMesh
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.Move(-pushOut * (overlap * (1f - pushPlayerForce) * 0.5f));
                }
            }
        }

        if (anim != null)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag("Hit") || Time.time < _hitStunUntil)
            {
                if (agent != null && agent.enabled) agent.isStopped = true;
                return;
            }
        }

        if (agent != null && agent.enabled && agent.isOnOffMeshLink)
        {
            if (!_offMeshJumping)
                StartCoroutine(CompleteOffMeshJump());
            return;
        }

        // Distance aur territory check
        float distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
        bool isPlayerInTerritory = territoryTrigger != null && player != null && territoryTrigger.bounds.Contains(player.position);
        bool shouldChase = isPlayerInTerritory || (distanceToPlayer <= aggroDistance);

        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else if (shouldChase)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }

        _animSpeedTarget = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        _animSpeedCurrent = Mathf.Lerp(_animSpeedCurrent, _animSpeedTarget, Time.deltaTime * 8f);
        if (anim != null) anim.SetFloat(hashSpeed, _animSpeedCurrent);
    }

    private System.Collections.IEnumerator CompleteOffMeshJump()
    {
        _offMeshJumping = true;
        if (anim != null)
        {
            anim.ResetTrigger(hashJump);
            anim.SetTrigger(hashJump);
        }
        yield return new WaitForSeconds(0.35f);
        if (agent != null && agent.enabled && agent.isOnOffMeshLink)
            agent.CompleteOffMeshLink();
        _offMeshJumping = false;
    }

    private void Patrol()
    {
        if (agent == null || !agent.enabled) return;

        _isChasing = false;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stoppingDistance;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Vector3 randomDir;

            if (territoryTrigger != null)
            {
                Bounds b = territoryTrigger.bounds;
                randomDir = new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    transform.position.y,
                    Random.Range(b.min.z, b.max.z)
                );
            }
            else
            {
                randomDir = startPos + Random.insideUnitSphere * patrolRadius;
            }

            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void ChasePlayer()
    {
        if (player == null || agent == null || !agent.enabled) return;

        _isChasing = true;
        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stoppingDistance;
        agent.SetDestination(player.position);
    }

    private void AttackPlayer()
    {
        _isChasing = false;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Smooth look-at player during attack
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }

        if (!_isAttacking && Time.time >= lastAttackTime + attackCooldown)
        {
            if (anim != null)
            {
                anim.ResetTrigger(hashAttack);
                anim.SetTrigger(hashAttack);
            }
            lastAttackTime = Time.time;
            StartCoroutine(DealDamageAfterDelay(damageHitDelay));
        }
    }

    private System.Collections.IEnumerator DealDamageAfterDelay(float delay)
    {
        _isAttacking = true;
        yield return new WaitForSeconds(delay);

        if (!isDead && player != null && Vector3.Distance(transform.position, player.position) <= attackRange + damageReachTolerance)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null && !pc.IsDead)
            {
                pc.TakeDamage((int)damageToPlayer);
                if (enableAudio) GameAudio.PlayBulletHit(player.position, true);
                if (enableVFX)
                {
                    VFXUtil.Burst(player.position + Vector3.up * 1.1f, transform.forward,
                        new Color(0.8f, 0.08f, 0.05f, 1f), 12, 4f, 10f, 0.06f, 0.16f, 0.15f, 0.3f, true, true, 1.5f, 30f);
                }
            }
        }
        _isAttacking = false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DAMAGE, REACTION & DEATH
    // ═══════════════════════════════════════════════════════════════════════════

    public void TakeDamage(float amount, Vector3 hitPoint = default, Vector3 hitNormal = default)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"<color=#FF2222><b>[Spider Hit!]</b></color> Damage: {amount} | HP: {Mathf.Max(0f, currentHealth)}/{maxHealth}");

        // Instantly aggro and chase player if shot or struck
        if (player != null && agent != null && agent.enabled)
        {
            _isChasing = true;
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
        }

        // Visceral Hit Knockback
        if (player != null && knockbackDistance > 0.001f)
        {
            Vector3 pushDir = (transform.position - player.position).normalized;
            pushDir.y = 0f;
            if (agent != null && agent.enabled)
            {
                agent.Move(pushDir * knockbackDistance);
            }
            else
            {
                transform.position += pushDir * knockbackDistance;
            }
        }

        if (GunShooter.Instance != null)
            GunShooter.Instance.RegisterHitMarker(currentHealth <= 0);

        // Resolve exact blood impact origin and spray angle
        Vector3 fxPos = hitPoint;
        if (fxPos == default)
        {
            if (player != null)
            {
                Vector3 toPlayer = (player.position - transform.position).normalized;
                fxPos = transform.position + Vector3.up * 2.8f + toPlayer * 4.2f;
            }
            else
            {
                fxPos = transform.position + Vector3.up * 2.8f;
            }
        }

        if (enableHitFlash)
        {
            VFXUtil.SpawnFadingLight(fxPos, new Color(1.0f, 0.04f, 0.02f), 5.0f, 3.5f, 0.08f);
        }

        _hitStunUntil = Time.time + hitStunDuration;

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (anim != null)
        {
            anim.ResetTrigger(hashHit);
            anim.SetTrigger(hashHit);
        }
    }

    private void Die()
    {
        isDead = true;
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (anim != null)
        {
            anim.ResetTrigger(hashDie);
            anim.SetTrigger(hashDie);
        }

        // Disable colliders so player doesn't collide with dead spider
        Collider[] cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i] != territoryTrigger)
                cols[i].enabled = false;
        }

        // Despawn after configurable delay
        Destroy(gameObject, corpseDespawnDelay);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SCENE VIEW DEBUG GIZMOS
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        Vector3 pos = transform.position;

        // Attack Range (Red Wire Sphere)
        Gizmos.color = attackRangeColor;
        Gizmos.DrawWireSphere(pos, attackRange);

        // Stopping Distance (Green Wire Sphere)
        Gizmos.color = stoppingDistanceColor;
        Gizmos.DrawWireSphere(pos, stoppingDistance);

        // Aggro Distance (Yellow Wire Sphere)
        Gizmos.color = aggroDistanceColor;
        Gizmos.DrawWireSphere(pos, aggroDistance);

        // Anti-Penetration Safe Distance (Blue Wire Sphere)
        if (enableAntiPenetration)
        {
            Gizmos.color = safeDistanceColor;
            Gizmos.DrawWireSphere(pos, safeDistance);
        }

        // Territory Trigger bounds if assigned
        if (territoryTrigger != null)
        {
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.4f);
            Gizmos.DrawWireCube(territoryTrigger.bounds.center, territoryTrigger.bounds.size);
        }
    }
}
