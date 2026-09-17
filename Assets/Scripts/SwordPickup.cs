using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class SwordPickup : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The tag of the player object")]
    public string playerTag = "Player";
    
    [Tooltip("UI prompt to show when player is in range")]
    public GameObject pickupPrompt;

    private bool playerInRange = false;
    private SwordEquipSystem playerEquipSystem;

    private void Start()
    {
        // Ensure prompt is disabled at start
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }

        // Make sure collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        // Check if player is in range and presses 'E'
        if (playerInRange && playerEquipSystem != null)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                Pickup();
            }
        }
    }

    private void Pickup()
    {
        // Hide the prompt
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }

        // Trigger the pickup logic on the player
        playerEquipSystem.PickupSwordFromGround();

        // Since the sword is moved to the player's back, we disable the pickup script and trigger
        this.enabled = false; 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            SwordEquipSystem sys = other.GetComponentInParent<SwordEquipSystem>();
            if (sys != null && sys.CurrentState == SwordEquipSystem.SwordState.Ground)
            {
                playerInRange = true;
                playerEquipSystem = sys;

                if (pickupPrompt != null)
                {
                    pickupPrompt.SetActive(true);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (playerInRange)
            {
                playerInRange = false;
                playerEquipSystem = null;

                if (pickupPrompt != null)
                {
                    pickupPrompt.SetActive(false);
                }
            }
        }
    }
}
