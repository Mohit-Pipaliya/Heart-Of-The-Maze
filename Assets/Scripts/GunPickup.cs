using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class GunPickup : MonoBehaviour
{
    [Header("Settings")]
    public string playerTag = "Player";
    public GameObject pickupPrompt;

    private bool playerInRange = false;
    private GunEquipSystem playerEquipSystem;

    private void Start()
    {
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
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
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }

        playerEquipSystem.PickupGunFromGround();
        this.enabled = false; 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            GunEquipSystem sys = other.GetComponentInParent<GunEquipSystem>();
            if (sys != null && sys.CurrentState == GunEquipSystem.GunState.Ground)
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
