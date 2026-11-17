using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PlatePickup / ObjectPickup
/// -------------------------------------------------------------------------
/// Jab player trigger area mein enter kare:
///   1. "Press E to Pick Up [Object Name]" HUD prompt show karta hai.
///   2. Player 'E' dabaye toh:
///      - Player freeze hota hai aur object ki taraf face karta hai.
///      - Animator par 'PickupObject' animation chalti hai.
///      - Jab haath animation mein neeche object ke paas pahuche, object player ke RightHand ke 'PlateHandSocket' mein attach ho jata hai.
///      - CRITICAL: Object ka ground visual size (scale) bilkul exact wahi rehta hai jo ground pe tha.
///      - Character khade hone ke baad player unfreeze hota hai.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Maze/Plate Pickup")]
public class PlatePickup : MonoBehaviour
{
    // ── Static Instance / Global Tracking ────────────────────────────────────
    public static PlatePickup CurrentHeldPlate { get; private set; }
    public static event System.Action<PlatePickup> OnPlatePickedUp;
    public static event System.Action<PlatePickup> OnPlatePlaced;

    [Header("Plate Identity")]
    [Tooltip("Plate ka ID (1 to 8). Agar 0 ho toh GameObject name se auto-detect hoga.")]
    public int plateID = 0;

    [Tooltip("Prompt UI mein dikhane wala naam (agar blank ho toh GameObject name use hoga).")]
    public string objectDisplayName = "";

    [Header("UI Prompt")]
    [Tooltip("Custom prompt text override (default: '[E] Pick Up <Name>')")]
    public string customPromptText = "";

    [Tooltip("Optional custom UI Canvas/Panel (agar scene mein bana ho)")]
    public GameObject pickupPromptUI;

    [Header("Hand Socket Attachment")]
    [Tooltip("Player ke RightHand mein bana 'PlateHandSocket' empty object. Agar null ho toh auto-find karega.")]
    public Transform plateHandSocket;

    [Tooltip("PlateHandSocket pe local position offset")]
    public Vector3 localHoldOffset = Vector3.zero;

    [Tooltip("PlateHandSocket pe local rotation (Euler angles)")]
    public Vector3 localHoldRotation = Vector3.zero;

    [Header("Animation Settings")]
    [Tooltip("Animator trigger parameter ka naam")]
    public string pickupAnimTrigger = "PickupObject";

    [Tooltip("Pickup animation mein jab haath object tak pahuche (normalized time: 0.35 - 0.50)")]
    [Range(0.2f, 0.8f)]
    public float grabNormalizedTime = 0.44f;

    [Tooltip("Hand socket aur object ke beech max distance threshold jahan grab trigger ho sake")]
    public float grabDistanceThreshold = 0.45f;

    [Header("Audio (Optional)")]
    public AudioClip pickupSound;

    // ── Private Runtime State ────────────────────────────────────────────────
    private bool _playerInRange = false;
    private bool _isPickedUp = false;
    private bool _isPickingUp = false;
    private bool _isPlaced = false;

    private PlayerController _playerController;
    private Animator _playerAnimator;
    private Vector3 _originalWorldScale;

    // ── Public Accessors ─────────────────────────────────────────────────────
    public bool IsPickedUp => _isPickedUp;
    public bool IsPickingUp => _isPickingUp;
    public bool IsPlaced => _isPlaced;
    public Vector3 OriginalWorldScale => _originalWorldScale;

    private void Awake()
    {
        // Auto detect ID from name if not specified
        if (plateID <= 0)
        {
            var match = System.Text.RegularExpressions.Regex.Match(gameObject.name, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int parsedId))
            {
                plateID = parsedId;
            }
        }

        if (string.IsNullOrEmpty(objectDisplayName))
        {
            objectDisplayName = gameObject.name;
        }
    }

    private void Start()
    {
        // Record the EXACT original ground scale in world space
        _originalWorldScale = transform.lossyScale;
        if (_originalWorldScale == Vector3.zero)
        {
            _originalWorldScale = transform.localScale;
        }

        // Pre-cache player references early
        CachePlayerReferences(null);

        // Ensure collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (pickupPromptUI != null)
        {
            pickupPromptUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (_isPickedUp || _isPickingUp || _isPlaced) return;

        if (_playerInRange)
        {
            // Player movement / sequence check
            if (_playerController != null && (_playerController.IsDead || _playerController.isFrozen || _playerController.IsSequenceRunning))
                return;

            // Check if player is already holding a plate
            if (CurrentHeldPlate != null && CurrentHeldPlate != this)
                return;

            // Check E key
            bool ePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                ePressed = true;
#endif
            if (Input.GetKeyDown(KeyCode.E))
                ePressed = true;

            if (ePressed)
            {
                StartCoroutine(PickupSequence());
            }
        }
    }

    // ── Trigger Detection ────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp || _isPickingUp || _isPlaced) return;
        if (!IsPlayerCollider(other)) return;

        CachePlayerReferences(other.gameObject);

        _playerInRange = true;
        if (pickupPromptUI != null)
        {
            pickupPromptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;

        _playerInRange = false;
        if (pickupPromptUI != null)
        {
            pickupPromptUI.SetActive(false);
        }
    }

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.transform.root != null && col.transform.root.CompareTag("Player")) return true;
        if (col.GetComponentInParent<PlayerController>() != null) return true;
        return false;
    }

    private void CachePlayerReferences(GameObject playerObj)
    {
        if (_playerController == null && playerObj != null)
            _playerController = playerObj.GetComponentInParent<PlayerController>() ?? playerObj.GetComponent<PlayerController>();

        if (_playerController == null)
            _playerController = PlayerController.Instance ?? FindFirstObjectByType<PlayerController>();

        if (_playerAnimator == null)
            _playerAnimator = (playerObj != null ? playerObj.GetComponentInChildren<Animator>() : null) ?? 
                              (_playerController != null ? _playerController.GetComponentInChildren<Animator>() : null);

        if (plateHandSocket == null && _playerController != null)
        {
            plateHandSocket = FindPlateHandSocket(_playerController.transform.root);
        }
    }

    /// <summary>
    /// Check karta hai ki player ke haath mein koi bhi weapon equipped hai ya nahi.
    /// </summary>
    public bool IsPlayerHoldingAnyWeapon()
    {
        if (_playerController == null)
            _playerController = PlayerController.Instance ?? FindFirstObjectByType<PlayerController>();

        if (_playerController != null)
        {
            if (_playerController.IsHoldingWeapon || _playerController.WeaponIndex != 0)
                return true;
        }

        // Direct system checks
        var swordSys = (_playerController != null) ? _playerController.GetComponentInChildren<SwordEquipSystem>() : FindFirstObjectByType<SwordEquipSystem>();
        if (swordSys != null && (swordSys.CurrentState == SwordEquipSystem.SwordState.Equipped || swordSys.CurrentState == SwordEquipSystem.SwordState.Unequipping))
            return true;

        var gunSys = (_playerController != null) ? _playerController.GetComponentInChildren<GunEquipSystem>() : FindFirstObjectByType<GunEquipSystem>();
        if (gunSys != null && (gunSys.CurrentState == GunEquipSystem.GunState.Equipped || gunSys.CurrentState == GunEquipSystem.GunState.Unequipping))
            return true;

        var torchSys = (_playerController != null) ? _playerController.GetComponentInChildren<TorchInteractionSystem>() : FindFirstObjectByType<TorchInteractionSystem>();
        if (torchSys != null && (torchSys.currentState == TorchInteractionSystem.TorchState.Equipped || torchSys.currentState == TorchInteractionSystem.TorchState.Unequipping))
            return true;

        return false;
    }

    // ── Pickup Sequence ──────────────────────────────────────────────────────
    private IEnumerator PickupSequence()
    {
        _isPickingUp = true;
        _playerInRange = false;

        if (pickupPromptUI != null)
        {
            pickupPromptUI.SetActive(false);
        }

        // Cache references
        CachePlayerReferences(null);

        // STEP 1: Pehle player movement freeze karo (aur ground position lock karo)
        if (_playerController != null)
        {
            _playerController.SetFrozen(true, lockWorldPosition: true);
        }

        // STEP 2: Agar player ke haath mein koi bhi weapon hai:
        //         Pehle uska UNEQUIP animation POORA COMPLETE chalne do!
        if (IsPlayerHoldingAnyWeapon())
        {
            Debug.Log($"[PlatePickup] Weapon detected in hand! Playing unequip animation first...");
            if (_playerController != null)
            {
                yield return _playerController.UnequipCurrentWeaponForPickup();
            }
            else
            {
                yield return new WaitForSeconds(1.2f);
            }

            // Unequip animation finish hone ke baad thoda settle hone ka wait karo
            yield return new WaitForSeconds(0.15f);
        }

        // STEP 3: Smoothly object ki taraf face karo
        if (_playerController != null)
        {
            Vector3 lookDir = transform.position - _playerController.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
                Quaternion startRot = _playerController.transform.rotation;
                float rotTime = 0f;
                while (rotTime < 0.15f)
                {
                    rotTime += Time.deltaTime;
                    _playerController.transform.rotation = Quaternion.Slerp(startRot, targetRot, rotTime / 0.15f);
                    yield return null;
                }
                _playerController.transform.rotation = targetRot;
            }
        }

        // Ensure socket is found
        if (plateHandSocket == null && _playerController != null)
        {
            plateHandSocket = FindPlateHandSocket(_playerController.transform.root);
        }

        // STEP 4: USKE BAAD: PickupObject animation chalao!
        Debug.Log($"[PlatePickup] Unequip complete. Now playing {pickupAnimTrigger} animation...");
        int pickupHash = Animator.StringToHash(pickupAnimTrigger);
        if (_playerAnimator != null)
        {
            _playerAnimator.applyRootMotion = false;
            _playerAnimator.ResetTrigger(pickupAnimTrigger);
            _playerAnimator.CrossFadeInFixedTime(pickupAnimTrigger, 0.15f, 0, 0f);
        }

        // Wait 1 frame for Animator state evaluation
        yield return null;

        // STEP 5: Dynamically wait karo jab tak haath object ke paas neeche na pahuche
        float timer = 0f;
        const float maxWait = 3.5f;
        bool grabbed = false;

        while (timer < maxWait && !grabbed)
        {
            timer += Time.deltaTime;

            if (_playerAnimator != null)
            {
                AnimatorStateInfo info = _playerAnimator.GetCurrentAnimatorStateInfo(0);
                bool inState = (info.shortNameHash == pickupHash) ||
                               (_playerAnimator.GetNextAnimatorStateInfo(0).shortNameHash == pickupHash);

                if (inState)
                {
                    float normTime = (info.shortNameHash == pickupHash) ? info.normalizedTime : 0f;
                    float distToHand = (plateHandSocket != null) ? Vector3.Distance(plateHandSocket.position, transform.position) : float.MaxValue;

                    // Jab normalized time threshold cross ho ya haath bilkul paas ho
                    if (normTime >= grabNormalizedTime || (normTime >= 0.32f && distToHand <= grabDistanceThreshold))
                    {
                        grabbed = true;
                        break;
                    }
                }
            }
            else
            {
                if (timer >= 0.75f)
                {
                    grabbed = true;
                    break;
                }
            }

            yield return null;
        }

        // STEP 6: Object ko player ke right hand ke PlateHandSocket mein attach karo
        AttachToHandSocket();

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        // STEP 7: Wait karo jab tak character khada na ho jaye (animation finish)
        float finishTimer = 0f;
        const float maxFinishTime = 3.0f;
        while (finishTimer < maxFinishTime)
        {
            finishTimer += Time.deltaTime;

            if (_playerAnimator != null)
            {
                AnimatorStateInfo info = _playerAnimator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == pickupHash && info.normalizedTime >= 0.92f)
                    break;
                if (info.shortNameHash != pickupHash && finishTimer > 0.35f)
                    break;
            }
            else
            {
                if (finishTimer >= 0.8f) break;
            }

            yield return null;
        }

        // STEP 8: Player movement unfreeze karo
        if (_playerController != null)
        {
            _playerController.SetFrozen(false);
        }

        _isPickingUp = false;
        _isPickedUp = true;
        CurrentHeldPlate = this;
        PlateStandInteraction.MarkPlateCollected(plateID);
        OnPlatePickedUp?.Invoke(this);

        Debug.Log($"[PlatePickup] '{name}' successfully picked up into PlateHandSocket!");
    }

    // ── Attach Logic with Exact Scale Preservation ───────────────────────────
    private void AttachToHandSocket()
    {
        if (plateHandSocket == null)
        {
            Debug.LogError($"[PlatePickup] Cannot attach '{name}': PlateHandSocket not found!");
            return;
        }

        // Collider disable karo taaki trigger ya physics interfere na kare
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Socket mein parent karo
        transform.SetParent(plateHandSocket, false);
        transform.localPosition = localHoldOffset;
        transform.localRotation = Quaternion.Euler(localHoldRotation);

        // ── SCALE PRESERVATION ────────────────────────────────────────────────
        // Plate ka world scale wahi rahega jo ground pe tha.
        // Child's localScale = DesiredWorldScale / Socket's LossyScale
        Vector3 socketLossy = plateHandSocket.lossyScale;
        Vector3 preservedLocalScale = new Vector3(
            Mathf.Approximately(socketLossy.x, 0f) ? _originalWorldScale.x : (_originalWorldScale.x / socketLossy.x),
            Mathf.Approximately(socketLossy.y, 0f) ? _originalWorldScale.y : (_originalWorldScale.y / socketLossy.y),
            Mathf.Approximately(socketLossy.z, 0f) ? _originalWorldScale.z : (_originalWorldScale.z / socketLossy.z)
        );

        transform.localScale = preservedLocalScale;
    }

    // ── Stand Placement Support ──────────────────────────────────────────────
    /// <summary>
    /// Stand par plate place karne ke liye call kiya ja sakta hai.
    /// </summary>
    public void PlaceOnStand(Transform socket, Vector3 localOffset = default, Vector3 localRot = default)
    {
        if (socket == null) return;

        transform.SetParent(socket, false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.Euler(localRot);

        // Preserve world scale on stand socket too
        Vector3 socketLossy = socket.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Approximately(socketLossy.x, 0f) ? _originalWorldScale.x : (_originalWorldScale.x / socketLossy.x),
            Mathf.Approximately(socketLossy.y, 0f) ? _originalWorldScale.y : (_originalWorldScale.y / socketLossy.y),
            Mathf.Approximately(socketLossy.z, 0f) ? _originalWorldScale.z : (_originalWorldScale.z / socketLossy.z)
        );

        _isPickedUp = false;
        _isPlaced = true;
        if (CurrentHeldPlate == this)
        {
            CurrentHeldPlate = null;
        }

        OnPlatePlaced?.Invoke(this);

        Debug.Log($"[PlatePickup] '{name}' placed onto stand socket '{socket.name}'!");
    }

    // ── OnGUI Prompt ─────────────────────────────────────────────────────────
    private void OnGUI()
    {
        if (_playerInRange && !_isPickedUp && !_isPickingUp && !_isPlaced)
        {
            if (CurrentHeldPlate != null && CurrentHeldPlate != this)
                return;

            string prompt = !string.IsNullOrEmpty(customPromptText)
                ? customPromptText
                : $"[E] Pick Up {objectDisplayName}";

            GameHUDPrompt.DrawPromptBox(prompt);
        }
    }

    // ── Socket Finder Utility ────────────────────────────────────────────────
    public static Transform FindPlateHandSocket(Transform root)
    {
        if (root == null) return null;

        Transform socket = FindChildRecursive(root, "PlateHandSocket");
        if (socket != null) return socket;

        // Fallback: RightHand bone
        Transform rightHand = FindChildRecursive(root, "RightHand");
        if (rightHand != null) return rightHand;

        return null;
    }

    private static Transform FindChildRecursive(Transform current, string targetName)
    {
        if (current.name == targetName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindChildRecursive(current.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
