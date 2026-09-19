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

    [Tooltip("Animator Trigger name for Equipping")]
    public string equipTrigger = "Equip";
    
    [Tooltip("Animator Trigger name for Unequipping")]
    public string unequipTrigger = "Unequip";

    private GunState currentState = GunState.Ground;
    private PlayerController playerController;
    
    public GunState CurrentState => currentState;
    public bool IsGunEquipped => currentState == GunState.Equipped;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
    }

    // Update method hata diya gaya hai taaki PlayerController input handle kare

    public void PickupGunFromGround()
    {
        if (currentState != GunState.Ground) return;
        
        Debug.Log("[Gun] Pickup");
        
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

        currentState = GunState.Holstered;
        AttachToSocket(gunHolsterSocket, holsterLocalPosition, holsterLocalRotation);
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
        if (playerController != null) playerController.SyncWeaponStateFromExternal(0, false);
    }

    private void AttachToSocket(Transform socket, Vector3 localPos, Vector3 localRot)
    {
        if (gun == null || socket == null) return;
        gun.transform.SetParent(socket);
        gun.transform.localPosition = localPos;
        gun.transform.localEulerAngles = localRot;
    }
}
