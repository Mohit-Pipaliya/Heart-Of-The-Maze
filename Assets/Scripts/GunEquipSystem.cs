using UnityEngine;
using UnityEngine.InputSystem;

public class GunEquipSystem : MonoBehaviour
{
    public enum GunState
    {
        Ground,
        Holstered,
        Equipping,
        Equipped,
        Unequipping
    }

    private System.Collections.IEnumerator WaitUntilAnimationReaches(Animator anim, int stateHash, float targetNormalizedTime, float timeout)
    {
        float timer = 0f;
        bool hasStarted = false;
        
        // Wait for the animation to start (either as current or next state)
        while (timer < timeout && !hasStarted)
        {
            timer += Time.deltaTime;
            for (int i = 0; i < anim.layerCount; i++)
            {
                if (anim.GetCurrentAnimatorStateInfo(i).shortNameHash == stateHash ||
                    anim.GetNextAnimatorStateInfo(i).shortNameHash == stateHash)
                {
                    hasStarted = true;
                    break;
                }
            }
            yield return null;
        }

        // If it never started within timeout, just return to prevent infinite freezing
        if (!hasStarted) yield break;

        // Now wait for it to reach the target time
        while (true)
        {
            bool isPlaying = false;
            bool reachedTarget = false;

            for (int i = 0; i < anim.layerCount; i++)
            {
                var current = anim.GetCurrentAnimatorStateInfo(i);
                var next = anim.GetNextAnimatorStateInfo(i);

                if (current.shortNameHash == stateHash)
                {
                    isPlaying = true;
                    if (current.normalizedTime >= targetNormalizedTime) reachedTarget = true;
                }
                else if (next.shortNameHash == stateHash)
                {
                    isPlaying = true; // Still transitioning into it
                }
            }

            if (reachedTarget || !isPlaying) break;
            yield return null;
        }
    }

    /// <summary>Waits until the given state reaches normalizedTime on any layer (no early exit when state changes).</summary>
    private System.Collections.IEnumerator WaitUntilStateNormalizedTime(Animator anim, int stateHash, float targetNormalizedTime, float timeout)
    {
        float timer = 0f;
        while (timer < timeout)
        {
            timer += Time.deltaTime;
            for (int i = 0; i < anim.layerCount; i++)
            {
                var current = anim.GetCurrentAnimatorStateInfo(i);
                if (current.shortNameHash == stateHash && current.normalizedTime >= targetNormalizedTime)
                    yield break;
            }
            yield return null;
        }
    }

    private System.Collections.IEnumerator WaitUntilStateActive(Animator anim, int stateHash, float timeout)
    {
        float timer = 0f;
        while (timer < timeout)
        {
            timer += Time.deltaTime;
            if (IsInOrTransitioningToState(anim, stateHash))
                yield break;
            yield return null;
        }
    }

    private System.Collections.IEnumerator WaitUntilCurrentStateIs(Animator anim, int stateHash, float timeout)
    {
        float timer = 0f;
        while (timer < timeout)
        {
            timer += Time.deltaTime;
            for (int i = 0; i < anim.layerCount; i++)
            {
                if (anim.GetCurrentAnimatorStateInfo(i).shortNameHash == stateHash)
                    yield break;
            }
            yield return null;
        }
    }

    private static bool IsInOrTransitioningToState(Animator anim, int stateHash)
    {
        for (int i = 0; i < anim.layerCount; i++)
        {
            if (anim.GetCurrentAnimatorStateInfo(i).shortNameHash == stateHash ||
                anim.GetNextAnimatorStateInfo(i).shortNameHash == stateHash)
                return true;
        }
        return false;
    }

    private static float GetStateNormalizedTime(Animator anim, int stateHash)
    {
        for (int i = 0; i < anim.layerCount; i++)
        {
            var current = anim.GetCurrentAnimatorStateInfo(i);
            if (current.shortNameHash == stateHash)
                return current.normalizedTime;
        }
        return 0f;
    }

    private static void SnapAnimatorStateToEnd(Animator anim, int stateHash, float normalizedTime = 0.99f)
    {
        for (int layer = 0; layer < anim.layerCount; layer++)
        {
            if (anim.GetCurrentAnimatorStateInfo(layer).shortNameHash == stateHash)
                anim.Play(stateHash, layer, normalizedTime);
        }
    }

    private void SetWeaponStateForUnequip(int weaponState)
    {
        if (playerController != null)
            playerController.SetAnimatorWeaponState(weaponState);
        else if (playerAnimator != null)
            playerAnimator.SetInteger(HashWeaponState, weaponState);
    }

    [Header("References")]
    [Tooltip("The actual gun GameObject in the scene")]
    public GameObject gun;
    
    [Tooltip("The socket on the player's body (e.g., hip or back)")]
    public Transform gunHolsterSocket;
    
    [Tooltip("The socket in the player's hand")]
    public Transform gunHandSocket;
    
    [Tooltip("Optional: An empty object placed inside the gun at the handle to automatically align it.")]
    public Transform gunHandlePoint;
    
    [Tooltip("The Animator playing the equip/unequip animations")]
    public Animator playerAnimator;

    [Header("Transform Settings")]
    public Vector3 holsterLocalPosition = Vector3.zero;
    public Vector3 holsterLocalRotation = Vector3.zero;
    public Vector3 handLocalPosition = Vector3.zero;
    public Vector3 handLocalRotation = Vector3.zero;

    [Header("Animation Settings")]
    [Tooltip("If true, relies on Animation Events. If false, uses a time delay (easier!).")]
    public bool useAnimationEvents = false;
    
    [Tooltip("Time delay before the gun jumps to hand/holster (if not using events)")]
    public float grabDelay = 0.4f;
    
    [Tooltip("Normalized time (0.0 to 1.0) of the Pickup animation when the hand reaches the ground. 0.5 is usually the middle (lowest point).")]
    [Range(0f, 1f)]
    public float grabNormalizedTime = 0.5f;
    
    [Tooltip("Fallback time delay before the weapon jumps to hand during ground pickup (used if animator is missing).")]
    public float pickupDelay = 2.0f;

    [Header("Terrain Sinking Fix")]
    [Tooltip("Lift player up to prevent animation clipping into terrain during pickup")]
    public float pickupGroundLift = 0.25f;

    [Tooltip("Extra lift applied right before UnequipGun (uses PlayerController value when 0)")]
    public float unequipGroundLiftOverride = 0f;

    [Tooltip("PickupGun normalized time — pickup is considered finished at this point")]
    [Range(0.85f, 1f)]
    public float pickupCompleteNormalizedTime = 0.95f;

    [Tooltip("Animator trigger parameter for picking up from ground")]
    public string pickupTrigger = "PickupGun";

    [Header("Smooth Pickup")]
    [Tooltip("Kitne seconds mein gun smoothly haath tak aaye (0 = instant snap)")]
    public float smoothPickupDuration = 0.25f;

    private static readonly int HashWeaponState = Animator.StringToHash("WeaponState");
    private static readonly int HashUnequip = Animator.StringToHash("Unequip");

    private GunState currentState = GunState.Ground;
    private PlayerController playerController;
    
    public GunState CurrentState => currentState;
    public bool IsGunEquipped => currentState == GunState.Equipped;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
            
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
            if (playerAnimator == null)
            {
                playerAnimator = GetComponentInParent<Animator>();
            }
        }
    }

    // Update method hata diya gaya hai taaki PlayerController input handle kare

    private bool isPickingUpSequence = false;

    public void PickupGunFromGround()
    {
        if (currentState != GunState.Ground) return;
        
        Debug.Log("[Gun] Pickup Started");
        
        Rigidbody rb = gun.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = gun.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        isPickingUpSequence = true;
        currentState = GunState.Equipping;

        StartCoroutine(PickupSequence());
    }

    private System.Collections.IEnumerator PickupSequence()
    {
        const float unequipCompleteTime = 0.95f;
        const float animWaitTimeout = 20f;
        const float unequipStartTimeout = 1.5f;
        float originalY = transform.position.y; // Ground Y yaad rakho

        int pickupHash = Animator.StringToHash("PickupGun");
        int unequipGunHash = Animator.StringToHash("UnequipGun");

        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
            playerAnimator.ResetTrigger(pickupTrigger);
            playerAnimator.SetTrigger(pickupTrigger);
        }

        yield return null;

        if (playerController != null)
        {
            playerController.SetFrozen(true, lockWorldPosition: true);
            if (pickupGroundLift > 0f)
                playerController.ApplyGroundLift(pickupGroundLift);
        }

        if (playerAnimator != null)
            playerAnimator.applyRootMotion = false;

        if (playerAnimator != null)
            yield return WaitUntilAnimationReaches(playerAnimator, pickupHash, grabNormalizedTime, animWaitTimeout);
        else
            yield return new WaitForSeconds(pickupDelay);

        // Smooth lerp gun to hand (realistic pickup feel)
        if (smoothPickupDuration > 0f)
            yield return StartCoroutine(SmoothSnapGunToHand(smoothPickupDuration));
        else
            SwitchGunToHand();

        // Note: Yahan extra lift nahi — pickup lift already lag chuki hai.
        // Double lift player ko hawa mein bhej deta tha.

        isPickingUpSequence = false;

        if (playerAnimator != null)
        {
            // PickupGun animation ke khatam hone ka wait karo
            yield return WaitUntilStateNormalizedTime(
                playerAnimator, pickupHash, pickupCompleteNormalizedTime, animWaitTimeout);

            // Animator ko PickupGun state se bahar snap karo
            SnapAnimatorStateToEnd(playerAnimator, pickupHash, 0.99f);
            yield return null;
            yield return null;
            yield return null;

            currentState = GunState.Unequipping;
            if (playerController != null)
                playerController.SyncWeaponStateFromExternal(2, true);

            // ── Ground Snap ─────────────────────────────────────────────────────
            // Pickup animation ke liye player ko upar uthaya tha — ab unequip
            // animation shuru hone se pehle original ground Y pe wapas laao.
            // Negative ApplyGroundLift = neeche move (gravity ki tarah).
            float liftedY   = transform.position.y;
            float snapDelta = originalY - liftedY;   // negative value = neeche
            Debug.Log($"[Gun] Ground snap: liftedY={liftedY:F3}  originalY={originalY:F3}  delta={snapDelta:F3}");
            if (playerController != null && Mathf.Abs(snapDelta) > 0.01f)
                playerController.ApplyGroundLift(snapDelta); // negative = neeche move


            // WeaponState=2 set karo aur direct CrossFade se UnequipGun force karo.
            // Trigger approach unreliable hai jab PickupGun → UnequipGun transition
            // Animator Controller mein sahi conditions nahi hoti — CrossFade guaranteed hai.
            SetWeaponStateForUnequip(2);
            yield return null;

            playerAnimator.ResetTrigger(HashUnequip);
            Debug.Log("[Gun] Forcing UnequipGun via CrossFade...");
            playerAnimator.CrossFadeInFixedTime(unequipGunHash, 0.15f, 0, 0f);

            // State active hone ka wait karo (5 sec timeout)
            const float unequipWaitTimeout = 5f;
            yield return WaitUntilStateActive(playerAnimator, unequipGunHash, unequipWaitTimeout);

            bool unequipStarted = IsInOrTransitioningToState(playerAnimator, unequipGunHash);
            Debug.Log($"[Gun] UnequipGun state active: {unequipStarted}");

            if (!unequipStarted)
            {
                Debug.LogWarning("[Gun] UnequipGun state nahi mila! Animator mein 'UnequipGun' state ka naam check karo.");
            }
            else
            {
                // Current state banne ka wait (transition complete)
                yield return WaitUntilCurrentStateIs(playerAnimator, unequipGunHash, unequipWaitTimeout);

                // Animation 95% complete hone tak wait karo
                float unequipTimer = 0f;
                while (unequipTimer < animWaitTimeout)
                {
                    unequipTimer += Time.deltaTime;
                    float nt = GetStateNormalizedTime(playerAnimator, unequipGunHash);
                    if (nt >= unequipCompleteTime)
                        break;
                    yield return null;
                }
                Debug.Log("[Gun] UnequipGun animation complete.");
            }
        }
        else
        {
            currentState = GunState.Unequipping;
            if (playerController != null)
                playerController.SyncWeaponStateFromExternal(2, true);
        }

        SwitchGunToHolster();
        OnUnequipAnimationFinished();

        SetWeaponStateForUnequip(0);

        if (playerController != null)
            playerController.SetFrozen(false);
    }

    public void StartEquip()
    {
        Debug.Log("[Gun] Equip Started");
        currentState = GunState.Equipping;
        
        if (playerController != null) playerController.SyncWeaponStateFromExternal(2, true); // 2 = Gun

        // Animations ab PlayerController handle karega
        // playerAnimator.SetInteger("WeaponState", 2); 
        // playerAnimator.SetTrigger(equipTrigger);
        
        if (!useAnimationEvents)
        {
            StartCoroutine(EquipRoutine());
        }
    }

    public void StartUnequip()
    {
        Debug.Log("[Gun] Unequip Started");
        currentState = GunState.Unequipping;
        
        if (playerController != null) playerController.SyncWeaponStateFromExternal(2, true);

        // Animations ab PlayerController handle karega
        // playerAnimator.SetTrigger(unequipTrigger);
        
        if (!useAnimationEvents)
        {
            StartCoroutine(UnequipRoutine());
        }
    }

    private System.Collections.IEnumerator EquipRoutine()
    {
        yield return new WaitForSeconds(grabDelay);
        SwitchGunToHand();
        yield return new WaitForSeconds(grabDelay);
        OnEquipAnimationFinished();
    }

    private System.Collections.IEnumerator UnequipRoutine()
    {
        yield return new WaitForSeconds(grabDelay);
        SwitchGunToHolster();
        yield return new WaitForSeconds(grabDelay);
        
        // WeaponState reset ab PlayerController handle karega
        // playerAnimator.SetInteger("WeaponState", 0);
        OnUnequipAnimationFinished();
    }

    public void SwitchGunToHand()
    {
        if (currentState != GunState.Equipping) return;
        
        Debug.Log("[Gun] Switched To Hand");
        
        if (gunHandlePoint != null)
        {
            gun.transform.SetParent(gunHandSocket);
            gun.transform.rotation = gunHandSocket.rotation * Quaternion.Inverse(gunHandlePoint.rotation) * gun.transform.rotation;
            gun.transform.position += (gunHandSocket.position - gunHandlePoint.position);
        }
        else
        {
            AttachToSocket(gunHandSocket, handLocalPosition, handLocalRotation);
        }
    }

    /// <summary>
    /// Gun ko smoothly haath tak lerp karta hai (realistic pickup feel).
    /// SmoothStep curve use karta hai taaki motion natural lage.
    /// </summary>
    private System.Collections.IEnumerator SmoothSnapGunToHand(float duration)
    {
        if (currentState != GunState.Equipping) yield break;
        if (gun == null || gunHandSocket == null) { SwitchGunToHand(); yield break; }

        // ── Target world rotation compute karo (ek baar, constant hai) ──
        Quaternion rotOffset = gunHandlePoint != null
            ? Quaternion.Inverse(gun.transform.rotation) * gunHandlePoint.rotation
            : Quaternion.identity;
        Quaternion targetRot = gunHandlePoint != null
            ? gunHandSocket.rotation * Quaternion.Inverse(rotOffset)
            : gunHandSocket.rotation * Quaternion.Euler(handLocalRotation);

        // Handle position gun ke local space mein (constant)
        Vector3 localHandle = gunHandlePoint != null
            ? gun.transform.InverseTransformPoint(gunHandlePoint.position)
            : Vector3.zero;

        Vector3 startPos = gun.transform.position;
        Quaternion startRot = gun.transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            // Target position har frame update karo (socket hand ke saath move karta hai)
            Vector3 targetPos = gunHandlePoint != null
                ? gunHandSocket.position - (targetRot * localHandle)
                : gunHandSocket.TransformPoint(handLocalPosition);

            gun.transform.position = Vector3.Lerp(startPos, targetPos, t);
            gun.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        // Final precise snap + parent
        SwitchGunToHand();
    }

    public void SwitchGunToHolster()
    {
        if (currentState != GunState.Unequipping) return;
        
        Debug.Log("[Gun] Switched To Holster");
        AttachToSocket(gunHolsterSocket, holsterLocalPosition, holsterLocalRotation);
    }

    public void OnEquipAnimationFinished()
    {
        if (currentState != GunState.Equipping) return;
        
        Debug.Log("[Gun] Equip Finished");
        currentState = GunState.Equipped;
        if (playerController != null) playerController.SyncWeaponStateFromExternal(2, false);
    }

    public void OnUnequipAnimationFinished()
    {
        if (currentState != GunState.Unequipping) return;
        
        Debug.Log("[Gun] Unequip Finished");
        if (useAnimationEvents)
        {
            // playerAnimator.SetInteger("WeaponState", 0);
        }
        currentState = GunState.Holstered;
        if (playerController != null)
            playerController.SyncWeaponStateFromExternal(0, false);
        if (playerAnimator != null)
            playerAnimator.SetInteger(HashWeaponState, 0);
    }

    private void AttachToSocket(Transform socket, Vector3 localPos, Vector3 localRot)
    {
        if (gun == null || socket == null) return;
        gun.transform.SetParent(socket);
        gun.transform.localPosition = localPos;
        gun.transform.localEulerAngles = localRot;
    }
}
