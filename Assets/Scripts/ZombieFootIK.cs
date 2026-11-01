using UnityEngine;

/// <summary>
/// ZombieFootIK
/// ============
/// AAA-Grade Procedural Foot Inverse Kinematics (IK) & LookAt System for Humanoid Zombies.
/// 
/// ── FEATURES ──────────────────────────────────────────────────────────────────
///  1. Ground-Conforming Feet: Raycasts each foot (Left & Right) to ground surfaces, 
///     stairs, and terrain slopes; plants feet firmly on contact points.
///  2. Surface Normal Alignment: Rotates feet to match terrain slope angle.
///  3. Pelvis / Hips Height Adjustment: Lowers body so feet reach ground without overextending.
///  4. LookAt Head Tracking: Smoothly turns head toward player during alert/chase states.
///  5. State Awareness: Automatically blends out IK when sliding, crawling, or dead.
///  6. Zero Allocations: All calculations use cached variables and direct math.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ZombieFootIK : MonoBehaviour
{
    [Header("── IK Master Control ──")]
    [Tooltip("Global toggle for Foot IK solver.")]
    public bool enableFootIK = true;

    [Tooltip("Global toggle for LookAt head tracking toward player.")]
    public bool enableLookAt = true;

    [Header("── Foot Raycast Settings ──")]
    [Tooltip("Layers considered solid ground (Terrain, Default, Obstacles).")]
    public LayerMask groundLayer = ~0;

    [Tooltip("Height above foot to begin downward raycast.")]
    [Range(0.2f, 1.5f)]
    public float raycastUpDistance = 0.65f;

    [Tooltip("Distance below foot to search for ground.")]
    [Range(0.2f, 1.5f)]
    public float raycastDownDistance = 0.85f;

    [Tooltip("Vertical offset from ground hit to foot bone pivot (sole thickness).")]
    public float footBottomOffset = 0.11f;

    [Header("── Pelvis / Hips Adjustment ──")]
    [Tooltip("Multiplier for how much hips drop when one foot is on lower ground.")]
    [Range(0f, 1.2f)]
    public float pelvisDropMultiplier = 0.85f;

    [Tooltip("Maximum distance the pelvis is allowed to drop downwards.")]
    public float maxPelvisDrop = 0.45f;

    [Tooltip("Speed of pelvis height adjustment.")]
    public float pelvisSpeed = 16f;

    [Header("── Weight Smoothing ──")]
    [Tooltip("Smoothing speed for foot position/rotation weights.")]
    public float ikWeightSpeed = 18f;

    [Header("── LookAt Head Tracking ──")]
    [Tooltip("Transform of the target to look at (usually player's head).")]
    public Transform lookTarget;

    [Tooltip("Maximum horizontal angle to look toward target.")]
    [Range(30f, 130f)]
    public float maxLookAngle = 90f;

    [Range(0f, 1f)] public float bodyWeight = 0.25f;
    [Range(0f, 1f)] public float headWeight = 0.85f;
    [Range(0f, 1f)] public float eyesWeight = 1.00f;
    [Range(0f, 1f)] public float clampWeight = 0.50f;

    // Internal references & state
    private Animator _anim;
    private ZombieAI _zombieAI;
    private float _currentLeftWeight = 0f;
    private float _currentRightWeight = 0f;
    private float _currentPelvisOffset = 0f;
    private float _currentLookWeight = 0f;

    private Vector3 _leftFootPosition;
    private Vector3 _rightFootPosition;
    private Quaternion _leftFootRotation = Quaternion.identity;
    private Quaternion _rightFootRotation = Quaternion.identity;

    private float _lastPelvisY = 0f;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _zombieAI = GetComponent<ZombieAI>() ?? GetComponentInParent<ZombieAI>();

        // Default ground mask: ignore Player and Ignore Raycast
        if (groundLayer == ~0 || groundLayer == 0)
        {
            groundLayer = ~LayerMask.GetMask("Player", "Ignore Raycast");
        }
    }

    private void Start()
    {
        if (lookTarget == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) lookTarget = pObj.transform;
        }
    }

    /// <summary>
    /// Smoothly enables or disables IK based on AI state (crawl, slide, dead).
    /// </summary>
    public void SetIKActive(bool active)
    {
        enableFootIK = active;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_anim == null) return;

        // Check if zombie is dead or sliding/crawling on belly
        bool isSuppressed = (_zombieAI != null && (_zombieAI.IsDead || _zombieAI.IsSliding));

        // 1. Foot IK Processing
        if (enableFootIK && !isSuppressed)
        {
            ProcessFootIK(AvatarIKGoal.LeftFoot, ref _currentLeftWeight, ref _leftFootPosition, ref _leftFootRotation, out float leftOffset);
            ProcessFootIK(AvatarIKGoal.RightFoot, ref _currentRightWeight, ref _rightFootPosition, ref _rightFootRotation, out float rightOffset);

            // Adjust Pelvis (Hips) Height based on the lower foot
            AdjustPelvisHeight(leftOffset, rightOffset);
        }
        else
        {
            // Smoothly fade out weights
            _currentLeftWeight = Mathf.MoveTowards(_currentLeftWeight, 0f, Time.deltaTime * ikWeightSpeed);
            _currentRightWeight = Mathf.MoveTowards(_currentRightWeight, 0f, Time.deltaTime * ikWeightSpeed);
            _currentPelvisOffset = Mathf.MoveTowards(_currentPelvisOffset, 0f, Time.deltaTime * pelvisSpeed);

            _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, _currentLeftWeight);
            _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, _currentLeftWeight);
            _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, _currentRightWeight);
            _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, _currentRightWeight);
        }

        // 2. LookAt Processing
        if (enableLookAt && !isSuppressed && lookTarget != null)
        {
            ProcessLookAt();
        }
        else
        {
            _currentLookWeight = Mathf.MoveTowards(_currentLookWeight, 0f, Time.deltaTime * 6f);
            if (_currentLookWeight > 0.001f)
            {
                _anim.SetLookAtWeight(_currentLookWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
            }
        }
    }

    private void ProcessFootIK(AvatarIKGoal foot, ref float currentWeight, ref Vector3 targetPos, ref Quaternion targetRot, out float groundOffset)
    {
        Vector3 footAnimPos = _anim.GetIKPosition(foot);
        Quaternion footAnimRot = _anim.GetIKRotation(foot);

        // Raycast origin elevated above the foot
        Vector3 rayOrigin = footAnimPos + Vector3.up * raycastUpDistance;
        float rayLength = raycastUpDistance + raycastDownDistance;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, groundLayer, QueryTriggerInteraction.Ignore))
        {
            currentWeight = Mathf.MoveTowards(currentWeight, 1f, Time.deltaTime * ikWeightSpeed);

            // Target position: hit point + sole thickness offset
            targetPos = hit.point + Vector3.up * footBottomOffset;

            // Surface normal alignment (tilt foot to match terrain slope)
            Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
            targetRot = surfaceRot * footAnimRot;

            groundOffset = hit.point.y - transform.position.y;
        }
        else
        {
            currentWeight = Mathf.MoveTowards(currentWeight, 0f, Time.deltaTime * ikWeightSpeed);
            targetPos = footAnimPos;
            targetRot = footAnimRot;
            groundOffset = 0f;
        }

        _anim.SetIKPositionWeight(foot, currentWeight);
        _anim.SetIKRotationWeight(foot, currentWeight);
        _anim.SetIKPosition(foot, targetPos);
        _anim.SetIKRotation(foot, targetRot);
    }

    private void AdjustPelvisHeight(float leftOffset, float rightOffset)
    {
        // Find lower foot offset relative to character root
        float lowerOffset = Mathf.Min(leftOffset, rightOffset);
        float targetPelvisDrop = 0f;

        if (lowerOffset < 0f)
        {
            targetPelvisDrop = Mathf.Clamp(lowerOffset * pelvisDropMultiplier, -maxPelvisDrop, 0f);
        }

        _currentPelvisOffset = Mathf.Lerp(_currentPelvisOffset, targetPelvisDrop, Time.deltaTime * pelvisSpeed);

        if (Mathf.Abs(_currentPelvisOffset) > 0.001f)
        {
            Vector3 bodyPos = _anim.bodyPosition;
            bodyPos.y += _currentPelvisOffset;
            _anim.bodyPosition = bodyPos;
        }
    }

    private void ProcessLookAt()
    {
        Vector3 lookPoint = lookTarget.position + Vector3.up * 1.4f;
        Vector3 dirToTarget = (lookPoint - transform.position).normalized;

        float angle = Vector3.Angle(transform.forward, dirToTarget);

        // Target look weight fades out if player is behind zombie
        float targetWeight = angle <= maxLookAngle ? 1f : 0f;
        _currentLookWeight = Mathf.MoveTowards(_currentLookWeight, targetWeight, Time.deltaTime * 4.5f);

        if (_currentLookWeight > 0.01f)
        {
            _anim.SetLookAtWeight(_currentLookWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
            _anim.SetLookAtPosition(lookPoint);
        }
    }
}
