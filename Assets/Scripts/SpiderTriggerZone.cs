using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SpiderTrigger collider par automatically attach hota hai aur player presence ko track karta hai.
/// Dedicated file so Unity can properly serialize it and assign a valid MonoScript GUID.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpiderTriggerZone : MonoBehaviour
{
    private readonly HashSet<Collider> _collidersInside = new HashSet<Collider>();

    public bool IsPlayerInside
    {
        get
        {
            _collidersInside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            return _collidersInside.Count > 0;
        }
    }

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            _collidersInside.Add(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other))
        {
            _collidersInside.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            _collidersInside.Remove(other);
        }
    }

    private bool IsPlayer(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponent<PlayerController>() != null || col.GetComponentInParent<PlayerController>() != null) return true;
        return false;
    }

    private void OnDisable()
    {
        _collidersInside.Clear();
    }
}
