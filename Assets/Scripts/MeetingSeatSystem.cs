using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Meeting Seat System
/// ─────────────────────────────────────────────────────────────────────────────
/// SCENE SETUP:
///   1. Ek Empty GameObject banao (e.g. "MeetingTrigger") — jahan player trigger zone mein aaye.
///      Us par Box Collider lagao, "Is Trigger" ON karo.
///      Is GameObject par yeh script lagao.
///
///   2. "SeatArrivalPoint" — Seat ke saamne ek Empty GameObject jahan player khada hoga.
///
///   3. "SeatFaceTarget" — Jis direction mein player ka munh hoga jab baithega (e.g. table).
///
///   4. Player par "Player" Tag lagao.
///
///   5. Player par NavMeshAgent component lagao (Inspector mein):
///      Speed = 2 | Angular Speed = 180 | Stopping Distance = 0.2 | Auto Braking ON
///
/// ANIMATOR SETUP:
///   Parameters banao:
///     "SitDown"  → Trigger
///     "SeatIdle" → Bool
///     "StandUp"  → Trigger
///
///   Transitions:
///     Any State → SitDown State:  Condition = SitDown trigger. Has Exit Time OFF.
///     SitDown   → SeatIdle State: Has Exit Time ON. Exit Time ~0.9.
///     SeatIdle  (Loop Time ON on the clip)
///     SeatIdle  → StandUp State:  Condition = StandUp trigger. Has Exit Time OFF.
///     StandUp   → Idle/Locomotion: Has Exit Time ON. Exit Time ~0.9.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MeetingSeatSystem : MonoBehaviour
{
    // ── References ───────────────────────────────────────────────────────────
    [Header("── References ──────────────────────────────────────────────")]
    [Tooltip("Jahan player ko walk karke khada hona hai (seat ke bilkul saamne Empty GameObject)")]
    public Transform seatArrivalPoint;

    [Tooltip("Is Transform ki position ki taraf player ka munh hoga (e.g. table ka center)")]
    public Transform seatFaceTarget;

    [Tooltip("'Press C to Join Meeting' wala UI Panel (Canvas ke andar hona chahiye)")]
    public GameObject joinPromptUI;

    // ── Timing ───────────────────────────────────────────────────────────────
    [Header("── Timing ───────────────────────────────────────────────────")]
    [Tooltip("Sit Down animation kitne seconds ka hai — apne animation clip ki length daalo")]
    [Min(0.1f)]
    public float sitDownDuration = 2.0f;

    [Tooltip("Kitne seconds tak player seat par baithega (Seat Idle duration)")]
    [Min(1f)]
    public float seatIdleDuration = 10f;

    [Tooltip("Stand Up animation kitne seconds ka hai — apne animation clip ki length daalo")]
    [Min(0.1f)]
    public float standUpDuration = 2.0f;

    // ── Walk Settings ────────────────────────────────────────────────────────
    [Header("── Walk To Seat Settings ───────────────────────────────────")]
    [Tooltip("Player ki speed jab seat tak chal raha ho")]
    public float walkToSeatSpeed = 2.0f;

    [Tooltip("Kitna paas aane par ruk jaaye")]
    public float stoppingDistance = 0.3f;

    [Tooltip("Seat par pahunchne ke baad face rotate karne ka time")]
    public float faceRotateDuration = 0.35f;

    // ── Animator Parameters ──────────────────────────────────────────────────
    [Header("── Animator Parameter Names (Exactly Match karo) ───────────")]
    [Tooltip("Sit Down ka Animator TRIGGER naam")]
    public string sitDownParam = "SitDown";

    [Tooltip("Seat Idle ka Animator BOOL naam")]
    public string seatIdleParam = "SeatIdle";

    [Tooltip("Stand Up ka Animator TRIGGER naam")]
    public string standUpParam = "StandUp";

    // ── Private Fields ───────────────────────────────────────────────────────
    private PlayerController    _pc;
    private Animator            _anim;
    private NavMeshAgent        _agent;
    private CharacterController _cc;
    private bool _playerInRange = false;
    private bool _busy          = false;

    private static readonly int HashSpeed       = Animator.StringToHash("Speed");
    private static readonly int HashWeaponState = Animator.StringToHash("WeaponState");

    // ── Unity Callbacks ──────────────────────────────────────────────────────
    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (joinPromptUI != null) joinPromptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_busy || !other.CompareTag("Player")) return;

        GameObject go = other.gameObject;
        _pc    = go.GetComponentInParent<PlayerController>()   ?? go.GetComponent<PlayerController>();
        _anim  = go.GetComponentInParent<Animator>()           ?? go.GetComponent<Animator>();
        _agent = go.GetComponentInParent<NavMeshAgent>()        ?? go.GetComponent<NavMeshAgent>();
        _cc    = go.GetComponentInParent<CharacterController>() ?? go.GetComponent<CharacterController>();

        _playerInRange = true;
        if (joinPromptUI != null) joinPromptUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (!_busy && joinPromptUI != null) joinPromptUI.SetActive(false);
    }

    private void Update()
    {
        if (_playerInRange && !_busy && Input.GetKeyDown(KeyCode.C))
            StartCoroutine(MeetingSequence());
    }

    // ── Main Sequence ────────────────────────────────────────────────────────
    private IEnumerator MeetingSequence()
    {
        _busy = true;
        if (joinPromptUI != null) joinPromptUI.SetActive(false);

        // 1. Weapon unequip karo (agar equipped hai)
        if (_pc != null)
            yield return StartCoroutine(_pc.UnequipCurrentWeaponForPickup());

        // Root motion ko poori tarah band karo taaki animation player ko aage na khinche
        bool originalRootMotion = false;
        if (_anim != null) 
        {
            originalRootMotion = _anim.applyRootMotion;
            _anim.applyRootMotion = false;
        }

        // 2. Player controls freeze karo
        if (_pc != null) _pc.isFrozen = true;

        // 3. NavMeshAgent ON karo, CharacterController ko OFF karo (dono ek saath ladenge)
        if (_cc != null) _cc.enabled = false;
        if (_agent != null)
        {
            _agent.enabled = true;
            _agent.speed = walkToSeatSpeed;
            _agent.stoppingDistance = stoppingDistance;
        }

        // 4. Seat tak walk karo (NavMesh)
        if (seatArrivalPoint != null && _agent != null)
            yield return StartCoroutine(WalkToSeat());

        // 5. NavMeshAgent band karo
        if (_agent != null) { _agent.ResetPath(); _agent.enabled = false; }
        // Note: CharacterController abhi bhi band hai taaki smooth move perfectly kaam kare

        // 6. Seat ki taraf munh karo aur EXACT position par aao
        if (seatArrivalPoint != null && seatFaceTarget != null && _pc != null)
        {
            Vector3 dir = seatFaceTarget.position - seatArrivalPoint.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                yield return StartCoroutine(SmoothMoveAndRotate(seatArrivalPoint.position, dir.normalized, faceRotateDuration));
            }
        }

        // Speed 0 — walk animation band
        if (_anim != null) _anim.SetFloat(HashSpeed, 0f);
        yield return null;

        // 7. SIT DOWN animation + Lock Position
        if (_anim != null)
        {
            _anim.SetInteger(HashWeaponState, 0);
            _anim.ResetTrigger(sitDownParam);
            _anim.SetTrigger(sitDownParam);
        }
        
        // Hamesha SeatArrivalPoint aur FaceTarget par force karke rakho
        float timer = 0f;
        Quaternion faceRot = Quaternion.identity;
        if (seatArrivalPoint != null && seatFaceTarget != null)
        {
            Vector3 dir = seatFaceTarget.position - seatArrivalPoint.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) faceRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        while (timer < sitDownDuration)
        {
            if (_pc != null && seatArrivalPoint != null)
            {
                _pc.transform.position = seatArrivalPoint.position;
                if (seatFaceTarget != null) _pc.transform.rotation = faceRot;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 8. SEAT IDLE animation (inspector se duration)
        if (_anim != null) _anim.SetBool(seatIdleParam, true);
        
        timer = 0f;
        while (timer < seatIdleDuration)
        {
            if (_pc != null && seatArrivalPoint != null)
            {
                _pc.transform.position = seatArrivalPoint.position;
                if (seatFaceTarget != null) _pc.transform.rotation = faceRot;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 9. STAND UP animation
        if (_anim != null)
        {
            _anim.SetBool(seatIdleParam, false);
            _anim.ResetTrigger(standUpParam);
            _anim.SetTrigger(standUpParam);
        }
        
        timer = 0f;
        while (timer < standUpDuration)
        {
            if (_pc != null && seatArrivalPoint != null)
            {
                _pc.transform.position = seatArrivalPoint.position;
                if (seatFaceTarget != null) _pc.transform.rotation = faceRot;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 10. Normal idle pe wapas aao + controls restore
        if (_anim != null) 
        {
            _anim.SetFloat(HashSpeed, 0f);
            _anim.applyRootMotion = originalRootMotion;
        }
        if (_pc != null) _pc.isFrozen = false;
        
        // Sab kuch hone ke baad CharacterController wapas ON karo
        if (_cc != null) _cc.enabled = true;

        _busy = false;
    }

    // ── Walk To Seat via NavMesh ─────────────────────────────────────────────
    private IEnumerator WalkToSeat()
    {
        _agent.SetDestination(seatArrivalPoint.position);
        if (_anim != null) _anim.SetFloat(HashSpeed, 0.5f);

        float timeout = 15f, elapsed = 0f;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            if (_agent.pathPending) { yield return null; continue; }
            if (!_agent.pathPending && _agent.remainingDistance <= stoppingDistance) break;

            // Walk blend tree update (0.5 = normal WASD walk speed)
            if (_anim != null)
            {
                // Jaise PlayerController mein smooth hota hai, waisa hi same feel
                float currentSpeed = _anim.GetFloat(HashSpeed);
                float smoothSpeed = Mathf.MoveTowards(currentSpeed, 0.5f, Time.deltaTime / 0.1f);
                _anim.SetFloat(HashSpeed, smoothSpeed);
            }
            yield return null;
        }

        if (_anim != null) _anim.SetFloat(HashSpeed, 0f);
    }

    // ── Smooth Move & Face Rotation (Exact Snapping) ─────────────────────────
    private IEnumerator SmoothMoveAndRotate(Vector3 targetPos, Vector3 targetForward, float duration)
    {
        if (_pc == null) yield break;
        
        Vector3 startPos = _pc.transform.position;
        Quaternion startRot = _pc.transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(targetForward, Vector3.up);
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            
            _pc.transform.position = Vector3.Lerp(startPos, targetPos, t);
            _pc.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
        
        _pc.transform.position = targetPos;
        _pc.transform.rotation = targetRot;
    }
}
