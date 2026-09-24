using System.Collections;
using UnityEngine;

/// <summary>
/// OarPickup — Oar ke trigger area mein player aaye to "Press E" UI aaye.
/// E dabane par pickup animation chale aur oar smoothly right hand mein aa jaye.
///
/// ── Scene Setup ─────────────────────────────────────────────────────────────
///  1. Oar GameObject ke upar is script lagao.
///  2. Is Oar GameObject pe Trigger Collider lagao (IsTrigger = true).
///  3. Inspector mein assign karo:
///       • pickupPromptUI  → "Press E to Pick Up Oar" Canvas/WorldUI
///       • playerRightHandMount → Player ke Right Hand pe rakha Empty Object
///       • playerAnimator  → Player ka Animator
///  4. Animator mein "PickupOar" trigger parameter banana padega.
///  5. Player tag "Player" hona chahiye.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OarPickup : MonoBehaviour
{
    [Header("References")]
    [Tooltip("'Press E to Pick Up Oar' UI element (Canvas/UI Panel)")]
    public GameObject pickupPromptUI;

    [Tooltip("Player ke Right Hand pe rakha Empty Object — oar yahan parent hoga")]
    public Transform playerRightHandMount;

    [Tooltip("Player ka Animator component")]
    public Animator playerAnimator;

    [Header("Pickup Animation")]
    [Tooltip("Animator mein pickup trigger ka naam")]
    public string pickupAnimTrigger = "PickupOar";

    [Tooltip("Pickup animation mein normalized time jab haath neeche ho (0-1)")]
    [Range(0f, 1f)]
    public float snapNormalizedTime = 0.5f;

    [Header("Smooth Attach")]
    [Tooltip("Oar kitne seconds mein haath tak smoothly aaye")]
    public float smoothAttachDuration = 0.35f;

    [Header("Local Transform After Pickup")]
    [Tooltip("Right hand mount pe local position")]
    public Vector3 oarLocalPosition = Vector3.zero;
    [Tooltip("Right hand mount pe local rotation (Euler)")]
    public Vector3 oarLocalRotation = Vector3.zero;

    // ── Private State ──────────────────────────────────────────────────────────
    private bool _playerInRange = false;
    private bool _isPickedUp    = false;
    private bool _isPickingUp   = false;
    private PlayerController _playerController;
    private Vector3 _originalScale;

    // ─── Public Property ───────────────────────────────────────────────────────
    public bool IsPickedUp => _isPickedUp;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Start()
    {
        _originalScale = transform.localScale;

        if (pickupPromptUI != null)
            pickupPromptUI.SetActive(false);

        // IsTrigger ensure karo
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        if (_isPickedUp || _isPickingUp) return;

        if (_playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(PickupSequence());
        }
    }

    // ─── Trigger Detection ─────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;
        if (!other.CompareTag("Player")) return;

        // Player ka animator aur controller dhundho
        if (playerAnimator == null)
            playerAnimator = other.GetComponentInChildren<Animator>();
        if (_playerController == null)
            _playerController = other.GetComponent<PlayerController>();

        _playerInRange = true;
        if (pickupPromptUI != null) pickupPromptUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    }

    // ─── Pickup Sequence ───────────────────────────────────────────────────────
    private IEnumerator PickupSequence()
    {
        _isPickingUp = true;

        // UI band karo
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);

        // Player ko freeze karo
        if (_playerController != null)
            _playerController.SetFrozen(true, lockWorldPosition: false);

        // Pickup animation trigger karo
        int triggerHash = Animator.StringToHash(pickupAnimTrigger);
        if (playerAnimator != null)
        {
            playerAnimator.ResetTrigger(triggerHash);
            playerAnimator.SetTrigger(triggerHash);
        }

        yield return null; // Ek frame wait — animation shuru ho jaye

        // Animation ke snapNormalizedTime tak wait karo
        if (playerAnimator != null)
        {
            float timer = 0f;
            const float timeout = 10f;

            // Pehle animation start hone ka wait
            while (timer < timeout)
            {
                timer += Time.deltaTime;
                bool isInState = IsInState(playerAnimator, triggerHash);
                if (isInState) break;
                yield return null;
            }

            // Ab normalizedTime tak wait
            timer = 0f;
            while (timer < timeout)
            {
                timer += Time.deltaTime;
                float nt = GetNormalizedTime(playerAnimator, triggerHash);
                if (nt >= snapNormalizedTime) break;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Oar ko smoothly right hand mount pe laao
        if (playerRightHandMount != null)
            yield return StartCoroutine(SmoothAttachToHand());
        else
            AttachToHandInstant();

        // Pickup animation khatam hone do
        if (playerAnimator != null)
        {
            float timer2 = 0f;
            const float timeout2 = 10f;
            int trigHash = Animator.StringToHash(pickupAnimTrigger);
            while (timer2 < timeout2)
            {
                timer2 += Time.deltaTime;
                float nt = GetNormalizedTime(playerAnimator, trigHash);
                if (nt >= 0.95f || !IsInState(playerAnimator, trigHash)) break;
                yield return null;
            }
        }

        // Player ko unfreeze karo
        if (_playerController != null)
            _playerController.SetFrozen(false);

        _isPickedUp  = true;
        _isPickingUp = false;

        // Oar collider disable karo (pickup ke baad trigger nahi chahiye)
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Debug.Log("[OarPickup] Oar picked up successfully!");
    }

    // ─── Smooth Attach ─────────────────────────────────────────────────────────
    private IEnumerator SmoothAttachToHand()
    {
        Vector3    startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // Rigidbody disable (agar hai)
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        float elapsed = 0f;
        while (elapsed < smoothAttachDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / smoothAttachDuration));

            // Target har frame recalculate (haath move karta hai)
            Vector3    targetPos = playerRightHandMount.TransformPoint(oarLocalPosition);
            Quaternion targetRot = playerRightHandMount.rotation * Quaternion.Euler(oarLocalRotation);

            transform.position   = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation   = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        // Final snap + parent set
        AttachToHandInstant();
    }

    private void AttachToHandInstant()
    {
        if (playerRightHandMount == null) return;

        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        transform.SetParent(playerRightHandMount);
        transform.localPosition    = oarLocalPosition;
        transform.localEulerAngles = oarLocalRotation;
    }

    // ─── Drop Oar (boat trigger se bahar nikalne par) ─────────────────────────
    /// <summary>
    /// Oar ko haath se cool smooth animation ke saath gira do.
    /// BoatSystem is method ko call karega jab player boat area se bahar jaye.
    /// </summary>
    public void DropOar(Vector3 dropPosition)
    {
        if (!_isPickedUp) return;
        StartCoroutine(DropSequence(dropPosition));
    }

    private IEnumerator DropSequence(Vector3 dropPosition)
    {
        _isPickedUp = false;

        // Parent hata do
        transform.SetParent(null);

        // Smooth drop animation
        Vector3    startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3    targetPos = dropPosition;
        Quaternion targetRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        const float dropDuration = 0.6f;
        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dropDuration));

            // Arc motion — upar uthke neeche aao
            float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.4f;
            Vector3 arcPos = Vector3.Lerp(startPos, targetPos, t);
            arcPos.y += arcHeight;

            transform.position   = arcPos;
            transform.rotation   = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        transform.position   = targetPos;
        transform.rotation   = targetRot;

        // Rigidbody restore
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }

        // Collider restore (phir se pickup ho sake)
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Debug.Log("[OarPickup] Oar dropped!");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private static bool IsInState(Animator anim, int stateHash)
    {
        for (int i = 0; i < anim.layerCount; i++)
        {
            if (anim.GetCurrentAnimatorStateInfo(i).shortNameHash == stateHash ||
                anim.GetNextAnimatorStateInfo(i).shortNameHash == stateHash)
                return true;
        }
        return false;
    }

    private static float GetNormalizedTime(Animator anim, int stateHash)
    {
        for (int i = 0; i < anim.layerCount; i++)
        {
            var info = anim.GetCurrentAnimatorStateInfo(i);
            if (info.shortNameHash == stateHash)
                return info.normalizedTime;
        }
        return 0f;
    }
}
