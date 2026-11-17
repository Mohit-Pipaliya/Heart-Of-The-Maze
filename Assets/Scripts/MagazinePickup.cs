using UnityEngine;
using UnityEngine.InputSystem;

public class MagazinePickup : MonoBehaviour
{
    [Header("── Settings ──")]
    public string playerTag = "Player";

    [Tooltip("UI prompt GameObject (e.g. 'Press E to Pickup Magazine'). DO NOT assign this magazine model here!")]
    public GameObject pickupPrompt;

    [Tooltip("Floating bobbing & rotating animation in world. Disable if you placed it flat on ground.")]
    public bool enableBobbing = false;
    public float bobSpeed = 2.5f;
    public float bobHeight = 0.08f;
    public float rotateSpeed = 45f;

    private bool _playerInRange = false;
    private PlayerController _playerController;
    private Vector3 _startPos;
    private Vector3 _startLocalPos;
    private bool _isPickedUp = false;

    private void Awake()
    {
        // SAFETY GUARD 1: Prevent user from accidentally hiding their own magazine!
        // If pickupPrompt references this GameObject, its children, or its parent, CLEAR IT!
        if (pickupPrompt != null)
        {
            if (pickupPrompt == gameObject || pickupPrompt.transform.IsChildOf(transform) || transform.IsChildOf(pickupPrompt.transform))
            {
                Debug.LogWarning($"<color=yellow>[MagazinePickup]</color> 'pickupPrompt' on '{gameObject.name}' was assigned to the magazine itself! Auto-cleared to PREVENT magazine from disappearing on Play.");
                pickupPrompt = null;
            }
        }
    }

    private void Start()
    {
        _startPos = transform.position;
        _startLocalPos = transform.localPosition;

        // Auto-link UI prompt from scene if none assigned
        if (pickupPrompt == null)
        {
            Transform[] allSceneTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < allSceneTransforms.Length; i++)
            {
                if (allSceneTransforms[i] != null && allSceneTransforms[i].gameObject.scene.isLoaded)
                {
                    string tName = allSceneTransforms[i].name;
                    if (tName == "Press E to Pickup Magazine" || tName == "Press E to Pickup M4")
                    {
                        pickupPrompt = allSceneTransforms[i].gameObject;
                        break;
                    }
                }
            }
        }

        // Deactivate prompt UI initially (only if it is truly an external UI element)
        if (pickupPrompt != null && pickupPrompt != gameObject && !pickupPrompt.transform.IsChildOf(transform))
        {
            pickupPrompt.SetActive(false);
        }

        // SAFETY GUARD 2: Prevent Rigidbody from falling through ground/void
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] != null)
            {
                rbs[i].isKinematic = true;
                rbs[i].useGravity = false;
            }
        }

        // SAFETY GUARD 3: Ensure colliders are triggers so player can enter pickup range
        Collider[] allCols = GetComponentsInChildren<Collider>(true);
        if (allCols == null || allCols.Length == 0)
        {
            // If no collider exists on root or children, add a sensible BoxCollider
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                box.center = transform.InverseTransformPoint(rend.bounds.center);
                Vector3 size = rend.bounds.size;
                Vector3 lossy = transform.lossyScale;
                box.size = new Vector3(
                    lossy.x > 0.0001f ? size.x / lossy.x : 0.2f,
                    lossy.y > 0.0001f ? size.y / lossy.y : 0.3f,
                    lossy.z > 0.0001f ? size.z / lossy.z : 0.1f
                );
            }
            else
            {
                box.size = new Vector3(0.15f, 0.25f, 0.1f);
            }
        }
        else
        {
            for (int i = 0; i < allCols.Length; i++)
            {
                if (allCols[i] != null)
                {
                    allCols[i].isTrigger = true;
                }
            }
        }

        // Verify renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasVisibleRenderer = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                hasVisibleRenderer = true;
                break;
            }
        }

        Debug.Log($"<color=green>[MagazinePickup]</color> Ready on '{gameObject.name}' at World Pos: {transform.position}, Active: {gameObject.activeInHierarchy}, Renderers: {renderers.Length} (visible: {hasVisibleRenderer})");
    }

    private void Update()
    {
        if (_isPickedUp) return;

        if (enableBobbing)
        {
            if (transform.parent != null)
            {
                float newY = _startLocalPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.localPosition = new Vector3(_startLocalPos.x, newY, _startLocalPos.z);
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);
            }
            else
            {
                float newY = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = new Vector3(_startPos.x, newY, _startPos.z);
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
            }
        }

        if (_playerInRange)
        {
            if (_playerController != null && (_playerController.IsDead || _playerController.isFrozen || _playerController.IsSequenceRunning))
                return;

            bool ePressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.E);
            if (ePressed)
            {
                Pickup();
            }
        }
    }

    private void Pickup()
    {
        if (_isPickedUp) return;
        _isPickedUp = true;
        _playerInRange = false;
        enableBobbing = false;

        if (pickupPrompt != null && pickupPrompt != gameObject && !pickupPrompt.transform.IsChildOf(transform))
        {
            pickupPrompt.SetActive(false);
        }

        // Disable all colliders so it no longer blocks or triggers physics
        Collider[] allCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < allCols.Length; i++)
        {
            if (allCols[i] != null) allCols[i].enabled = false;
        }

        // Disable any duplicate/child MagazinePickup scripts
        MagazinePickup[] allPickups = GetComponentsInChildren<MagazinePickup>(true);
        for (int i = 0; i < allPickups.Length; i++)
        {
            if (allPickups[i] != null && allPickups[i] != this)
            {
                allPickups[i].enableBobbing = false;
                allPickups[i].enabled = false;
            }
        }

        GunShooter shooter = GunShooter.Instance ?? FindFirstObjectByType<GunShooter>();
        if (shooter != null)
        {
            // Pass this actual GameObject so it snaps directly to the holster socket
            // while preserving its exact size
            shooter.PickUpSpareMagazine(this.gameObject);
            Debug.Log("<color=green>[MagazinePickup]</color> M4 Magazine attached to holster socket successfully!");
        }
        else
        {
            Debug.LogWarning("[MagazinePickup] GunShooter instance not found in scene!");
        }

        this.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;

        if (other.CompareTag(playerTag) || other.GetComponentInParent<PlayerController>() != null)
        {
            _playerInRange = true;
            _playerController = other.GetComponentInParent<PlayerController>();

            if (pickupPrompt != null && pickupPrompt != gameObject && !pickupPrompt.transform.IsChildOf(transform))
            {
                pickupPrompt.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_isPickedUp) return;

        if (other.CompareTag(playerTag) || other.GetComponentInParent<PlayerController>() != null)
        {
            if (_playerInRange)
            {
                _playerInRange = false;
                _playerController = null;

                if (pickupPrompt != null && pickupPrompt != gameObject && !pickupPrompt.transform.IsChildOf(transform))
                {
                    pickupPrompt.SetActive(false);
                }
            }
        }
    }

    private void OnGUI()
    {
        if (_playerInRange && !_isPickedUp)
        {
            GameHUDPrompt.DrawPromptBox("[E] Pick Up Magazine");
        }
    }
}
