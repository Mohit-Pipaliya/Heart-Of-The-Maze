using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerController
/// ================
/// Handles player locomotion, weapon equip/unequip, attacks, hit reactions and death.
///
/// ── ANIMATOR PARAMETERS REQUIRED ──────────────────────────────────────────────
///  Float   : Speed          (0 = Idle, 0.5 = Walk, 1 = Run)
///  Int     : WeaponState    (0 = Unarmed, 1 = Sword, 2 = Gun)
///  Int     : AttackIndex    (0 or 1 – random sword attack picker)
///  Trigger : Jump
///  Trigger : Attack
///  Trigger : Equip
///  Trigger : Unequip
///  Trigger : Hit
///  Trigger : Die
///
/// ── BLEND TREE LAYOUT (Locomotion layer) ──────────────────────────────────────
///  Each WeaponState should have its own blend tree driven by Speed:
///    Speed 0.0  → Idle   animations  (3 clip blend or random state machine)
///    Speed 0.5  → Walk   animations
///    Speed 1.0  → Run    animations
///  Jump is a separate state triggered by the Jump trigger.
///
/// ── INPUT MAP ─────────────────────────────────────────────────────────────────
///  WASD / Arrow Keys : Move
///  Left Shift        : Run
///  Space             : Jump
///  Left Mouse Button : Attack (Sword = random, Gun = fire)
///  1                 : Unequip current weapon
///  2                 : Equip Sword
///  3                 : Equip Gun
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    // ─── Inspector Fields ──────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float walkSpeed      = 4.8f;   // Natural human walk (~4.8 m/s)
    [SerializeField] private float runSpeed       = 9.5f;   // Natural human run (~9.5 m/s)
    [SerializeField] private float rotationSpeed  = 720f;   // Degrees/sec toward move (or aim) direction
    [SerializeField] private float mouseSensitivity = 2.0f; // Fallback if no CameraController
    [SerializeField] private float acceleration   = 60f;    // Snappy responsive start
    [SerializeField] private float deceleration   = 120f;   // Snappy stop
    [SerializeField] private float airControl     = 0.4f;
    [SerializeField] private float moveDeadZone   = 0.05f;

    [Header("Camera")]
    [Tooltip("If empty, Camera.main CameraController is used. Mouse look lives on the camera; WASD is camera-relative.")]
    [SerializeField] private CameraController cameraRig;

    [Header("Gun Movement")]
    [SerializeField] private float gunWalkSpeed   = 4.2f;
    [SerializeField] private float gunRunSpeed    = 8.5f;
    
    [SerializeField] private float jumpForce      = 1.8f;
    [SerializeField] private float gravity        = -55f;   // Strong realistic gravity (was -22 — caused floating)
    [SerializeField] private float fallMultiplier = 2.2f;
    [SerializeField] private float speedDampTime  = 0.04f;  // Was 0.1 — animator blend faster, less glide

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;          // LayerMask to define what is "Ground"
    [SerializeField] private float groundCheckRadius = 0.25f; // Radius of the ground check sphere
    [SerializeField] private float groundCheckOffset = 0.1f;  // Offset from the bottom of the CharacterController

    [Header("Foot IK (Pair zameen pe lagana)")]
    [Tooltip("Foot IK enable karo — pair zameen ki surface pe plant hote hain (IMPORTANT: Animator ke har layer pe 'IK Pass' checkbox ON karna padega)")]
    [SerializeField] private bool useFootIK = true;
    [Tooltip("Pair ka IK kitna strong ho (0 = off, 1 = full). Agar pair slide lage to thoda kam karo.")]
    [Range(0f, 1f)]
    [SerializeField] private float footIKWeight = 1.0f;
    [Tooltip("Pair ke upar se neeche raycast ki maximum distance")]
    [SerializeField] private float footRaycastDist = 1.2f;
    [Tooltip("Pair zameen se kitna upar rahe (micro lift to prevent clipping)")]
    [SerializeField] private float footGroundOffset = 0.08f;
    [Tooltip("IK ka weight kitni smoothly blend ho (0 = instant, 1 = slow)")]
    [SerializeField] private float footIKBlendSpeed = 10f;

    // Camera system ab alag hai (CameraController.cs), isliye yahan camera ka reference zaroori nahi.
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Weapon Systems")]
    [SerializeField] private GameObject swordObject;        // Sirf 1 actual sword mesh yahan daal do
    
    [Header("Sword Offsets (Agar hath/pith par ajeeb lage to yahan se theek karo)")]
    [SerializeField] private Vector3 swordHandScale = Vector3.one; 
    [SerializeField] private Vector3 swordHandOffsetPos = Vector3.zero; 
    [SerializeField] private Vector3 swordHandOffsetRot = Vector3.zero; 
    
    [SerializeField] private Vector3 swordBackScale = Vector3.one; 
    [SerializeField] private Vector3 swordBackOffsetPos = Vector3.zero; 
    [SerializeField] private Vector3 swordBackOffsetRot = Vector3.zero; 

    [Header("Sword Combat & Damage")]
    [Tooltip("Melee damage dealt per sword strike (Default: 180f for visceral combat against 1000 HP enemies).")]
    [SerializeField] private float swordDamage = 180f; 

    // Ye dono script apne aap player ki haddiyon (bones) me dhundh legi, tumhe banane ki zarurat nahi!
    private Transform swordHandTransform;  
    private Transform swordBackTransform;  
    
    [Header("Weapon Timings")]
    [SerializeField] private GameObject gunObject;          // Gun mesh
    [SerializeField] private float equipGrabDelay = 0.4f;
    [SerializeField] private float unequipPutDelay = 0.4f;
    [SerializeField] private float gunFireRate = 0.2f;
    [SerializeField] private float unequipAnimDuration = 1.2f;
    [SerializeField] private float equipAnimDuration   = 1.2f;

    [Header("Terrain Sinking Fix")]
    [Tooltip("Unequip animation ke dauran character ko upar uthana (metres). " +
             "Agar animation terrain mein jaati hai to yahan value badhao (e.g. 0.3).")]
    [SerializeField] private float unequipGroundLift    = 0.3f; // Sword unequip lift
    [SerializeField] private float gunUnequipGroundLift = 0.6f; // Gun unequip lift (gun stance is lower, needs more)
    [Tooltip("Pickup/unequip lock: minimum gap between feet/hips and ground (metres).")]
    [SerializeField] private float animationGroundClearance = 0.1f;

    [Header("Animation State Names")]
    [SerializeField] private string unequipStateName = "Unequip";
    [SerializeField] private string equipStateName   = "Equip";
    [Tooltip("Animator mein Gun Fire state ka exact naam. (Example: 'Firing Rifle' ya 'Gun Shoot')")]
    [SerializeField] private string gunFireStateName = "Firing Rifle";

    // ─── Private State ─────────────────────────────────────────────────────────

    // Animator
    private Animator          _anim;
    private CharacterController _cc;
    private SwordEquipSystem _swordSystem;
    private GunEquipSystem _gunSystem;
    private TorchInteractionSystem _torchSystem;

    // Animator parameter hashes (faster than string lookups every frame)
    private static readonly int HashSpeed       = Animator.StringToHash("Speed");
    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int HashWeaponState = Animator.StringToHash("WeaponState");
    private static readonly int HashAttackIndex = Animator.StringToHash("AttackIndex");
    private static readonly int HashJump        = Animator.StringToHash("Jump");
    private static readonly int HashAttack      = Animator.StringToHash("Attack");
    private static readonly int HashEquip       = Animator.StringToHash("Equip");
    private static readonly int HashUnequip     = Animator.StringToHash("Unequip");
    private static readonly int HashUnequipTorch = Animator.StringToHash("UnequipTorch");
    private static readonly int HashHit         = Animator.StringToHash("Hit");
    private static readonly int HashDie         = Animator.StringToHash("Die");
    private static readonly int HashFire        = Animator.StringToHash("Fire");

    // Weapon states
    private enum WeaponState { Unarmed = 0, Sword = 1, Gun = 2, Torch = 3 }
    private WeaponState _currentWeapon = WeaponState.Unarmed;

    // Flags
    private bool _isDead          = false;
    private bool _isEquipping     = false;   // Busy playing equip/unequip animation
    private bool _isSequenceRunning = false; // Busy running a full unequip->equip sequence
    private int  _health;

    private float _nextFireTime = 0f;
    private float _lastFireTime = -10f; // Track when the last shot was fired

    // Physics
    private Vector3 _velocity;
    private bool _isGrounded;
    public bool isFrozen = false;

    /// <summary>While true, player root Y is pinned after Animator runs (stops pickup clips pulling body into terrain).</summary>
    private bool _lockWorldPosition;
    private Vector3 _lockedWorldPosition;
    
    // Jump Buffer & Cooldown
    private float _jumpBufferCounter;
    private float jumpBufferTime = 0.2f;
    private float _jumpCooldownTimer;
    private float jumpCooldown = 0.15f; // Brief delay before we can be grounded again

    private bool _wasGrounded = true;
    private float _nextFootstepTime;
    private float _swordReadyTime;
    private int _swordCombo;
    private float _lastAirYVelocity;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _anim   = GetComponent<Animator>();
        _cc     = GetComponent<CharacterController>();
        _health = maxHealth;
        _swordSystem = GetComponentInChildren<SwordEquipSystem>();
        _gunSystem = GetComponentInChildren<GunEquipSystem>();

        // Disable NavMeshAgent during normal gameplay so it does not fight CharacterController,
        // cause sliding/floating, or block the player at NavMesh borders when walking to enemies.
        // (MeetingSeatSystem will selectively enable it only during its seated meeting cutscene).
        UnityEngine.AI.NavMeshAgent nma = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nma != null)
        {
            nma.enabled = false;
        }

        // NAYA JADOO: Tumhe empty object banane ki zarurat nahi! 
        // Unity automatically player ka sidha hath (Right Hand) aur Pith (Spine) dhundh lega.
        if (_anim.isHuman)
        {
            swordHandTransform = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            swordBackTransform = _anim.GetBoneTransform(HumanBodyBones.Spine);
            
            // Agar Spine nahi milti to Chest ya Hips use karenge
            if (swordBackTransform == null) swordBackTransform = _anim.GetBoneTransform(HumanBodyBones.Chest);
        }

        // Sword hijacking logic removed to allow SwordEquipSystem to handle it
        /*
        // NAYA JADOO: Agar tumne Inspector me Sword Object nahi dala, to script khud player ke andar se dhoondh legi!
        if (swordObject == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                // Agar kisi object ke naam me "sword" likha hai, to wo player ki sword hai
                if (t.name.ToLower().Contains("sword"))
                {
                    swordObject = t.gameObject;
                    break;
                }
            }
        }

        if (swordObject)
        {
            swordObject.SetActive(true);
            MoveSwordTo(swordBackTransform, swordBackScale, swordBackOffsetPos, swordBackOffsetRot);
        }
        */
        if (gunObject) gunObject.SetActive(false);
        _anim.SetInteger(HashWeaponState, (int)WeaponState.Unarmed);

        if (cameraRig == null && Camera.main != null)
            cameraRig = Camera.main.GetComponent<CameraController>();

        // Ensure SwordAttackEffect is present for AAA sword VFX & sound
        if (SwordAttackEffect.Instance == null)
        {
            SwordAttackEffect sae = GetComponentInChildren<SwordAttackEffect>(true);
            if (sae == null)
            {
                GameObject sObj = swordObject;
                if (sObj == null && _swordSystem != null && _swordSystem.sword != null)
                    sObj = _swordSystem.sword;
                if (sObj == null) sObj = gameObject;
                sObj.AddComponent<SwordAttackEffect>();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_isDead) return;

        if (isFrozen)
        {
            _anim.SetBool(HashIsGrounded, _isGrounded);
            return;
        }

        // Step 1: Check if player is grounded using improved logic
        CalculateGroundCheck();

        // Step 2: Apply Gravity
        CalculateGravity();

        // Step 3: Handle Jump input (overrides Y velocity if jumping)
        HandleJump();

        // Step 4: Handle Horizontal Movement (sets X and Z velocity)
        CalculateHorizontalMovement();

        // Step 5: APPLY MOVEMENT ONCE (Combines X, Y, and Z)
        _cc.Move(_velocity * Time.deltaTime);

        // Step 6: Update Animator
        _anim.SetBool(HashIsGrounded, _isGrounded);

        HandleLocomotionFeel();
        HandleWeaponSwitch();
        HandleAttack();
    }

    private void OnAnimatorMove()
    {
        if (_isDead || _cc == null || !_cc.enabled) return;

        // If root motion is enabled (e.g. during pickup or unequip), apply X/Z only
        if (_anim.applyRootMotion)
        {
            Vector3 rootMotionDelta = _anim.deltaPosition;
            rootMotionDelta.y = 0f; // STRICTLY discard Y-axis root motion so animation cannot lift player

            // Apply rotation
            transform.rotation *= _anim.deltaRotation;

            _cc.Move(rootMotionDelta);
        }
    }

    // ─── Foot IK ──────────────────────────────────────────────────────────────
    // Pair zameen pe plant karta hai — hawa mein tairna band karta hai.
    // ZARURI: Animator window mein har layer ka 'IK Pass' checkbox ON karo!

    private float _leftFootIKWeight  = 0f;
    private float _rightFootIKWeight = 0f;

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useFootIK || _anim == null || !_anim.isHuman) return;
        if (_isDead)
        {
            _leftFootIKWeight  = 0f;
            _rightFootIKWeight = 0f;
            ApplyFootIKWeights();
            return;
        }
        if (isFrozen) return;
        // Jump ke dauraan IK off
        if (!_isGrounded) 
        {
            _leftFootIKWeight  = Mathf.MoveTowards(_leftFootIKWeight,  0f, Time.deltaTime * footIKBlendSpeed);
            _rightFootIKWeight = Mathf.MoveTowards(_rightFootIKWeight, 0f, Time.deltaTime * footIKBlendSpeed);
            ApplyFootIKWeights();
            return;
        }

        _leftFootIKWeight  = Mathf.MoveTowards(_leftFootIKWeight,  footIKWeight, Time.deltaTime * footIKBlendSpeed);
        _rightFootIKWeight = Mathf.MoveTowards(_rightFootIKWeight, footIKWeight, Time.deltaTime * footIKBlendSpeed);

        SetFootIK(AvatarIKGoal.LeftFoot,  _leftFootIKWeight);
        SetFootIK(AvatarIKGoal.RightFoot, _rightFootIKWeight);
    }

    private void ApplyFootIKWeights()
    {
        _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot,  _leftFootIKWeight);
        _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot,  _leftFootIKWeight);
        _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, _rightFootIKWeight);
        _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, _rightFootIKWeight);
    }

    private void SetFootIK(AvatarIKGoal foot, float weight)
    {
        // Animator se current animated foot position lo
        Vector3 animFootPos = _anim.GetIKPosition(foot);

        // Pair ke upar se neeche raycast maro
        Vector3 rayOrigin = animFootPos + Vector3.up * 0.5f;
        LayerMask mask = groundMask.value != 0 ? groundMask : Physics.DefaultRaycastLayers;

        _anim.SetIKPositionWeight(foot, weight);
        _anim.SetIKRotationWeight(foot, weight);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, footRaycastDist, mask, QueryTriggerInteraction.Ignore))
        {
            // Pair ko zameen ki surface pe plant karo
            _anim.SetIKPosition(foot, hit.point + Vector3.up * footGroundOffset);

            // Pair ko zameen ki slope ke saath align karo
            Quaternion footRot = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, hit.normal),
                hit.normal);
            _anim.SetIKRotation(foot, footRot);
        }
        // Raycast miss hua to animated position hi use hogi (weight already set hai)
    }

    private void LateUpdate()
    {
        if (_isDead) return;

        if (isFrozen)
        {
            _velocity = Vector3.zero; // Prevent physics drifting while frozen
        }

        if (_lockWorldPosition && _cc != null && _cc.enabled)
        {
            CompensateAnimatedBodyBelowGround();
            ClampLockedPositionAboveGround();
            Vector3 delta = _lockedWorldPosition - transform.position;
            if (delta.sqrMagnitude > 0.000001f)
                _cc.Move(delta);
        }
    }

    private bool TrySampleGroundY(Vector3 referencePosition, out float groundY)
    {
        Vector3 origin = referencePosition + Vector3.up * 4f;
        const float maxDistance = 15f;

        if (groundMask.value != 0 &&
            Physics.Raycast(origin, Vector3.down, out RaycastHit maskedHit, maxDistance, groundMask,
                QueryTriggerInteraction.Ignore))
        {
            groundY = maskedHit.point.y;
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, ~0,
            QueryTriggerInteraction.Ignore);

        float bestY = float.NegativeInfinity;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            if (hits[i].point.y > bestY)
            {
                bestY = hits[i].point.y;
                found = true;
            }
        }

        groundY = bestY;
        return found;
    }

    /// <summary>
    /// Pickup clips move bones down while the capsule stays — lift the whole player when feet/hips dip below ground.
    /// </summary>
    private void CompensateAnimatedBodyBelowGround()
    {
        if (!TrySampleGroundY(transform.position, out float groundY)) return;

        float lowestWorldY = transform.position.y;

        if (_anim.isHuman)
        {
            Transform leftFoot = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = _anim.GetBoneTransform(HumanBodyBones.RightFoot);

            if (leftFoot != null) lowestWorldY = Mathf.Min(lowestWorldY, leftFoot.position.y);
            if (rightFoot != null) lowestWorldY = Mathf.Min(lowestWorldY, rightFoot.position.y);
            // Hips removed from check because pickup animations lower the hips, causing the player to fly up
        }
        else
        {
            lowestWorldY = _cc.bounds.min.y;
        }

        float minAllowedY = groundY + animationGroundClearance;
        if (lowestWorldY < minAllowedY)
            _lockedWorldPosition.y += minAllowedY - lowestWorldY;
    }

    private void ClampLockedPositionAboveGround()
    {
        if (!TrySampleGroundY(transform.position, out float groundY)) return;

        float capsuleBottomOffset = _cc.center.y - _cc.height * 0.5f;
        float minRootY = groundY - capsuleBottomOffset + animationGroundClearance;
        if (_lockedWorldPosition.y < minRootY)
            _lockedWorldPosition.y = minRootY;
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    private void CalculateGroundCheck()
    {
        // If we recently jumped, force player to be un-grounded for a short time
        if (_jumpCooldownTimer > 0f)
        {
            _jumpCooldownTimer -= Time.deltaTime;
            _isGrounded = false;
            return;
        }

        // Default check using CharacterController
        _isGrounded = _cc.isGrounded;
        
        // 100% Pivot-Independent check: SphereCast/CheckSphere
        if (!_isGrounded)
        {
            Vector3 spherePosition = _cc.bounds.center;
            spherePosition.y -= _cc.bounds.extents.y;     // Go to the base
            spherePosition.y += groundCheckRadius - groundCheckOffset; // Offset it slightly up

            // Include Default layer (1 << 0) so checkered plane, terrain, etc. are always ground
            int effectiveMask = (groundMask.value != 0) ? (groundMask.value | (1 << 0)) : ~LayerMask.GetMask("Player", "Ignore Raycast");
            _isGrounded = Physics.CheckSphere(spherePosition, groundCheckRadius, effectiveMask);
        }
    }

    private void CalculateGravity()
    {
        if (_isGrounded && _velocity.y < 0f)
        {
            // Strong downward snap keeps player glued to ground slopes — NO FLOATING
            _velocity.y = -15f; 
        }

        // Apply stronger gravity when falling for a snappy, realistic feel (no moon float)
        if (_velocity.y < 0 && !_isGrounded)
        {
            _velocity.y += gravity * fallMultiplier * Time.deltaTime;
        }
        else if (_velocity.y > 0 && !Input.GetKey(KeyCode.Space))
        {
            _velocity.y += gravity * (fallMultiplier / 1.5f) * Time.deltaTime;
        }
        else
        {
            _velocity.y += gravity * Time.deltaTime;
        }
    }

    private void CalculateHorizontalMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 moveInput = new Vector2(h, v);
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        bool hasRawKey = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || 
                         Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                         Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.LeftArrow) ||
                         Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.RightArrow);

        bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool aiming = IsPlayerAiming();
        bool hasMove = (hasRawKey || moveInput.sqrMagnitude > 0.01f) && moveInput.sqrMagnitude > moveDeadZone * moveDeadZone;

        Vector3 planarForward;
        Vector3 planarRight;
        GetCameraPlanarAxes(out planarForward, out planarRight);

        Vector3 desiredDir = Vector3.zero;
        if (hasMove)
            desiredDir = (planarForward * moveInput.y + planarRight * moveInput.x).normalized;

        float currentWalkSpeed = (_currentWeapon == WeaponState.Gun) ? gunWalkSpeed : walkSpeed;
        float currentRunSpeed = (_currentWeapon == WeaponState.Gun) ? gunRunSpeed : runSpeed;
        float maxSpeed = running ? currentRunSpeed : currentWalkSpeed;
        if (aiming)
            maxSpeed = currentWalkSpeed;

        float accel = _isGrounded ? acceleration : acceleration * airControl;
        if (!hasMove)
        {
            if (_isGrounded)
            {
                _velocity.x = 0f;
                _velocity.z = 0f;
            }
            else
            {
                Vector3 horizAir = new Vector3(_velocity.x, 0f, _velocity.z);
                horizAir = Vector3.MoveTowards(horizAir, Vector3.zero, deceleration * 0.15f * Time.deltaTime);
                _velocity.x = horizAir.x;
                _velocity.z = horizAir.z;
            }
            _anim.SetFloat(HashSpeed, 0f);
        }
        else
        {
            Vector3 targetHoriz = desiredDir * maxSpeed;
            Vector3 horiz = new Vector3(_velocity.x, 0f, _velocity.z);
            horiz = Vector3.MoveTowards(horiz, targetHoriz, accel * Time.deltaTime);
            _velocity.x = horiz.x;
            _velocity.z = horiz.z;
        }

        Vector3 faceDir = Vector3.zero;
        if (aiming)
            faceDir = planarForward;
        else if (hasMove)
            faceDir = desiredDir;

        if (faceDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(faceDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // Drive the 1D blend tree: immediate snap to 0 on release, smooth blend on move
        if (!hasMove)
        {
            _anim.SetFloat(HashSpeed, 0f);
        }
        else
        {
            float targetAnimSpeed = (running && !aiming) ? 1.0f : 0.5f;
            float currentAnimSpeed = _anim.GetFloat(HashSpeed);
            float smoothSpeed = Mathf.MoveTowards(currentAnimSpeed, targetAnimSpeed, Time.deltaTime / Mathf.Max(0.01f, speedDampTime));
            _anim.SetFloat(HashSpeed, smoothSpeed);
        }
    }

    private void GetCameraPlanarAxes(out Vector3 forward, out Vector3 right)
    {
        if (cameraRig != null)
        {
            forward = cameraRig.PlanarForward;
            right = cameraRig.PlanarRight;
            return;
        }

        if (Camera.main != null)
        {
            forward = Camera.main.transform.forward;
            right = Camera.main.transform.right;
        }
        else
        {
            forward = transform.forward;
            right = transform.right;
        }

        forward.y = 0f;
        right.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        if (right.sqrMagnitude < 0.0001f)
            right = transform.right;
        forward.Normalize();
        right.Normalize();
    }

    private bool IsPlayerAiming()
    {
        if (_currentWeapon != WeaponState.Gun) return false;
        return Input.GetMouseButton(1) || Input.GetMouseButton(0);
    }

    // ─── Jump ─────────────────────────────────────────────────────────────────

    private void HandleJump()
    {
        // Jump Buffer Logic: Hawa me space dabane par bhi yaad rakhega
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            _jumpBufferCounter -= Time.deltaTime;
        }

        // Jaise hi player zameen chuega, turant jump kar dega
        if (_jumpBufferCounter > 0f && _isGrounded)
        {
            _jumpBufferCounter = 0f;  // Buffer clear
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            _anim.ResetTrigger(HashJump);
            _anim.SetTrigger(HashJump);
            GameAudio.PlayJump(transform.position);
            if (cameraRig != null) cameraRig.AddRecoil(0.25f, 0f, 0.02f);
            
            // Start the jump cooldown so the ground check sphere doesn't immediately 
            // touch the ground in the next few frames while we're starting to rise up.
            _jumpCooldownTimer = jumpCooldown;
            _isGrounded = false;
        }
    }

    // ─── Weapon Switching ─────────────────────────────────────────────────────

    private void HandleWeaponSwitch()
    {
        if (_isEquipping || _isSequenceRunning) return;

        // Press 1: Jo bhi weapon hath mein hai, uska unequip animation chalao → Unarmed
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            if (_currentWeapon != WeaponState.Unarmed)
                StartCoroutine(DoUnequip());
        }

        // Press 2: Sword equip karo
        // - Agar hath khali (Unarmed): Seedha Sword equip animation
        // - Agar Gun hath mein hai: Pehle Gun unequip → phir Sword equip
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            if (_currentWeapon == WeaponState.Sword) return; // Pehle se sword hai
            bool swordAvailable = _swordSystem != null &&
                                  (_swordSystem.CurrentState == SwordEquipSystem.SwordState.OnBack ||
                                   _swordSystem.CurrentState == SwordEquipSystem.SwordState.Equipped);
            if (!swordAvailable) return;
            StartCoroutine(DoSwitchToSword());
        }

        // Press 3: Gun equip karo
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            if (_currentWeapon == WeaponState.Gun) return; // Pehle se gun hai
            bool gunAvailable = _gunSystem != null &&
                                (_gunSystem.CurrentState == GunEquipSystem.GunState.Holstered ||
                                 _gunSystem.CurrentState == GunEquipSystem.GunState.Equipped);
            if (!gunAvailable) return;
            StartCoroutine(DoSwitchToGun());
        }

        // Press 4: Torch equip karo
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            if (_currentWeapon == WeaponState.Torch) return; // Pehle se torch hai
            bool torchAvailable = _torchSystem != null && _torchSystem.currentState == TorchInteractionSystem.TorchState.Placed;
            if (!torchAvailable) return;
            StartCoroutine(DoSwitchToTorch());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Press 1: Jo bhi weapon hath mein hai → Unequip → Unarmed
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator DoUnequip()
    {
        _isSequenceRunning = true;

        if (_currentWeapon == WeaponState.Sword && _swordSystem != null)
        {
            yield return RunUnequipAnimation();
            _swordSystem.StartUnequip();
            yield return new WaitForSeconds(unequipAnimDuration);
            while (_swordSystem.CurrentState != SwordEquipSystem.SwordState.OnBack) yield return null;
        }
        else if (_currentWeapon == WeaponState.Gun && _gunSystem != null)
        {
            yield return RunGunUnequipAnimation(); // Gun ke liye special fix
            _gunSystem.StartUnequip();
            yield return new WaitForSeconds(unequipAnimDuration);
            while (_gunSystem.CurrentState != GunEquipSystem.GunState.Holstered) yield return null;
        }
        else if (_currentWeapon == WeaponState.Torch && _torchSystem != null)
        {
            yield return RunTorchUnequipAnimation();    // UnequipTorch trigger fire karo
            _torchSystem.StartUnequip();                // VFX off + torch holster
            // Torch unequip animation khatam hone ka wait karo
            while (_torchSystem.currentState != TorchInteractionSystem.TorchState.Placed) yield return null;
            // Animation done — WeaponState=0 reset karo
            _anim.SetInteger(HashWeaponState, 0);
            _anim.applyRootMotion = false;
            yield return null;
        }

        _currentWeapon = WeaponState.Unarmed;
        _anim.SetInteger(HashWeaponState, 0);
        _anim.applyRootMotion = false; // Restore
        _isSequenceRunning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Press 2: Unarmed→Sword  OR  Gun→(Unequip Gun)→(Equip Sword)
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator DoSwitchToSword()
    {
        _isSequenceRunning = true;

        // Step 1: Gun unequip (agar gun hath mein hai)
        if (_currentWeapon == WeaponState.Gun && _gunSystem != null)
        {
            yield return RunGunUnequipAnimation(); // Gun ke liye special fix
            _gunSystem.StartUnequip();
            yield return new WaitForSeconds(unequipAnimDuration);
            while (_gunSystem.CurrentState != GunEquipSystem.GunState.Holstered) yield return null;
        }
        else if (_currentWeapon == WeaponState.Torch && _torchSystem != null)
        {
            yield return RunTorchUnequipAnimation();    // UnequipTorch trigger fire karo
            _torchSystem.StartUnequip();                // VFX off + torch holster
            // Torch unequip animation khatam hone ka wait karo
            while (_torchSystem.currentState != TorchInteractionSystem.TorchState.Placed) yield return null;
            // Unarmed state mein wapas aao before sword equip
            _anim.SetInteger(HashWeaponState, 0);
            _anim.applyRootMotion = false;
            yield return null;
        }

        // Step 2: Sword Equip (directly 2→1)
        if (_swordSystem != null && _swordSystem.CurrentState == SwordEquipSystem.SwordState.OnBack)
        {
            _currentWeapon = WeaponState.Sword;
            _anim.SetInteger(HashWeaponState, 1);
            _anim.applyRootMotion = false; // Restore before equip
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return FireTriggerInstant(HashEquip);
            GameAudio.PlayEquip(transform.position);
            _swordSystem.StartEquip();
            yield return new WaitForSeconds(equipAnimDuration);
            while (_swordSystem.CurrentState != SwordEquipSystem.SwordState.Equipped) yield return null;
        }

        _isSequenceRunning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Press 3: Unarmed→Gun  OR  Sword→(Unequip Sword)→(Equip Gun)
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator DoSwitchToGun()
    {
        _isSequenceRunning = true;

        // Step 1: Sword unequip (agar sword hath mein hai)
        if (_currentWeapon == WeaponState.Sword && _swordSystem != null)
        {
            yield return RunUnequipAnimation();
            _swordSystem.StartUnequip();
            yield return new WaitForSeconds(unequipAnimDuration);
            while (_swordSystem.CurrentState != SwordEquipSystem.SwordState.OnBack) yield return null;
        }
        else if (_currentWeapon == WeaponState.Torch && _torchSystem != null)
        {
            yield return RunTorchUnequipAnimation();    // UnequipTorch trigger fire karo
            _torchSystem.StartUnequip();                // VFX off + torch holster
            // Torch unequip animation khatam hone ka wait karo
            while (_torchSystem.currentState != TorchInteractionSystem.TorchState.Placed) yield return null;
            // Unarmed state mein wapas aao before gun equip
            _anim.SetInteger(HashWeaponState, 0);
            _anim.applyRootMotion = false;
            yield return null;
        }

        // Step 2: Gun Equip (directly 1→2)
        if (_gunSystem != null && _gunSystem.CurrentState == GunEquipSystem.GunState.Holstered)
        {
            _currentWeapon = WeaponState.Gun;
            _anim.SetInteger(HashWeaponState, 2);
            _anim.applyRootMotion = false; // Restore before equip
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return FireTriggerInstant(HashEquip);
            GameAudio.PlayEquip(transform.position);
            _gunSystem.StartEquip();
            yield return new WaitForSeconds(equipAnimDuration);
            while (_gunSystem.CurrentState != GunEquipSystem.GunState.Equipped) yield return null;
        }

        _isSequenceRunning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Press 4: Unarmed→Torch  OR  Sword/Gun→(Unequip)→(Equip Torch)
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator DoSwitchToTorch()
    {
        _isSequenceRunning = true;

        // Step 1: Pehle current weapon unequip karo (agar koi equipped hai)
        if (_currentWeapon == WeaponState.Sword && _swordSystem != null)
        {
            yield return RunUnequipAnimation();          // Sword unequip animation
            _swordSystem.StartUnequip();                 // Sword back pe bhejo
            yield return new WaitForSeconds(unequipAnimDuration);
            while (_swordSystem.CurrentState != SwordEquipSystem.SwordState.OnBack) yield return null;

            // Unequip khatam — Animator ko Unarmed pe reset karo
            _currentWeapon = WeaponState.Unarmed;
            _anim.SetInteger(HashWeaponState, 0);
            _anim.applyRootMotion = false;
            yield return null; // 1 frame: Animator settle ho
        }
        else if (_currentWeapon == WeaponState.Gun && _gunSystem != null)
        {
            yield return RunGunUnequipAnimation();       // Gun unequip animation
            _gunSystem.StartUnequip();                   // Gun holster pe bhejo
            yield return new WaitForSeconds(unequipAnimDuration);
            while (_gunSystem.CurrentState != GunEquipSystem.GunState.Holstered) yield return null;

            // Unequip khatam — Animator ko Unarmed pe reset karo
            _currentWeapon = WeaponState.Unarmed;
            _anim.SetInteger(HashWeaponState, 0);
            _anim.applyRootMotion = false;
            yield return null; // 1 frame: Animator settle ho
        }

        // Step 2: Ab mashal equip karo (unequip ke baad ya directly unarmed se)
        if (_torchSystem != null && _torchSystem.currentState == TorchInteractionSystem.TorchState.Placed)
        {
            _anim.applyRootMotion = false;
            GameAudio.PlayEquip(transform.position);
            _torchSystem.StartEquip();   // EquipTorch trigger + lighter + fire VFX
            while (_torchSystem.currentState != TorchInteractionSystem.TorchState.Equipped) yield return null;
        }

        _isSequenceRunning = false;
    }
    // ─── Attack ───────────────────────────────────────────────────────────────

    private static readonly int HashIsAiming = Animator.StringToHash("IsAiming");
    
    private void HandleAttack()
    {
        if (_isEquipping || _isSequenceRunning) return;
        if (_currentWeapon == WeaponState.Unarmed) return;

        if (_currentWeapon == WeaponState.Sword)
        {
            if (Input.GetMouseButtonDown(0) && Time.time >= _swordReadyTime)
            {
                _swordReadyTime = Time.time + 0.48f;

                // Auto-orient player towards camera aim direction so swing strikes where player is looking
                GetCameraPlanarAxes(out Vector3 camFwd, out _);
                if (camFwd.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(camFwd, Vector3.up);
                }

                _swordCombo = (_swordCombo + 1) % 2;
                _anim.SetInteger(HashAttackIndex, _swordCombo);
                _anim.ResetTrigger(HashAttack);
                _anim.SetTrigger(HashAttack);

                // Subtle windup screen tension
                if (cameraRig != null) cameraRig.AddRecoil(0.08f, Random.Range(-0.06f, 0.06f), 0.012f);
                GameFeel.FovPunch(1.1f);

                StartCoroutine(PerformSwordAttackSequence(swordDamage));
            }
        }
        else if (_currentWeapon == WeaponState.Gun)
        {
            // Aim Logic: 
            // 1. Right Click (Mouse 1) daba kar rakhne par Aim hoga.
            // 2. Left Click (Mouse 0) daba kar rakhne par bhi Aim hoga.
            // 3. Firing chhodne ke baad 0.5 sec tak aim barkarar rahega (smoothness ke liye)
            bool isRightClickAiming = Input.GetMouseButton(1);
            bool isLeftClickHeld = Input.GetMouseButton(0);
            bool isFiringCooldown = (Time.time - _lastFireTime) < 0.5f;

            bool isAiming = isRightClickAiming || isLeftClickHeld || isFiringCooldown;
            _anim.SetBool(HashIsAiming, isAiming);
        }
    }

    private IEnumerator PerformSwordAttackSequence(float damage)
    {
        // ── Phase 1: Windup Anticipation (0.08s) ───────────────────────────
        yield return new WaitForSeconds(0.08f);

        // ── Phase 2: Swing Start (Whoosh SFX & Glowing Trail Activation) ────
        GameAudio.PlaySwordSwing(transform.position);
        if (SwordAttackEffect.Instance != null)
            SwordAttackEffect.Instance.TriggerAttack();

        // ── Phase 3: Continuous Active Hit Window (0.08s to 0.36s) ─────────
        // Sweeps across frames during the full blade arc so enemies cannot slip through
        float swingDuration = 0.28f;
        float elapsed = 0f;
        HashSet<GameObject> hitEnemies = new HashSet<GameObject>();
        bool connectedAnyHit = false;

        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;

            // Sweeping volume: Capsule from player chest to forward arc reach
            // Covers point-blank 0.0m up to 3.8m distance, from ground level (0.1m) to tall height (2.3m)
            Vector3 capsuleBottom = transform.position + Vector3.up * 0.35f;
            Vector3 capsuleTop = transform.position + transform.forward * 2.4f + Vector3.up * 1.15f;
            float capsuleRadius = 1.8f;

            Collider[] hits = Physics.OverlapCapsule(capsuleBottom, capsuleTop, capsuleRadius, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                // Deep search in parent, children, and root to guarantee finding enemy script
                SpiderEnemy spider = col.GetComponentInParent<SpiderEnemy>() ?? col.GetComponentInChildren<SpiderEnemy>() ?? col.transform.root.GetComponentInChildren<SpiderEnemy>();
                ZombieAI zombie = col.GetComponentInParent<ZombieAI>() ?? col.GetComponentInChildren<ZombieAI>() ?? col.transform.root.GetComponentInChildren<ZombieAI>();

                if (spider != null && !spider.IsDead && !hitEnemies.Contains(spider.gameObject))
                {
                    hitEnemies.Add(spider.gameObject);
                    connectedAnyHit = true;

                    spider.TakeDamage(damage);
                    bool isKill = spider.IsDead;

                    Vector3 contactPoint = col.ClosestPoint(transform.position + transform.forward * 1.5f + Vector3.up * 0.8f);
                    GameAudio.PlaySwordImpact(contactPoint, true);
                    if (SwordAttackEffect.Instance != null)
                        SwordAttackEffect.Instance.TriggerImpact(contactPoint, -transform.forward, true);
                    if (GunShooter.Instance != null)
                        GunShooter.Instance.RegisterHitMarker(isKill);
                    if (cameraRig != null) cameraRig.AddRecoil(0.38f, Random.Range(-0.1f, 0.1f), 0.04f);
                    GameFeel.HitStop(0.045f);
                    GameFeel.FovPunch(2.2f);
                }
                else if (zombie != null && !zombie.IsDead && !hitEnemies.Contains(zombie.gameObject))
                {
                    hitEnemies.Add(zombie.gameObject);
                    connectedAnyHit = true;

                    zombie.TakeDamage(damage);
                    bool isKill = zombie.IsDead;

                    Vector3 contactPoint = col.ClosestPoint(transform.position + transform.forward * 1.5f + Vector3.up * 1.1f);
                    GameAudio.PlaySwordImpact(contactPoint, true);
                    if (SwordAttackEffect.Instance != null)
                        SwordAttackEffect.Instance.TriggerImpact(contactPoint, -transform.forward, true);
                    if (GunShooter.Instance != null)
                        GunShooter.Instance.RegisterHitMarker(isKill);
                    if (cameraRig != null) cameraRig.AddRecoil(0.42f, Random.Range(-0.12f, 0.12f), 0.05f);
                    GameFeel.HitStop(0.05f);
                    GameFeel.FovPunch(2.5f);
                }
            }

            yield return null;
        }

        // Secondary forward SphereCast check as an absolute guarantee for point-blank or forward lunge
        if (!connectedAnyHit)
        {
            Vector3 chestOrigin = transform.position + Vector3.up * 1.0f;
            if (Physics.SphereCast(chestOrigin, 0.75f, transform.forward, out RaycastHit directHit, 3.0f, ~0, QueryTriggerInteraction.Collide))
            {
                Collider col = directHit.collider;
                if (col != null && !col.transform.IsChildOf(transform))
                {
                    SpiderEnemy spider = col.GetComponentInParent<SpiderEnemy>() ?? col.GetComponentInChildren<SpiderEnemy>() ?? col.transform.root.GetComponentInChildren<SpiderEnemy>();
                    ZombieAI zombie = col.GetComponentInParent<ZombieAI>() ?? col.GetComponentInChildren<ZombieAI>() ?? col.transform.root.GetComponentInChildren<ZombieAI>();

                    if (spider != null && !spider.IsDead && !hitEnemies.Contains(spider.gameObject))
                    {
                        hitEnemies.Add(spider.gameObject);
                        connectedAnyHit = true;
                        spider.TakeDamage(damage);
                        GameAudio.PlaySwordImpact(directHit.point, true);
                        if (SwordAttackEffect.Instance != null)
                            SwordAttackEffect.Instance.TriggerImpact(directHit.point, directHit.normal, true);
                        if (GunShooter.Instance != null)
                            GunShooter.Instance.RegisterHitMarker(spider.IsDead);
                        GameFeel.HitStop(0.045f);
                    }
                    else if (zombie != null && !zombie.IsDead && !hitEnemies.Contains(zombie.gameObject))
                    {
                        hitEnemies.Add(zombie.gameObject);
                        connectedAnyHit = true;
                        zombie.TakeDamage(damage);
                        GameAudio.PlaySwordImpact(directHit.point, true);
                        if (SwordAttackEffect.Instance != null)
                            SwordAttackEffect.Instance.TriggerImpact(directHit.point, directHit.normal, true);
                        if (GunShooter.Instance != null)
                            GunShooter.Instance.RegisterHitMarker(zombie.IsDead);
                        GameFeel.HitStop(0.05f);
                    }
                }
            }
        }

        // ── Phase 4: Environment / World Hit Fallback (Clash against walls / terrain) ──
        if (!connectedAnyHit)
        {
            Vector3 origin = transform.position + Vector3.up * 1.0f + transform.forward * 0.4f;
            if (Physics.SphereCast(origin, 0.35f, transform.forward, out RaycastHit worldHit, 2.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!worldHit.collider.transform.IsChildOf(transform))
                {
                    GameAudio.PlaySwordImpact(worldHit.point, false);
                    if (SwordAttackEffect.Instance != null)
                        SwordAttackEffect.Instance.TriggerImpact(worldHit.point, worldHit.normal, false);
                    if (cameraRig != null) cameraRig.AddRecoil(0.18f, 0f, 0.02f);
                }
            }
        }
    }

    private void HandleLocomotionFeel()
    {
        if (_isGrounded && !_wasGrounded)
        {
            float impact = Mathf.Clamp01(-_lastAirYVelocity / 18f);
            if (impact > 0.08f)
            {
                GameAudio.PlayLand(transform.position);
                if (cameraRig != null)
                    cameraRig.AddRecoil(0.2f + impact * 0.5f, 0f, 0.015f + impact * 0.04f);
            }
        }

        if (!_isGrounded)
            _lastAirYVelocity = _velocity.y;

        if (_isGrounded && !isFrozen)
        {
            float spd = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            if (spd > 1.2f && Time.time >= _nextFootstepTime)
            {
                float interval = spd > runSpeed * 0.75f ? 0.32f : 0.46f;
                _nextFootstepTime = Time.time + interval;
                GameAudio.PlayFootstep(transform.position, spd > runSpeed * 0.75f ? 0.38f : 0.24f);
            }
        }

        _wasGrounded = _isGrounded;
    }

    /// <summary>
    /// Synchronized trigger from GunShooter when a bullet is actually fired.
    /// </summary>
    public void TriggerFireAnimation()
    {
        _lastFireTime = Time.time;
        if (_anim != null)
        {
            _anim.ResetTrigger(HashFire);
            _anim.SetTrigger(HashFire);
        }
    }

    // ─── Damage & Death ───────────────────────────────────────────────────────

    /// <summary>
    /// Call this from your enemy / damage system to deal damage to the player.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (_isDead) return;

        _health -= damage;
        _health  = Mathf.Max(_health, 0);

        if (_health <= 0)
        {
            Die();
        }
        else
        {
            _anim.ResetTrigger(HashHit);
            _anim.SetTrigger(HashHit);
            GameAudio.PlayHit(transform.position);
            if (cameraRig != null) cameraRig.AddRecoil(0.55f, Random.Range(-0.3f, 0.3f), 0.06f);
            GameFeel.FovPunch(2.5f);
        }
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        _velocity = Vector3.zero;
        GameAudio.PlayDeath(transform.position);

        if (_anim != null)
        {
            _anim.applyRootMotion = false;

            // Shooting Layer (UpperBody) disable karo taaki full-body death anim properly chale
            for (int i = 1; i < _anim.layerCount; i++)
            {
                _anim.SetLayerWeight(i, 0f);
            }

            // Foot IK zero out karo taaki dead body hawa me na tange
            _leftFootIKWeight = 0f;
            _rightFootIKWeight = 0f;
            ApplyFootIKWeights();

            // The Animator plays Die (no weapon) or Die2 (with weapon) based on WeaponState.
            // We set the WeaponState BEFORE firing the trigger so the Animator can branch.
            _anim.SetTrigger(HashDie);
        }
        
        // Zameen par snap karo taaki hawa me float na kare
        SnapToGround();

        // Disable controller so the body stops moving
        if (_cc != null)
        {
            _cc.enabled = false;
        }
    }


    private void MoveSwordTo(Transform targetParent, Vector3 targetScale, Vector3 targetPos, Vector3 targetRot)
    {
        if (swordObject != null && targetParent != null)
        {
            // Sword ko physically naye parent (Hath ya Pith) ke andar daal do
            swordObject.transform.SetParent(targetParent);
            // Aur uski position/rotation/scale Inspector ki custom values par set kar do
            swordObject.transform.localPosition = targetPos;
            swordObject.transform.localRotation = Quaternion.Euler(targetRot);
            swordObject.transform.localScale = targetScale;
        }
    }

    // ─── Helper: Unequip Animation Runner ─────────────────────────────────────

    /// <summary>
    /// Unequip animation safely chalata hai:
    /// 1. Root Motion disable karta hai (animation ka downward root motion terrain sinking ka karan)
    /// 2. Character ko unequipGroundLift units upar uthata hai (bone-level clipping fix)
    /// 3. FireTriggerInstant se instant start (exit time bypass)
    /// </summary>
    private IEnumerator RunUnequipAnimation()
    {
        _anim.applyRootMotion = false;
        if (unequipGroundLift > 0f && _cc != null && _cc.enabled)
            _cc.Move(Vector3.up * unequipGroundLift);

        _anim.SetInteger(HashWeaponState, 1);
        _anim.ResetTrigger(HashUnequip);
        _anim.CrossFadeInFixedTime("UnequipSword", 0.15f, 0, 0f);
        yield return null;
    }

    private IEnumerator RunTorchUnequipAnimation()
    {
        _anim.applyRootMotion = false;
        if (unequipGroundLift > 0f && _cc != null && _cc.enabled)
            _cc.Move(Vector3.up * unequipGroundLift);
        yield return FireTriggerInstant(HashUnequipTorch);
    }

    /// <summary>
    /// Gun ke liye special unequip:
    /// WeaponState = 2 set karke direct UnequipGun animation crossfade karta hai.
    /// </summary>
    public IEnumerator RunGunUnequipAnimation()
    {
        _anim.applyRootMotion = false;
        if (gunUnequipGroundLift > 0f && _cc != null && _cc.enabled)
            _cc.Move(Vector3.up * gunUnequipGroundLift);

        _anim.SetInteger(HashWeaponState, 2);
        _anim.ResetTrigger(HashUnequip);
        _anim.CrossFadeInFixedTime("UnequipGun", 0.15f, 0, 0f);
        yield return null;
    }


    private IEnumerator WaitForAnimationToFinish(float duration)
    {
        yield return new WaitForSeconds(duration);
    }

    /// <summary>
    /// PERMANENT FIX for terrain sinking and "1 second delay before animation":
    ///
    /// Root cause: Animator transitions have "Has Exit Time" = true.
    /// This makes Unity WAIT for the current animation to reach ~80-100%
    /// before starting the Unequip transition. During this wait (up to 1 sec),
    /// the character stays in Gun/Sword pose — causing terrain clipping.
    ///
    /// Fix: Force current state to normalizedTime=0.99 instantly.
    /// This satisfies ANY "Has Exit Time" condition in 1 frame.
    /// Then fire the trigger → transition starts immediately → no wait, no sinking.
    /// </summary>
    public IEnumerator FireTriggerInstant(int triggerHash)
    {
        // ALL layers mein current state ko 99% pe force karo (exit time bypass)
        for (int layer = 0; layer < _anim.layerCount; layer++)
        {
            var st = _anim.GetCurrentAnimatorStateInfo(layer);
            if (st.shortNameHash != 0)
                _anim.Play(st.shortNameHash, layer, 0.99f);
        }

        _anim.ResetTrigger(triggerHash);
        _anim.SetTrigger(triggerHash);

        yield return null; // 1 frame: transition register hone do

        // ALL layers mein transition blend skip karo
        for (int layer = 0; layer < _anim.layerCount; layer++)
        {
            if (_anim.IsInTransition(layer))
            {
                int destHash = _anim.GetNextAnimatorStateInfo(layer).shortNameHash;
                if (destHash != 0)
                    _anim.Play(destHash, layer, 0f);
            }
        }
    }

    // ─── External Sync for SwordEquipSystem ───────────────────────────────────
    
    public void SyncWeaponStateFromExternal(int weaponIndex, bool isEquippingStatus)
    {
        _currentWeapon = (WeaponState)weaponIndex;
        _isEquipping = isEquippingStatus;
    }

    public void SetAnimatorWeaponState(int weaponIndex)
    {
        _anim.SetInteger(HashWeaponState, weaponIndex);
    }

    public void RegisterTorch(TorchInteractionSystem torch)
    {
        _torchSystem = torch;
    }

    /// <summary>
    /// Weapon pickup ke liye: currently equipped weapon ko unequip karta hai
    /// aur tab tak wait karta hai jab tak weapon holster/back pe nahi chala jaata.
    /// Ground pickup shuru hone se pehle isko use karein.
    /// </summary>
    public IEnumerator UnequipCurrentWeaponForPickup()
    {
        // Agar kuch bhi equipped nahi hai ya already sequence chal raha hai toh seedha return karo
        if (_currentWeapon == WeaponState.Unarmed) yield break;

        _isSequenceRunning = true;
        SetFrozen(true);

        if (_currentWeapon == WeaponState.Sword && _swordSystem != null)
        {
            yield return RunUnequipAnimation();
            _swordSystem.StartUnequip();
            while (_swordSystem.CurrentState != SwordEquipSystem.SwordState.OnBack) yield return null;
        }
        else if (_currentWeapon == WeaponState.Gun && _gunSystem != null)
        {
            yield return RunGunUnequipAnimation();
            _gunSystem.StartUnequip();
            while (_gunSystem.CurrentState != GunEquipSystem.GunState.Holstered) yield return null;
        }
        else if (_currentWeapon == WeaponState.Torch && _torchSystem != null)
        {
            yield return RunTorchUnequipAnimation();
            _torchSystem.StartUnequip();
            while (_torchSystem.currentState != TorchInteractionSystem.TorchState.Placed) yield return null;
        }

        _currentWeapon = WeaponState.Unarmed;
        _anim.SetInteger(HashWeaponState, 0);
        _anim.applyRootMotion = false;
        _anim.CrossFadeInFixedTime("Locomotion", 0.15f, 0, 0f);
        yield return new WaitForSeconds(0.15f);
        _isSequenceRunning = false;
    }

    public void SetFrozen(bool frozen, bool lockWorldPosition = false)
    {
        isFrozen = frozen;
        _lockWorldPosition = frozen && lockWorldPosition;
        if (frozen)
        {
            _velocity = Vector3.zero;
            _anim.SetFloat(HashSpeed, 0f);
            _lockedWorldPosition = transform.position;
            _anim.applyRootMotion = true;
        }
        else
        {
            _lockWorldPosition = false;
            _anim.applyRootMotion = false;
            SnapToGround();
        }
    }

    /// <summary>
    /// Snaps the character to the ground using a Raycast.
    /// This fixes any Y-position floating that might have happened during animation.
    /// </summary>
    public void SnapToGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1.5f;
        LayerMask mask = groundMask.value != 0 ? groundMask : Physics.DefaultRaycastLayers;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, mask, QueryTriggerInteraction.Ignore))
        {
            float capsuleBottomOffset = (_cc != null) ? (_cc.center.y - (_cc.height * 0.5f)) : 0f;
            Vector3 targetPos = hit.point + Vector3.up * (-capsuleBottomOffset + 0.05f); // 0.05f clearance
            
            if (_cc != null && _cc.enabled)
            {
                Vector3 delta = targetPos - transform.position;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    _cc.Move(delta);
                }
            }
            else
            {
                transform.position = targetPos;
            }
        }
    }

    /// <summary>Moves the CharacterController up (e.g. during pickup/unequip animations while frozen).</summary>
    public void ApplyGroundLift(float metres)
    {
        if (Mathf.Abs(metres) <= 0.001f || _cc == null) return;

        if (_lockWorldPosition)
        {
            _lockedWorldPosition.y += metres;
            _cc.Move(Vector3.up * metres);
        }
        else
        {
            _cc.Move(Vector3.up * metres);
        }
    }

    public float GunUnequipGroundLift => gunUnequipGroundLift;

    // ─── Public Accessors ─────────────────────────────────────────────────────

    /// <summary>Returns the player's current health (0–maxHealth).</summary>
    public int  Health      => _health;

    /// <summary>Returns true if the player is dead.</summary>
    public bool IsDead      => _isDead;

    /// <summary>Returns the active weapon as an integer (0=None, 1=Sword, 2=Gun, 3=Torch).</summary>
    public int  WeaponIndex => (int)_currentWeapon;

    /// <summary>Returns true if player currently has any weapon equipped in hand.</summary>
    public bool IsHoldingWeapon => _currentWeapon != WeaponState.Unarmed;

    /// <summary>Returns true if an unequip or equip sequence is currently active.</summary>
    public bool IsSequenceRunning => _isSequenceRunning;
}
