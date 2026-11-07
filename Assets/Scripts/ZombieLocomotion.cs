using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ZombieLocomotion
/// ================
/// AAA Locomotion, Grounding, and NavMesh Synchronization Engine for Zombies.
/// 
/// ── CORE SYSTEMS ─────────────────────────────────────────────────────────────
///  1. Continuous Ground Snapping: High-frequency downward raycasting ensures the 
///     character's feet stay glued to terrain, slopes, and stairs with zero air-floating.
///  2. Zero Foot-Sliding: Dynamically synchronizes Animator playback cadence with 
///     actual NavMeshAgent ground velocity.
///  3. Heavy AAA Movement: Organic acceleration/deceleration curves and natural 
///     turn deceleration (zombie slows down into sharp turns instead of sliding sideways).
///  4. Per-Zombie Variance: Unique speed multipliers (0.88x - 1.12x), randomized 
///     animation cycle offsets, and subtle Perlin noise speed flutter for a creepy, 
///     unsteady shambling cadence.
///  5. Full Pooling & Zero GC: Caches parameter hashes and resets in OnEnable.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZombieLocomotion : MonoBehaviour
{
    [Header("── Ground Snapping (Anti-Floating) ──")]
    [Tooltip("Enable continuous ground surface tracking.")]
    public bool enableGroundSnapping = true;

    [Tooltip("Layers considered solid ground (Terrain, Environment, Obstacles).")]
    public LayerMask groundLayer = ~0;

    [Tooltip("Height above zombie origin to start downward ground raycast.")]
    public float raycastOriginHeight = 1.6f;

    [Tooltip("Maximum raycast distance downwards to locate ground surface.")]
    public float maxGroundRayDistance = 4.0f;

    [Tooltip("Snappy lerp speed to glue zombie feet to the ground.")]
    public float groundSnapSpeed = 35f;

    [Tooltip("Vertical foot adjustment offset.")]
    public float groundOffset = 0.0f;

    [Header("── AAA Motion Weight & Speed Matching ──")]
    [Tooltip("Base walking speed of the walk animation (m/s).")]
    public float baseWalkSpeed = 1.3f;

    [Tooltip("Base running speed of the run animation (m/s).")]
    public float baseRunSpeed = 4.2f;

    [Tooltip("Smoothing time for Speed float parameter.")]
    public float speedDampTime = 0.15f;

    [Tooltip("Smoothing time for Turn float parameter.")]
    public float turnDampTime = 0.12f;

    [Tooltip("Slow down forward speed when turning sharply to prevent sliding sideways.")]
    public bool enableTurnDeceleration = true;

    [Range(20f, 90f)]
    public float sharpTurnAngleThreshold = 45f;

    [Header("── Creepy Unsteady Shambling (Perlin Noise) ──")]
    [Tooltip("Adds slight organic irregularity to chase speed for scary uncanny feel.")]
    public bool enableSpeedNoise = true;

    [Range(0f, 0.45f)]
    public float speedNoiseAmount = 0.22f;

    public float speedNoiseFrequency = 1.2f;

    // Component references
    private NavMeshAgent _agent;
    private Animator _anim;
    private Rigidbody _rb;
    private ZombieAI _zombieAI;

    // Cached Animator Parameter Hashes
    private int _hashSpeed;
    private int _hashTurn;
    private int _hashIsMoving;
    private int _hashIsRunning;
    private int _hashIsSliding;
    private int _hashIsDead;

    // Instance Variance
    private float _speedMultiplier = 1.0f;
    private float _perlinSeed;
    private float _currentSmoothedSpeed = 0f;
    private float _currentSmoothedTurn = 0f;
    private float _turnStrideScale = 1f;
    private bool _isGrounded = true;

    public float SpeedMultiplier => _speedMultiplier;
    public bool IsGrounded => _isGrounded;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
        _zombieAI = GetComponent<ZombieAI>();

        // Cache Hashes for Zero GC in Update
        _hashSpeed     = Animator.StringToHash("Speed");
        _hashTurn      = Animator.StringToHash("Turn");
        _hashIsMoving  = Animator.StringToHash("IsMoving");
        _hashIsRunning = Animator.StringToHash("IsRunning");
        _hashIsSliding = Animator.StringToHash("IsSliding");
        _hashIsDead    = Animator.StringToHash("IsDead");

        // Ground Layer Setup
        if (groundLayer == ~0 || groundLayer == 0)
        {
            groundLayer = ~LayerMask.GetMask("Player", "Ignore Raycast");
        }

        // Configure Rigidbody: Keep Kinematic so physics engine doesn't fight NavMeshAgent
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.None;
        }

        // Configure NavMeshAgent for AAA Heavy Feel
        if (_agent != null)
        {
            _agent.baseOffset = 0f;
            _agent.acceleration = 9.0f;       // Heavy natural acceleration
            _agent.angularSpeed = 135f;      // Heavy, menacing turn speed (no instant robotic pivot)
            _agent.autoBraking = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        // Animator Performance Setting
        if (_anim != null)
        {
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _anim.applyRootMotion = true;
        }

        // Generate per-instance random seed for organic variance
        _perlinSeed = Random.Range(10f, 1000f);
        _speedMultiplier = Random.Range(0.88f, 1.12f);
    }

    private void OnEnable()
    {
        // Object Pooling Reset
        _currentSmoothedSpeed = 0f;
        _currentSmoothedTurn = 0f;
        _speedMultiplier = Random.Range(0.88f, 1.12f);

        if (_anim != null)
        {
            _anim.speed = 1f;
            // Desynchronize initial walk cycle across multiple zombies
            _anim.Play(0, -1, Random.Range(0f, 1f));
        }

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
        }

        // Immediate snap to ground on spawn
        SnapToGroundImmediate();
    }

    private void Update()
    {
        if (_zombieAI != null && _zombieAI.IsDead)
        {
            if (_anim != null) _anim.speed = 1f;
            return;
        }

        // 1. Continuous Ground Snapping
        if (enableGroundSnapping)
        {
            ProcessGroundSnapping();
        }

        // 2. Velocity & Locomotion Parameter Processing
        ProcessLocomotionParameters();

        // 3. Foot Sliding Elimination (Stride Playback Matching)
        ProcessStrideMatching();
    }

    private void OnAnimatorMove()
    {
        // Swallow root motion. The NavMeshAgent owns the body, so a clip cannot lift it.
    }

    // ─── 1. Ground Snapping Engine ───────────────────────────────────────────
    private void ProcessGroundSnapping()
    {
        // On a NavMesh the agent is already on the floor. Raycasting here hits the
        // zombie's own capsule and ratchets the body upward.
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _isGrounded = true;
            if (_agent.baseOffset != 0f)
                _agent.baseOffset = 0f;
            return;
        }

        if (TryGetGroundPoint(transform.position, 8f, out Vector3 ground))
        {
            _isGrounded = true;
            Vector3 currentPos = transform.position;
            float targetY = ground.y + groundOffset;
            // Only pull toward real ground. Never climb onto a body or prop.
            if (targetY < currentPos.y + 0.05f)
            {
                currentPos.y = Mathf.Lerp(currentPos.y, targetY, Time.deltaTime * groundSnapSpeed);
                transform.position = currentPos;
            }
        }
        else
        {
            _isGrounded = false;
        }
    }

    private static readonly RaycastHit[] GroundHits = new RaycastHit[16];

    public static bool TryGetGroundPoint(Vector3 around, float distance, out Vector3 point)
    {
        Vector3 origin = around + Vector3.up * 3.5f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, distance + 3.5f, ~0, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        bool found = false;
        point = around;

        for (int i = 0; i < count; i++)
        {
            Transform hitTransform = GroundHits[i].transform;
            if (hitTransform == null) continue;
            if (hitTransform.GetComponentInParent<ZombieAI>() != null) continue;
            if (hitTransform.GetComponentInParent<PlayerController>() != null) continue;
            if (GroundHits[i].distance < closest)
            {
                closest = GroundHits[i].distance;
                point = GroundHits[i].point;
                found = true;
            }
        }

        if (found) return true;

        if (NavMesh.SamplePosition(around, out NavMeshHit navHit, distance, NavMesh.AllAreas))
        {
            point = navHit.position;
            return true;
        }

        return false;
    }

    public void SnapToGroundImmediate()
    {
        if (ZombieLocomotion.TryGetGroundPoint(transform.position, 8f, out Vector3 ground))
        {
            Vector3 p = transform.position;
            p.y = ground.y + groundOffset;
            transform.position = p;
        }
        else if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
        {
            transform.position = navHit.position;
        }

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.Warp(transform.position);
    }

    // ─── 2. Locomotion & Turning Parameters ─────────────────────────────────
    private void ProcessLocomotionParameters()
    {
        if (_agent == null || !_agent.enabled || _anim == null) return;

        // Actual linear ground speed
        Vector3 actualVelocity = _agent.velocity;
        actualVelocity.y = 0f;
        float actualSpeed = actualVelocity.magnitude;

        // Calculate Signed Turn Angle (-180 to +180 deg)
        float turnAngle = 0f;
        Vector3 desiredDir = _agent.desiredVelocity;
        desiredDir.y = 0f;

        if (desiredDir.sqrMagnitude > 0.05f)
        {
            turnAngle = Vector3.SignedAngle(transform.forward, desiredDir.normalized, Vector3.up);
        }

        // Sharp turns only slow the stride. Never multiply NavMeshAgent.speed —
        // that decay stuck every zombie at crawl pace after one corner.
        float playbackScale = 1f;
        if (enableTurnDeceleration && Mathf.Abs(turnAngle) > sharpTurnAngleThreshold)
        {
            playbackScale = Mathf.Clamp(1f - (Mathf.Abs(turnAngle) - sharpTurnAngleThreshold) / 140f, 0.72f, 1f);
        }
        _turnStrideScale = playbackScale;

        // Feed parameters to Animator with smooth damping
        _anim.SetFloat(_hashSpeed, actualSpeed, speedDampTime, Time.deltaTime);
        _anim.SetFloat(_hashTurn, turnAngle / 90f, turnDampTime, Time.deltaTime);

        // Movement booleans
        bool isMoving = actualSpeed > 0.15f;
        _anim.SetBool(_hashIsMoving, isMoving);
    }

    // ─── 3. Eliminate Foot Sliding (Stride-Matching) ─────────────────────────
    private void ProcessStrideMatching()
    {
        if (_anim == null || _agent == null || !_agent.enabled) return;

        // When dead, leave playback speed at normal
        if (_zombieAI != null && _zombieAI.IsDead)
        {
            _anim.speed = 1f;
            return;
        }

        Vector3 v = _agent.velocity;
        v.y = 0f;
        float groundSpeed = v.magnitude;

        // Crawl / Slide stride synchronization: matches crawl animation to actual ground speed
        if (_zombieAI != null && _zombieAI.IsSliding)
        {
            if (groundSpeed > 0.1f)
            {
                float baseSlideSpeed = 1.05f; // Cadence baseline for crawl animation
                float crawlRatio = groundSpeed / baseSlideSpeed;
                _anim.speed = Mathf.Clamp(crawlRatio, 0.65f, 1.35f);
            }
            else
            {
                _anim.speed = 1f;
            }
            return;
        }

        if (groundSpeed > 0.15f)
        {
            bool isRunning = _anim.GetBool(_hashIsRunning);
            float expectedBaseSpeed = isRunning ? baseRunSpeed : baseWalkSpeed;

            // Ratio of actual ground speed to expected animation speed
            float strideRatio = groundSpeed / Mathf.Max(0.2f, expectedBaseSpeed);

            // Optional: Creepy speed flutter (unsteady limp feel)
            if (enableSpeedNoise && isRunning)
            {
                float noise = (Mathf.PerlinNoise(Time.time * speedNoiseFrequency, _perlinSeed) - 0.5f) * 2f * speedNoiseAmount;
                strideRatio += noise;
            }

            // Clamp playback speed within natural biomechanical limits (0.75x to 1.30x)
            _anim.speed = Mathf.Clamp(strideRatio * _turnStrideScale, 0.75f, 1.30f);
        }
        else
        {
            _anim.speed = 1f;
        }
    }
}
