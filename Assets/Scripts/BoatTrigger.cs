using UnityEngine;

/// <summary>
/// Legacy BoatTrigger component.
/// Proximity detection and boarding are now handled directly by BoatSystem via boatNearPoint.
/// This component is kept for scene compatibility without conflicts.
/// </summary>
public class BoatTrigger : MonoBehaviour
{
    public GameObject driveBoatUI;
    public GameObject collectOarUI;
    public OarPickup oarPickup;
    public BoatSystem boatSystem;

    private void Awake()
    {
        // Hand over UI references to BoatSystem if unassigned there
        if (boatSystem == null)
            boatSystem = BoatSystem.Instance ?? FindFirstObjectByType<BoatSystem>();

        if (boatSystem != null)
        {
            if (boatSystem.driveBoatUI == null && driveBoatUI != null)
                boatSystem.driveBoatUI = driveBoatUI;
            if (boatSystem.collectOarUI == null && collectOarUI != null)
                boatSystem.collectOarUI = collectOarUI;
            if (boatSystem.oarPickup == null && oarPickup != null)
                boatSystem.oarPickup = oarPickup;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (boatSystem == null) boatSystem = BoatSystem.Instance ?? FindFirstObjectByType<BoatSystem>();
            if (boatSystem != null) boatSystem.SetPlayerNear(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (boatSystem == null) boatSystem = BoatSystem.Instance ?? FindFirstObjectByType<BoatSystem>();
            if (boatSystem != null) boatSystem.SetPlayerNear(false);
        }
    }

    public void OnPlayerExitedBoat()
    {
        if (boatSystem != null) boatSystem.SetPlayerNear(false);
    }
}
