using UnityEngine;
using UnityEngine.InputSystem;

public class SwordEquipSystem : MonoBehaviour
{
    public enum SwordState
    {
        Ground,
        OnBack,
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

    [Header("References")]
    [Tooltip("The actual sword GameObject in the scene")]
    public GameObject sword;
    
    [Tooltip("The socket on the player's back")]
    public Transform swordBackSocket;
    
    [Tooltip("The socket in the player's hand")]
    public Transform swordHandSocket;
    
    [Tooltip("Optional: An empty object placed inside the sword at the handle to automatically align it perfectly with the hand socket.")]
    public Transform swordHandlePoint;
    
    [Tooltip("The Animator playing the equip/unequip animations")]
    public Animator playerAnimator;

    [Header("Transform Settings")]
    public Vector3 backLocalPosition = Vector3.zero;
    public Vector3 backLocalRotation = Vector3.zero;
    public Vector3 handLocalPosition = Vector3.zero;
    public Vector3 handLocalRotation = Vector3.zero;

    [Header("Animation Settings")]
    [Tooltip("If true, relies on Animation Events. If false, uses a time delay (easier!).")]
    public bool useAnimationEvents = false;
    
    [Tooltip("Time delay before the sword jumps to hand/back (if not using events). " +
             "Match this with the frame in your Equip animation when hand reaches the back socket.")]
    public float grabDelay = 0.7f;
    
    [Tooltip("Normalized time (0.0 to 1.0) of the Pickup animation when the hand reaches the ground. 0.5 is usually the middle (lowest point).")]
    [Range(0f, 1f)]
    public float grabNormalizedTime = 0.5f;
    
    [Tooltip("Fallback time delay before the weapon jumps to hand during ground pickup (used if animator is missing).")]
    public float pickupDelay = 2.4f;

    [Header("Terrain Sinking Fix")]
    [Tooltip("Lift player up to prevent animation clipping into terrain during pickup")]
    public float pickupGroundLift = 1.0f;

    [Tooltip("Animator trigger parameter for picking up from ground")]
    public string pickupTrigger = "PickupSword";

    private SwordState currentState = SwordState.Ground;
    private PlayerController playerController;
    
    public SwordState CurrentState => currentState;
    public bool IsSwordEquipped => currentState == SwordState.Equipped;

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

    /// <summary>
    /// Called by the SwordPickup script when the player picks up the sword from the ground.
    /// </summary>
    private bool isPickingUpSequence = false;

    /// <summary>
    /// Called by the SwordPickup script when the player picks up the sword from the ground.
    /// </summary>
    public void PickupSwordFromGround()
    {
        if (currentState != SwordState.Ground) return;
        
        Debug.Log("[Sword] Pickup Started");
        
        Rigidbody rb = sword.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = sword.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        isPickingUpSequence = true;
        currentState = SwordState.Equipping;

        StartCoroutine(PickupSequence());
    }

    private System.Collections.IEnumerator PickupSequence()
    {
        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
            playerAnimator.SetTrigger("PickupSword");
        }

        // Wait 1 frame to let Animator register the trigger before freezing
        yield return null;

        if (playerController != null)
            playerController.SetFrozen(true);

        // Apply ground lift
        if (playerController != null && pickupGroundLift > 0f)
        {
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.Move(Vector3.up * pickupGroundLift);
        }

        // Wait dynamically for the hand to reach the ground (approx 50% of the animation)
        if (playerAnimator != null)
        {
            int pickupHash = Animator.StringToHash("PickupSword");
            yield return WaitUntilAnimationReaches(playerAnimator, pickupHash, grabNormalizedTime, 5.0f);
        }
        else
        {
            yield return new WaitForSeconds(pickupDelay);
        }

        SwitchSwordToHand();
        
        // Wait dynamically for the character to finish the Pickup animation (stand back up)
        if (playerAnimator != null)
        {
            int pickupHash = Animator.StringToHash("PickupSword");
            yield return WaitUntilAnimationReaches(playerAnimator, pickupHash, 0.95f, 5.0f);
        }
        
        isPickingUpSequence = false;
        
        // Unfreeze player BEFORE unequip so physics doesn't sink into terrain
        if (playerController != null)
            playerController.SetFrozen(false);
        
        // Small lift to prevent sinking at the Pickup→Unequip transition
        if (playerController != null)
        {
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.Move(Vector3.up * 0.3f);
        }
        
        // MUST call StartUnequip FIRST so WeaponState is synced to 1.
        // If WeaponState is 0, the Animator will ignore the Unequip trigger!
        StartUnequip();
        
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Unequip");
        }
    }


    public void StartEquip()
    {
        Debug.Log("[Sword] Equip Started");
        currentState = SwordState.Equipping;
        
        // Sync with PlayerController so Attack logic works
        if (playerController != null) playerController.SyncWeaponStateFromExternal(1, true);

        // Animations ab PlayerController handle karega
        // playerAnimator.SetInteger("WeaponState", 1); 
        // playerAnimator.SetTrigger(equipTrigger);
        
        if (!useAnimationEvents)
        {
            StartCoroutine(EquipRoutine());
        }
    }

    public void StartUnequip()
    {
        Debug.Log("[Sword] Unequip Started");
        currentState = SwordState.Unequipping;
        
        if (playerController != null) playerController.SyncWeaponStateFromExternal(1, true);

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
        SwitchSwordToHand();
        yield return new WaitForSeconds(grabDelay); // Wait a bit more for animation to finish
        OnEquipAnimationFinished();
    }

    private System.Collections.IEnumerator UnequipRoutine()
    {
        yield return new WaitForSeconds(grabDelay);
        SwitchSwordToBack();
        yield return new WaitForSeconds(grabDelay);
        
        // WeaponState reset ab PlayerController handle karega
        // playerAnimator.SetInteger("WeaponState", 0);
        OnUnequipAnimationFinished();
    }

    /// <summary>
    /// ANIMATION EVENT: Called when hand reaches the back to grab the sword.
    /// </summary>
    public void SwitchSwordToHand()
    {
        if (currentState != SwordState.Equipping) return;
        
        Debug.Log("[Sword] Switched To Hand");
        
        if (swordHandlePoint != null)
        {
            // Parent the sword to the hand socket
            sword.transform.SetParent(swordHandSocket);
            
            // Bulletproof World-Space Rotation Alignment
            // (Target Rotation) * Inverse(Current Handle Rotation) * (Current Sword Rotation)
            sword.transform.rotation = swordHandSocket.rotation * Quaternion.Inverse(swordHandlePoint.rotation) * sword.transform.rotation;
            
            // World-Space Position Alignment
            // Offset the sword by the difference between the target socket and the current handle position
            sword.transform.position += (swordHandSocket.position - swordHandlePoint.position);
        }
        else
        {
            AttachToSocket(swordHandSocket, handLocalPosition, handLocalRotation);
        }
    }

    /// <summary>
    /// ANIMATION EVENT: Called when hand places the sword back.
    /// </summary>
    public void SwitchSwordToBack()
    {
        if (currentState != SwordState.Unequipping) return;
        
        Debug.Log("[Sword] Switched To Back");
        AttachToSocket(swordBackSocket, backLocalPosition, backLocalRotation);
    }

    /// <summary>
    /// ANIMATION EVENT: Called when the Equip animation finishes completely.
    /// </summary>
    public void OnEquipAnimationFinished()
    {
        if (currentState != SwordState.Equipping) return;
        
        Debug.Log("[Sword] Equip Finished");
        currentState = SwordState.Equipped;
        if (playerController != null) playerController.SyncWeaponStateFromExternal(1, false);
    }

    /// <summary>
    /// ANIMATION EVENT: Called when the Unequip animation finishes completely.
    /// </summary>
    public void OnUnequipAnimationFinished()
    {
        if (currentState != SwordState.Unequipping) return;
        
        Debug.Log("[Sword] Unequip Finished");
        if (useAnimationEvents)
        {
            // playerAnimator.SetInteger("WeaponState", 0); // Reset to unarmed if using events
        }
        currentState = SwordState.OnBack;
        if (playerController != null) 
        {
            playerController.SyncWeaponStateFromExternal(0, false);
            playerController.SetFrozen(false);
        }
    }

    private void AttachToSocket(Transform socket, Vector3 localPos, Vector3 localRot)
    {
        sword.transform.SetParent(socket);
        sword.transform.localPosition = localPos;
        sword.transform.localEulerAngles = localRot;
    }
}
