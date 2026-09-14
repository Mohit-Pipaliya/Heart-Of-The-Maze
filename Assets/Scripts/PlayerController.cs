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
    [SerializeField] private float jumpForce      = 5f;
    [SerializeField] private float gravity        = -9.81f;
    [SerializeField] private float speedDampTime  = 0.1f;   // Blend-tree smoothing

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;          // LayerMask to define what is "Ground"
    [SerializeField] private float groundCheckRadius = 0.25f; // Radius of the ground check sphere
    [SerializeField] private float groundCheckOffset = 0.1f;  // Offset from the bottom of the CharacterController

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;     // Main Camera transform

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Weapon Systems")]
    public bool hasSword = false;                           // Tracks if player picked up the sword
    [SerializeField] private GameObject swordObject;        // The actual sword mesh/gameobject
    [SerializeField] private GameObject gunObject;          // Gun  mesh in hand
    [SerializeField] private Transform swordHandTransform;  // Empty GameObject in the player's Hand
    [SerializeField] private Transform swordBackTransform;  // Empty GameObject on the player's Back (pith)
    [SerializeField] private float equipGrabDelay = 0.4f;   // Time until hand reaches back to grab sword
    [SerializeField] private float unequipPutDelay = 0.4f;  // Time until hand puts sword back

    // ─── Private State ─────────────────────────────────────────────────────────

    // Animator
    private Animator          _anim;
    private CharacterController _cc;

    // Animator parameter hashes (faster than string lookups every frame)
    private static readonly int HashSpeed       = Animator.StringToHash("Speed");
    private static readonly int HashWeaponState = Animator.StringToHash("WeaponState");
    private static readonly int HashAttackIndex = Animator.StringToHash("AttackIndex");
    private static readonly int HashJump        = Animator.StringToHash("Jump");
    private static readonly int HashAttack      = Animator.StringToHash("Attack");
    private static readonly int HashEquip       = Animator.StringToHash("Equip");
    private static readonly int HashUnequip     = Animator.StringToHash("Unequip");
    private static readonly int HashHit         = Animator.StringToHash("Hit");
    private static readonly int HashDie         = Animator.StringToHash("Die");

    // Weapon states
    private enum WeaponState { Unarmed = 0, Sword = 1, Gun = 2 }
    private WeaponState _currentWeapon = WeaponState.Unarmed;

    // Flags
    private bool _isDead          = false;
    private bool _isEquipping     = false;   // Busy playing equip/unequip animation
    private int  _health;

    // Physics
    private Vector3 _velocity;
    private bool    _isGrounded;
    
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

        // Start with no weapon and sword hidden until picked up
        hasSword = false;
        if (swordObject) swordObject.SetActive(false);
        if (gunObject) gunObject.SetActive(false);
        _anim.SetInteger(HashWeaponState, (int)WeaponState.Unarmed);
    }

    private void Update()
    {
        if (_isDead) return;

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
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool  running   = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float inputMag  = new Vector2(h, v).magnitude;

        // Target blend-tree speed value
        float targetSpeed = 0f;
        if (inputMag > 0.1f)
            targetSpeed = running ? 1f : 0.5f;

        // Smooth transition
        float currentSpeed = _anim.GetFloat(HashSpeed);
        float smoothSpeed  = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime / speedDampTime);
        _anim.SetFloat(HashSpeed, smoothSpeed);

        // Actual character movement
        if (inputMag > 0.1f)
        {
            float   moveSpeed = running ? runSpeed : walkSpeed;
            Vector3 dir       = GetMoveDirection(h, v);
            
            // Assign horizontal velocity instead of calling _cc.Move() here
            _velocity.x = dir.x * moveSpeed;
            _velocity.z = dir.z * moveSpeed;

            // Rotate player to face move direction
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    10f * Time.deltaTime
                );
        }
        else 
        {
            // Reset horizontal velocity when no input
            _velocity.x = 0f;
            _velocity.z = 0f;
        }
    }

    /// <summary>Returns movement direction relative to the camera.</summary>
    private Vector3 GetMoveDirection(float h, float v)
    {
        Vector3 forward = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : transform.forward;

        Vector3 right = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
            : transform.right;

        return (forward * v + right * h).normalized;
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
        if (_isEquipping) return;   // Wait for current equip/unequip to finish

        if (Input.GetKeyDown(KeyCode.Alpha1))
            StartCoroutine(SwitchWeapon(WeaponState.Unarmed));

        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (hasSword)
                StartCoroutine(SwitchWeapon(WeaponState.Sword));
            else
                Debug.Log("Abhi tumhare paas sword nahi hai!");
        }

        else if (Input.GetKeyDown(KeyCode.Alpha3))
            StartCoroutine(SwitchWeapon(WeaponState.Gun));
    }

    /// <summary>
    /// Handles the full equip/unequip sequence:
    ///   1. If already holding a weapon → play Unequip, wait for it to finish.
    ///   2. Update WeaponState parameter.
    ///   3. If new weapon is not Unarmed → play Equip, wait for it to finish.
    /// </summary>
    private IEnumerator SwitchWeapon(WeaponState target)
    {
        if (target == _currentWeapon) yield break;

        _isEquipping = true;

        // Step 1 – Unequip current weapon (if any)
        if (_currentWeapon != WeaponState.Unarmed)
        {
            _anim.SetTrigger(HashUnequip);
            yield return WaitForAnimationState("Unequip");  // Wait until Unequip state starts
            
            // Animation ke beech me (jaise 0.4 seconds baad) sword ko wapas pith par rakh do
            yield return new WaitForSeconds(_currentWeapon == WeaponState.Sword ? unequipPutDelay : 0.2f);
            
            if (_currentWeapon == WeaponState.Sword) MoveSwordTo(swordBackTransform);
            if (_currentWeapon == WeaponState.Gun && gunObject) gunObject.SetActive(false);

            yield return WaitForAnimationEnd("Unequip");    // Wait until it finishes
        }

        // Step 2 – Change weapon state parameter so blend tree switches layers
        _currentWeapon = target;
        _anim.SetInteger(HashWeaponState, (int)_currentWeapon);

        // Step 3 – Equip new weapon (if not going to Unarmed)
        if (_currentWeapon != WeaponState.Unarmed)
        {
            _anim.SetTrigger(HashEquip);
            yield return WaitForAnimationState("Equip");    // Wait until Equip state starts
            
            // Animation ke beech me (jaise hi hath pith tak pahuche), sword ko hath me le lo
            yield return new WaitForSeconds(_currentWeapon == WeaponState.Sword ? equipGrabDelay : 0.2f);

            if (_currentWeapon == WeaponState.Sword) MoveSwordTo(swordHandTransform);
            if (_currentWeapon == WeaponState.Gun && gunObject) gunObject.SetActive(true);

            yield return WaitForAnimationEnd("Equip");      // Wait until it finishes
        }

        _isEquipping = false;
    }

    // ─── Attack ───────────────────────────────────────────────────────────────

    private void HandleAttack()
    {
        if (_isEquipping)                    return;
        if (_currentWeapon == WeaponState.Unarmed) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (_currentWeapon == WeaponState.Sword)
            {
                // Randomly pick one of the 2 sword attack animations
                _anim.SetInteger(HashAttackIndex, Random.Range(0, 2));
            }
            // For Gun, AttackIndex is irrelevant (only one fire animation)
            _anim.SetTrigger(HashAttack);
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

    // ─── Pickup System ────────────────────────────────────────────────────────

    public void PickupSword()
    {
        hasSword = true;
        if (swordObject) 
        {
            swordObject.SetActive(true);
            // Starting me sword ko pith (back) par rakh do
            MoveSwordTo(swordBackTransform);
        }
    }

    private void MoveSwordTo(Transform targetParent)
    {
        if (swordObject != null && targetParent != null)
        {
            swordObject.transform.SetParent(targetParent);
            swordObject.transform.localPosition = Vector3.zero;
            swordObject.transform.localRotation = Quaternion.identity;
        }
    }

    // ─── Helper: Animation Waiting Coroutines ─────────────────────────────────

    /// <summary>
    /// Waits one frame then waits until the Animator enters a state whose name
    /// contains <paramref name="stateName"/> on layer 0.
    /// </summary>
    private IEnumerator WaitForAnimationState(string stateName)
    {
        yield return null;  // Let the trigger propagate
        while (!_anim.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;
    }

    /// <summary>
    /// Waits until the current state on layer 0 whose name contains
    /// <paramref name="stateName"/> has played past 95% of its duration.
    /// </summary>
    private IEnumerator WaitForAnimationEnd(string stateName)
    {
        // Wait until we are IN the target state
        while (!_anim.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;

        // Wait until it is almost finished (normalizedTime >= 0.95)
        while (_anim.GetCurrentAnimatorStateInfo(0).IsName(stateName) &&
               _anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
            yield return null;
    }

    // ─── Public Accessors ─────────────────────────────────────────────────────

    /// <summary>Returns the player's current health (0–maxHealth).</summary>
    public int  Health      => _health;

    /// <summary>Returns true if the player is dead.</summary>
    public bool IsDead      => _isDead;

    /// <summary>Returns the active weapon as an integer (0=None, 1=Sword, 2=Gun).</summary>
    public int  WeaponIndex => (int)_currentWeapon;
}
