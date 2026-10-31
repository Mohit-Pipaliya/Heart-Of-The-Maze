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
    private PlayerController playerController;

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
            if (playerController != null && (playerController.IsDead || playerController.isFrozen || playerController.IsSequenceRunning))
                return;

            bool ePressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.E);
            if (ePressed)
            {
                GameAudio.PlayUI();
                Pickup();
            }
        }
    }

    private void Pickup()
    {
        playerInRange = false;
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }

        if (playerEquipSystem != null)
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
                playerController = other.GetComponentInParent<PlayerController>();

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
                playerController = null;

                if (pickupPrompt != null)
                {
                    pickupPrompt.SetActive(false);
                }
            }
        }
    }
}
