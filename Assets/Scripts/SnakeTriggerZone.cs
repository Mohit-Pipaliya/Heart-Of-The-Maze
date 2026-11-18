using UnityEngine;

/// <summary>
/// HumanSnakeEnemy ke trigger area ke liye helper.
/// Is script ko us GameObject par attach karo jis par Trigger Collider hai.
/// HumanSnakeEnemy.cs isko automatically find karke attach kar deta hai.
/// </summary>
public class SnakeTriggerZone : MonoBehaviour
{
    public bool IsPlayerInside { get; private set; }

    private bool CheckIsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.GetComponentInParent<PlayerController>() != null) return true;
        if (other.GetComponent<CharacterController>() != null) return true;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (CheckIsPlayer(other))
            IsPlayerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (CheckIsPlayer(other))
            IsPlayerInside = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (CheckIsPlayer(other))
            IsPlayerInside = true;
    }
}
