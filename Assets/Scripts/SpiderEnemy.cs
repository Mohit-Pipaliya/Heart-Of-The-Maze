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

    [Tooltip("Agar TRUE karenge toh NavMeshAgent ke radius aur height ko script override karegi. FALSE rakhne par NavMeshAgent component ki apni setting use hogi (badhegi nahi).")]
    public bool overrideAgentDimensions = false;

    [Tooltip("NavMeshAgent avoidance radius (sirf tab apply hoga agar overrideAgentDimensions TRUE ho)")]
    public float agentRadius = 0.5f;

    [Tooltip("NavMeshAgent height (sirf tab apply hoga agar overrideAgentDimensions TRUE ho)")]
    public float agentHeight = 1.2f;

    [Header("─── Physical Collision & Body Size ──────────────────────────")]
    [Tooltip("Player spider ke andar na ghus sake")]
    public bool enableAntiPenetration = true;

    [Header("─── Terrain Surface & Slope Alignment (Chipak ke chalna) ────")]
    [Tooltip("Spider ko terrain slope ke hisaab se tilt karna (Pitch/Roll) aur zameen pe chipka ke chalana")]
    public bool enableTerrainAlignment = true;

    [Tooltip("Layers considered solid ground for spider terrain alignment")]
    public LayerMask groundLayer = ~0;

    [Tooltip("Terrain slope par spider body ke tilt hone ki speed")]
    public float slopeAlignSpeed = 14f;

    [Tooltip("Zameen se chipke rehne ki snapping speed (anti-floating / anti-sinking)")]
    public float groundSnapSpeed = 22f;

    [Tooltip("Spider ki body ka zameen se vertical offset")]
    public float groundOffset = 0.0f;

    [Tooltip("Agar TRUE karenge toh CapsuleCollider ke size ko script override karegi. FALSE rakhne par aapki apni collider settings use hongi.")]
    public bool overrideColliderDimensions = false;

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
    private float _lastRescueTime;
    private bool _offMeshJumping;
    private SpiderTriggerZone _triggerZone;

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
        slopeAlignSpeed = Mathf.Max(1f, slopeAlignSpeed);
        groundSnapSpeed = Mathf.Max(1f, groundSnapSpeed);

        // Live-sync with NavMeshAgent in Play Mode
        if (Application.isPlaying && agent != null && agent.enabled)
        {
            agent.baseOffset = groundOffset;
            if (enableTerrainAlignment)
            {
                agent.updateRotation = false;
            }
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

        // Setup CapsuleCollider for physical body collision if enabled
        if (overrideColliderDimensions)
        {
            UpdateCapsuleCollider();
        }

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

    /// <summary>
    /// Territory trigger zone ko dhoondhta hai aur uspar real-time trigger detector attach karta hai.
    /// </summary>
    private void EnsureTriggerZone()
    {
        if (territoryTrigger == null)
        {
            GameObject st = GameObject.Find("SpiderTrigger");
            if (st != null)
            {
                territoryTrigger = st.GetComponent<Collider>();
            }
        }

        if (territoryTrigger != null && _triggerZone == null)
        {
            _triggerZone = territoryTrigger.GetComponent<SpiderTriggerZone>();
            if (_triggerZone == null)
            {
                _triggerZone = territoryTrigger.gameObject.AddComponent<SpiderTriggerZone>();
            }
        }
    }

    /// <summary>
    /// Check karta hai ki koi specific world position territory trigger ke andar hai ya nahi.
    /// BoxCollider rotation, center, scaling aur vertical slope tolerance (+/- 15m) ko accurately handle karta hai.
    /// </summary>
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

    /// <summary>
    /// Check karta hai ki player territory area me moujood hai ya nahi.
    /// Physics Trigger events aur spatial coordinates dono use karta hai.
    /// </summary>
    public bool IsPlayerInTerritory()
    {
        if (territoryTrigger == null || player == null) return false;

        // 1. Direct Trigger physics check
        if (_triggerZone != null && _triggerZone.IsPlayerInside)
            return true;

        // 2. Spatial coordinate check (feet aur center)
        Vector3 feet = player.position;
        Vector3 center = feet + Vector3.up * 1.0f;
        if (IsPointInTerritory(feet) || IsPointInTerritory(center))
            return true;

        // 3. ClosestPoint check
        Vector3 closest = territoryTrigger.ClosestPoint(center);
        if ((closest - center).sqrMagnitude < 0.25f)
            return true;

        return false;
    }

    private void Start()
    {
        EnsureTriggerZone();

        // NO HARDCODED OVERWRITES! What the user enters in Inspector stays 100% in game!
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        if (anim != null) anim.applyRootMotion = false;

        currentHealth = maxHealth;
        startPos = transform.position;

        // Snap to solid terrain surface immediately so spider NEVER spawns or walks under the terrain
        SnapToGroundImmediate();

        // Automatically find player if not assigned
        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        if (agent != null)
        {
            agent.baseOffset = groundOffset;
            if (!agent.isOnNavMesh)
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 50f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            agent.speed = walkSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = stoppingDistance;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            if (enableTerrainAlignment)
            {
                agent.updateRotation = false;
            }

            if (overrideAgentDimensions)
            {
                agent.radius = agentRadius;
                agent.height = agentHeight;
            }
            else
            {
                // Inspector me jo NavMeshAgent component par values hai vahi rahengi, play karne par badhengi nahi
                agentRadius = agent.radius;
                agentHeight = agent.height;
            }
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
        if (isDead)
        {
            ProcessTerrainAlignment();
            return;
        }

        // ─── Terrain Surface Clamping & Slope Alignment (Chipak ke chalna) ────
        ProcessTerrainAlignment();

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

        // ─── Territory & Aggro Decision ─────────────────────────────────
        float distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
        bool playerInTerritory = IsPlayerInTerritory();

        // Niyam:
        // 1. Agar player trigger area me enter kare, spider ko chahe dikhe ya na dikhe,
        //    spider turant usko dhundhte hue uske paas jayega aur attack karega.
        // 2. Agar player trigger area se bahar nikal jaye, toh spider chase karna bandh kar dega aur attack nahi karega.
        bool shouldTargetPlayer;
        if (territoryTrigger != null)
        {
            shouldTargetPlayer = playerInTerritory;
        }
        else
        {
            // Fallback agar territoryTrigger assign na ho
            shouldTargetPlayer = (distanceToPlayer <= aggroDistance);
        }

        // Effective melee attack range is close-quarters (stoppingDistance se striking distance)
        // Chahe inspector me attackRange 30 ho, spider player ke paas daud kar aayega aur stopping distance pe attack karega
        float effectiveAttackDistance = Mathf.Min(attackRange, Mathf.Max(stoppingDistance, 4.0f));

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
        agent.stoppingDistance = 1.5f;

        bool reachedDest = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.5f);
        bool spiderInsideTerritory = territoryTrigger == null || IsPointInTerritory(transform.position);

        if (reachedDest || !spiderInsideTerritory)
        {
            Vector3 targetPoint;

            if (territoryTrigger != null)
            {
                Bounds b = territoryTrigger.bounds;
                float marginX = Mathf.Min(6f, b.extents.x * 0.3f);
                float marginZ = Mathf.Min(6f, b.extents.z * 0.3f);

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

            if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 35f, NavMesh.AllAreas))
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
        // Stopping distance must allow spider to close in to attack range
        agent.stoppingDistance = Mathf.Min(stoppingDistance, Mathf.Max(1.5f, attackRange * 0.75f));
        agent.SetDestination(player.position);
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

        // Sirf attack strike animation chalte waqt agent ko roko, cooldown ke waqt spider player ke paas move hota rahe
        if (_isAttacking && agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        else if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = walkSpeed;
            agent.stoppingDistance = stoppingDistance;
            if (player != null) agent.SetDestination(player.position);
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
            // Player must still be inside territory if territory trigger is assigned
            if (territoryTrigger == null || IsPlayerInTerritory())
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

        // Instantly aggro and chase player if shot or struck, but only if player is in territory
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
    //  TERRAIN SURFACE & SLOPE ALIGNMENT (Chipak ke chalna)
    // ═══════════════════════════════════════════════════════════════════════════

    private static readonly RaycastHit[] s_SpiderHits = new RaycastHit[8];

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

        // 1. Raycast downward from high above (covers any terrain, hills, meshes, bridges)
        float skyY = atPosition.y + 150f;
        Vector3 rayStart = new Vector3(atPosition.x, skyY, atPosition.z);
        int mask = (groundLayer.value != 0) ? (groundLayer.value & ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast")) : ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast");

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 300f, mask, QueryTriggerInteraction.Ignore);
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

        // 2. Sample Unity Terrain directly if available
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

        // 3. Sample NavMesh near the best detected ground height
        float sampleCenterY = found ? bestY : atPosition.y;
        if (NavMesh.SamplePosition(new Vector3(atPosition.x, sampleCenterY, atPosition.z), out NavMeshHit navHit, 30f, NavMesh.AllAreas))
        {
            groundSurface = navHit.position;
            return true;
        }

        return found;
    }

    // ── Terrain alignment smoothing state ──
    private float _smoothedBaseOffset = 0f;
    private float _smoothedBaseOffsetVel = 0f;

    private void ProcessTerrainAlignment()
    {
        if (!enableTerrainAlignment) return;

        Vector3 curPos = transform.position;
        Vector3 center = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.nextPosition : curPos;
        Vector3 fwd    = transform.forward;
        Vector3 right  = transform.right;

        // 1. Dynamic Ground Snapping & Anti-Sinking Rescue
        float currentGroundY = curPos.y;
        bool hasGround = false;

        // Probe origin ko 6 unit upar rakhenge taaki uneven terrain pe bhi miss na ho
        Vector3 probeOrigin = curPos + Vector3.up * 6.0f;
        int mask = (groundLayer.value != 0) ? (groundLayer.value & ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast")) : ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast");

        // Raycast downward to locate ground, ignoring all of the spider's own colliders
        RaycastHit[] hits = Physics.RaycastAll(probeOrigin, Vector3.down, 20.0f, mask, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform ht = hits[i].transform;
            if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;
            if (hits[i].distance < closestDist)
            {
                closestDist = hits[i].distance;
                currentGroundY = hits[i].point.y;
                hasGround = true;
            }
        }

        // Unity Terrain direct height sample (most accurate for terrain)
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

            // Emergency Anti-Fall Rescue
            if (curPos.y < currentGroundY - 4.0f && Time.time > _lastRescueTime + 2.5f)
            {
                _lastRescueTime = Time.time;
                Vector3 rescuePos = new Vector3(curPos.x, targetGroundY, curPos.z);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.Warp(rescuePos);
                else
                    transform.position = rescuePos;

                _smoothedBaseOffset = groundOffset;
                _smoothedBaseOffsetVel = 0f;
            }
            else if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // ── FIX: baseOffset ko SmoothDamp se adjust karo (bobbing band hoga) ──
                // heightError = kitna upar/niche adjust karna hai
                float heightError = targetGroundY - agent.nextPosition.y;

                // Sirf small corrections (±1.5m) allow karo — badi jumps NavMesh khud handle karta hai
                heightError = Mathf.Clamp(heightError, -1.5f, 1.5f);

                // Dead zone: agar error bahut chhota hai toh bilkul mat touch karo (micro-oscillation hatane ke liye)
                if (Mathf.Abs(heightError - _smoothedBaseOffset) < 0.02f)
                {
                    // Kuch mat karo — stable hai
                }
                else
                {
                    // SmoothDamp — spring-like smooth follow, koi overshoot nahi
                    _smoothedBaseOffset = Mathf.SmoothDamp(
                        _smoothedBaseOffset,
                        heightError,
                        ref _smoothedBaseOffsetVel,
                        smoothTime: 0.25f,          // 0.25s lag — stable, no bobbing
                        maxSpeed: 3.0f              // Max 3 unit/sec shift — slow enough to not oscillate
                    );
                    agent.baseOffset = _smoothedBaseOffset;
                }
            }
            else
            {
                curPos.y = Mathf.Lerp(curPos.y, targetGroundY, Time.deltaTime * Mathf.Min(groundSnapSpeed, 8f));
                transform.position = curPos;
            }
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // Ground nahi mila toh baseOffset ko dhheere dhheere groundOffset pe wapas lao
                _smoothedBaseOffset = Mathf.SmoothDamp(_smoothedBaseOffset, groundOffset, ref _smoothedBaseOffsetVel, 0.3f);
                agent.baseOffset = _smoothedBaseOffset;
            }
        }

        // 2. Multi-point surface sampling for slope alignment (5 raycasts around body)
        float sampleDist = 3.2f;
        float rayUp = 3.0f;
        float rayDown = 5.0f;

        Vector3 pCenter = center;
        Vector3 pFront  = center + fwd * sampleDist;
        Vector3 pBack   = center - fwd * sampleDist;
        Vector3 pLeft   = center - right * sampleDist;
        Vector3 pRight  = center + right * sampleDist;

        Vector3 normalSum = Vector3.zero;
        int hitCount = 0;

        if (SampleGroundRay(pCenter, center.y, rayUp, rayDown, mask, out Vector3 nrm)) { normalSum += nrm; hitCount++; }
        if (SampleGroundRay(pFront,  center.y, rayUp, rayDown, mask, out nrm)) { normalSum += nrm; hitCount++; }
        if (SampleGroundRay(pBack,   center.y, rayUp, rayDown, mask, out nrm)) { normalSum += nrm; hitCount++; }
        if (SampleGroundRay(pLeft,   center.y, rayUp, rayDown, mask, out nrm)) { normalSum += nrm; hitCount++; }
        if (SampleGroundRay(pRight,  center.y, rayUp, rayDown, mask, out nrm)) { normalSum += nrm; hitCount++; }

        Vector3 avgNormal = (hitCount > 0) ? (normalSum / hitCount).normalized : Vector3.up;

        // Determine desired horizontal heading:
        Vector3 desiredHeading = transform.forward;
        if (_isAttacking && player != null)
        {
            Vector3 toPlayer = player.position - center;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f) desiredHeading = toPlayer.normalized;
        }
        else if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 vel = agent.velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 0.01f) desiredHeading = vel.normalized;
        }
        else if (agent != null && agent.enabled && agent.hasPath && !agent.isStopped)
        {
            Vector3 toCorner = agent.steeringTarget - center;
            toCorner.y = 0f;
            if (toCorner.sqrMagnitude > 0.01f) desiredHeading = toCorner.normalized;
        }

        // Project desired heading onto terrain slope plane
        Vector3 projectedForward = Vector3.ProjectOnPlane(desiredHeading, avgNormal).normalized;
        if (projectedForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(projectedForward, avgNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * slopeAlignSpeed);
        }
    }

    private bool SampleGroundRay(Vector3 pos, float centerY, float up, float down, int mask, out Vector3 normal)
    {
        normal = Vector3.up;
        Vector3 origin = pos + Vector3.up * up;
        float dist = up + down;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, s_SpiderHits, dist, mask, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Transform ht = s_SpiderHits[i].transform;
            if (ht == null || ht == transform || ht.IsChildOf(transform)) continue;
            // Strict sanity check: only consider hits near the current surface level (rejecting void/deep falloff)
            if (Mathf.Abs(s_SpiderHits[i].point.y - centerY) > 4.5f) continue;

            if (s_SpiderHits[i].distance < closestDist)
            {
                closestDist = s_SpiderHits[i].distance;
                normal = s_SpiderHits[i].normal;
                found = true;
            }
        }

        if (!found && Terrain.activeTerrain != null && Terrain.activeTerrain.terrainData != null)
        {
            Terrain t = Terrain.activeTerrain;
            Vector3 tPos = t.transform.position;
            float normX = (pos.x - tPos.x) / t.terrainData.size.x;
            float normZ = (pos.z - tPos.z) / t.terrainData.size.z;
            if (normX >= 0f && normX <= 1f && normZ >= 0f && normZ <= 1f)
            {
                normal = t.terrainData.GetInterpolatedNormal(normX, normZ);
                found = true;
            }
        }

        return found;
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

/// <summary>
/// SpiderTrigger collider par automatically attach hota hai aur player presence ko track karta hai.
/// </summary>
public class SpiderTriggerZone : MonoBehaviour
{
    private readonly System.Collections.Generic.HashSet<Collider> _collidersInside = new System.Collections.Generic.HashSet<Collider>();

    public bool IsPlayerInside
    {
        get
        {
            _collidersInside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            return _collidersInside.Count > 0;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            _collidersInside.Add(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other))
        {
            _collidersInside.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            _collidersInside.Remove(other);
        }
    }

    private bool IsPlayer(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponent<PlayerController>() != null || col.GetComponentInParent<PlayerController>() != null) return true;
        return false;
    }

    private void OnDisable()
    {
        _collidersInside.Clear();
    }
}
