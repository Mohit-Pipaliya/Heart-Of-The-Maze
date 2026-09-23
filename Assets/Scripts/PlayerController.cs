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
    // ─── Inspector Fields ──────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float walkSpeed      = 2f;
    [SerializeField] private float runSpeed       = 5f;
    [SerializeField] private float rotationSpeed  = 150f; // Keyboard turning speed
    [SerializeField] private float mouseSensitivity = 2.0f; // Mouse turning speed


    
    [Header("Gun Movement")]
    [SerializeField] private float gunWalkSpeed   = 2f;
    [SerializeField] private float gunRunSpeed    = 5f;
    
    [SerializeField] private float jumpForce      = 5f;
    [SerializeField] private float gravity        = -20f;   // -20 = grounded feel, -9.81 = real world
    [SerializeField] private float speedDampTime  = 0.1f;   // Blend-tree smoothing

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

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _anim   = GetComponent<Animator>();
        _cc     = GetComponent<CharacterController>();
        _health = maxHealth;
        _swordSystem = GetComponentInChildren<SwordEquipSystem>();
        _gunSystem = GetComponentInChildren<GunEquipSystem>();

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
    }

    private void Update()
    {
        if (_isDead) return;

        if (isFrozen)
        {
            _anim.SetBool("IsGrounded", _isGrounded);
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
        _anim.SetBool("IsGrounded", _isGrounded);

        HandleWeaponSwitch();
        HandleAttack();
    }

    private void OnAnimatorMove()
    {
        if (_lockWorldPosition)
            return;
    }

    // ─── Foot IK ──────────────────────────────────────────────────────────────
    // Pair zameen pe plant karta hai — hawa mein tairna band karta hai.
    // ZARURI: Animator window mein har layer ka 'IK Pass' checkbox ON karo!

    private float _leftFootIKWeight  = 0f;
    private float _rightFootIKWeight = 0f;

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useFootIK || _anim == null || !_anim.isHuman) return;
        if (_isDead || isFrozen) return;
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
        if (_isDead || !_lockWorldPosition) return;

        _anim.applyRootMotion = false;
        CompensateAnimatedBodyBelowGround();
        ClampLockedPositionAboveGround();

        Vector3 correction = _lockedWorldPosition - transform.position;
        if (correction.sqrMagnitude > 0.000001f)
            _cc.Move(correction);

        _velocity = Vector3.zero;
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
            Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);

            if (leftFoot != null) lowestWorldY = Mathf.Min(lowestWorldY, leftFoot.position.y);
            if (rightFoot != null) lowestWorldY = Mathf.Min(lowestWorldY, rightFoot.position.y);
            if (hips != null) lowestWorldY = Mathf.Min(lowestWorldY, hips.position.y);
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
        // Better than Raycast because a thin ray can easily miss edges.
        if (!_isGrounded)
        {
            // Position the sphere at the bottom of the capsule controller
            Vector3 spherePosition = _cc.bounds.center;
            spherePosition.y -= _cc.bounds.extents.y;     // Go to the base
            spherePosition.y += groundCheckRadius - groundCheckOffset; // Offset it slightly up

            // Use LayerMask so we don't accidentally hit the player's own triggers/weapons
            _isGrounded = Physics.CheckSphere(spherePosition, groundCheckRadius, groundMask);
        }
    }

    private void CalculateGravity()
    {
        if (_isGrounded && _velocity.y < 0f)
        {
            // Setting a small negative value like -2f is perfectly correct! 
            // It ensures the CharacterController stays snapped to the ground next frame.
            _velocity.y = -2f; 
        }

        // Accumulate gravity
        _velocity.y += gravity * Time.deltaTime;
    }

    private void CalculateHorizontalMovement()
    {
        // Raw input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float mouseX = Input.GetAxis("Mouse X");

        // 1. Player Rotation (A/D and Mouse X physically rotate the character)
        float rotationInput = (h * rotationSpeed * Time.deltaTime) + (mouseX * mouseSensitivity);
        if (Mathf.Abs(rotationInput) > 0.001f)
        {
            transform.Rotate(Vector3.up, rotationInput);
        }

        bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        // Target blend-tree speed value
        float targetSpeed = 0f;
        float inputMag = Mathf.Clamp01(new Vector2(h, v).magnitude);
        
        if (inputMag > 0.1f)
            targetSpeed = running ? 1f : 0.5f;

        // Smooth transition for Animator
        float currentSpeed = _anim.GetFloat(HashSpeed);
        float smoothSpeed  = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime / speedDampTime);
        _anim.SetFloat(HashSpeed, smoothSpeed);

        // 2. Character-Relative Forward/Backward Movement
        float forwardInput = v;
        if (Mathf.Abs(h) > 0.01f && Mathf.Abs(v) < 0.01f)
        {
            // Give a slight forward movement when only turning
            forwardInput = Mathf.Abs(h) * 0.2f; 
        }

        if (Mathf.Abs(forwardInput) > 0.01f)
        {
            float currentWalkSpeed = (_currentWeapon == WeaponState.Gun) ? gunWalkSpeed : walkSpeed;
            float currentRunSpeed = (_currentWeapon == WeaponState.Gun) ? gunRunSpeed : runSpeed;
            float moveSpeed = running ? currentRunSpeed : currentWalkSpeed;
            
            // Move along the player's own forward vector
            Vector3 dir = transform.forward * forwardInput;
            
            _velocity.x = dir.x * moveSpeed;
            _velocity.z = dir.z * moveSpeed;
        }
        else 
        {
            // Reset horizontal velocity when no movement input
            _velocity.x = 0f;
            _velocity.z = 0f;
        }
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
            _anim.SetTrigger(HashJump);
            
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
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (_currentWeapon != WeaponState.Unarmed)
                StartCoroutine(DoUnequip());
        }

        // Press 2: Sword equip karo
        // - Agar hath khali (Unarmed): Seedha Sword equip animation
        // - Agar Gun hath mein hai: Pehle Gun unequip → phir Sword equip
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (_currentWeapon == WeaponState.Sword) return; // Pehle se sword hai
            bool swordAvailable = _swordSystem != null &&
                                  (_swordSystem.CurrentState == SwordEquipSystem.SwordState.OnBack ||
                                   _swordSystem.CurrentState == SwordEquipSystem.SwordState.Equipped);
            if (!swordAvailable) return;
            StartCoroutine(DoSwitchToSword());
        }

        // Press 3: Gun equip karo
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (_currentWeapon == WeaponState.Gun) return; // Pehle se gun hai
            bool gunAvailable = _gunSystem != null &&
                                (_gunSystem.CurrentState == GunEquipSystem.GunState.Holstered ||
                                 _gunSystem.CurrentState == GunEquipSystem.GunState.Equipped);
            if (!gunAvailable) return;
            StartCoroutine(DoSwitchToGun());
        }

        // Press 4: Torch equip karo
        else if (Input.GetKeyDown(KeyCode.Alpha4))
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
            if (Input.GetMouseButtonDown(0))
            {
                // Randomly pick one of the 2 sword attack animations
                _anim.SetInteger(HashAttackIndex, Random.Range(0, 2));
                _anim.ResetTrigger(HashAttack);
                _anim.SetTrigger(HashAttack);
            }
        }
        else if (_currentWeapon == WeaponState.Gun)
        {
            // Firing Logic: Left Click (Mouse 0)
            if (Input.GetMouseButton(0) && Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + gunFireRate;
                _lastFireTime = Time.time;
                
                // Fire ka animation chalao (Trigger se, ye zyada reliable hai)
                _anim.ResetTrigger(HashFire);
                _anim.SetTrigger(HashFire);
            }

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
            _anim.SetTrigger(HashHit);   // Play hit reaction
        }
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // The Animator plays Die (no weapon) or Die2 (with weapon) based on WeaponState.
        // We set the WeaponState BEFORE firing the trigger so the Animator can branch.
        _anim.SetTrigger(HashDie);

        // Disable controller so the body stops moving
        _cc.enabled = false;
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
        if (unequipGroundLift > 0f)
            _cc.Move(Vector3.up * unequipGroundLift);
        yield return FireTriggerInstant(HashUnequip);
    }

    private IEnumerator RunTorchUnequipAnimation()
    {
        _anim.applyRootMotion = false;
        if (unequipGroundLift > 0f)
            _cc.Move(Vector3.up * unequipGroundLift);
        yield return FireTriggerInstant(HashUnequipTorch);
    }

    /// <summary>
    /// Gun ke liye special unequip:
    /// Gun stance (WeaponState=2) mein character zyada jhuka hota hai.
    /// Step 1: WeaponState=0 set karke Animator ko Unarmed stance mein lao (character seedha khada hoga)
    /// Step 2: Extra lift apply karo (gun stance ke liye zyada zaroori)
    /// Step 3: Trigger fire karo
    /// Agar Animator mein Gun aur Sword ka alag Unequip hai, tab bhi apna kaam karega
    /// kyunki hum pehle neutral (Unarmed) state mein jaate hain.
    /// </summary>
    public IEnumerator RunGunUnequipAnimation()
    {
        _anim.applyRootMotion = false;

        // Gun stance se seedha Unarmed mein jao — character normal height pe aayega
        _anim.SetInteger(HashWeaponState, 0);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate(); // 3 physics frames: Animator settle ho

        // Extra lift for gun (gun pose me character sword se zyada neeche hota hai)
        if (gunUnequipGroundLift > 0f)
            _cc.Move(Vector3.up * gunUnequipGroundLift);

        yield return FireTriggerInstant(HashUnequip);
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
    /// Mashal pickup ke liye: currently equipped weapon ko unequip karta hai
    /// aur tab tak wait karta hai jab tak weapon holster/back pe nahi chala jaata.
    /// TorchInteractionSystem ke GrabRoutine mein 'yield return' ke saath use karo.
    /// </summary>
    public IEnumerator UnequipCurrentWeaponForPickup()
    {
        // Agar kuch bhi equipped nahi hai ya already busy hai toh seedha return karo
        if (_currentWeapon == WeaponState.Unarmed || _isSequenceRunning) yield break;

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
            yield return RunGunUnequipAnimation();
            _gunSystem.StartUnequip();
            yield return new WaitForSeconds(unequipAnimDuration);
            while (_gunSystem.CurrentState != GunEquipSystem.GunState.Holstered) yield return null;
        }
        else if (_currentWeapon == WeaponState.Torch && _torchSystem != null)
        {
            yield return RunTorchUnequipAnimation();
            _torchSystem.StartUnequip();
            while (_torchSystem.currentState != TorchInteractionSystem.TorchState.Placed) yield return null;
            _anim.SetInteger(HashWeaponState, 0);
            _anim.applyRootMotion = false;
            yield return null;
        }

        _currentWeapon = WeaponState.Unarmed;
        _anim.SetInteger(HashWeaponState, 0);
        _anim.applyRootMotion = false;
        _isSequenceRunning = false;
    }

    public void SetFrozen(bool frozen, bool lockWorldPosition = false)
    {
        isFrozen = frozen;
        if (frozen)
        {
            _velocity = Vector3.zero;
            _anim.SetFloat(HashSpeed, 0f);
            _anim.applyRootMotion = false;

            if (lockWorldPosition)
            {
                _lockWorldPosition = true;
                _lockedWorldPosition = transform.position;
            }
        }
        else
        {
            _lockWorldPosition = false;
        }
    }

    /// <summary>Moves the CharacterController up (e.g. during pickup/unequip animations while frozen).</summary>
    public void ApplyGroundLift(float metres)
    {
        if (metres <= 0f || _cc == null) return;

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

    /// <summary>Returns the active weapon as an integer (0=None, 1=Sword, 2=Gun).</summary>
    public int  WeaponIndex => (int)_currentWeapon;
}
