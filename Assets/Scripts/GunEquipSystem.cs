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

        SwitchGunToHand();

        if (playerController != null && pickupGroundLift > 0f)
            playerController.ApplyGroundLift(pickupGroundLift * 0.35f);

        isPickingUpSequence = false;

        if (playerAnimator != null)
        {
            yield return WaitUntilStateNormalizedTime(
                playerAnimator, pickupHash, pickupCompleteNormalizedTime, animWaitTimeout);

            SnapAnimatorStateToEnd(playerAnimator, pickupHash, 0.99f);
            yield return null;
            yield return null;

            currentState = GunState.Unequipping;
            if (playerController != null)
                playerController.SyncWeaponStateFromExternal(2, true);

            float unequipLift = unequipGroundLiftOverride > 0f
                ? unequipGroundLiftOverride
                : (playerController != null ? playerController.GunUnequipGroundLift : 0.6f);
            if (playerController != null && unequipLift > 0f)
                playerController.ApplyGroundLift(unequipLift);

            SetWeaponStateForUnequip(2);
            playerAnimator.ResetTrigger(HashUnequip);
            playerAnimator.SetTrigger(HashUnequip);

            yield return WaitUntilStateActive(playerAnimator, unequipGunHash, unequipStartTimeout);

            if (!IsInOrTransitioningToState(playerAnimator, unequipGunHash))
            {
                Debug.LogWarning("[Gun] Unequip trigger missed — starting UnequipGun via CrossFade.");
                SetWeaponStateForUnequip(2);
                playerAnimator.CrossFadeInFixedTime(unequipGunHash, 0.15f, 0, 0f);
                yield return null;
                yield return WaitUntilStateActive(playerAnimator, unequipGunHash, unequipStartTimeout);
            }

            yield return WaitUntilCurrentStateIs(playerAnimator, unequipGunHash, unequipStartTimeout);

            float unequipTimer = 0f;
            while (unequipTimer < animWaitTimeout)
            {
                unequipTimer += Time.deltaTime;
                if (GetStateNormalizedTime(playerAnimator, unequipGunHash) >= unequipCompleteTime)
                    break;
                yield return null;
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
