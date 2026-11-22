using System.Collections;
using System.Collections.Generic;
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
///   4. Player par PlayerController aur CharacterController hona chahiye.
///
///   5. Baked NavMesh ho to seat tak obstacles ke around route calculate hoga.
///      NavMeshAgent optional hai; meeting sequence ke dauran CharacterController move karta hai.
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
    public float walkToSeatSpeed = 4.2f;

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
    private bool _reachedSeat;
    private readonly HashSet<Collider> _playerCollidersInRange = new HashSet<Collider>();
    private readonly NavMeshPath _seatPath = new NavMeshPath();
    private Vector3[] _seatPathCorners = new Vector3[0];
    private int _seatPathCornerIndex;
    private bool _hasSeatPath;
    private float _nextSeatPathRefresh;

    private static readonly int HashSpeed       = Animator.StringToHash("Speed");
    private static readonly int HashWeaponState = Animator.StringToHash("WeaponState");

    // ── Unity Callbacks ──────────────────────────────────────────────────────
    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (joinPromptUI != null) joinPromptUI.SetActive(false);
        EnsureObstaclesCarved();
    }

    private void EnsureObstaclesCarved()
    {
        // Nearby wood benches, chairs, and campfire colliders ko NavMeshObstacle with carving assign karo
        Collider[] nearbyCols = Physics.OverlapSphere(transform.position, 12f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < nearbyCols.Length; i++)
        {
            Collider col = nearbyCols[i];
            if (col == null || col.isTrigger) continue;
            if (col.CompareTag("Player") || col.GetComponentInParent<PlayerController>() != null) continue;

            string objName = col.gameObject.name.ToLower();
            string rootName = col.transform.root.gameObject.name.ToLower();
            bool isObstacle = objName.Contains("bench") || objName.Contains("chair") || objName.Contains("table") ||
                              objName.Contains("campfire") || rootName.Contains("bench") || rootName.Contains("chair") ||
                              rootName.Contains("table") || rootName.Contains("campfire");

            if (isObstacle)
            {
                if (col.GetComponentInParent<NavMeshObstacle>() != null) continue;

                NavMeshObstacle obs = col.GetComponent<NavMeshObstacle>();
                if (obs == null) obs = col.gameObject.AddComponent<NavMeshObstacle>();
                obs.carving = true;
                obs.carveOnlyStationary = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (_busy || player == null || (_pc != null && _pc != player)) return;

        _pc = player;
        _anim = player.GetComponentInChildren<Animator>();
        _agent = player.GetComponent<NavMeshAgent>() ?? player.GetComponentInChildren<NavMeshAgent>();
        _cc = player.CharacterController;
        _playerCollidersInRange.Add(other);

        _playerInRange = true;
        if (joinPromptUI != null) joinPromptUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != _pc) return;

        _playerCollidersInRange.Remove(other);
        if (_playerCollidersInRange.Count > 0) return;

        _playerInRange = false;
        if (!_busy)
        {
            if (joinPromptUI != null) joinPromptUI.SetActive(false);
            _pc = null;
            _anim = null;
            _agent = null;
            _cc = null;
        }
    }

    private void Update()
    {
        if (_playerInRange && !_busy && _pc != null && !_pc.IsDead && !_pc.IsSequenceRunning &&
            Input.GetKeyDown(KeyCode.C))
            StartCoroutine(MeetingSequence());
    }

    // ── Main Sequence ────────────────────────────────────────────────────────
    private IEnumerator MeetingSequence()
    {
        _busy = true;
        if (joinPromptUI != null) joinPromptUI.SetActive(false);
        GameAudio.PlayUI();

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

        if (_pc != null) _pc.SetFrozen(true, lockWorldPosition: false);

        // 3. CharacterController ko HAMESHA ENABLED rakhein taaki PhysX collisions bench aur props se ho!
        if (_cc != null) _cc.enabled = true;
        if (_agent != null && _agent.enabled)
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;
        }

        if (seatArrivalPoint != null)
        {
            _reachedSeat = false;
            yield return StartCoroutine(WalkToSeatPhysX());
            if (!_reachedSeat)
            {
                Debug.LogWarning("[MeetingSeatSystem] Could not find a clear route to the meeting seat.", this);
                if (_anim != null)
                {
                    _anim.SetFloat(HashSpeed, 0f);
                    _anim.applyRootMotion = originalRootMotion;
                }
                if (_pc != null) _pc.SetFrozen(false);
                if (_cc != null) _cc.enabled = true;
                _busy = false;
                if (_playerInRange && joinPromptUI != null) joinPromptUI.SetActive(true);
                else ClearPlayerReferences();
                yield break;
            }
        }

        // 5. Seat ki taraf munh karo aur EXACT position par aao
        if (seatArrivalPoint != null && seatFaceTarget != null && _pc != null)
        {
            Vector3 dir = seatFaceTarget.position - seatArrivalPoint.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                yield return StartCoroutine(SmoothAlignToSeat(seatArrivalPoint.position, dir.normalized, faceRotateDuration));
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
        Vector3 standUpStartPos = seatArrivalPoint != null ? seatArrivalPoint.position : _pc.transform.position;
        Vector3 standUpEndPos = standUpStartPos;
        
        if (seatArrivalPoint != null && seatFaceTarget != null)
        {
            Vector3 dir = seatFaceTarget.position - seatArrivalPoint.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                // Khade hote waqt player ko 0.8 meter aage nikal lo taaki bench ke collider se takraye na
                standUpEndPos = standUpStartPos + (dir.normalized * 0.8f);
            }
        }

        while (timer < standUpDuration)
        {
            if (_pc != null && seatArrivalPoint != null)
            {
                float t = timer / standUpDuration;
                // Smooth step for natural look
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                
                _pc.transform.position = Vector3.Lerp(standUpStartPos, standUpEndPos, smoothT);
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
        if (_pc != null) _pc.SetFrozen(false);
        if (_cc != null) _cc.enabled = true;

        _busy = false;
        if (_playerInRange && joinPromptUI != null)
            joinPromptUI.SetActive(true);
        else if (!_playerInRange)
            ClearPlayerReferences();
    }

    // ── Walk To Seat via CharacterController (PhysX Collision + Whisker Avoidance) ──
    private IEnumerator WalkToSeatPhysX()
    {
        if (_pc == null || seatArrivalPoint == null) yield break;
        if (_anim != null) _anim.SetFloat(HashSpeed, 0.5f);

        float timeout = 30f, elapsed = 0f;
        float speed = Mathf.Clamp(walkToSeatSpeed, 1f, 8.5f);

        Vector3 lastPos = _pc.transform.position;
        float stuckTimer = 0f;
        _hasSeatPath = false;
        _nextSeatPathRefresh = 0f;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            Vector3 currentPos = _pc.transform.position;
            Vector3 target = seatArrivalPoint.position;

            Vector3 flatCurrent = new Vector3(currentPos.x, 0f, currentPos.z);
            Vector3 flatTarget = new Vector3(target.x, 0f, target.z);
            float distToTarget = Vector3.Distance(flatCurrent, flatTarget);

            if (distToTarget <= stoppingDistance)
            {
                _reachedSeat = true;
                break;
            }

            if (!_hasSeatPath && Time.time >= _nextSeatPathRefresh)
            {
                _hasSeatPath = TryBuildSeatPath(currentPos, target);
                _nextSeatPathRefresh = Time.time + 0.25f;
            }

            Vector3 waypoint = GetSeatWaypoint(currentPos, target);
            Vector3 flatWaypoint = new Vector3(waypoint.x, 0f, waypoint.z);
            Vector3 toTarget = flatWaypoint - flatCurrent;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                toTarget.Normalize();

                Vector3 avoidSteer = ComputeObstacleSteer(currentPos, flatWaypoint);
                Vector3 moveDir = avoidSteer.sqrMagnitude > 0.001f ? avoidSteer : toTarget;

                // Smooth rotation towards move direction
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                _pc.transform.rotation = Quaternion.Slerp(_pc.transform.rotation, targetRot, Time.deltaTime * 10f);

                // PhysX Sweep movement: CC physically slides against solid colliders without penetrating
                Vector3 motion = moveDir * (speed * Time.deltaTime) + Vector3.down * (9.8f * Time.deltaTime);
                if (_cc != null && _cc.enabled)
                {
                    _cc.Move(motion);
                }
                else
                {
                    _pc.transform.position += moveDir * (speed * Time.deltaTime);
                }

                // Stuck detection: if CC is physically pressed against a collider
                if (Vector3.Distance(_pc.transform.position, lastPos) < 0.02f * Time.deltaTime * speed)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer > 0.35f)
                    {
                        _hasSeatPath = false;
                        _nextSeatPathRefresh = 0f;
                        stuckTimer = 0f;
                    }
                }
                else
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - Time.deltaTime);
                    lastPos = _pc.transform.position;
                }
            }

            // Keep walk blend tree active
            if (_anim != null)
            {
                float curSpeed = _anim.GetFloat(HashSpeed);
                _anim.SetFloat(HashSpeed, Mathf.MoveTowards(curSpeed, 0.7f, Time.deltaTime * 5f));
            }

            yield return null;
        }

        if (_anim != null) _anim.SetFloat(HashSpeed, 0f);
    }

    private bool TryBuildSeatPath(Vector3 currentPosition, Vector3 targetPosition)
    {
        if (!NavMesh.SamplePosition(currentPosition, out NavMeshHit start, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(targetPosition, out NavMeshHit destination, 2f, NavMesh.AllAreas))
            return false;

        if (!NavMesh.CalculatePath(start.position, destination.position, NavMesh.AllAreas, _seatPath) ||
            _seatPath.status != NavMeshPathStatus.PathComplete)
            return false;

        _seatPathCorners = _seatPath.corners;
        if (_seatPathCorners.Length == 0)
            return false;

        _seatPathCornerIndex = Mathf.Min(1, _seatPathCorners.Length - 1);
        return true;
    }

    private Vector3 GetSeatWaypoint(Vector3 currentPosition, Vector3 finalTarget)
    {
        if (!_hasSeatPath || _seatPathCorners.Length == 0)
            return finalTarget;

        Vector3 flatCurrent = new Vector3(currentPosition.x, 0f, currentPosition.z);
        while (_seatPathCornerIndex < _seatPathCorners.Length - 1)
        {
            Vector3 corner = _seatPathCorners[_seatPathCornerIndex];
            Vector3 flatCorner = new Vector3(corner.x, 0f, corner.z);
            if (Vector3.Distance(flatCurrent, flatCorner) > 0.45f)
                break;

            _seatPathCornerIndex++;
        }

        if (_seatPathCornerIndex == _seatPathCorners.Length - 1)
        {
            Vector3 lastCorner = _seatPathCorners[_seatPathCornerIndex];
            Vector3 flatLastCorner = new Vector3(lastCorner.x, 0f, lastCorner.z);
            if (Vector3.Distance(flatCurrent, flatLastCorner) <= 0.75f)
                return finalTarget;
        }

        return _seatPathCorners[_seatPathCornerIndex];
    }

    /// <summary>
    /// Computes lateral steering vector to navigate around obstacles (chairs, tables, props, walls).
    /// </summary>
    private Vector3 ComputeObstacleSteer(Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 forward = (targetPos - currentPos);
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return Vector3.zero;
        forward.Normalize();

        Vector3 leftWhisker = Quaternion.Euler(0f, -38f, 0f) * forward;
        Vector3 rightWhisker = Quaternion.Euler(0f, 38f, 0f) * forward;

        Vector3 rayOrigin = currentPos + Vector3.up * 0.8f;
        float checkDist = 1.6f;
        LayerMask mask = ~LayerMask.GetMask("Ignore Raycast");

        bool hitCenter = Physics.SphereCast(rayOrigin, 0.35f, forward, out RaycastHit hitC, checkDist, mask, QueryTriggerInteraction.Ignore);
        bool hitLeft   = Physics.Raycast(rayOrigin, leftWhisker, out RaycastHit hitL, checkDist, mask, QueryTriggerInteraction.Ignore);
        bool hitRight  = Physics.Raycast(rayOrigin, rightWhisker, out RaycastHit hitR, checkDist, mask, QueryTriggerInteraction.Ignore);

        // Ignore self colliders
        if (hitCenter && IsPlayerCollider(hitC.collider)) hitCenter = false;
        if (hitLeft && IsPlayerCollider(hitL.collider)) hitLeft = false;
        if (hitRight && IsPlayerCollider(hitR.collider)) hitRight = false;

        if (!hitCenter && !hitLeft && !hitRight)
            return Vector3.zero;

        // If obstacle ahead, steer towards the side with more clearance
        float leftClearance = hitLeft ? hitL.distance : checkDist;
        float rightClearance = hitRight ? hitR.distance : checkDist;

        Vector3 normal = hitCenter ? hitC.normal : (hitLeft ? hitL.normal : hitR.normal);
        normal.y = 0f;
        normal.Normalize();

        Vector3 left = -Vector3.Cross(Vector3.up, forward);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        // Commit to the side with more clearance; blending both directions caused
        // left-right oscillation and made the player appear to walk very slowly.
        Vector3 chosen = leftClearance >= rightClearance ? left : right;
        Vector3 candidate = (chosen + normal * 0.25f).normalized;
        if (Physics.SphereCast(rayOrigin, 0.3f, candidate, out _, 0.85f, mask, QueryTriggerInteraction.Ignore))
        {
            Vector3 other = chosen == left ? right : left;
            candidate = other;
        }
        return candidate;
    }

    private bool IsPlayerCollider(Collider collider)
    {
        return collider != null && _pc != null &&
               (collider.transform == _pc.transform || collider.transform.IsChildOf(_pc.transform));
    }

    private void OnGUI()
    {
        if (joinPromptUI == null && _playerInRange && !_busy && _pc != null && !_pc.IsDead)
            GameHUDPrompt.DrawPromptBox("[C] Join Meeting");
    }

    private void ClearPlayerReferences()
    {
        _pc = null;
        _anim = null;
        _agent = null;
        _cc = null;
        _playerCollidersInRange.Clear();
    }

    // ── Smooth Align To Seat (Exact Physical Snapping) ──────────────────────
    private IEnumerator SmoothAlignToSeat(Vector3 targetPos, Vector3 targetForward, float duration)
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

            Vector3 desiredPos = Vector3.Lerp(startPos, targetPos, t);
            if (_cc != null && _cc.enabled)
            {
                Vector3 delta = desiredPos - _pc.transform.position;
                _cc.Move(delta);
            }
            else
            {
                _pc.transform.position = desiredPos;
            }

            _pc.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        // CharacterController.Move correctly respects the bench during approach, but
        // its collision skin can prevent the final few centimetres of seat alignment.
        // Disable it for this single final placement, then restore it for the pose.
        bool ccWasEnabled = _cc != null && _cc.enabled;
        if (ccWasEnabled) _cc.enabled = false;
        _pc.transform.position = targetPos;
        _pc.transform.rotation = targetRot;
        if (ccWasEnabled) _cc.enabled = true;
    }
}
