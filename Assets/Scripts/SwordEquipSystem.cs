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
    
    [Tooltip("Time delay before the sword jumps to hand/back (if not using events)")]
    public float grabDelay = 0.4f;

    [Tooltip("Animator Trigger name for Equipping")]
    public string equipTrigger = "Equip";
    
    [Tooltip("Animator Trigger name for Unequipping")]
    public string unequipTrigger = "Unequip";

    private SwordState currentState = SwordState.Ground;
    
    public SwordState CurrentState => currentState;
    public bool IsSwordEquipped => currentState == SwordState.Equipped;

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Press 2 to Equip the sword
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (currentState == SwordState.OnBack)
            {
                StartEquip();
            }
        }

        // Press 1 to Unequip the weapon (Go unarmed)
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (currentState == SwordState.Equipped)
            {
                StartUnequip();
            }
        }
    }

    /// <summary>
    /// Called by the SwordPickup script when the player picks up the sword from the ground.
    /// </summary>
    public void PickupSwordFromGround()
    {
        if (currentState != SwordState.Ground) return;
        
        Debug.Log("[Sword] Pickup");
        
        // Handle physical properties so it stops acting like a dropped item
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

        currentState = SwordState.OnBack;
        AttachToSocket(swordBackSocket, backLocalPosition, backLocalRotation);
    }


    private void StartEquip()
    {
        Debug.Log("[Sword] Equip Started");
        currentState = SwordState.Equipping;
        
        // Use existing triggers and parameters from your PlayerController
        playerAnimator.SetInteger("WeaponState", 1); // 1 = Sword
        playerAnimator.SetTrigger(equipTrigger);
        
        if (!useAnimationEvents)
        {
            StartCoroutine(EquipRoutine());
        }
    }

    private void StartUnequip()
    {
        Debug.Log("[Sword] Unequip Started");
        currentState = SwordState.Unequipping;
        
        playerAnimator.SetTrigger(unequipTrigger);
        
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
        
        playerAnimator.SetInteger("WeaponState", 0); // 0 = Unarmed
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
            playerAnimator.SetInteger("WeaponState", 0); // Reset to unarmed if using events
        }
        currentState = SwordState.OnBack;
    }

    private void AttachToSocket(Transform socket, Vector3 localPos, Vector3 localRot)
    {
        sword.transform.SetParent(socket);
        sword.transform.localPosition = localPos;
        sword.transform.localEulerAngles = localRot;
    }
}
