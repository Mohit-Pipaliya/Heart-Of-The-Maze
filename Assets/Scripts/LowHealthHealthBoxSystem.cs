using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Automatically spawns an AAA-style health box 20 metres away when player health becomes low.
/// The box restores full health when the player presses E nearby.
/// </summary>
public class LowHealthHealthBoxSystem : MonoBehaviour
{
    [Header("Low Health Settings")]
    [Range(1, 100)] public int lowHealthPercent = 30;
    [Range(1f, 100f)] public float spawnDistance = 50f;
    [Range(1f, 100f)] public float healthRecoveryResetPercent = 10f;

    [Header("Spawn Search")]
    [Range(5f, 100f)] public float groundRaycastDistance = 50f;
    [Range(0f, 45f)] public float alternateDirectionAngle = 35f;

    [Header("AAA Box Size & Glow")]
    [Range(0.5f, 3f)] public float boxScaleMultiplier = 1.15f;
    [Range(1f, 12f)] public float glowIntensity = 5f;
    [Range(2f, 15f)] public float glowRange = 7f;

    private bool _hasSpawnedForLowHealth;
    private HealthBoxPickup _spawnedBox;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSystemExistsAfterSceneLoad()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[Low Health] PlayerController not found after scene load.");
            return;
        }

        if (player.GetComponent<LowHealthHealthBoxSystem>() == null)
        {
            player.gameObject.AddComponent<LowHealthHealthBoxSystem>();
            Debug.Log("[Low Health] Health-box system attached automatically.");
        }
    }

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        int currentHealth = player.Health;
        int maxHealth = player.MaxHealth;
        float lowThreshold = Mathf.Max(1f, maxHealth * (lowHealthPercent * 0.01f));

        if (currentHealth <= lowThreshold && !_hasSpawnedForLowHealth)
        {
            SpawnHealthBox(player);
        }
        else if (currentHealth > lowThreshold + maxHealth * (healthRecoveryResetPercent * 0.01f))
        {
            _hasSpawnedForLowHealth = false;
        }
    }

    private void SpawnHealthBox(PlayerController player)
    {
        if (_spawnedBox != null) return;

        Vector3 spawnPosition = FindGroundPosition(player.transform.position, player.transform.forward);
        if (!TryGetGroundPosition(spawnPosition, out spawnPosition))
            spawnPosition = player.transform.position + player.transform.forward * spawnDistance;

        GameObject root = new GameObject("Procedural Health Box");
        root.transform.position = spawnPosition;
        root.transform.rotation = Quaternion.identity;

        float playerScale = Mathf.Clamp(
            player.transform.lossyScale.y *
            (player.GetComponent<CharacterController>() != null
                ? player.GetComponent<CharacterController>().height
                : 2f) / 2f,
            1.5f, 3.5f);

        float finalScale = playerScale * boxScaleMultiplier;
        finalScale = Mathf.Clamp(finalScale, 1.5f, 2.8f);
        root.transform.localScale = Vector3.one * finalScale;
        LowHealthBoxVisual.Create(root.transform, finalScale, glowIntensity, glowRange);

        _spawnedBox = root.AddComponent<HealthBoxPickup>();
        _spawnedBox.Initialize(player);
        _hasSpawnedForLowHealth = true;
        _spawnedBox.OnCollected = () => _spawnedBox = null;

        Debug.Log($"[Low Health] Health box spawned {spawnDistance:F0}m from player.");
    }

    private Vector3 FindGroundPosition(Vector3 origin, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        Vector3[] candidates =
        {
            origin + forward * spawnDistance,
            origin + (Quaternion.Euler(0f, alternateDirectionAngle, 0f) * forward) * spawnDistance,
            origin + (Quaternion.Euler(0f, -alternateDirectionAngle, 0f) * forward) * spawnDistance,
            origin + (Quaternion.Euler(0f, 90f, 0f) * forward) * spawnDistance,
            origin + (Quaternion.Euler(0f, -90f, 0f) * forward) * spawnDistance
        };

        foreach (Vector3 candidate in candidates)
        {
            if (TryGetGroundPosition(candidate, out Vector3 grounded))
                return grounded;
        }

        return origin + forward * spawnDistance;
    }

    private bool TryGetGroundPosition(Vector3 xzPosition, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = xzPosition + Vector3.up * groundRaycastDistance;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                groundRaycastDistance * 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundedPosition = hit.point;
            return true;
        }

        groundedPosition = xzPosition;
        return false;
    }
}

public class HealthBoxPickup : MonoBehaviour
{
    [HideInInspector] public System.Action OnCollected;

    private PlayerController _player;
    private bool _playerInRange;
    private bool _collected;
    private Vector3 _startPosition;
    private Light _glowLight;
    private float _baseGlowIntensity;

    public void Initialize(PlayerController player)
    {
        _player = player;
        _startPosition = transform.position;
        _glowLight = GetComponentInChildren<Light>();
        if (_glowLight != null)
            _baseGlowIntensity = _glowLight.intensity;

        if (GetComponent<Collider>() == null)
        {
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.7f, 0f);
            trigger.size = new Vector3(2.2f, 1.8f, 2.2f);
        }
    }

    private void Update()
    {
        if (_collected) return;

        // Keep the pickup planted on the ground. Only the model accents rotate;
        // the whole box must never drift upward like a separate floating prop.
        transform.position = _startPosition;
        transform.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);

        if (_glowLight != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 3.2f) * 0.22f;
            _glowLight.intensity = _baseGlowIntensity * pulse;
        }

        if (_playerInRange && _player != null)
        {
            if (_player.IsDead || _player.isFrozen || _player.IsSequenceRunning)
                return;

            bool ePressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                            || Input.GetKeyDown(KeyCode.E);
            if (ePressed) Collect();
        }
    }

    private void Collect()
    {
        if (_collected) return;
        _collected = true;

        if (_player != null)
        {
            _player.ResetHealth();
            GameFeel.FovPunch(1.5f);
            GameAudio.PlayPickup(transform.position);
        }

        OnCollected?.Invoke();
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            _playerInRange = false;
    }

    private void OnGUI()
    {
        if (_playerInRange && !_collected)
            GameHUDPrompt.DrawPromptBox("[E] Collect Health Box  |  Restore 100% Health");
    }
}

internal static class LowHealthBoxVisual
{
    private static Shader GetShader(string preferredName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null) return shader;
        shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) return shader;
        shader = Shader.Find(preferredName);
        if (shader != null) return shader;
        shader = Shader.Find("HDRP/Lit");
        if (shader != null) return shader;
        return Shader.Find("Sprites/Default");
    }

    public static void Create(Transform root, float boxScale, float glowIntensity, float glowRange)
    {
        Shader litShader = GetShader("Standard");

        Material darkMetal = CreateMaterial(litShader,
            new Color(0.025f, 0.045f, 0.055f, 1f), 0f);
        darkMetal.SetFloat("_Metallic", 0.75f);
        darkMetal.SetFloat("_Smoothness", 0.78f);

        Material greenGlow = CreateMaterial(litShader,
            new Color(0.12f, 1f, 0.42f, 1f), 3.5f);

        CreateCube(root, "Armor Shell", new Vector3(0f, 0.72f, 0f),
            new Vector3(1.15f, 1.05f, 0.8f), darkMetal);

        CreateCube(root, "Top Panel", new Vector3(0f, 1.24f, 0f),
            new Vector3(0.96f, 0.08f, 0.65f), greenGlow);

        CreateCube(root, "Green Cross Vertical", new Vector3(0f, 0.78f, -0.406f),
            new Vector3(0.18f, 0.68f, 0.06f), greenGlow);
        CreateCube(root, "Green Cross Horizontal", new Vector3(0f, 0.78f, -0.408f),
            new Vector3(0.58f, 0.18f, 0.07f), greenGlow);

        CreateCube(root, "Gold Accent Left", new Vector3(-0.47f, 0.72f, -0.42f),
            new Vector3(0.06f, 0.68f, 0.05f),
            CreateMaterial(litShader, new Color(1f, 0.72f, 0.08f, 1f), 1.2f));
        CreateCube(root, "Gold Accent Right", new Vector3(0.47f, 0.72f, -0.42f),
            new Vector3(0.06f, 0.68f, 0.05f),
            CreateMaterial(litShader, new Color(1f, 0.72f, 0.08f, 1f), 1.2f));

        Light light = new GameObject("Health Box Glow").AddComponent<Light>();
        light.transform.SetParent(root, false);
        light.transform.localPosition = new Vector3(0f, 0.9f, -0.55f);
        light.color = new Color(0.15f, 1f, 0.45f);
        light.range = Mathf.Max(4f, glowRange);
        light.intensity = glowIntensity;

        CreateGlowHalo(root, greenGlow, boxScale);
    }

    private static void CreateGlowHalo(Transform root, Material glowMaterial, float boxScale)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Health Box Ground Glow";
        ring.transform.SetParent(root, false);
        ring.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        ring.transform.localScale = new Vector3(1.35f / boxScale, 0.018f / boxScale, 1.35f / boxScale);

        Collider collider = ring.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        Renderer renderer = ring.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = glowMaterial;
    }

    private static Material CreateMaterial(Shader shader, Color color, float emission)
    {
        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (emission > 0f)
        {
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * emission);
            if (material.HasProperty("_EmissiveColor"))
                material.SetColor("_EmissiveColor", color * emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return material;
    }

    private static void CreateCube(Transform parent, string name, Vector3 localPosition,
        Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }
}
