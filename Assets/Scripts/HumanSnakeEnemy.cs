using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// ═══════════════════════════════════════════════════════════════════════════════
//  HumanSnakeEnemy  —  Half-Human, Half-Snake Boss AI
//  Built with the exact same architecture, terrain alignment, territory logic,
//  and combat feel as SpiderEnemy.cs.
//
//  Animator Parameters:
//   Float   : Speed       (0 = Idle, ~2 = Walk, ~5+ = Run/Chase)
//   Trigger : Attack      (Melee strike)
//   Trigger : Hit         (Hit flinch reaction)
//   Trigger : Die         (Death animation)
//
//  Also supports Snake-specific parameter aliases:
//   Float   : locomotion
//   Trigger : attack1, attack2, attack3, gotHit, death, idleBreak
// ═══════════════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class HumanSnakeEnemy : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  INSPECTOR CONTROLS (Fully Tunable In Inspector — Exactly Like SpiderEnemy)
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("─── Health & Durability ─────────────────────────────────────")]
    [Tooltip("HumanSnake ki total max health")]
    public float maxHealth = 1500f;

    [SerializeField, Tooltip("Runtime active health (inspector me live dikhta hai)")]
    private float currentHealth;

    [Header("─── Floating Health Bar (Boss Naga) ───────────────────────────")]
    [Tooltip("HumanSnake ke upar floating BOSS health bar dikhaye")]
    public bool showFloatingHealthBar = true;
    [Tooltip("Health bar ka size / scale (1.0 = normal, 2.0 = double, 3.0 = bada)")]
    [Range(0.5f, 8f)]
    public float healthBarScale = 2.5f;
    [Tooltip("Health bar kitne meters upar float kare agar custom anchor na ho")]
    public float healthBarHeightOffset = 5.5f;
    [Tooltip("Aapne jo 'healthbar position' empty object banaya hai usko yahan drag karein (ya script auto-detect karegi)")]
    public Transform healthBarPosition;
    private HealthBar _floatingHealthBar;

    [Tooltip("Damage lagne par snake kitne second stun rahega (seconds)")]
    [Range(0f, 2f)]
    public float hitStunDuration = 0.35f;

    [Tooltip("Goli ya talwar lagne par snake kitna peeche hatega (meters)")]
    [Range(0f, 2f)]
    public float knockbackDistance = 0.35f;

    [Tooltip("Marne ke baad body kitne second baad gayab hogi (seconds)")]
    [Range(0.5f, 30f)]
    public float corpseDespawnDelay = 6.0f;

    [Header("─── AI Ranges & Territory ───────────────────────────────────")]
    [Tooltip("Kitne paas aane par snake attack shuru karega (meters)")]
    public float attackRange = 3.5f;

    [Tooltip("NavMeshAgent player ke kitne paas aake rukega (meters)")]
    public float stoppingDistance = 2.4f;

    [Tooltip("Player kitni doori pe aane se snake chase shuru karega (meters)")]
    public float aggroDistance = 35f;

    [Tooltip("Territory Trigger: is collider ke andar snake patrol aur guard karega (optional)")]
    public Collider territoryTrigger;

    [Tooltip("Territory na ho toh spawn point ke aas paas kitne area me ghumega (meters)")]
    public float patrolRadius = 12f;

    [Header("─── Combat & Attack Damage ──────────────────────────────────")]
    [Tooltip("Ek attack me player ko kitna damage hoga (HP)")]
    public float damageToPlayer = 20f;

    [Tooltip("Do attacks ke beech ka cooldown time (seconds)")]
    [Range(0.1f, 10f)]
    public float attackCooldown = 2.0f;

    [Tooltip("Attack animation start hone ke kitne second baad damage lage (seconds)")]
    [Range(0.05f, 2f)]
    public float damageHitDelay = 0.45f;

    [Tooltip("Attack ke time kitni extra reach me player ho toh damage lage (meters)")]
    public float damageReachTolerance = 1.8f;

    [Header("─── Movement Speeds & Steering ──────────────────────────────")]
    [Tooltip("Patrol karte waqt chalne ki speed (units/sec)")]
    public float walkSpeed = 2.2f;

    [Tooltip("Player ko chase karte waqt bhagne ki speed (units/sec)")]
    public float runSpeed = 5.2f;

    [Tooltip("NavMeshAgent ki acceleration")]
    public float acceleration = 14f;

    [Tooltip("Player ki taraf ghoomne / face karne ki speed")]
    public float rotationSpeed = 8f;

    [Tooltip("Agar TRUE karenge toh NavMeshAgent ke radius aur height ko script override karegi.")]
    public bool overrideAgentDimensions = false;
    public float agentRadius = 0.5f;
    public float agentHeight = 2.0f;

    [Header("─── Physical Collision & Body Size ──────────────────────────")]
    [Tooltip("Player snake ke andar na ghus sake")]
    public bool enableAntiPenetration = true;

    [Tooltip("Player ko snake se kitni door push karna hai (meters)")]
    public float safeDistance = 1.8f;

    [Tooltip("Player ko bahar dhakelne ki force (0-1)")]
    [Range(0.1f, 1f)]
    public float pushPlayerForce = 0.85f;

    public bool overrideColliderDimensions = false;
    public float bodyColliderRadius = 0.5f;
    public float bodyColliderHeight = 2.0f;
    public float bodyColliderCenterY = 1.0f;

    [Header("─── Terrain Surface & Slope Alignment (Chipak ke chalna) ────")]
    [Tooltip("Snake ko terrain slope ke hisaab se tilt karna aur zameen pe chipka ke chalana")]
    public bool enableTerrainAlignment = true;

    [Tooltip("Layers considered solid ground")]
    public LayerMask groundLayer = ~0;

    [Tooltip("Terrain slope par tilt hone ki speed")]
    public float slopeAlignSpeed = 12f;

    [Tooltip("Zameen se chipke rehne ki snapping speed")]
    public float groundSnapSpeed = 20f;

    [Tooltip("Snake ki body ka zameen se vertical offset")]
    public float groundOffset = 0.0f;

    [Header("─── HitBox & Targeting ───────────────────────────────────────")]
    public bool autoFitHitbox = true;
    public Vector3 manualHitboxSize = new Vector3(1.2f, 2.2f, 1.2f);
    public Vector3 manualHitboxCenter = new Vector3(0f, 1.1f, 0f);

    [Header("─── Audio & Visual Effects ──────────────────────────────────")]
    public bool enableAudio = true;
    public bool enableVFX = true;
    public bool enableHitFlash = true;

    [Header("─── Target Reference ─────────────────────────────────────────")]
    [Tooltip("Player transform reference (khali ho toh automatically find karega)")]
    public Transform player;

    [Header("─── Scene View Debug Gizmos ─────────────────────────────────")]
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
    private bool _isAttacking = false;
    private bool _isChasing = false;
    private float _hitStunUntil;
    private float _lastRescueTime;
    private SnakeTriggerZone _triggerZone;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    // Animator Parameter Hashes (Primary: Spider style)
    private int hashSpeed;
    private int hashAttack;
    private int hashHit;
    private int hashDie;

    // Fallback/Legacy Hashes (Snake controller compatibility)
    private int hashLocomotion;
    private int hashAttack1;
    private int hashAttack2;
    private int hashAttack3;
    private int hashTailWhipRight;
    private int hashTailWhipLeft;
    private int hashGotHit;
    private int hashDeath;
    private int hashIdleBreak;

    private int _attackCounter = 0;

    // ═══════════════════════════════════════════════════════════════════════════
    //  ANIMATION EVENT RECEIVER (Fixes Console Errors!)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Animation event receivers — catch ALL events from the SerpentWarrior FBX clips.
    /// These stubs silence "has no receiver" errors and optionally play audio.
    /// </summary>
    public void PlayAudio(string clipName)
    {
        // Receiver for PlayAudio(string) animation events on attack/cast/taunt/etc clips
        // Optionally play footstep/slither sound if GameAudio has it
    }

    public void PlayAudio()
    {
        // Parameterless overload for any no-arg PlayAudio events
    }

    // Additional animation event stubs to prevent "has no receiver" console errors
    public void LeftHandCast()   { /* Left hand cast VFX trigger stub */ }
    public void RightHandCast()  { /* Right hand cast VFX trigger stub */ }
    public void FootstepLeft()   { /* Footstep left stub */ }
    public void FootstepRight()  { /* Footstep right stub */ }
    public void OnAttackHit()    { /* Melee impact stub */ }
    public void OnAttackStart()  { /* Attack wind-up stub */ }
    public void OnAttackEnd()    { /* Attack recovery stub */ }

    // ═══════════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE & LIVE INSPECTOR SYNC
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        attackRange = Mathf.Max(0.5f, attackRange);
        aggroDistance = Mathf.Max(attackRange, aggroDistance);
        stoppingDistance = Mathf.Clamp(stoppingDistance, 0.5f, attackRange);
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
        slopeAlignSpeed = Mathf.Max(1f, slopeAlignSpeed);
        groundSnapSpeed = Mathf.Max(1f, groundSnapSpeed);

        if (Application.isPlaying && agent != null && agent.enabled)
        {
            agent.baseOffset = groundOffset;
            if (enableTerrainAlignment) agent.updateRotation = false;
            agent.stoppingDistance = stoppingDistance;
            agent.acceleration = acceleration;
            if (overrideAgentDimensions)
            {
                agent.radius = agentRadius;
                agent.height = agentHeight;
            }
            agent.speed = _isChasing ? runSpeed : walkSpeed;
        }

        if (overrideColliderDimensions)
        {
            UpdateCapsuleCollider();
        }
    }

    private void Awake()
    {
        EnsureTriggerZone();

        anim = GetComponent<Animator>();
        if (anim != null) anim.applyRootMotion = false;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) gameObject.layer = enemyLayer;

        if (overrideColliderDimensions)
        {
            UpdateCapsuleCollider();
        }

        SetupHitbox(enemyLayer);
    }

    private void Start()
    {
        EnsureTriggerZone();

        agent = GetComponent<NavMeshAgent>();
        anim  = GetComponent<Animator>();
        if (anim != null) anim.applyRootMotion = false;

        currentHealth = maxHealth;
        startPos = transform.position;

        SnapToGroundImmediate();

        FindPlayer();

        SetupFloatingHealthBar();

        if (agent != null)
        {
            agent.baseOffset = groundOffset;
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 50f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            agent.speed = walkSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = stoppingDistance;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            if (enableTerrainAlignment) agent.updateRotation = false;

            if (overrideAgentDimensions)
            {
                agent.radius = agentRadius;
                agent.height = agentHeight;
            }
            else
            {
                agentRadius = agent.radius;
                agentHeight = agent.height;
            }
        }

        if (autoFitHitbox)
        {
            FitHitboxToBody();
        }

        EnsurePlayerCollision();

        // Initialize animator parameter hashes
        hashSpeed         = Animator.StringToHash("Speed");
        hashAttack        = Animator.StringToHash("Attack");
        hashHit           = Animator.StringToHash("Hit");
        hashDie           = Animator.StringToHash("Die");

        hashLocomotion    = Animator.StringToHash("locomotion");
        hashAttack1       = Animator.StringToHash("attack1");
        hashAttack2       = Animator.StringToHash("attack2");
        hashAttack3       = Animator.StringToHash("attack3");
        hashTailWhipRight = Animator.StringToHash("tailWhipRight");
        hashTailWhipLeft  = Animator.StringToHash("tailWhipLeft");
        hashGotHit        = Animator.StringToHash("gotHit");
        hashDeath         = Animator.StringToHash("death");
        hashIdleBreak     = Animator.StringToHash("idleBreak");

        StartCoroutine(RandomIdleBreakLoop());
    }

    private void SetupHitbox(int enemyLayer)
    {
        float sx = Mathf.Max(0.001f, transform.lossyScale.x);
        float sy = Mathf.Max(0.001f, transform.lossyScale.y);
        float sz = Mathf.Max(0.001f, transform.lossyScale.z);

        Transform hb = transform.Find("SnakeHitBox");
        if (hb == null)
        {
            GameObject hbObj = new GameObject("SnakeHitBox");
            hbObj.transform.SetParent(transform, false);
            if (enemyLayer >= 0) hbObj.layer = enemyLayer;

            BoxCollider box = hbObj.AddComponent<BoxCollider>();
            box.size = new Vector3(manualHitboxSize.x / sx, manualHitboxSize.y / sy, manualHitboxSize.z / sz);
            box.center = new Vector3(manualHitboxCenter.x / sx, manualHitboxCenter.y / sy, manualHitboxCenter.z / sz);
            box.isTrigger = true; // MUST BE TRIGGER so player is never blocked by invisible wall!
        }
        else
        {
            // Fix existing hitbox if it was saved as non-trigger
            BoxCollider box = hb.GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = true;
        }
    }

    public void UpdateCapsuleCollider()
    {
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap == null) cap = gameObject.AddComponent<CapsuleCollider>();

        float sx = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
        float sy = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));

        cap.direction = 1;
        // Keep body collider radius sensible (~1.2m world space) so player can walk up to the boss
        cap.radius = Mathf.Max(0.12f, bodyColliderRadius / sx);
        cap.height = Mathf.Max(0.5f, bodyColliderHeight / sy);
        cap.center = new Vector3(0f, bodyColliderCenterY / sy, 0f);
        cap.isTrigger = false;
    }

    private void EnsureTriggerZone()
    {
        if (territoryTrigger == null)
        {
            GameObject st = GameObject.Find("SnakeTrigger") ?? GameObject.Find("SpiderTrigger");
            if (st != null) territoryTrigger = st.GetComponent<Collider>();
        }

        if (territoryTrigger != null && _triggerZone == null)
        {
            _triggerZone = territoryTrigger.GetComponent<SnakeTriggerZone>();
            if (_triggerZone == null)
                _triggerZone = territoryTrigger.gameObject.AddComponent<SnakeTriggerZone>();
        }
    }

    public bool IsPointInTerritory(Vector3 worldPoint)
    {
        if (territoryTrigger == null) return true;

        if (territoryTrigger is BoxCollider box)
        {
            Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint);
            Vector3 halfSize = box.size * 0.5f;
            Vector3 center = box.center;

            bool inX = Mathf.Abs(localPoint.x - center.x) <= (halfSize.x + 0.5f);
            bool inZ = Mathf.Abs(localPoint.z - center.z) <= (halfSize.z + 0.5f);
            bool inY = Mathf.Abs(localPoint.y - center.y) <= (halfSize.y + 15.0f);

            return inX && inZ && inY;
        }

        Bounds b = territoryTrigger.bounds;
        b.Expand(new Vector3(1f, 30f, 1f));
        return b.Contains(worldPoint);
    }

    private void FindPlayer()
    {
        if (player != null) return;
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            return;
        }
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) player = pObj.transform;
    }

    public bool IsPlayerInTerritory()
    {
        if (player == null) FindPlayer();
        if (player == null) return false;
        if (territoryTrigger == null) return true;

        if (_triggerZone != null && _triggerZone.IsPlayerInside) return true;

        Vector3 feet = player.position;
        Vector3 center = feet + Vector3.up * 1.0f;
        if (IsPointInTerritory(feet) || IsPointInTerritory(center)) return true;

        Vector3 closest = territoryTrigger.ClosestPoint(center);
        if ((closest - center).sqrMagnitude < 0.25f) return true;

        return false;
    }

    private void FitHitboxToBody()
    {
        Renderer[] renders = GetComponentsInChildren<Renderer>();
        if (renders == null || renders.Length == 0) return;

        Bounds world = renders[0].bounds;
        for (int i = 1; i < renders.Length; i++)
        {
            if (renders[i] != null) world.Encapsulate(renders[i].bounds);
        }

        Transform hb = transform.Find("SnakeHitBox");
        if (hb == null) return;
        BoxCollider box = hb.GetComponent<BoxCollider>();
        if (box == null) return;

        float sx = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        float sy = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float sz = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.z));
        box.center = transform.InverseTransformPoint(world.center);
        box.size = new Vector3(world.size.x / sx, world.size.y / sy, world.size.z / sz);
        box.isTrigger = true;
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
                Physics.IgnoreCollision(myCol, pc, false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  UPDATE & AI LOGIC (Exactly Like SpiderEnemy)
    // ═══════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (isDead)
        {
            ProcessTerrainAlignment();
            return;
        }

        if (_floatingHealthBar != null && !Mathf.Approximately(_floatingHealthBar.worldScale, healthBarScale))
        {
            _floatingHealthBar.SetWorldScale(healthBarScale);
        }

        ProcessTerrainAlignment();

        // Anti-Penetration: Player ko sirf tab push karo jab literally mesh ke andar ghus jaaye
        // Scale compensate: 12x scale par safeDistance world-space mein sahi honi chahiye
        if (enableAntiPenetration && player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distToPlayer = toPlayer.magnitude;

            // World-space safe distance (independent of object scale)
            // safeDistance = 1.8f lekin 12x scale par physically body ~6m wide hoti hai
            // Player ke attack range ko block na kare isliye very tight = 0.6f
            float worldSafeDistance = Mathf.Min(safeDistance, 0.6f);

            if (distToPlayer < worldSafeDistance && distToPlayer > 0.001f)
            {
                Vector3 pushOut = toPlayer / distToPlayer;
                float overlap = worldSafeDistance - distToPlayer;

                // Sirf tab push karo jab player literally body ke andar ho
                // Player attacking ho toh push mat karo (taaki sword connect ho sake)
                CharacterController playerCC = player.GetComponent<CharacterController>();
                if (playerCC != null && playerCC.enabled)
                {
                    playerCC.Move(pushOut * (overlap * pushPlayerForce * 0.3f));
                }
                else
                {
                    player.position += pushOut * (overlap * pushPlayerForce * 0.3f);
                }
            }
        }

        // Hit stun / Attack action in progress check
        if (IsPlayingAttackOrHit())
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // Keep facing player smoothly during attack windup
            if (player != null && !isDead)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed * 1.5f);
                }
            }

            _animSpeedTarget = 0f;
            _animSpeedCurrent = Mathf.Lerp(_animSpeedCurrent, 0f, Time.deltaTime * 10f);
            if (anim != null)
            {
                anim.SetFloat(hashSpeed, _animSpeedCurrent);
                anim.SetFloat(hashLocomotion, 0f);
            }
            return;
        }

        // Territory & Aggro Decision (Player inside trigger area = chase, inside attackRange = attack)
        float distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
        bool playerInTerritory = IsPlayerInTerritory();

        bool shouldTargetPlayer = (territoryTrigger != null && playerInTerritory) || (distanceToPlayer <= aggroDistance) || _isChasing;

        // Effective melee attack reach (ensures snake strikes even if stoppingDistance or size offsets)
        float effectiveAttackDistance = Mathf.Max(attackRange, stoppingDistance + 1.2f);

        if (shouldTargetPlayer)
        {
            if (distanceToPlayer <= effectiveAttackDistance)
            {
                AttackPlayer();
            }
            else
            {
                ChasePlayer();
            }
        }
        else
        {
            Patrol();
        }

        // Drive speed parameters smoothly
        _animSpeedTarget = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        _animSpeedCurrent = Mathf.Lerp(_animSpeedCurrent, _animSpeedTarget, Time.deltaTime * 8f);

        if (anim != null)
        {
            anim.SetFloat(hashSpeed, _animSpeedCurrent);

            // Also drive locomotion blend tree float if present
            float normLoco = _animSpeedCurrent > 2.5f ? 1.0f : (_animSpeedCurrent > 0.1f ? 0.5f : 0f);
            anim.SetFloat(hashLocomotion, normLoco);
        }
    }

    private void Patrol()
    {
        if (agent == null || !agent.enabled) return;

        _isChasing = false;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = 1.0f;

        bool reachedDest = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.5f);
        bool insideTerritory = territoryTrigger == null || IsPointInTerritory(transform.position);

        if (reachedDest || !insideTerritory)
        {
            Vector3 targetPoint;

            if (territoryTrigger != null)
            {
                Bounds b = territoryTrigger.bounds;
                float marginX = Mathf.Min(4f, b.extents.x * 0.3f);
                float marginZ = Mathf.Min(4f, b.extents.z * 0.3f);

                float randX = Random.Range(b.min.x + marginX, b.max.x - marginX);
                float randZ = Random.Range(b.min.z + marginZ, b.max.z - marginZ);
                float sampleY = b.center.y;

                if (TryFindGroundSurface(new Vector3(randX, sampleY, randZ), out Vector3 gPoint, out _))
                {
                    sampleY = gPoint.y;
                }

                targetPoint = new Vector3(randX, sampleY, randZ);
            }
            else
            {
                targetPoint = startPos + Random.insideUnitSphere * patrolRadius;
                if (TryFindGroundSurface(targetPoint, out Vector3 gPoint, out _))
                {
                    targetPoint.y = gPoint.y;
                }
            }

            if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 25f, NavMesh.AllAreas))
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
        agent.stoppingDistance = Mathf.Max(1.0f, attackRange * 0.75f);
        agent.SetDestination(player.position);

        // Smooth look-at player
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    public bool IsPlayingAttackOrHit()
    {
        if (_isAttacking || Time.time < _hitStunUntil) return true;
        if (anim != null)
        {
            AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.IsTag("Hit") || state.IsTag("Attack")) return true;
        }
        return false;
    }

    private void AttackPlayer()
    {
        _isChasing = false;

        // Smooth look-at player during attack
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed * 2f);
            }
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (!_isAttacking && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            StartCoroutine(ExecuteAttackRoutine());
        }
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        _isAttacking = true;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        int attackIndex = _attackCounter % 4;
        _attackCounter++;

        float totalDuration = 0.95f;
        float hitDelay = damageHitDelay;

        if (anim != null)
        {
            // Reset all attack triggers
            anim.ResetTrigger(hashAttack);
            anim.ResetTrigger(hashAttack1);
            anim.ResetTrigger(hashAttack2);
            anim.ResetTrigger(hashAttack3);
            anim.ResetTrigger(hashTailWhipRight);

            switch (attackIndex)
            {
                case 0:
                    anim.SetTrigger(hashAttack1);
                    anim.SetTrigger(hashAttack); // Spider-style compatibility
                    totalDuration = 0.90f;
                    hitDelay = 0.38f;
                    break;
                case 1:
                    anim.SetTrigger(hashAttack2);
                    totalDuration = 0.90f;
                    hitDelay = 0.38f;
                    break;
                case 2:
                    anim.SetTrigger(hashAttack3);
                    totalDuration = 1.20f;
                    hitDelay = 0.50f;
                    break;
                case 3:
                    anim.SetTrigger(hashTailWhipRight);
                    totalDuration = 1.10f;
                    hitDelay = 0.45f;
                    break;
            }
        }

        // Wait until attack strikes
        yield return new WaitForSeconds(hitDelay);

        float effectiveReach = Mathf.Max(attackRange, stoppingDistance + 1.2f) + damageReachTolerance;
        if (!isDead && player != null && Vector3.Distance(transform.position, player.position) <= effectiveReach)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null && !pc.IsDead)
            {
                pc.TakeDamage((int)damageToPlayer);
                if (enableAudio) GameAudio.PlayBulletHit(player.position, true);
                if (enableVFX)
                {
                    VFXUtil.Burst(player.position + Vector3.up * 1.1f, transform.forward,
                        new Color(0.85f, 0.08f, 0.05f, 1f), 12, 4f, 10f, 0.06f, 0.16f, 0.15f, 0.3f, true, true, 1.5f, 30f);
                }
            }
        }

        // Wait for remaining swing / recovery so attack animation finishes completely
        float remaining = Mathf.Max(0.05f, totalDuration - hitDelay);
        yield return new WaitForSeconds(remaining);

        _isAttacking = false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DAMAGE, REACTION & DEATH (Exactly Like SpiderEnemy)
    // ═══════════════════════════════════════════════════════════════════════════

    public void TakeDamage(float amount, Vector3 hitPoint = default, Vector3 hitNormal = default)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"<color=#FF2222><b>[Snake Hit!]</b></color> Damage: {amount} | HP: {Mathf.Max(0f, currentHealth)}/{maxHealth}");

        // Update floating Boss health bar
        if (_floatingHealthBar != null)
            _floatingHealthBar.SetHealth(currentHealth);

        // Aggro if hit
        bool canAggro = territoryTrigger == null || IsPlayerInTerritory();
        if (canAggro && player != null && agent != null && agent.enabled)
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
                agent.Move(pushDir * knockbackDistance);
            else
                transform.position += pushDir * knockbackDistance;
        }

        if (GunShooter.Instance != null)
            GunShooter.Instance.RegisterHitMarker(currentHealth <= 0);

        Vector3 fxPos = hitPoint;
        if (fxPos == default)
        {
            if (player != null)
            {
                Vector3 toPlayer = (player.position - transform.position).normalized;
                fxPos = transform.position + Vector3.up * 1.5f + toPlayer * 1.2f;
            }
            else
            {
                fxPos = transform.position + Vector3.up * 1.5f;
            }
        }

        if (enableHitFlash)
        {
            VFXUtil.SpawnFadingLight(fxPos, new Color(1.0f, 0.1f, 0.05f), 4.5f, 3.0f, 0.08f);
        }

        _hitStunUntil = Time.time + hitStunDuration;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            _isAttacking = false; // Cancels active attack windup on hit flinch
            if (anim != null)
            {
                anim.ResetTrigger(hashHit);
                anim.SetTrigger(hashHit);
                anim.ResetTrigger(hashGotHit);
                anim.SetTrigger(hashGotHit);
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  FLOATING BOSS HEALTH BAR
    // ───────────────────────────────────────────────────────────────────────────

    private Transform FindHealthBarAnchor()
    {
        if (healthBarPosition != null) return healthBarPosition;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == transform) continue;
            string n = children[i].name.Trim().ToLowerInvariant();
            if ((n.Contains("health") && n.Contains("pos")) ||
                (n.Contains("healthbar") && n.Contains("pos")) ||
                (n.Contains("health") && n.Contains("anchor")) ||
                n == "healthbar position" || n == "healthbar pos" || n == "healthbar_position")
            {
                healthBarPosition = children[i];
                return children[i];
            }
        }
        return null;
    }

    private void SetupFloatingHealthBar()
    {
        if (!showFloatingHealthBar) return;

        Transform customAnchor = FindHealthBarAnchor();

        _floatingHealthBar = GetComponentInChildren<HealthBar>(true);
        if (_floatingHealthBar == null)
        {
            GameObject barGO = new GameObject("Boss Health Bar", typeof(RectTransform));
            barGO.SetActive(false);
            _floatingHealthBar = barGO.AddComponent<HealthBar>();
            _floatingHealthBar.barType = BarType.Boss;
            barGO.SetActive(true);
        }
        else if (_floatingHealthBar.barType != BarType.Boss)
        {
            _floatingHealthBar.SetBarType(BarType.Boss);
        }

        _floatingHealthBar.SetMaxHealth(maxHealth);
        _floatingHealthBar.SetHealth(currentHealth);

        if (player != null)
            _floatingHealthBar.SetProximityTarget(player);

        if (customAnchor != null)
        {
            _floatingHealthBar.SetCustomAnchor(customAnchor, healthBarScale);
        }
        else
        {
            _floatingHealthBar.SetWorldPositionAnchor(transform, healthBarHeightOffset, healthBarScale);
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        _isAttacking = false;
        _isChasing = false;

        // Notify floating Boss health bar
        if (_floatingHealthBar != null)
            _floatingHealthBar.SetEnemyDead();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (anim != null)
        {
            anim.ResetTrigger(hashDie);
            anim.SetTrigger(hashDie);
            anim.ResetTrigger(hashDeath);
            anim.SetTrigger(hashDeath);
        }

        Collider[] cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i] != territoryTrigger)
                cols[i].enabled = false;
        }

        Destroy(gameObject, corpseDespawnDelay);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TERRAIN SURFACE & SLOPE ALIGNMENT (Chipak ke chalna — Like SpiderEnemy)
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnEnable()
    {
        SnapToGroundImmediate();
    }

    [ContextMenu("Snap To Terrain Surface Now")]
    public void SnapToGroundImmediate()
    {
        if (TryFindGroundSurface(transform.position, out Vector3 groundSurface, out Vector3 groundNormal))
        {
            Vector3 targetPos = groundSurface + Vector3.up * groundOffset;
            transform.position = targetPos;

            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.Warp(targetPos);
                }
                else if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                }
            }

            if (groundNormal.sqrMagnitude > 0.001f)
            {
                Vector3 projectedFwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
                if (projectedFwd.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(projectedFwd, groundNormal);
                }
            }

            startPos = transform.position;
        }
    }

    public bool TryFindGroundSurface(Vector3 atPosition, out Vector3 groundSurface, out Vector3 groundNormal)
    {
        groundSurface = atPosition;
        groundNormal = Vector3.up;
        float bestY = float.MinValue;
        bool found = false;

        float skyY = atPosition.y + 100f;
        Vector3 rayStart = new Vector3(atPosition.x, skyY, atPosition.z);
        int mask = (groundLayer.value != 0) ? (groundLayer.value & ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast")) : ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast");

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 200f, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform ht = hits[i].transform;
            if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;
            if (hits[i].point.y > bestY)
            {
                bestY = hits[i].point.y;
                groundSurface = hits[i].point;
                groundNormal = hits[i].normal;
                found = true;
            }
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float tY = terrain.SampleHeight(atPosition) + terrain.transform.position.y;
            if (tY > bestY)
            {
                bestY = tY;
                groundSurface = new Vector3(atPosition.x, bestY, atPosition.z);
                groundNormal = terrain.terrainData != null ? terrain.terrainData.GetInterpolatedNormal((atPosition.x - terrain.transform.position.x) / terrain.terrainData.size.x, (atPosition.z - terrain.transform.position.z) / terrain.terrainData.size.z) : Vector3.up;
                found = true;
            }
        }

        float sampleCenterY = found ? bestY : atPosition.y;
        if (NavMesh.SamplePosition(new Vector3(atPosition.x, sampleCenterY, atPosition.z), out NavMeshHit navHit, 30f, NavMesh.AllAreas))
        {
            groundSurface = navHit.position;
            return true;
        }

        return found;
    }

    private void ProcessTerrainAlignment()
    {
        if (!enableTerrainAlignment) return;

        Vector3 curPos = transform.position;
        float currentGroundY = curPos.y;
        bool hasGround = false;

        Vector3 probeOrigin = curPos + Vector3.up * 5.0f;
        int mask = (groundLayer.value != 0) ? (groundLayer.value & ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast")) : ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast");

        RaycastHit[] hits = Physics.RaycastAll(probeOrigin, Vector3.down, 15.0f, mask, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;
        Vector3 surfaceNormal = Vector3.up;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform ht = hits[i].transform;
            if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;
            if (hits[i].distance < closestDist)
            {
                closestDist = hits[i].distance;
                currentGroundY = hits[i].point.y;
                surfaceNormal = hits[i].normal;
                hasGround = true;
            }
        }

        if (Terrain.activeTerrain != null)
        {
            float tY = Terrain.activeTerrain.SampleHeight(curPos) + Terrain.activeTerrain.transform.position.y;
            if (!hasGround || tY > currentGroundY)
            {
                currentGroundY = tY;
                hasGround = true;
            }
        }

        if (!hasGround && NavMesh.SamplePosition(curPos, out NavMeshHit navGround, 10f, NavMesh.AllAreas))
        {
            currentGroundY = navGround.position.y;
            hasGround = true;
        }

        if (hasGround)
        {
            float targetGroundY = currentGroundY + groundOffset;

            // Anti-fall rescue
            if (curPos.y < currentGroundY - 3.0f && Time.time > _lastRescueTime + 2.5f)
            {
                _lastRescueTime = Time.time;
                Vector3 rescuePos = new Vector3(curPos.x, targetGroundY, curPos.z);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.Warp(rescuePos);
                else
                    transform.position = rescuePos;
            }
            else
            {
                // Smooth snap to ground surface
                float newY = Mathf.MoveTowards(curPos.y, targetGroundY, groundSnapSpeed * Time.deltaTime);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.baseOffset = groundOffset + (newY - targetGroundY);
                }
                else
                {
                    transform.position = new Vector3(curPos.x, newY, curPos.z);
                }
            }

            // Slope tilt alignment
            if (surfaceNormal.sqrMagnitude > 0.001f)
            {
                Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
                if (projectedForward.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(projectedForward, surfaceNormal);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, slopeAlignSpeed * Time.deltaTime);
                }
            }
        }
    }

    private IEnumerator RandomIdleBreakLoop()
    {
        while (!isDead)
        {
            yield return new WaitForSeconds(Random.Range(8f, 18f));
            if (!isDead && !_isAttacking && !_isChasing && !IsPlayerInTerritory())
            {
                if (anim != null) anim.SetTrigger(hashIdleBreak);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SCENE GIZMOS
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // 1. Attack Range (Red wire sphere + ground disc) - ALWAYS visible when selected!
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        DrawWireDisc(transform.position + Vector3.up * 0.05f, attackRange, 32);

        // 2. Stopping Distance (Green wire sphere)
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
        DrawWireDisc(transform.position + Vector3.up * 0.05f, stoppingDistance, 24);

        // 3. Aggro Distance (Yellow wire sphere)
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, aggroDistance);

        if (enableAntiPenetration)
        {
            Gizmos.color = safeDistanceColor;
            Gizmos.DrawWireSphere(transform.position, safeDistance);
        }
    }

    private void DrawWireDisc(Vector3 center, float radius, int segments)
    {
        if (radius <= 0.01f || segments < 3) return;
        float angleStep = 360f / segments;
        Vector3 prev = center + new Vector3(Mathf.Sin(0) * radius, 0f, Mathf.Cos(0) * radius);
        for (int i = 1; i <= segments; i++)
        {
            float rad = i * angleStep * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
