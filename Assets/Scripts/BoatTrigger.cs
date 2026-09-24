using System.Collections;
using UnityEngine;

/// <summary>
/// BoatTrigger — Boat ke around ek trigger zone.
/// Player andar aaye to:
///   • Oar hai → "Press B to Drive Boat" UI show karo
///   • Oar nahi hai → "Collect the Oar First" UI show karo
///
/// Player B dabaye to BoatSystem.EnterBoat() start karo.
///
/// ── Scene Setup ─────────────────────────────────────────────────────────────
///  1. Boat ke paas ek Empty GameObject banao "BoatTrigger" naam se.
///  2. Is Empty pe BoatTrigger script aur Trigger Collider lagao.
///  3. Inspector mein assign karo:
///       • driveBoatUI       → "Press B to Drive Boat" UI
///       • collectOarUI      → "Collect the Oar First" UI
///       • oarPickup         → Scene mein rakha OarPickup script
///       • boatSystem        → BoatSystem script (boat pe hai)
///  4. Player tag "Player" hona chahiye.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BoatTrigger : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("'Press B to Drive Boat' UI Panel")]
    public GameObject driveBoatUI;

    [Tooltip("'Collect the Oar First' UI Panel")]
    public GameObject collectOarUI;

    [Header("References")]
    [Tooltip("Scene mein rakha OarPickup script — oar pickup status check karne ke liye")]
    public OarPickup oarPickup;

    [Tooltip("Boat ka BoatSystem script")]
    public BoatSystem boatSystem;

    // ── Private State ──────────────────────────────────────────────────────────
    private bool _playerInside = false;
    private PlayerController _playerController;
    private float _exitCooldown = 0f;

    // ── Public: kya player is waqt trigger ke andar hai ──────────────────────
    public bool IsPlayerInside => _playerInside;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Start()
    {
        HideAllUI();
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        if (_exitCooldown > 0f)
        {
            _exitCooldown -= Time.deltaTime;
            return;
        }

        if (!_playerInside) return;
        if (boatSystem != null && boatSystem.IsOccupied) return;

        // Failsafe: Agar player trigger area se dur chala gaya aur OnTriggerExit fail ho gaya
        if (_playerController != null && Vector3.Distance(transform.position, _playerController.transform.position) > 10f)
        {
            _playerInside = false;
            HideAllUI();
            return;
        }

        // Har frame UI refresh karo (oar status change ho sakti hai)
        RefreshUI();

        // B press → boat enter karo
        if (Input.GetKeyDown(KeyCode.B))
        {
            bool hasOar = oarPickup != null && oarPickup.IsPickedUp;
            if (!hasOar)
            {
                Debug.Log("[BoatTrigger] Oar nahi hai! Pehle oar collect karo.");
                return;
            }

            HideAllUI();
            if (boatSystem != null)
                boatSystem.EnterBoat(_playerController);
        }
    }

    // ─── Trigger Detection ─────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (boatSystem != null && boatSystem.IsOccupied) return;

        _playerController = other.GetComponent<PlayerController>();
        _playerInside = true;
        RefreshUI();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;
        HideAllUI();
    }

    // ─── UI Helpers ────────────────────────────────────────────────────────────
    private void RefreshUI()
    {
        bool hasOar = oarPickup != null && oarPickup.IsPickedUp;

        if (driveBoatUI != null)  driveBoatUI.SetActive(hasOar);
        if (collectOarUI != null) collectOarUI.SetActive(!hasOar);
    }

    private void HideAllUI()
    {
        if (driveBoatUI != null)  driveBoatUI.SetActive(false);
        if (collectOarUI != null) collectOarUI.SetActive(false);
    }

    public void OnPlayerExitedBoat()
    {
        HideAllUI();
        _exitCooldown = 2.5f; // Utarne ke 2.5 second tak koi popup UI nahi aayega
        
        // Target point pe aane ke baad hi oar drop hogi
        if (oarPickup != null && oarPickup.IsPickedUp && _playerController != null)
        {
            Vector3 dropPos = _playerController.transform.position + _playerController.transform.forward * 0.5f + Vector3.up * 0.1f;
            oarPickup.DropOar(dropPos);
            Debug.Log("[BoatTrigger] Player reached exit target — Oar dropped!");
        }
    }
}
