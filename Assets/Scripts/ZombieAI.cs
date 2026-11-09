using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ZombieAI
/// ========
/// AAA zombie brain:
/// 1. Sight is a cone plus a ray, so walls block aggro. Smell only works up close.
/// 2. Hearing: gunshots, sword swings, and another zombie's scream pull an investigate.
/// 3. Chase uses the last place the player was actually seen, with a short lead on their velocity.
/// 4. Lost sight becomes a search, then the zombie gives up and wanders.
/// 5. A scream wakes the pack. Each one screams only when it sees the player itself.
/// 6. Attacks need a facing commit, can whiff, and light hits do not stunlock the swing.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZombieAI : MonoBehaviour
{
    public enum State
    {
        Wander,
        Investigate,
        Raad,
        Chase,
        Search,
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
    [Tooltip("Optional designer volume. Standing inside it still counts as contact.")]
    public Collider triggerArea;
    [Tooltip("How far the zombie can see, in metres.")]
    public float detectionRadius = 18f;
    [Tooltip("Hard drop distance once the player has been out of sight for a while.")]
    public float loseTargetRadius = 36f;

    [Header("── Senses ──")]
    [Range(40f, 180f)] public float viewAngle = 118f;
    [Tooltip("Behind or beside the zombie, this close still counts as contact.")]
    public float proximityRadius = 2.8f;
    [Tooltip("Eye height used for the line-of-sight ray.")]
    public float eyeHeight = 1.55f;
    [Tooltip("Seconds without a visual before the chase becomes a search.")]
    public float loseSightDelay = 1.15f;
    [Tooltip("How long a search lasts before the zombie goes back to wandering.")]
    public float searchDuration = 7.5f;
    public float senseInterval = 0.12f;

    [Header("── Movement Settings ──")]
    public float walkSpeed = 1.6f;
    public float runSpeed = 4.8f;
    [Tooltip("Health <= 600 hone par zombie crawl/slide karke chase karega uski speed.")]
    public float slideSpeed = 1.1f;
    public float wanderRadius = 18f;
    public float wanderWaitMin = 1.5f;
    public float wanderWaitMax = 3.5f;
    public float rotationSpeed = 8.0f;
    [Tooltip("Extra burst when the zombie commits a lunge just outside melee.")]
    public float lungeSpeed = 6.4f;

    [Header("── Combat & Attack Settings ──")]
    public float attackRange = 2.2f;
    public float attackDamage = 15f;
    public float attackCooldown = 1.7f;
    public float attackHitDelay = 0.45f;
    [Tooltip("Hits at or above this value always break the current action. Sword does. Bullets do not.")]
    public float heavyStaggerDamage = 140f;
    [Tooltip("Minimum time between flinches so gunfire cannot freeze the zombie.")]
    public float staggerCooldown = 0.8f;

    [Header("── Raad (Scream) Settings ──")]
    [Tooltip("Scream animation kitni der tak hold karke player ki taraf dekhega.")]
    public float screamDuration = 1.9f;

    [Header("── Audio Volume & Sound Controls ───────────────────────────")]
    [Tooltip("Zombie ke sabhi sounds ka master volume multiplier (0.0 = Mute, 1.0 = Normal, 2.0 = Extra Loud)")]
    [Range(0f, 2f)]
    public float masterAudioVolume = 1.0f;

    [Tooltip("Zombie ke chillane / scream / roar sound ka volume")]
    [Range(0f, 1f)]
    public float screamVolume = 1.0f;

    [Tooltip("Zombie ke chalne / walk footsteps sound ka volume")]
    [Range(0f, 1f)]
    public float walkVolume = 0.6f;

    [Tooltip("Zombie ke attack swipe / strike sound ka volume")]
    [Range(0f, 1f)]
    public float attackVolume = 1.0f;

    [Tooltip("Zombie ko goli ya talwar lagne par hurt sound ka volume")]
    [Range(0f, 1f)]
    public float hurtVolume = 0.9f;

    [Tooltip("Zombie ke marne par death sound ka volume")]
    [Range(0f, 1f)]
    public float deathVolume = 1.0f;

    [Header("── Custom Audio Clips (Optional Override) ───────────────────")]
    [Tooltip("Custom Scream audio (khali chhodne par default 'scream sound zombie' chalega)")]
    public AudioClip screamSound;

    [Tooltip("Custom Walk audio (khali chhodne par default 'Zombie Walking Sound Effect' chalega)")]
    public AudioClip walkSound;

    [Tooltip("Custom Attack audio (khali chhodne par dynamic sword/claw slash whoosh chalega)")]
    public AudioClip attackSound;

    [Tooltip("Custom Hurt audio (khali chhodne par flesh hit impact chalega)")]
    public AudioClip hurtSound;

    [Tooltip("Custom Death audio (khali chhodne par guttural death moan chalega)")]
    public AudioClip deathSound;

    private AudioSource _zombieAudio;
    private float _nextWalkSoundTime;

    [Header("── Target Reference ──")]
    public Transform player;

    [Header("── UI Feedback ──")]
    [Tooltip("Zombie ke sir ke upar health bar show karein jab damage ho ya player pass ho.")]
    public bool showFloatingHealthBar = false;

    private NavMeshAgent _agent;
    private Animator _anim;
    private Collider _collider;
    private ZombieLocomotion _locomotion;
    private ZombieFootIK _footIK;

    private State _currentState = State.Wander;
    public State CurrentState => _currentState;

    private bool _isDead = false;
    private bool _isSliding = false;
    public bool IsSliding => _isSliding;
    private Vector3 _spawnPosition;
    private float _wanderWaitTimer = 0f;
    private Coroutine _activeRoutine;
    private string _currentAnimState = "";

    private bool _canSee;
    private float _nextSenseTime;
    private float _lastSeenTime = -999f;
    private Vector3 _lastKnown;
    private Vector3 _prevPlayerPos;
    private Vector3 _playerVelocity;
    private bool _screamedThisEngagement;
    private bool _attackCommitted;
    private float _nextStaggerTime;
    private float _nextRepathTime;
    private float _stuckTimer;
    private float _searchEndTime;
    private int _searchStepsLeft;
    private float _flankOffset;
    private int _avoidancePriority;

    private static readonly Collider[] SepBuffer = new Collider[16];
    private static readonly RaycastHit[] LosBuffer = new RaycastHit[16];

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponent<Animator>();
        _locomotion = GetComponent<ZombieLocomotion>() ?? gameObject.AddComponent<ZombieLocomotion>();
        _footIK = GetComponent<ZombieFootIK>() ?? gameObject.AddComponent<ZombieFootIK>();

        if (_anim != null)
            _anim.applyRootMotion = true;

        if (_agent != null)
        {
            _agent.baseOffset = 0f;
            _agent.updatePosition = true;
            _agent.updateRotation = true;
            _agent.acceleration = 14f;
            _agent.angularSpeed = 260f;
            _agent.autoBraking = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            _avoidancePriority = Random.Range(28, 72);
            _agent.avoidancePriority = _avoidancePriority;
        }

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
            FitCapsule(capsule, 1.8f, 0.45f, 0.9f);
            _collider = capsule;
        }
        else
        {
            _collider.isTrigger = false;
            if (_collider is CapsuleCollider cap)
                FitCapsule(cap, 1.8f, 0.45f, 0.9f);
        }

        _zombieAudio = GetComponent<AudioSource>();
        if (_zombieAudio == null)
        {
            _zombieAudio = gameObject.AddComponent<AudioSource>();
            _zombieAudio.spatialBlend = 1.0f;
            _zombieAudio.minDistance = 3.0f;
            _zombieAudio.maxDistance = 35.0f;
            _zombieAudio.rolloffMode = AudioRolloffMode.Linear;
            _zombieAudio.playOnAwake = false;
        }
        _zombieAudio.volume = masterAudioVolume;

        if (screamSound == null) screamSound = GameAudio.GetClip(GameAudio.SoundType.ZombieScream);
        if (walkSound == null) walkSound = GameAudio.GetClip(GameAudio.SoundType.ZombieWalk);
        if (attackSound == null) attackSound = GameAudio.GetClip(GameAudio.SoundType.ZombieAttack);
        if (hurtSound == null) hurtSound = GameAudio.GetClip(GameAudio.SoundType.ZombieHurt);
        if (deathSound == null) deathSound = GameAudio.GetClip(GameAudio.SoundType.ZombieDeath);

        _flankOffset = Random.Range(-1.35f, 1.35f);
        _nextSenseTime = Time.time + Random.Range(0f, senseInterval);
    }

    private void OnValidate()
    {
        masterAudioVolume = Mathf.Clamp(masterAudioVolume, 0f, 2f);
        screamVolume = Mathf.Clamp01(screamVolume);
        walkVolume = Mathf.Clamp01(walkVolume);
        attackVolume = Mathf.Clamp01(attackVolume);
        hurtVolume = Mathf.Clamp01(hurtVolume);
        deathVolume = Mathf.Clamp01(deathVolume);

        if (_zombieAudio != null)
        {
            _zombieAudio.volume = masterAudioVolume;
        }
    }

    public void PlayScreamSound()
    {
        float vol = screamVolume * masterAudioVolume;
        if (vol <= 0.001f) return;
        if (screamSound != null && _zombieAudio != null && _zombieAudio.isActiveAndEnabled)
        {
            _zombieAudio.pitch = Random.Range(0.95f, 1.05f);
            _zombieAudio.PlayOneShot(screamSound, vol);
        }
        else
        {
            GameAudio.PlayZombieScream(transform.position, _zombieAudio, vol);
        }
    }

    public void PlayWalkSound()
    {
        float vol = walkVolume * masterAudioVolume;
        if (vol <= 0.001f) return;
        if (walkSound != null && _zombieAudio != null && _zombieAudio.isActiveAndEnabled)
        {
            _zombieAudio.pitch = Random.Range(0.92f, 1.08f);
            _zombieAudio.PlayOneShot(walkSound, vol);
        }
        else
        {
            GameAudio.PlayZombieWalk(transform.position, _zombieAudio, vol);
        }
    }

    public void PlayAttackSound()
    {
        float vol = attackVolume * masterAudioVolume;
        if (vol <= 0.001f) return;
        if (attackSound != null && _zombieAudio != null && _zombieAudio.isActiveAndEnabled)
        {
            _zombieAudio.pitch = Random.Range(0.85f, 1.05f);
            _zombieAudio.PlayOneShot(attackSound, vol);
        }
        else
        {
            GameAudio.PlayZombieAttack(transform.position, _zombieAudio, vol);
        }
    }

    public void PlayHurtSound()
    {
        float vol = hurtVolume * masterAudioVolume;
        if (vol <= 0.001f) return;
        if (hurtSound != null && _zombieAudio != null && _zombieAudio.isActiveAndEnabled)
        {
            _zombieAudio.pitch = Random.Range(0.9f, 1.15f);
            _zombieAudio.PlayOneShot(hurtSound, vol);
        }
        else
        {
            GameAudio.PlayZombieHurt(transform.position, _zombieAudio, vol);
        }
    }

    public void PlayDeathSound()
    {
        float vol = deathVolume * masterAudioVolume;
        if (vol <= 0.001f) return;
        if (deathSound != null && _zombieAudio != null && _zombieAudio.isActiveAndEnabled)
        {
            _zombieAudio.pitch = Random.Range(0.72f, 0.88f);
            _zombieAudio.PlayOneShot(deathSound, vol);
        }
        else
        {
            GameAudio.PlayZombieDeath(transform.position, _zombieAudio, vol);
        }
    }

    private void OnEnable()
    {
        ZombieSense.OnNoise += OnNoise;
    }

    private void OnDisable()
    {
        ZombieSense.OnNoise -= OnNoise;
    }

    public void ApplySlidingProfile()
    {
        if (_collider is CapsuleCollider cap)
            FitCapsule(cap, 0.55f, 0.4f, 0.28f);
        if (_agent != null && _agent.enabled)
        {
            _agent.height = 0.6f;
            _agent.radius = 0.45f;
            _agent.stoppingDistance = 1.2f;
            _agent.angularSpeed = 45f;
            _agent.acceleration = 4.5f;
        }
    }

    /// <summary>
    /// Collider numbers are world metres. The zombie prefab is scaled, so the
    /// local capsule has to be divided by that scale or the player cannot reach the body.
    /// </summary>
    private void FitCapsule(CapsuleCollider cap, float worldHeight, float worldRadius, float worldCenterY)
    {
        float sx = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        float sy = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        cap.direction = 1;
        cap.height = worldHeight / sy;
        cap.radius = worldRadius / sx;
        cap.center = new Vector3(0f, worldCenterY / sy, 0f);
        cap.isTrigger = false;
    }

    private void Start()
    {
        _currentHealth = maxHealth;

        if (_locomotion != null)
            _locomotion.SnapToGroundImmediate();
        else
            SnapToGround();

        _spawnPosition = transform.position;
        _lastKnown = _spawnPosition;

        AutoFindPlayer();
        if (player != null) _prevPlayerPos = player.position;
        EnsurePlayerCollision();

        _currentState = State.Wander;
        if (_agent != null && _agent.enabled)
        {
            _agent.speed = walkSpeed;
            _agent.stoppingDistance = 1.2f;
        }
        PlayAnimation("Walk", 0.1f);
        SetNewWanderDestination();
    }

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

    public void SnapToGround()
    {
        if (_anim != null) _anim.applyRootMotion = true;
        if (_agent != null) _agent.baseOffset = 0f;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
        {
            if (_agent != null && _agent.enabled)
                _agent.Warp(navHit.position);
            else
                transform.position = navHit.position;
            return;
        }

        if (ZombieLocomotion.TryGetGroundPoint(transform.position, 30f, out Vector3 ground))
        {
            transform.position = ground;
            if (_agent != null && _agent.enabled && NavMesh.SamplePosition(ground, out NavMeshHit snapHit, 10f, NavMesh.AllAreas))
                _agent.Warp(snapHit.position);
        }
    }

    private void LateUpdate()
    {
        if (_anim != null && !_anim.applyRootMotion)
            _anim.applyRootMotion = true;

        if (_isDead) return;

        if (_agent != null && _agent.enabled && _agent.baseOffset != 0f)
            _agent.baseOffset = 0f;
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
        SeparateFromPack();
        KeepBodiesApart(distanceToPlayer);

        if (Time.time >= _nextSenseTime)
        {
            _nextSenseTime = Time.time + senseInterval;
            RefreshSenses(distanceToPlayer);
        }

        switch (_currentState)
        {
            case State.Wander:
                HandleWander();
                break;
            case State.Investigate:
                HandleInvestigate();
                break;
            case State.Raad:
                FacePlayer();
                break;
            case State.Chase:
                HandleChase(distanceToPlayer);
                break;
            case State.Search:
                HandleSearch();
                break;
            case State.Attack:
                FacePlayer();
                break;
            case State.Hit:
                break;
        }
    }

    // ─── Senses ─────────────────────────────────────────────────────────────

    private void RefreshSenses(float distanceToPlayer)
    {
        Vector3 current = player.position;
        float dt = Mathf.Max(senseInterval, Time.deltaTime);
        Vector3 delta = current - _prevPlayerPos;
        if (delta.sqrMagnitude > 64f)
        {
            _playerVelocity = Vector3.zero;
        }
        else
        {
            _playerVelocity = delta / dt;
            _playerVelocity.y = 0f;
        }
        _prevPlayerPos = current;

        _canSee = CanSeePlayer(distanceToPlayer);

        if (_canSee)
        {
            _lastSeenTime = Time.time;
            _lastKnown = current + Vector3.ClampMagnitude(_playerVelocity, 8f) * 0.35f;

            if (_currentState == State.Wander || _currentState == State.Investigate || _currentState == State.Search)
                EngageFromSight();
        }
        else
        {
            HearFootsteps(distanceToPlayer);
        }
    }

    private bool CanSeePlayer(float distanceToPlayer)
    {
        if (player == null) return false;

        if (triggerArea != null && triggerArea.bounds.Contains(player.position))
            return true;

        if (distanceToPlayer <= 1.15f)
            return true;

        bool close = distanceToPlayer <= proximityRadius;
        if (!close && distanceToPlayer > detectionRadius)
            return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 chest = player.position + Vector3.up * 1.05f;
        Vector3 to = chest - eye;
        float dist = to.magnitude;
        if (dist < 0.05f) return true;

        Vector3 dir = to / dist;
        Vector3 flatFwd = transform.forward;
        flatFwd.y = 0f;
        Vector3 flatTo = to;
        flatTo.y = 0f;
        if (!close && flatFwd.sqrMagnitude > 0.001f && flatTo.sqrMagnitude > 0.001f)
        {
            if (Vector3.Angle(flatFwd, flatTo) > viewAngle * 0.5f)
                return false;
        }

        int count = Physics.RaycastNonAlloc(eye, dir, LosBuffer, dist - 0.15f, ~0, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        int closestIndex = -1;
        for (int i = 0; i < count; i++)
        {
            Transform hitTransform = LosBuffer[i].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;
            if (hitTransform.GetComponentInParent<ZombieAI>() != null)
                continue;
            if (LosBuffer[i].distance < closest)
            {
                closest = LosBuffer[i].distance;
                closestIndex = i;
            }
        }

        if (closestIndex < 0)
            return true;

        Transform blocker = LosBuffer[closestIndex].transform;
        return blocker == player || blocker.IsChildOf(player) || player.IsChildOf(blocker);
    }

    private void HearFootsteps(float distanceToPlayer)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) return;

        Vector3 flat = cc.velocity;
        flat.y = 0f;
        float speed = flat.magnitude;
        float hearRange = 6.5f + speed * 0.45f;
        if (speed <= 3.4f || distanceToPlayer > hearRange) return;

        _lastKnown = player.position;
        if (_currentState == State.Investigate)
        {
            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + 0.45f;
                SetGoal(player.position, 2.5f);
            }
            return;
        }

        if (_currentState == State.Wander || _currentState == State.Search)
            BeginInvestigate(player.position, speed > 5.5f);
    }

    private void OnNoise(ZombieSense.Noise noise)
    {
        if (_isDead || !isActiveAndEnabled) return;

        float dist = Vector3.Distance(transform.position, noise.position);
        if (dist < 0.4f || dist > noise.radius) return;

        bool loud = noise.kind == ZombieSense.Kind.Gunshot || noise.kind == ZombieSense.Kind.Scream;

        if (_canSee && (_currentState == State.Chase || _currentState == State.Attack || _currentState == State.Raad))
            return;

        _lastKnown = noise.position;

        if (_currentState == State.Chase)
        {
            SetGoal(noise.position, _isSliding ? 1.1f : 1.4f);
            return;
        }

        if (_currentState == State.Attack || _currentState == State.Raad || _currentState == State.Hit)
            return;

        BeginInvestigate(noise.position, loud);
    }

    private void EngageFromSight()
    {
        if (!_screamedThisEngagement && !_isSliding)
            StartRaad();
        else
            StartChase();
    }

    // ─── Wander ─────────────────────────────────────────────────────────────

    private void HandleWander()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

        SetAgentFacing(true);
        _agent.speed = _isSliding ? slideSpeed : walkSpeed;
        PlayAnimation(_isSliding ? "Slide" : "Walk", 0.2f);
        TryPlayWalkSound();

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.25f)
        {
            _wanderWaitTimer -= Time.deltaTime;
            if (_wanderWaitTimer <= 0f)
            {
                SetNewWanderDestination();
                _wanderWaitTimer = Random.Range(wanderWaitMin, wanderWaitMax);
            }
        }
    }

    private void TryPlayWalkSound()
    {
        if (_agent == null || _agent.velocity.sqrMagnitude < 0.1f || Time.time < _nextWalkSoundTime) return;
        _nextWalkSoundTime = Time.time + Random.Range(3.0f, 5.5f);
        PlayWalkSound();
    }

    private void SetNewWanderDestination()
    {
        Vector3 targetCenter = triggerArea != null ? triggerArea.bounds.center : _spawnPosition;
        Vector3 randomPoint = targetCenter + Random.insideUnitSphere * wanderRadius;
        randomPoint.y = transform.position.y;
        SetGoal(randomPoint, 0.8f);
    }

    // ─── Investigate (heard, not seen) ──────────────────────────────────────

    private void BeginInvestigate(Vector3 point, bool sprint)
    {
        if (_isDead) return;
        if (_currentState == State.Raad || _currentState == State.Attack || _currentState == State.Hit)
            return;

        StopActiveRoutine();
        _currentState = State.Investigate;
        _lastKnown = point;
        _stuckTimer = 0f;

        if (_agent != null && _agent.enabled)
        {
            SetAgentFacing(true);
            _agent.isStopped = false;
            _agent.speed = _isSliding ? slideSpeed : (sprint ? runSpeed : walkSpeed * 1.25f);
            _agent.stoppingDistance = 1.1f;
        }

        PlayAnimation(_isSliding ? "Slide" : (sprint ? "Run" : "Walk"), 0.18f);
        SetGoal(point, 2.5f);
    }

    private void HandleInvestigate()
    {
        if (_canSee)
        {
            EngageFromSight();
            return;
        }

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

        SetAgentFacing(true);
        bool arrived = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.45f;
        RecoverIfStuck();

        if (arrived)
        {
            BeginSearch();
            return;
        }

        if (Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + 0.45f;
            SetGoal(_lastKnown, 2.5f);
        }
    }

    // ─── Raad ───────────────────────────────────────────────────────────────

    private void StartRaad()
    {
        if (_currentState == State.Raad || _isDead) return;

        _currentState = State.Raad;
        _screamedThisEngagement = true;
        StopAgent();
        SetAgentFacing(false);

        StopActiveRoutine();
        _activeRoutine = StartCoroutine(RaadRoutine());
    }

    private IEnumerator RaadRoutine()
    {
        PlayAnimation("Raad", 0.12f);
        PlayScreamSound();
        ZombieSense.Report(transform.position, ZombieSense.Kind.Scream);

        float elapsed = 0f;
        while (elapsed < screamDuration)
        {
            if (_isDead) yield break;
            FacePlayer();
            elapsed += Time.deltaTime;
            yield return null;
        }

        StartChase();
    }

    // ─── Chase ──────────────────────────────────────────────────────────────

    private void StartChase()
    {
        if (_isDead) return;

        StopActiveRoutine();
        _currentState = State.Chase;
        _stuckTimer = 0f;
        _attackCommitted = false;

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = false;
            _agent.speed = _isSliding ? slideSpeed : runSpeed;
            _agent.stoppingDistance = _isSliding ? 1.15f : 1.35f;
            SetAgentFacing(true);
        }

        PlayAnimation(_isSliding ? "Slide" : "Run", 0.15f);
        SetGoal(ChaseDestination(), 2f);
    }

    private void HandleChase(float distanceToPlayer)
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || player == null) return;

        float melee = _isSliding ? 1.4f : attackRange;
        bool recentlySeen = Time.time - _lastSeenTime <= loseSightDelay;

        if (_canSee && distanceToPlayer <= melee && FacingPlayer(42f))
        {
            StartAttack();
            return;
        }

        if (!_canSee && !recentlySeen)
        {
            BeginSearch();
            return;
        }

        if (!_canSee && distanceToPlayer > loseTargetRadius && Time.time - _lastSeenTime > 2.5f)
        {
            Disengage();
            return;
        }

        bool close = _canSee && distanceToPlayer < melee * 1.7f;
        SetAgentFacing(!close);
        if (close) FacePlayer();

        _agent.isStopped = false;
        _agent.speed = _isSliding ? slideSpeed : runSpeed;
        PlayAnimation(_isSliding ? "Slide" : "Run", 0.15f);
        TryPlayWalkSound();
        RecoverIfStuck();

        if (Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + (close ? 0.12f : 0.22f);
            SetGoal(ChaseDestination(), 2f);
        }
    }

    private Vector3 ChaseDestination()
    {
        if (!_canSee || player == null) return _lastKnown;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) return player.position;

        float dist = toPlayer.magnitude;
        if (dist > attackRange * 3.2f)
            return player.position + Vector3.ClampMagnitude(_playerVelocity, 7f) * 0.28f;

        Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized);
        float holdOff = Mathf.Max(0.4f, attackRange * 0.72f);
        return player.position - toPlayer.normalized * holdOff + side * _flankOffset;
    }

    // ─── Search ─────────────────────────────────────────────────────────────

    private void BeginSearch()
    {
        if (_isDead) return;
        if (_currentState == State.Search) return;

        StopActiveRoutine();
        _currentState = State.Search;
        _searchEndTime = Time.time + searchDuration;
        _searchStepsLeft = 3;
        _stuckTimer = 0f;

        if (_agent != null && _agent.enabled)
        {
            SetAgentFacing(true);
            _agent.isStopped = false;
            _agent.speed = _isSliding ? slideSpeed : walkSpeed;
            _agent.stoppingDistance = 0.7f;
        }

        PlayAnimation(_isSliding ? "Slide" : "Walk", 0.22f);
        PickSearchPoint();
    }

    private void HandleSearch()
    {
        if (_canSee)
        {
            EngageFromSight();
            return;
        }

        if (Time.time >= _searchEndTime || _searchStepsLeft <= 0)
        {
            Disengage();
            return;
        }

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

        SetAgentFacing(true);
        _agent.speed = _isSliding ? slideSpeed : walkSpeed;
        PlayAnimation(_isSliding ? "Slide" : "Walk", 0.2f);
        RecoverIfStuck();

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.35f)
        {
            _searchStepsLeft--;
            if (_searchStepsLeft <= 0)
            {
                Disengage();
                return;
            }
            PickSearchPoint();
        }
    }

    private void PickSearchPoint()
    {
        Vector3 offset = Random.insideUnitSphere * Random.Range(2.5f, 6.5f);
        offset.y = 0f;
        SetGoal(_lastKnown + offset, 4f);
    }

    private void Disengage()
    {
        StopActiveRoutine();
        _screamedThisEngagement = false;
        _canSee = false;
        _currentState = State.Wander;
        _stuckTimer = 0f;
        if (_agent != null && _agent.enabled)
        {
            SetAgentFacing(true);
            _agent.isStopped = false;
            _agent.speed = _isSliding ? slideSpeed : walkSpeed;
            _agent.stoppingDistance = 0.8f;
        }
        PlayAnimation(_isSliding ? "Slide" : "Walk", 0.25f);
        SetNewWanderDestination();
    }

    // ─── Attack ─────────────────────────────────────────────────────────────

    private void StartAttack()
    {
        if (_currentState == State.Attack || _isDead) return;

        _currentState = State.Attack;
        _attackCommitted = false;
        StopAgent();
        SetAgentFacing(false);

        StopActiveRoutine();
        _activeRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        float melee = _isSliding ? 1.4f : attackRange;

        if (!_isSliding && player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > melee * 0.85f && dist < melee * 2.5f)
            {
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.speed = lungeSpeed;
                    _agent.stoppingDistance = 0.85f;
                    SetGoal(player.position, 1.5f);
                }
                PlayAnimation("Run", 0.08f);

                float lunged = 0f;
                while (lunged < 0.32f && !_isDead)
                {
                    FacePlayer();
                    lunged += Time.deltaTime;
                    if (player != null && Vector3.Distance(transform.position, player.position) <= melee * 0.9f)
                        break;
                    yield return null;
                }

                StopAgent();
            }
        }

        while (_currentState == State.Attack && !_isDead)
        {
            float aimTime = 0f;
            while (aimTime < 0.28f && !FacingPlayer(32f))
            {
                if (_isDead) yield break;
                FacePlayer();
                aimTime += Time.deltaTime;
                yield return null;
            }

            _attackCommitted = true;
            FacePlayer();
            PlayAnimation(_isSliding ? "Slide" : "Attack", 0.08f);

            // 1. Play Zombie Attack Swipe Sound
            PlayAttackSound();

            // 2. Spawn Sword-like Slashing Blade/Claw VFX Arc in front of zombie
            Vector3 swipeOrigin = transform.position + Vector3.up * 1.25f + transform.forward * 0.75f;
            VFXUtil.SpawnSwordSlashCut(swipeOrigin, transform.forward, transform.right, isZombie: true);

            float wind = 0f;
            while (wind < attackHitDelay)
            {
                if (_isDead) yield break;
                FacePlayer();
                wind += Time.deltaTime;
                yield return null;
            }

            melee = _isSliding ? 1.4f : attackRange;
            bool inReach = player != null && Vector3.Distance(transform.position, player.position) <= melee + 0.55f;
            bool aimed = FacingPlayer(58f);
            if (inReach && aimed)
            {
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null && !pc.IsDead)
                {
                    pc.TakeDamage((int)attackDamage);

                    Vector3 strikePos = player.position + Vector3.up * 1.1f;

                    // 1. Visceral flesh impact audio with inspector volume scaling
                    GameAudio.PlaySwordImpact(strikePos, true, attackVolume * masterAudioVolume);

                    // 2. AAA Sword-like Flesh Hit VFX: slicing blood fans, sparks, flesh burst, light flash
                    VFXUtil.SwordFleshHit(strikePos, transform.forward);
                    VFXUtil.SpawnSwordSlashCut(strikePos, transform.forward, -transform.right, isZombie: true);

                    // 3. Screen shake on player camera
                    if (Camera.main != null)
                    {
                        CameraController cc = Camera.main.GetComponent<CameraController>();
                        if (cc != null) cc.AddRecoil(0.28f, Random.Range(-0.15f, 0.15f), 0.035f);
                    }
                }
            }

            _attackCommitted = false;

            float rest = Mathf.Max(0.2f, attackCooldown - attackHitDelay + Random.Range(-0.15f, 0.25f));
            float timer = 0f;
            while (timer < rest)
            {
                if (_isDead) yield break;
                FacePlayer();
                timer += Time.deltaTime;
                yield return null;
            }

            if (player == null || Vector3.Distance(transform.position, player.position) > melee || !FacingPlayer(70f))
            {
                StartChase();
                yield break;
            }
        }
    }

    // ─── Damage ─────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, Vector3 hitPoint = default, Vector3 hitNormal = default)
    {
        if (_isDead) return;

        _currentHealth -= amount;
        PlayHurtSound();

        Vector3 fxPos = hitPoint == default ? (transform.position + Vector3.up * 1.1f) : hitPoint;
        VFXUtil.SpawnFadingLight(fxPos, new Color(1.0f, 0.04f, 0.02f), 4.5f, 3.2f, 0.08f);

        if (GunShooter.Instance != null)
            GunShooter.Instance.RegisterHitMarker(_currentHealth <= 0f);

        if (player != null)
        {
            _lastKnown = player.position;
            _lastSeenTime = Time.time;
            _screamedThisEngagement = true;
        }

        bool heavy = amount >= heavyStaggerDamage;
        bool armored = _attackCommitted && !heavy;
        bool canStagger = heavy || Time.time >= _nextStaggerTime;
        float knock = heavy ? 0.55f : 0.08f;
        if (_isSliding) knock *= 0.55f;

        if (player != null && knock > 0f)
        {
            Vector3 pushDir = (transform.position - player.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.001f)
            {
                pushDir.Normalize();
                if (_agent != null && _agent.enabled)
                    _agent.Move(pushDir * knock);
                else
                    transform.position += pushDir * knock;
            }
        }

        if (_currentHealth <= 0f)
        {
            Die();
            return;
        }

        if (_currentHealth <= 600f && !_isSliding)
        {
            _isSliding = true;
            if (_footIK != null) _footIK.SetIKActive(false);
            ApplySlidingProfile();
        }

        if (_currentState == State.Wander || _currentState == State.Investigate || _currentState == State.Search)
            _currentState = State.Chase;

        if (armored || !canStagger)
        {
            if (_currentState != State.Attack && _currentState != State.Raad && _currentState != State.Hit)
                StartChase();
            return;
        }

        _nextStaggerTime = Time.time + (heavy ? 0.35f : staggerCooldown);

        StopActiveRoutine();
        _activeRoutine = StartCoroutine(_isSliding ? SlideHitRoutine() : HitRoutine());
    }

    private IEnumerator SlideHitRoutine()
    {
        _currentState = State.Hit;
        _attackCommitted = false;
        StopAgent();
        PlayAnimation("Slide", 0.05f);

        yield return new WaitForSeconds(0.22f);
        if (_isDead) yield break;
        StartChase();
    }

    private IEnumerator HitRoutine()
    {
        _currentState = State.Hit;
        _attackCommitted = false;
        StopAgent();
        SetAgentFacing(false);
        PlayAnimation("Hit", 0.06f);

        float elapsed = 0f;
        while (elapsed < 0.42f)
        {
            if (_isDead) yield break;
            FacePlayer();
            elapsed += Time.deltaTime;
            yield return null;
        }

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
        _attackCommitted = false;

        StopActiveRoutine();
        PlayDeathSound();

        if (_footIK != null) _footIK.SetIKActive(false);
        if (_locomotion != null) _locomotion.enabled = false;

        if (_anim != null)
        {
            _anim.applyRootMotion = true;
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

        if (_agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        SnapToGround();
        StartCoroutine(DeathGroundClampRoutine());

        if (_collider != null) _collider.enabled = false;
        PlayAnimation("Die", 0.08f);
        Destroy(gameObject, 25f);
    }

    private IEnumerator DeathGroundClampRoutine()
    {
        float duration = 5.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (_anim != null && !_anim.applyRootMotion)
                _anim.applyRootMotion = true;

            if (ZombieLocomotion.TryGetGroundPoint(transform.position, 6f, out Vector3 ground))
                transform.position = new Vector3(transform.position.x, ground.y, transform.position.z);

            yield return null;
        }
    }

    // ─── Steering helpers ───────────────────────────────────────────────────

    private void FacePlayer()
    {
        if (player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        if (_isSliding)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 45f * Time.deltaTime);
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }

    private bool FacingPlayer(float degrees)
    {
        if (player == null) return false;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return true;
        return Vector3.Angle(transform.forward, dir) <= degrees;
    }

    private void SetAgentFacing(bool agentDrivesRotation)
    {
        if (_agent != null && _agent.enabled)
            _agent.updateRotation = agentDrivesRotation && !_isSliding;
    }

    private void StopAgent()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.ResetPath();
        }
    }

    private void StopActiveRoutine()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
    }

    private bool SetGoal(Vector3 world, float sampleRadius)
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return false;
        if (!NavMesh.SamplePosition(world, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return false;

        _agent.isStopped = false;
        _agent.SetDestination(hit.position);
        return true;
    }

    private void RecoverIfStuck()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        bool blocked = _agent.velocity.sqrMagnitude < 0.04f && _agent.remainingDistance > 1.25f && !_agent.pathPending;
        _stuckTimer = blocked ? _stuckTimer + Time.deltaTime : 0f;
        if (_stuckTimer < 1.05f) return;

        _stuckTimer = 0f;
        Vector3 side = transform.right * Random.Range(-2.4f, 2.4f) + transform.forward * Random.Range(1.2f, 2.8f);
        SetGoal(transform.position + side, 3f);
    }

    private void SeparateFromPack()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        if (_currentState == State.Raad || _currentState == State.Hit || _currentState == State.Dead) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, 1.45f, SepBuffer, ~0, QueryTriggerInteraction.Ignore);
        Vector3 push = Vector3.zero;
        int hits = 0;
        for (int i = 0; i < count; i++)
        {
            Collider col = SepBuffer[i];
            if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;
            ZombieAI other = col.GetComponentInParent<ZombieAI>();
            if (other == null || other == this || other.IsDead) continue;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float mag = away.magnitude;
            if (mag < 0.05f) away = transform.right;
            else away /= mag;
            push += away * (1.45f - Mathf.Min(mag, 1.45f));
            hits++;
        }

        if (hits > 0)
            _agent.Move((push / hits) * (2.2f * Time.deltaTime));
    }

    private void KeepBodiesApart(float distanceToPlayer)
    {
        if (distanceToPlayer >= 1.35f) return;
        Vector3 pushDir = transform.position - player.position;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.001f || _agent == null || !_agent.enabled) return;
        float overlap = 1.35f - distanceToPlayer;
        _agent.Move(pushDir.normalized * (overlap * 0.5f));
    }

    private void PlayAnimation(string stateName, float crossfade = 0.15f)
    {
        if (_anim == null) return;

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
                SetAnimBoolSafe("IsRunning", false);
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

        int hash = Animator.StringToHash(stateName);
        if (_anim.HasState(0, hash))
            _anim.CrossFadeInFixedTime(stateName, crossfade);
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

    private void OnGUI()
    {
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.85f);
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(eye, eye + left * detectionRadius);
        Gizmos.DrawLine(eye, eye + right * detectionRadius);

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, proximityRadius);

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Vector3 center = triggerArea != null ? triggerArea.bounds.center : (Application.isPlaying ? _spawnPosition : transform.position);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(center, wanderRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.9f);
            Gizmos.DrawSphere(_lastKnown, 0.25f);
            Gizmos.DrawLine(transform.position + Vector3.up, _lastKnown + Vector3.up * 0.2f);
        }
    }
}
