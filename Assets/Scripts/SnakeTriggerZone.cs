using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HumanSnakeEnemy ke trigger area ke liye helper.
/// Is script ko us GameObject par attach karo jis par Trigger Collider hai.
/// HumanSnakeEnemy.cs isko automatically find karke attach kar deta hai.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SnakeTriggerZone : MonoBehaviour
{
    public bool IsPlayerInside { get; private set; }
    private readonly HashSet<Collider> _playerColliders = new HashSet<Collider>();
    private HumanSnakeEnemy _owner;

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void SetOwner(HumanSnakeEnemy owner)
    {
        _owner = owner;
        if (IsPlayerInside)
            _owner.OnPlayerEnteredTerritory();
    }

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
            RegisterPlayerCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (CheckIsPlayer(other))
            UnregisterPlayerCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (CheckIsPlayer(other))
            RegisterPlayerCollider(other);
    }

    private void RegisterPlayerCollider(Collider playerCollider)
    {
        if (!_playerColliders.Add(playerCollider))
            return;

        IsPlayerInside = true;
        _owner?.OnPlayerEnteredTerritory();
    }

    private void UnregisterPlayerCollider(Collider playerCollider)
    {
        _playerColliders.Remove(playerCollider);
        IsPlayerInside = _playerColliders.Count > 0;
    }
}
