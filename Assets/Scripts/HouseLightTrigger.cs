using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HouseLightTrigger : MonoBehaviour
{
    [Tooltip("Generator puzzle that supplies power. If empty, the scene's first GeneratorLightPuzzle is used.")]
    [SerializeField] private GeneratorLightPuzzle generator;
    [SerializeField] private Light[] houseLights;

    private readonly HashSet<Collider> _playerColliders = new HashSet<Collider>();
    private bool _hasPower;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (!trigger.isTrigger)
        {
            Debug.LogError($"[{nameof(HouseLightTrigger)}] The house light collider must have Is Trigger enabled.", this);
            enabled = false;
            return;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        if (generator == null)
            generator = FindFirstObjectByType<GeneratorLightPuzzle>();

        if (houseLights == null || houseLights.Length == 0)
            Debug.LogError($"[{nameof(HouseLightTrigger)}] Assign the lights controlled by this house trigger.", this);

        SetPowerSource(generator);
    }

    public void SetPowerSource(GeneratorLightPuzzle powerSource)
    {
        if (generator == powerSource)
        {
            _hasPower = generator != null && generator.IsPowerAvailable;
            RefreshLights();
            return;
        }

        if (generator != null)
            generator.OnPowerAvailabilityChanged -= OnPowerAvailabilityChanged;

        generator = powerSource;
        if (generator != null)
        {
            generator.OnPowerAvailabilityChanged += OnPowerAvailabilityChanged;
            _hasPower = generator.IsPowerAvailable;
        }
        else
        {
            _hasPower = false;
        }

        RefreshLights();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        _playerColliders.Add(other);
        RefreshLights();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        _playerColliders.Remove(other);
        RefreshLights();
    }

    private void OnPowerAvailabilityChanged(bool hasPower)
    {
        _hasPower = hasPower;
        RefreshLights();
    }

    private void OnDisable()
    {
        _playerColliders.Clear();
        if (generator != null)
            generator.OnPowerAvailabilityChanged -= OnPowerAvailabilityChanged;
        SetLights(false);
    }

    private void OnEnable()
    {
        if (generator != null)
        {
            generator.OnPowerAvailabilityChanged -= OnPowerAvailabilityChanged;
            generator.OnPowerAvailabilityChanged += OnPowerAvailabilityChanged;
            _hasPower = generator.IsPowerAvailable;
        }
        RefreshLights();
    }

    private void Update()
    {
        // Keep the trigger correct if it is enabled after the player has already entered.
        bool nowHasPower = generator != null && generator.IsPowerAvailable;
        if (nowHasPower != _hasPower)
        {
            _hasPower = nowHasPower;
            RefreshLights();
        }
    }

    private void RefreshLights()
    {
        SetLights(_hasPower && _playerColliders.Count > 0);
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
