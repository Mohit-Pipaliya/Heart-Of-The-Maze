using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class TorchInteractionSystem : MonoBehaviour
{
    public enum TorchState { OnGround, Grabbing, Placed, Equipping, Equipped, Unequipping }
    
    [Header("Current State")]
    public TorchState currentState = TorchState.OnGround;

    [Header("Object References")]
    [Tooltip("The actual Torch model/object that moves")]
    public GameObject torchObject;
    [Tooltip("The empty object ON THE TORCH where the hand should grip it")]
    public Transform torchGrabPoint;
    
    [Space(5)]
    [Tooltip("The Lighter object (right hand)")]
    public GameObject lighterObject;
    [Tooltip("The Lighter's lid (dhakkan) object. Will be turned off at start.")]
    public GameObject lighterLidObject;
    [Tooltip("The empty object on player belt/pocket where lighter is kept when not used")]
    public Transform lighterHolsterPoint;
    
    [Header("Mount Points (Empty Objects)")]
    [Tooltip("Empty object on the Player's Left Hand for the Torch")]
    public Transform playerHandTorchPoint;
    [Tooltip("Empty object on the Player's Right Hand for the Lighter")]
    public Transform playerHandLighterPoint;
    [Tooltip("Empty object on the player's body (back/belt) where torch is placed after grabbing")]
    public Transform playerBodyTorchHolster;
    
    [Header("VFX / Fire")]
    [Tooltip("The RealisticTorchFire particle system we created earlier")]
    public GameObject torchFireVFX;
    [Tooltip("The fire particle system on the lighter")]
    public GameObject lighterFireVFX;
    
    [Header("UI")]
    [Tooltip("UI prompt 'Press E to Grab'")]
    public GameObject pickupPromptUI;
    
    [Header("Timings (Match with animations)")]
    [Tooltip("Delay before torch snaps to hand during grab anim")]
    public float grabToHandDelay = 0.8f;
    [Tooltip("Total duration of grab anim before it is placed on the wall")]
    public float totalGrabDuration = 2.0f;
    
    [Space(10)]
    [Tooltip("Delay before lighter appears in right hand")]
    public float lighterAppearDelay = 0.1f;
    [Tooltip("Delay before torch snaps to left hand during equip anim")]
    public float equipTorchToHandDelay = 0.5f;
    [Tooltip("Delay before lighter flame transfers to torch")]
    public float lightTorchDelay = 1.2f;
    [Tooltip("Total duration of equip anim before returning to idle")]
    public float totalEquipDuration = 2.0f;
    
    [Space(5)]
    [Tooltip("Time delay before torch is put on the belt during Unequip (Adjust this to match when the hand reaches back)")]
    public float unequipHolsterDelay = 0.5f;

    [Header("Cinematic Camera")]
    public bool useCinematicCamera = true;
    [Tooltip("Distance from player (lower = closer)")]
    public float cinematicDistance = 1.2f;
    [Tooltip("Offset of the camera relative to player")]
    public Vector3 cinematicOffset = new Vector3(0f, 1.1f, 0f);
    [Tooltip("Angle relative to player (180 = full front)")]
    public float cinematicYaw = 180f; 
    [Tooltip("Vertical angle of the camera")]
    public float cinematicPitch = 5f;

    [Header("Player References")]
    public Animator playerAnimator;
    public PlayerController playerController;

    private bool playerInRange = false;

    private void Start()
    {
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
        if (lighterObject != null) 
        {
            // Move lighter to holster point instead of hiding it
            MoveObjectTo(lighterObject, lighterHolsterPoint); 
        }
        if (lighterLidObject != null) 
        {
            // Lid closed initially (Z = 180)
            lighterLidObject.transform.localEulerAngles = new Vector3(lighterLidObject.transform.localEulerAngles.x, lighterLidObject.transform.localEulerAngles.y, 180f); 
        }
        
        // Turn off torch fire initially since it's unlit
        if (torchFireVFX != null) torchFireVFX.SetActive(false);
        if (lighterFireVFX != null) lighterFireVFX.SetActive(false);
        
        GetComponent<Collider>().isTrigger = true;
    }

    private void Update()
    {
        // 1. Grab Phase (Press E)
        if (playerInRange && currentState == TorchState.OnGround)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                StartCoroutine(GrabRoutine());
            }
        }
    }

    private IEnumerator GrabRoutine()
    {
        currentState = TorchState.Grabbing;
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);

        // Tell Animator to play the Grab animation
        if (playerAnimator != null) playerAnimator.SetTrigger("GrabTorch");

        // Wait until player's hand reaches the torch in the animation
        yield return new WaitForSeconds(grabToHandDelay);
        
        // Snap torch to player's hand empty object, using the grab point offset
        MoveObjectTo(torchObject, playerHandTorchPoint, torchGrabPoint);

        // Wait for the rest of the grab animation to finish
        yield return new WaitForSeconds(totalGrabDuration - grabToHandDelay);

        // After grabbing, instantly place it at the designated world spot
        MoveObjectTo(torchObject, playerBodyTorchHolster, torchGrabPoint);
        // Register to PlayerController so it knows we have a torch
        if (playerController != null) playerController.RegisterTorch(this);
        
        currentState = TorchState.Placed;
    }

    public void StartEquip()
    {
        if (currentState == TorchState.Placed)
            StartCoroutine(EquipRoutine());
    }

    public void StartUnequip()
    {
        if (currentState != TorchState.Placed && currentState != TorchState.Unequipping)
            StartCoroutine(UnequipRoutine());
    }

    private IEnumerator EquipRoutine()
    {
        currentState = TorchState.Equipping;

        // Tell Animator to play Equip animation and set WeaponState to 3 (Torch Locomotion)
        if (playerAnimator != null) 
        {
            playerAnimator.SetInteger("WeaponState", 3);
            playerAnimator.ResetTrigger("EquipTorch"); // Prevent trigger getting stuck
            playerAnimator.SetTrigger("EquipTorch");
        }
        
        if (playerController != null)
        {
            // Sync with PlayerController so it knows we are busy equipping a new weapon
            playerController.SyncWeaponStateFromExternal(3, true);
        }

        CameraController cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (cam != null && useCinematicCamera)
        {
            cam.StartCinematic(cinematicDistance, cinematicOffset, cinematicYaw, cinematicPitch);
        }

        // 1. Wait until right hand reaches pocket/belt, then grab lighter
        yield return new WaitForSeconds(lighterAppearDelay);
        if (lighterObject != null)
        {
            lighterObject.SetActive(true);
            MoveObjectTo(lighterObject, playerHandLighterPoint);
        }
        // Lid opens instantly when it comes to hand (Z = 0)
        if (lighterLidObject != null) 
        {
            lighterLidObject.transform.localEulerAngles = new Vector3(lighterLidObject.transform.localEulerAngles.x, lighterLidObject.transform.localEulerAngles.y, 0f);
        }
        // Lighter flame turns on instantly
        if (lighterFireVFX != null) lighterFireVFX.SetActive(true);

        // 2. Wait for left hand to reach the placed torch
        float waitForTorch = equipTorchToHandDelay - lighterAppearDelay;
        if (waitForTorch > 0) yield return new WaitForSeconds(waitForTorch);

        // 3. Snap torch to left hand empty object
        MoveObjectTo(torchObject, playerHandTorchPoint, torchGrabPoint);

        // 4. Wait for lighter to touch torch to light it
        float waitForLight = lightTorchDelay - equipTorchToHandDelay;
        if (waitForLight > 0) yield return new WaitForSeconds(waitForLight);

        // 5. Transfer Fire: Turn on Torch Fire, Turn off Lighter Fire
        if (torchFireVFX != null) torchFireVFX.SetActive(true);
        if (lighterFireVFX != null) lighterFireVFX.SetActive(false);
        // Close the lid (Z = 180) instantly after lighting the torch
        if (lighterLidObject != null) 
        {
            lighterLidObject.transform.localEulerAngles = new Vector3(lighterLidObject.transform.localEulerAngles.x, lighterLidObject.transform.localEulerAngles.y, 180f); 
        }

        // 6. Wait for equip animation to fully end
        float waitToEnd = totalEquipDuration - lightTorchDelay;
        if (waitToEnd > 0) yield return new WaitForSeconds(waitToEnd);

        // Put away lighter in holster after lighting
        if (lighterObject != null) 
        {
            MoveObjectTo(lighterObject, lighterHolsterPoint);
        }

        if (cam != null && useCinematicCamera)
        {
            cam.StopCinematic();
        }

        // 7. FORCE ANIMATOR TO LOCOMOTION (Bulletproof fix for freezing/stuck animation)
        if (playerAnimator != null)
        {
            playerAnimator.SetInteger("WeaponState", 3);
            playerAnimator.CrossFade("TorchLocomotion", 0.2f);
        }

        currentState = TorchState.Equipped;
        if (playerController != null)
        {
            // Done equipping, idle starts naturally via Blend Tree because WeaponState is 3
            playerController.SyncWeaponStateFromExternal(3, false); 
        }
    }

    private IEnumerator UnequipRoutine()
    {
        currentState = TorchState.Unequipping;
        if (playerController != null) playerController.SyncWeaponStateFromExternal(3, true);

        // Turn off fire immediately
        if (torchFireVFX != null) torchFireVFX.SetActive(false);
        if (lighterFireVFX != null) lighterFireVFX.SetActive(false); // Make sure lighter is off too
        
        // Wait for hand to go down to idle position (controlled via Inspector)
        yield return new WaitForSeconds(unequipHolsterDelay);
        
        // FORCE ANIMATOR BACK TO UNARMED
        if (playerAnimator != null)
        {
            playerAnimator.SetInteger("WeaponState", 0);
            playerAnimator.CrossFade("UnarmedLocomotion", 0.2f);
        }

        // Uske baad mashal uski jagah pe (holster) jayegi
        MoveObjectTo(torchObject, playerBodyTorchHolster, torchGrabPoint);

        currentState = TorchState.Placed;
        if (playerController != null) playerController.SyncWeaponStateFromExternal(0, false);
    }

    private void MoveObjectTo(GameObject obj, Transform target, Transform grabPoint = null)
    {
        if (obj == null || target == null) return;
        
        // PREVENT PHYSICS JITTER (Hath hilna / shaking fix):
        // Disable Rigidbodies and Colliders when attaching to the player
        Rigidbody[] rbs = obj.GetComponentsInChildren<Rigidbody>();
        foreach(var rb in rbs)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        Collider[] cols = obj.GetComponentsInChildren<Collider>();
        foreach(var col in cols)
        {
            if (!col.isTrigger) col.enabled = false;
        }

        obj.transform.SetParent(target);
        
        if (grabPoint != null)
        {
            // Align rotation so grabPoint matches target
            Quaternion rotOffset = Quaternion.Inverse(obj.transform.rotation) * grabPoint.rotation;
            obj.transform.rotation = target.rotation * Quaternion.Inverse(rotOffset);
            
            // Align position so grabPoint matches target
            Vector3 posOffset = grabPoint.position - obj.transform.position;
            obj.transform.position = target.position - posOffset;
        }
        else
        {
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && currentState == TorchState.OnGround)
        {
            playerInRange = true;
            if (pickupPromptUI != null) pickupPromptUI.SetActive(true);
            
            // Auto-assign player references if they were empty
            if (playerAnimator == null) playerAnimator = other.GetComponentInChildren<Animator>();
            if (playerController == null) playerController = other.GetComponentInParent<PlayerController>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
        }
    }
}
