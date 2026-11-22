using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HouseLightTrigger : MonoBehaviour
{
    [SerializeField] private Light[] houseLights;

    private readonly HashSet<Collider> _playerColliders = new HashSet<Collider>();

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (!trigger.isTrigger)
        {
            Debug.LogError($"[{nameof(HouseLightTrigger)}] The house light collider must have Is Trigger enabled.", this);
            enabled = false;
            return;
        }

        if (houseLights == null || houseLights.Length == 0)
            Debug.LogError($"[{nameof(HouseLightTrigger)}] Assign the lights controlled by this house trigger.", this);

        SetLights(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        _playerColliders.Add(other);
        SetLights(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        _playerColliders.Remove(other);
        if (_playerColliders.Count == 0)
            SetLights(false);
    }

    private void OnDisable()
    {
        _playerColliders.Clear();
        SetLights(false);
    }

    private void SetLights(bool isOn)
    {
        if (houseLights == null)
            return;

        foreach (Light houseLight in houseLights)
        {
            if (houseLight != null)
                houseLight.enabled = isOn;
        }
    }
}
