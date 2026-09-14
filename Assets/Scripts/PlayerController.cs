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

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;     // Main Camera transform

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [Header("Weapon GameObjects (optional – for showing/hiding meshes)")]
    [SerializeField] private GameObject swordObject;        // Sword mesh in hand
    [SerializeField] private GameObject gunObject;          // Gun  mesh in hand

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

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _anim   = GetComponent<Animator>();
        _cc     = GetComponent<CharacterController>();
        _health = maxHealth;

        // Start with no weapon
        SetWeaponVisuals(WeaponState.Unarmed);
        _anim.SetInteger(HashWeaponState, (int)WeaponState.Unarmed);
    }

    private void Update()
    {
        if (_isDead) return;

        HandleGravity();
        HandleMovement();
        HandleJump();
        HandleWeaponSwitch();
        HandleAttack();
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    private void HandleGravity()
    {
        _isGrounded = _cc.isGrounded;
        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;                // Keep grounded

        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    private void HandleMovement()
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
            _cc.Move(dir * moveSpeed * Time.deltaTime);

            // Rotate player to face move direction
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    10f * Time.deltaTime
                );
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
        if (_isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            _anim.SetTrigger(HashJump);
        }
    }

    // ─── Weapon Switching ─────────────────────────────────────────────────────

    private void HandleWeaponSwitch()
    {
        if (_isEquipping) return;   // Wait for current equip/unequip to finish

        if (Input.GetKeyDown(KeyCode.Alpha1))
            StartCoroutine(SwitchWeapon(WeaponState.Unarmed));

        else if (Input.GetKeyDown(KeyCode.Alpha2))
            StartCoroutine(SwitchWeapon(WeaponState.Sword));

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
            yield return WaitForAnimationEnd("Unequip");    // Wait until it finishes
        }

        // Hide old weapon meshes now
        SetWeaponVisuals(WeaponState.Unarmed);

        // Step 2 – Change weapon state parameter so blend tree switches layers
        _currentWeapon = target;
        _anim.SetInteger(HashWeaponState, (int)_currentWeapon);

        // Step 3 – Equip new weapon (if not going to Unarmed)
        if (_currentWeapon != WeaponState.Unarmed)
        {
            _anim.SetTrigger(HashEquip);
            yield return WaitForAnimationState("Equip");    // Wait until Equip state starts
            yield return WaitForAnimationEnd("Equip");      // Wait until it finishes

            // Show new weapon mesh only after equip animation completes
            SetWeaponVisuals(_currentWeapon);
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

    // ─── Helper: Weapon Visuals ───────────────────────────────────────────────

    private void SetWeaponVisuals(WeaponState state)
    {
        if (swordObject) swordObject.SetActive(state == WeaponState.Sword);
        if (gunObject)   gunObject.SetActive(state == WeaponState.Gun);
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
