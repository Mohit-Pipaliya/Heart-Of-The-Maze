using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ZombieAI
/// ========
/// AAA State-Machine driven Zombie AI:
/// 1. Wander/Patrol: Walks naturally across the terrain on NavMesh ("Walk").
/// 2. Alert/Trigger: When player enters trigger area, turns to face player and screams ("Raad").
/// 3. Chase: Immediately sprints toward the player at run speed ("Run").
/// 4. Attack: Strikes player with melee attacks and inflicts damage ("Attack").
/// 5. Hit Reaction: Plays reaction hit animation and brief stun on taking damage ("Hit").
/// 6. Death: 1000 HP pool; plays death animation and disables collisions upon reaching 0 ("Die").
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZombieAI : MonoBehaviour
{
    public enum State
    {
        Wander,
        Raad,
        Chase,
        Attack,
        Hit,
        Dead
    }

    [Header("── Health Settings ──")]
    [Tooltip("Zombie ki total health (Default: 1000 as requested).")]
    public float maxHealth = 1000f;
    private float _currentHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _isDead;

    [Header("── AI Territory & Trigger Area ──")]
    [Tooltip("Optional: Inspector me koi BoxCollider/SphereCollider assign karein. Agar empty ho toh detectionRadius use hoga.")]
    public Collider triggerArea;
    [Tooltip("Player is radius me aate hi zombie use dekhkar 'Raad' (scream) karega.")]
    public float detectionRadius = 18f;
    [Tooltip("Player is distance ke bahar nikal jaye toh zombie chase chhodkar wapas walk karne lagega.")]
    public float loseTargetRadius = 36f;

    [Header("── Movement Settings ──")]
    public float walkSpeed = 1.6f;
    public float runSpeed = 4.8f;
    [Tooltip("Health <= 600 hone par zombie crawl/slide karke chase karega uski speed.")]
    public float slideSpeed = 3.0f;
    public float wanderRadius = 18f;
    public float wanderWaitMin = 1.5f;
    public float wanderWaitMax = 3.5f;
    public float rotationSpeed = 8.0f;

    [Header("── Combat & Attack Settings ──")]
    public float attackRange = 2.2f;
    public float attackDamage = 15f;
    public float attackCooldown = 2.0f;
    public float attackHitDelay = 0.45f;

    [Header("── Raad (Scream) Settings ──")]
    [Tooltip("Scream animation kitni der tak hold karke player ki taraf dekhega.")]
    public float screamDuration = 1.9f;

    [Header("── Target Reference ──")]
    public Transform player;

    [Header("── UI Feedback ──")]
    [Tooltip("Zombie ke sir ke upar health bar show karein jab damage ho ya player pass ho.")]
    public bool showFloatingHealthBar = false;

    // Component References
    private NavMeshAgent _agent;
    private Animator _anim;
    private Collider _collider;
    private ZombieLocomotion _locomotion;
    private ZombieFootIK _footIK;

    // State Machine
    private State _currentState = State.Wander;
    public State CurrentState => _currentState;

    private bool _isDead = false;
    private bool _isSliding = false;
    public bool IsSliding => _isSliding;
    private Vector3 _spawnPosition;
    private float _wanderWaitTimer = 0f;
    private Coroutine _activeRoutine;
    private string _currentAnimState = "";

    // Animator Hashes for high-performance crossfade & parameters
    private static readonly int HashWalk   = Animator.StringToHash("Walk");
    private static readonly int HashRaad   = Animator.StringToHash("Raad");
    private static readonly int HashRun    = Animator.StringToHash("Run");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashHit    = Animator.StringToHash("Hit");
    private static readonly int HashDie    = Animator.StringToHash("Die");

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponent<Animator>();
        _locomotion = GetComponent<ZombieLocomotion>() ?? gameObject.AddComponent<ZombieLocomotion>();
        _footIK = GetComponent<ZombieFootIK>() ?? gameObject.AddComponent<ZombieFootIK>();

        // Disable root motion strictly so animations don't float/lift zombie into the air
        if (_anim != null)
        {
            _anim.applyRootMotion = false;
        }

        if (_agent != null)
        {
            _agent.baseOffset = 0f;
            _agent.updatePosition = true;
            _agent.updateRotation = true;
        }

        // Rigidbody safety: on NavMeshAgent, Rigidbody must be kinematic so physics does not fling zombie up
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.radius = 0.5f;
            capsule.height = 1.8f;
            capsule.direction = 1;
            capsule.isTrigger = false; // Solid physical collider so player CANNOT walk through zombie!
            _collider = capsule;
        }
        else
        {
            _collider.isTrigger = false; // Solid physical collider so player CANNOT walk through zombie!
            if (_collider is CapsuleCollider cap)
            {
                cap.center = new Vector3(0f, 0.9f, 0f);
                cap.height = 1.8f;
                cap.radius = Mathf.Max(cap.radius, 0.5f);
            }
        }
    }

    private void Start()
    {
        _currentHealth = maxHealth;

        // Ensure zombie is firmly grounded on terrain/NavMesh at start
        if (_locomotion != null)
            _locomotion.SnapToGroundImmediate();
        else
            SnapToGround();

        _spawnPosition = transform.position;

        AutoFindPlayer();
        EnsurePlayerCollision();

        // Start wandering immediately
        _currentState = State.Wander;
        if (_agent != null && _agent.enabled)
        {
            _agent.speed = walkSpeed;
            _agent.stoppingDistance = 1.8f;
        }
        PlayAnimation("Walk", 0.1f);
    }

    /// <summary>
    /// Enforces solid physical collision with player CharacterController so player cannot walk through zombie.
    /// </summary>
    private void EnsurePlayerCollision()
    {
        if (player == null) AutoFindPlayer();
        if (player == null) return;

        Collider myCol = GetComponent<Collider>();
        if (myCol != null)
        {
            myCol.isTrigger = false;
            Collider[] playerCols = player.GetComponentsInChildren<Collider>();
            for (int i = 0; i < playerCols.Length; i++)
            {
                if (playerCols[i] != null && playerCols[i] != myCol)
                    Physics.IgnoreCollision(myCol, playerCols[i], false);
            }
        }
    }

    /// <summary>
    /// Clamps the zombie's feet firmly to the terrain surface or NavMesh.
    /// </summary>
    public void SnapToGround()
    {
        if (_anim != null) _anim.applyRootMotion = false;
        if (_agent != null) _agent.baseOffset = 0f;

        // 1. Try NavMesh sampling
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
        {
            if (_agent != null && _agent.enabled)
            {
                _agent.Warp(navHit.position);
            }
            else
            {
                transform.position = navHit.position;
            }
            return;
        }

        // 2. Fallback: Raycast down to Terrain / Mesh Collider
        int groundMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
        if (Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
            if (_agent != null && _agent.enabled && NavMesh.SamplePosition(hit.point, out NavMeshHit snapHit, 10f, NavMesh.AllAreas))
            {
                _agent.Warp(snapHit.position);
            }
        }
    }

    private void LateUpdate()
    {
        // Continuously enforce applyRootMotion = false so no animation clip ever floats the character (even during death!)
        if (_anim != null && _anim.applyRootMotion)
        {
            _anim.applyRootMotion = false;
        }

        if (_isDead) return;

        // Ensure baseOffset remains 0f
        if (_agent != null && _agent.enabled && _agent.baseOffset != 0f)
        {
            _agent.baseOffset = 0f;
        }
    }

    private void AutoFindPlayer()
    {
        if (player != null) return;

        if (PlayerController.Instance != null)
        {
            player = PlayerController.Instance.transform;
            return;
        }

        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) player = pObj.transform;
    }

    private void Update()
    {
        if (_isDead) return;

        if (player == null)
        {
            AutoFindPlayer();
            if (player == null) return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Anti-penetration safety: firmly prevent player and zombie from clipping/walking through each other
        if (distanceToPlayer < 1.4f)
        {
            Vector3 pushDir = (transform.position - player.position).normalized;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.001f && _agent != null && _agent.enabled)
            {
                float overlap = 1.4f - distanceToPlayer;
                _agent.Move(pushDir * (overlap * 0.5f));
            }
        }

        bool isPlayerInTrigger = IsPlayerInsideTrigger(distanceToPlayer);

        switch (_currentState)
        {
            case State.Wander:
                HandleWander(isPlayerInTrigger);
                break;

            case State.Raad:
                RotateTowardPlayer();
                break;

            case State.Chase:
                HandleChase(distanceToPlayer);
                break;

            case State.Attack:
                RotateTowardPlayer();
                break;

            case State.Hit:
                // Hit stun is handled by coroutine
                break;
        }
    }

    // ─── Trigger & Proximity Check ──────────────────────────────────────────

    private bool IsPlayerInsideTrigger(float distanceToPlayer)
    {
        if (player == null) return false;

        // 1. Collider trigger bounds (if assigned)
        if (triggerArea != null)
        {
            if (triggerArea.bounds.Contains(player.position))
                return true;
        }

        // 2. Spherical proximity radius
        return distanceToPlayer <= detectionRadius;
    }

    // ─── State: Wander / Patrol ─────────────────────────────────────────────

    private void HandleWander(bool isPlayerInTrigger)
    {
        // Player enters trigger area → trigger Raad (scream)
        if (isPlayerInTrigger)
        {
            StartRaad();
            return;
        }

        if (_agent == null || !_agent.enabled) return;

        _agent.speed = walkSpeed;
        PlayAnimation("Walk", 0.2f);

        // Check if reached wander destination
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
        {
            _wanderWaitTimer -= Time.deltaTime;
            if (_wanderWaitTimer <= 0f)
            {
                SetNewWanderDestination();
                _wanderWaitTimer = Random.Range(wanderWaitMin, wanderWaitMax);
            }
        }
    }

    private void SetNewWanderDestination()
    {
        Vector3 targetCenter = triggerArea != null ? triggerArea.bounds.center : _spawnPosition;
        Vector3 randomPoint = targetCenter + Random.insideUnitSphere * wanderRadius;
        randomPoint.y = transform.position.y;

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.SetDestination(hit.position);
        }
    }

    // ─── State: Raad (Scream / Alert) ────────────────────────────────────────

    private void StartRaad()
    {
        if (_currentState == State.Raad || _isDead) return;

        _currentState = State.Raad;
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(RaadRoutine());
    }

    private IEnumerator RaadRoutine()
    {
        PlayAnimation("Raad", 0.12f);

        float elapsed = 0f;
        while (elapsed < screamDuration)
        {
            if (_isDead) yield break;
            RotateTowardPlayer();
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Turant hi player ki side run karke jana chahiye
        StartChase();
    }

    // ─── State: Chase (Run / Slide) ─────────────────────────────────────────

    private void StartChase()
    {
        if (_isDead) return;

        _currentState = State.Chase;
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = false;
            _agent.speed = _isSliding ? slideSpeed : runSpeed;
            _agent.stoppingDistance = Mathf.Max(1.8f, attackRange * 0.8f);
            if (player != null) _agent.SetDestination(player.position);
        }

        PlayAnimation(_isSliding ? "Slide" : "Run", 0.15f);
    }

    private void HandleChase(float distanceToPlayer)
    {
        if (_agent == null || !_agent.enabled || player == null) return;

        _agent.isStopped = false;
        _agent.speed = _isSliding ? slideSpeed : runSpeed;
        PlayAnimation(_isSliding ? "Slide" : "Run", 0.15f);

        // Continuously refresh player position
        _agent.SetDestination(player.position);

        // Reached attack area → Attack
        if (distanceToPlayer <= attackRange)
        {
            StartAttack();
            return;
        }

        // Lost player outside chase perimeter → resume wander
        if (distanceToPlayer > loseTargetRadius)
        {
            _currentState = State.Wander;
            SetNewWanderDestination();
            PlayAnimation(_isSliding ? "Slide" : "Walk", 0.25f);
        }
    }

    // ─── State: Attack ──────────────────────────────────────────────────────

    private void StartAttack()
    {
        if (_currentState == State.Attack || _isDead) return;

        _currentState = State.Attack;
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        while (_currentState == State.Attack && !_isDead)
        {
            RotateTowardPlayer();
            // When sliding, strictly remain in Slide / crawl without popping upright
            PlayAnimation(_isSliding ? "Slide" : "Attack", 0.12f);

            // Wait for strike frame
            yield return new WaitForSeconds(attackHitDelay);

            if (_isDead) yield break;

            // Deal damage if player is still within melee reach
            if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 0.9f)
            {
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null && !pc.IsDead)
                {
                    pc.TakeDamage((int)attackDamage);
                    GameAudio.PlayBulletHit(player.position, true);

                    // Blood splash on player
                    Vector3 strikePos = player.position + Vector3.up * 1.1f;
                    VFXUtil.Burst(strikePos, transform.forward, new Color(0.85f, 0.08f, 0.05f, 1f),
                        14, 4f, 11f, 0.06f, 0.18f, 0.15f, 0.32f, true, true, 1.8f, 35f);
                }
            }

            // Remainder of attack cooldown
            float restCooldown = Mathf.Max(0.05f, attackCooldown - attackHitDelay);
            float timer = 0f;
            while (timer < restCooldown)
            {
                if (_isDead) yield break;
                RotateTowardPlayer();
                timer += Time.deltaTime;
                yield return null;
            }

            // Check if player fled out of attack range
            if (player != null && Vector3.Distance(transform.position, player.position) > attackRange)
            {
                StartChase();
                yield break;
            }
        }
    }

    // ─── Damage & Hit Reaction ──────────────────────────────────────────────

    /// <summary>
    /// Player attack (Bullet raycast or Sword overlap) calls this function.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _currentHealth -= amount;
        Debug.Log($"<color=#FF3333><b>[Zombie Hit!]</b></color> Damage: {amount} | HP: {Mathf.Max(0f, _currentHealth)}/{maxHealth}");

        // Blood VFX & flash
        Vector3 hitPoint = transform.position + Vector3.up * 1.1f;
        VFXUtil.SpawnFadingLight(hitPoint, new Color(0.9f, 0.1f, 0.05f), 4f, 2.0f, 0.08f);
        VFXUtil.Burst(hitPoint, Vector3.up, new Color(0.85f, 0.08f, 0.05f, 1f),
            20, 4f, 12f, 0.07f, 0.22f, 0.14f, 0.38f, true, true, 2.0f, 40f);

        if (GunShooter.Instance != null)
            GunShooter.Instance.RegisterHitMarker(_currentHealth <= 0f);

        // Visceral Hit Knockback (push zombie back away from player on impact so it visibly flinches)
        if (player != null)
        {
            Vector3 pushDir = (transform.position - player.position).normalized;
            pushDir.y = 0f;
            if (_agent != null && _agent.enabled)
            {
                _agent.Move(pushDir * 0.4f);
            }
            else
            {
                transform.position += pushDir * 0.4f;
            }
        }

        // Check for Death
        if (_currentHealth <= 0f)
        {
            Die();
            return;
        }

        // Check for 600 HP threshold: Activate Slide (Crawl) mode!
        if (_currentHealth <= 600f && !_isSliding)
        {
            _isSliding = true;
            if (_footIK != null) _footIK.SetIKActive(false);
            Debug.Log($"<color=#FFAA00><b>[Zombie Slide Triggered!]</b> Health reached {_currentHealth} <= 600. Playing Slide animation!</color>");
        }

        // Trigger Hit Reaction: Sliding zombies stay low, only upright zombies play standing Hit
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        if (_isSliding)
        {
            _activeRoutine = StartCoroutine(SlideHitRoutine());
        }
        else
        {
            _activeRoutine = StartCoroutine(HitRoutine());
        }
    }

    private IEnumerator SlideHitRoutine()
    {
        _currentState = State.Hit;
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        // Keep playing Slide — NEVER stand up!
        PlayAnimation("Slide", 0.05f);

        // Brief flinch while staying low
        yield return new WaitForSeconds(0.25f);

        if (_isDead) yield break;

        StartChase();
    }

    private IEnumerator HitRoutine()
    {
        _currentState = State.Hit;
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        PlayAnimation("Hit", 0.06f);

        // Stun window (allows animation to display)
        yield return new WaitForSeconds(0.42f);

        if (_isDead) yield break;

        // Enraged by damage: immediately sprint/slide after player
        StartChase();
    }

    // ─── Death ──────────────────────────────────────────────────────────────

    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        _isSliding = false;
        _currentState = State.Dead;
        _currentHealth = 0f;

        if (_activeRoutine != null) StopCoroutine(_activeRoutine);

        if (_footIK != null) _footIK.SetIKActive(false);
        if (_locomotion != null) _locomotion.enabled = false;

        // Reset all animation states and triggers so death can NEVER be interrupted
        if (_anim != null)
        {
            _anim.applyRootMotion = false;
            _anim.ResetTrigger("Hit");
            _anim.ResetTrigger("Attack");
            _anim.ResetTrigger("Raad");
            _anim.ResetTrigger("Slide");
            SetAnimBoolSafe("IsSliding", false);
            SetAnimBoolSafe("IsMoving", false);
            SetAnimBoolSafe("IsRunning", false);
            SetAnimFloatSafe("Speed", 0f);
            SetAnimBoolSafe("IsDead", true);
            SetAnimTriggerSafe("Die");
            _anim.CrossFadeInFixedTime("Die", 0.08f, 0, 0f);
        }

        // Stop navigation
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.enabled = false;
        }

        // Freeze Rigidbody so physics forces cannot propel corpse into air
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Snap firmly to terrain ground
        SnapToGround();

        // Start continuous ground clamp coroutine so corpse stays glued to ground surface throughout death animation
        StartCoroutine(DeathGroundClampRoutine());

        // Disable collider so player doesn't collide with corpse
        if (_collider != null) _collider.enabled = false;

        PlayAnimation("Die", 0.08f);

        Debug.Log("<color=#FF0000><b>[Zombie Dead!] Firmly grounded on terrain, death animation playing.</b></color>");

        // Destroy after 25 seconds (corpse remains on terrain for a while)
        Destroy(gameObject, 25f);
    }

    private IEnumerator DeathGroundClampRoutine()
    {
        float duration = 5.0f;
        float elapsed = 0f;
        int groundMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (_anim != null && _anim.applyRootMotion)
                _anim.applyRootMotion = false;

            // Firmly glue to ground, preventing any vertical displacement or floating
            Vector3 origin = transform.position + Vector3.up * 2.0f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            }

            yield return null;
        }
    }

    // ─── Helpers: Smooth Rotation & Animation ───────────────────────────────

    private void RotateTowardPlayer()
    {
        if (player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    private void PlayAnimation(string stateName, float crossfade = 0.15f)
    {
        if (_anim == null) return;

        // Synchronize Animator Parameters for Transition Conditions
        switch (stateName)
        {
            case "Walk":
                SetAnimBoolSafe("IsMoving", true);
                SetAnimBoolSafe("IsRunning", false);
                SetAnimFloatSafe("Speed", walkSpeed);
                break;

            case "Run":
                SetAnimBoolSafe("IsMoving", true);
                SetAnimBoolSafe("IsRunning", true);
                SetAnimBoolSafe("IsSliding", false);
                SetAnimFloatSafe("Speed", runSpeed);
                break;

            case "Slide":
                SetAnimBoolSafe("IsMoving", true);
                SetAnimBoolSafe("IsRunning", false);
                SetAnimBoolSafe("IsSliding", true);
                SetAnimFloatSafe("Speed", slideSpeed);
                SetAnimTriggerSafe("Slide");
                break;

            case "Raad":
                SetAnimBoolSafe("IsMoving", false);
                SetAnimBoolSafe("IsRunning", false);
                SetAnimFloatSafe("Speed", 0f);
                SetAnimTriggerSafe("Raad");
                break;

            case "Attack":
                SetAnimBoolSafe("IsMoving", false);
                SetAnimFloatSafe("Speed", 0f);
                SetAnimTriggerSafe("Attack");
                break;

            case "Hit":
                SetAnimTriggerSafe("Hit");
                break;

            case "Die":
                SetAnimBoolSafe("IsMoving", false);
                SetAnimBoolSafe("IsRunning", false);
                SetAnimBoolSafe("IsSliding", false);
                SetAnimFloatSafe("Speed", 0f);
                SetAnimResetTriggerSafe("Hit");
                SetAnimResetTriggerSafe("Attack");
                SetAnimResetTriggerSafe("Raad");
                SetAnimResetTriggerSafe("Slide");
                SetAnimTriggerSafe("Die");
                SetAnimBoolSafe("IsDead", true);
                break;
        }

        if (_currentAnimState == stateName) return;
        _currentAnimState = stateName;

        // Direct State Crossfade (works reliably with Zombie.controller states)
        int hash = Animator.StringToHash(stateName);
        if (_anim.HasState(0, hash))
        {
            _anim.CrossFadeInFixedTime(stateName, crossfade);
        }
    }

    private void SetAnimTriggerSafe(string paramName)
    {
        if (_anim == null) return;
        foreach (var p in _anim.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Trigger)
            {
                _anim.SetTrigger(paramName);
                return;
            }
        }
    }

    private void SetAnimResetTriggerSafe(string paramName)
    {
        if (_anim == null) return;
        foreach (var p in _anim.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Trigger)
            {
                _anim.ResetTrigger(paramName);
                return;
            }
        }
    }

    private void SetAnimBoolSafe(string paramName, bool value)
    {
        if (_anim == null) return;
        foreach (var p in _anim.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool)
            {
                _anim.SetBool(paramName, value);
                return;
            }
        }
    }

    private void SetAnimFloatSafe(string paramName, float value)
    {
        if (_anim == null) return;
        foreach (var p in _anim.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Float)
            {
                _anim.SetFloat(paramName, value);
                return;
            }
        }
    }

    // ─── Floating Health Bar (Disabled / Removed per user request) ───────────

    private void OnGUI()
    {
        // Health bar removed completely
    }

    private void OnDrawGizmosSelected()
    {
        // Detection Area (Yellow)
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Attack Range (Red)
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Wander Radius (Cyan)
        Vector3 center = triggerArea != null ? triggerArea.bounds.center : (Application.isPlaying ? _spawnPosition : transform.position);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
}
