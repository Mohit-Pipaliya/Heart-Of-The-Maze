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

    [Header("── Audio Clips (Optional overrides) ──")]
    public AudioClip screamSound;
    public AudioClip attackSound;
    public AudioClip hitSound;
    public AudioClip deathSound;
    public AudioClip groanSound;

    [Header("── Target Reference ──")]
    public Transform player;

    [Header("── UI Feedback ──")]
    [Tooltip("Zombie ke sir ke upar health bar show karein jab damage ho ya player pass ho.")]
    public bool showFloatingHealthBar = false;

    // Component References
    private NavMeshAgent _agent;
    private Animator _anim;
    private AudioSource _audioSource;
    private Collider _collider;

    // State Machine
    private State _currentState = State.Wander;
    public State CurrentState => _currentState;

    private bool _isDead = false;
    private Vector3 _spawnPosition;
    private float _wanderWaitTimer = 0f;
    private float _nextGroanTime = 0f;
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
            capsule.radius = 0.45f;
            capsule.height = 1.8f;
            capsule.direction = 1;
            _collider = capsule;
        }
        else if (_collider is CapsuleCollider cap)
        {
            cap.center = new Vector3(0f, 0.9f, 0f);
            cap.height = 1.8f;
            cap.radius = Mathf.Min(cap.radius, 0.45f);
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.45f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 50f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
        }
    }

    private void Start()
    {
        _currentHealth = maxHealth;

        // Ensure zombie is firmly grounded on terrain/NavMesh at start
        SnapToGround();
        _spawnPosition = transform.position;

        AutoFindPlayer();
        _nextGroanTime = Time.time + Random.Range(4f, 9f);

        // Start wandering immediately
        _currentState = State.Wander;
        if (_agent != null && _agent.enabled)
        {
            _agent.speed = walkSpeed;
            _agent.stoppingDistance = 0.5f;
        }
        PlayAnimation("Walk", 0.1f);
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
        if (_isDead) return;

        // Continuously enforce applyRootMotion = false so no animation clip ever floats the character
        if (_anim != null && _anim.applyRootMotion)
        {
            _anim.applyRootMotion = false;
        }

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

        // Ambient groaning while patrolling
        if (_currentState == State.Wander && Time.time >= _nextGroanTime)
        {
            _nextGroanTime = Time.time + Random.Range(6f, 14f);
            if (groanSound != null) _audioSource.PlayOneShot(groanSound, 0.6f);
            else GameAudio.PlayZombieGroan(transform.position, _audioSource);
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
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

        // Play Raad / Scream Audio
        if (screamSound != null)
            _audioSource.PlayOneShot(screamSound, 1.0f);
        else
            GameAudio.PlayZombieScream(transform.position, _audioSource);

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

    // ─── State: Chase (Run) ─────────────────────────────────────────────────

    private void StartChase()
    {
        if (_isDead) return;

        _currentState = State.Chase;
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = false;
            _agent.speed = runSpeed;
            if (player != null) _agent.SetDestination(player.position);
        }
        PlayAnimation("Run", 0.15f);
    }

    private void HandleChase(float distanceToPlayer)
    {
        if (_agent == null || !_agent.enabled || player == null) return;

        _agent.isStopped = false;
        _agent.speed = runSpeed;
        PlayAnimation("Run", 0.15f);

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
            PlayAnimation("Walk", 0.25f);
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
            PlayAnimation("Attack", 0.12f);

            // Play Attack SFX
            if (attackSound != null)
                _audioSource.PlayOneShot(attackSound, 0.9f);
            else
                GameAudio.PlayZombieAttack(transform.position, _audioSource);

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

        // Hurt audio
        if (hitSound != null)
            _audioSource.PlayOneShot(hitSound, 0.85f);
        else
            GameAudio.PlayZombieHurt(transform.position, _audioSource);

        // Blood VFX & flash
        Vector3 hitPoint = transform.position + Vector3.up * 1.1f;
        VFXUtil.SpawnFadingLight(hitPoint, new Color(0.9f, 0.1f, 0.05f), 4f, 2.0f, 0.08f);
        VFXUtil.Burst(hitPoint, Vector3.up, new Color(0.85f, 0.08f, 0.05f, 1f),
            20, 4f, 12f, 0.07f, 0.22f, 0.14f, 0.38f, true, true, 2.0f, 40f);

        // Check for Death
        if (_currentHealth <= 0f)
        {
            Die();
            return;
        }

        // Trigger Hit Reaction Animation & Stun
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(HitRoutine());
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

        // Enraged by damage: immediately sprint after player
        StartChase();
    }

    // ─── Death ──────────────────────────────────────────────────────────────

    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        _currentState = State.Dead;
        _currentHealth = 0f;

        if (_activeRoutine != null) StopCoroutine(_activeRoutine);

        // Stop navigation
        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.enabled = false;
        }

        // Disable collider so player doesn't collide with corpse
        if (_collider != null) _collider.enabled = false;

        PlayAnimation("Die", 0.12f);

        // Death SFX
        if (deathSound != null)
            _audioSource.PlayOneShot(deathSound, 1.0f);
        else
            GameAudio.PlayZombieDeath(transform.position, _audioSource);

        Debug.Log("<color=#FF0000><b>[Zombie Dead!]</b></color>");

        // Destroy after 25 seconds (corpse remains on terrain for a while)
        Destroy(gameObject, 25f);
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
                SetAnimFloatSafe("Speed", runSpeed);
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
                SetAnimFloatSafe("Speed", 0f);
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
