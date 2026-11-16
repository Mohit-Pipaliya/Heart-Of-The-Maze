using UnityEngine;

/// <summary>
/// LadderTriggerZone
/// =================
/// Attach to ladder triggers to notify LadderClimbSystem on player enter/stay/exit.
/// Automatically adds a kinematic Rigidbody so CharacterController triggers it reliably.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LadderTriggerZone : MonoBehaviour
{
    public enum ZoneType
    {
        Trigger1_BottomGround,
        Trigger2_BottomMount,
        Trigger3_TopMount,
        Trigger4_TopGround
    }

    [Header("Zone Configuration")]
    public ZoneType zoneType = ZoneType.Trigger1_BottomGround;
    public LadderClimbSystem ladderSystem;

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        EnsureComponents();
        FindLadderSystem();
    }

    private void EnsureComponents()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // In Unity, CharacterController requires a Rigidbody on the trigger to fire OnTriggerEnter
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void FindLadderSystem()
    {
        if (ladderSystem != null) return;
        ladderSystem = GetComponentInParent<LadderClimbSystem>();
        if (ladderSystem == null && transform.parent != null)
            ladderSystem = transform.parent.GetComponentInChildren<LadderClimbSystem>();
        if (ladderSystem == null)
            ladderSystem = FindFirstObjectByType<LadderClimbSystem>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ladderSystem == null) FindLadderSystem();
        if (ladderSystem == null) return;

        if (IsPlayer(other))
        {
            ladderSystem.OnPlayerEnterZone(zoneType);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (ladderSystem == null) FindLadderSystem();
        if (ladderSystem == null) return;

        if (IsPlayer(other))
        {
            ladderSystem.OnPlayerEnterZone(zoneType);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (ladderSystem == null) FindLadderSystem();
        if (ladderSystem == null) return;

        if (IsPlayer(other))
        {
            ladderSystem.OnPlayerExitZone(zoneType);
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.GetComponent<PlayerController>() != null) return true;
        if (other.GetComponentInParent<PlayerController>() != null) return true;
        if (other.GetComponent<CharacterController>() != null) return true;
        return false;
    }
}
