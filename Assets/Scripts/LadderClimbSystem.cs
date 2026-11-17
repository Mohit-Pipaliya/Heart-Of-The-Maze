using System.Collections;
using UnityEngine;

/// <summary>
/// LadderClimbSystem — AAA Precision Edition
/// ==========================================
/// Realistic, full-precision 4-trigger ladder climbing system for WatchTower.
///
/// 4 TRIGGER SETUP:
///  Trigger 1 (Bottom Ground) : WatchTower Up Trigger    (Ground prompt: "[ C ] Climb Up")
///  Trigger 2 (Bottom Mount)  : WatchTower Start Trigger (Ladder base rung mount position)
///  Trigger 3 (Top Mount)     : Watch Tower End Trigger  (Top ladder rung mount position)
///  Trigger 4 (Top Ground)    : WatchTower Down Trigger  (Top platform prompt: "[ C ] Climb Down")
///
/// CONTROLS:
///  [W] / Up Arrow   : Climb UP    (anim speed = +1)
///  [S] / Down Arrow : Climb DOWN  (anim speed = -1)
///  No Key           : Freeze on rung (anim speed = 0, frozen pose)
///
/// AUTO DISMOUNT:
///  Touch Trigger 3 going UP     -> Auto dismounts directly onto Trigger 4 (platform deck floor), Idle locomotion
///  Touch Trigger 2 going DOWN   -> Auto dismounts directly onto Trigger 1 (ground), Idle locomotion
/// </summary>
public class LadderClimbSystem : MonoBehaviour
{
    // ─── State Machine ────────────────────────────────────────────────────────
    public enum ClimbState
    {
        Idle,
        MountingUp,
        MountingDown,
        Climbing,
        DismountingToTop,
        DismountingToBottom
    }

    // ─── Inspector — 4 Trigger Waypoints ─────────────────────────────────────
    [Header("── 4 Trigger Waypoints ──")]
    [Tooltip("Trigger 1 (Bottom Ground): WatchTower Up Trigger (Ground prompt: Press C to Climb Up).")]
    public Transform trigger1_BottomGround;

    [Tooltip("Trigger 2 (Bottom Mount): WatchTower Start Trigger (Ladder base rung mount).")]
    public Transform trigger2_BottomMount;

    [Tooltip("Trigger 3 (Top Mount): Watch Tower End Trigger (Ladder top rung mount).")]
    public Transform trigger3_TopMount;

    [Tooltip("Trigger 4 (Top Ground): WatchTower Down Trigger (Top platform prompt: Press C to Climb Down).")]
    public Transform trigger4_TopGround;

    // ─── Inspector — Climb Settings ───────────────────────────────────────────
    [Header("── Climb Settings ──")]
    [Tooltip("Ladder climbing speed (metres per second).")]
    [SerializeField] private float climbSpeed = 5.5f;

    [Tooltip("Smooth mount transition duration (seconds).")]
    [SerializeField] private float mountDuration = 0.40f;

    [Tooltip("Smooth dismount transition duration (seconds).")]
    [SerializeField] private float dismountDuration = 0.45f;

    [Tooltip("Interaction key (default: C).")]
    [SerializeField] private KeyCode interactKey = KeyCode.C;

    [Tooltip("Proximity radius for interaction prompt.")]
    [SerializeField] private float proximityRadius = 25.0f;

    [Header("── Alignment & Micro-Tuning ──")]
    [Tooltip("Forward/backward offset from ladder rungs. Negative pulls player back; positive pushes into rungs.")]
    [SerializeField] private float ladderForwardOffset = -0.15f;

    [Tooltip("Vertical height offset on ladder.")]
    [SerializeField] private float ladderHeightOffset = 0f;

    [Header("── Top Platform Mount Tuning ──")]
    [Tooltip("Climb progress (0 to 1) when mounting down from platform deck. 0.82 places player feet on upper rungs and hands holding top rung.")]
    [Range(0.6f, 0.95f)]
    [SerializeField] private float topMountProgress = 0.82f;

    // ─── Inspector — Audio ────────────────────────────────────────────────────
    [Header("── Audio (Optional) ──")]
    public AudioClip mountSound;
    public AudioClip rungStepSound;
    public AudioClip dismountSound;
    [SerializeField] private float rungSoundInterval = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 0.85f;

    // ─── Inspector — Debug (Read-Only) ────────────────────────────────────────
    [Header("── Status (Read-Only) ──")]
    [SerializeField] private ClimbState currentState = ClimbState.Idle;
    [Range(0f, 1f)]
    [SerializeField] private float climbProgress = 0f;

    // ─── Private References ───────────────────────────────────────────────────
    private Transform           _pt;
    private CharacterController _cc;
    private PlayerController    _pc;
    private Animator            _anim;
    private AudioSource         _audio;
    private CameraController    _cam;

    private static readonly int HashIsClimbing       = Animator.StringToHash("IsClimbing");
    private static readonly int HashLadderClimbSpeed = Animator.StringToHash("LadderClimbSpeed");

    // Internal flags
    private bool  _inTrigger1;
    private bool  _inTrigger2;
    private bool  _inTrigger3;
    private bool  _inTrigger4;
    private bool  _isDismounting;
    private float _timeOnLadder;
    private float _nextRungSound;
    private float _prevShootingWeight;
    private float _nextValidationTime;

    // Public properties
    public ClimbState CurrentState => currentState;
    public bool IsClimbing => currentState == ClimbState.Climbing;
    public Transform PlayerTransform => _pt;
    public float ClimbProgress => climbProgress;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================
    private void Awake()
    {
        try
        {
            enabled = true;
            // Always force clean Idle state on start
            currentState = ClimbState.Idle;
            climbProgress = 0f;
            _isDismounting = false;
            _timeOnLadder = 0f;

            SetupAudio();
            ForceLinkWatchTowerTriggers();
            DisableOldCanvasUI();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LadderClimbSystem] Awake safe catch: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void Start()
    {
        try
        {
            // Ensure Idle state
            currentState = ClimbState.Idle;
            climbProgress = 0f;
            _isDismounting = false;
            _timeOnLadder = 0f;

            ForceLinkWatchTowerTriggers();
            FindPlayer();
            AutoAttachTriggerZones();
            DisableOldCanvasUI();

            Debug.Log($"[LadderClimbSystem] Initialized on {gameObject.name}. " +
                      $"T1: {trigger1_BottomGround?.name}, T2: {trigger2_BottomMount?.name}, " +
                      $"T3: {trigger3_TopMount?.name}, T4: {trigger4_TopGround?.name}. " +
                      $"Player: {(_pt != null ? _pt.name : "None")}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LadderClimbSystem] Start safe catch: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void Update()
    {
        try
        {
            if (_pt == null)
            {
                FindPlayer();
                if (_pt == null) return;
            }

            // Periodic safety check: ensure triggers stay linked
            if (Time.time > _nextValidationTime)
            {
                _nextValidationTime = Time.time + 3.0f;
                ValidateAndKeepTriggersLinked();
            }

            switch (currentState)
            {
                case ClimbState.Idle:     HandleIdleInput(); break;
                case ClimbState.Climbing: HandleClimbing();  break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LadderClimbSystem] Update safe catch: {ex.Message}");
        }
    }

    // =========================================================================
    // Auto-Link Real WatchTower 4 Triggers (100% Reliable)
    // =========================================================================
    public void ForceLinkWatchTowerTriggers()
    {
        try
        {
            Transform realUp    = null;
            Transform realStart = null;
            Transform realEnd   = null;
            Transform realDown  = null;

            // 1. Search in root/parent hierarchy
            Transform root = transform.parent != null ? transform.parent : transform.root;
            if (root != null)
            {
                Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allChildren)
                {
                    if (t == null || t == transform || t == root) continue;

                    string n = t.name.ToLower().Trim();

                    // If it's an old dummy trigger, deactivate it safely
                    if (n.StartsWith("trigger 1") || n.StartsWith("trigger 2") || n.StartsWith("trigger 3") || n.StartsWith("trigger 4") || n.StartsWith("trigger_"))
                    {
                        if (t.gameObject.activeSelf)
                            t.gameObject.SetActive(false);
                        continue;
                    }

                    string clean = n.Replace(" ", "");
                    if (clean.Contains("watchtower") || clean.Contains("watch") || clean.Contains("tower"))
                    {
                        if (clean.Contains("up") && !clean.Contains("down")) realUp = t;
                        else if (clean.Contains("start")) realStart = t;
                        else if (clean.Contains("end")) realEnd = t;
                        else if (clean.Contains("down")) realDown = t;
                    }
                }
            }

            // 2. If any trigger is still missing, search scene-wide
            if (realUp == null || realStart == null || realEnd == null || realDown == null)
            {
                var sceneTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (var t in sceneTransforms)
                {
                    if (t == null) continue;
                    string n = t.name.ToLower().Trim();
                    if (n.StartsWith("trigger ")) continue;

                    string clean = n.Replace(" ", "");
                    if (clean.Contains("watchtower") || clean.Contains("watch") || clean.Contains("tower"))
                    {
                        if (realUp == null && clean.Contains("up") && !clean.Contains("down")) realUp = t;
                        else if (realStart == null && clean.Contains("start")) realStart = t;
                        else if (realEnd == null && clean.Contains("end")) realEnd = t;
                        else if (realDown == null && clean.Contains("down")) realDown = t;
                    }
                }
            }

            // Bind triggers safely
            if (realUp != null)    trigger1_BottomGround = realUp;
            if (realStart != null) trigger2_BottomMount  = realStart;
            if (realEnd != null)   trigger3_TopMount     = realEnd;
            if (realDown != null)  trigger4_TopGround    = realDown;

            AutoAttachTriggerZones();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LadderClimbSystem] Error linking triggers: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void ValidateAndKeepTriggersLinked()
    {
        try
        {
            bool needsRebind = false;
            if (trigger1_BottomGround == null || trigger1_BottomGround.name.StartsWith("Trigger ")) needsRebind = true;
            if (trigger2_BottomMount == null  || trigger2_BottomMount.name.StartsWith("Trigger "))  needsRebind = true;
            if (trigger3_TopMount == null     || trigger3_TopMount.name.StartsWith("Trigger "))     needsRebind = true;
            if (trigger4_TopGround == null    || trigger4_TopGround.name.StartsWith("Trigger "))    needsRebind = true;

            if (needsRebind)
            {
                ForceLinkWatchTowerTriggers();
            }
        }
        catch (System.Exception)
        {
        }
    }

    // =========================================================================
    // Auto-Attach LadderTriggerZone & Kinematic Rigidbody to Triggers
    // =========================================================================
    private void AutoAttachTriggerZones()
    {
        try
        {
            AttachZone(trigger1_BottomGround, LadderTriggerZone.ZoneType.Trigger1_BottomGround);
            AttachZone(trigger2_BottomMount,  LadderTriggerZone.ZoneType.Trigger2_BottomMount);
            AttachZone(trigger3_TopMount,     LadderTriggerZone.ZoneType.Trigger3_TopMount);
            AttachZone(trigger4_TopGround,    LadderTriggerZone.ZoneType.Trigger4_TopGround);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LadderClimbSystem] Error attaching trigger zones: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void AttachZone(Transform t, LadderTriggerZone.ZoneType type)
    {
        if (t == null) return;
        Collider col = t.GetComponent<Collider>();
        if (col == null) return;
        col.isTrigger = true;

        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = t.gameObject.AddComponent<Rigidbody>();
        }
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        var zone = t.GetComponent<LadderTriggerZone>();
        if (zone == null)
        {
            zone = t.gameObject.AddComponent<LadderTriggerZone>();
        }
        if (zone != null)
        {
            zone.ladderSystem = this;
            zone.zoneType = type;
        }
    }

    // =========================================================================
    // Find Player References
    // =========================================================================
    private void FindPlayer()
    {
        if (_pt != null && _cc != null && _pc != null) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) playerObj = pc.gameObject;
        }

        if (playerObj != null)
        {
            _pt   = playerObj.transform;
            _cc   = playerObj.GetComponent<CharacterController>();
            _pc   = playerObj.GetComponent<PlayerController>();
            _anim = playerObj.GetComponentInChildren<Animator>();
        }

        // Camera Controller dhundho
        if (_cam == null)
            _cam = CameraController.Instance ?? FindFirstObjectByType<CameraController>();
    }

    // =========================================================================
    // Trigger Notifications from LadderTriggerZone
    // =========================================================================
    public void OnPlayerEnterZone(LadderTriggerZone.ZoneType type)
    {
        switch (type)
        {
            case LadderTriggerZone.ZoneType.Trigger1_BottomGround: _inTrigger1 = true; break;
            case LadderTriggerZone.ZoneType.Trigger2_BottomMount:  _inTrigger2 = true; break;
            case LadderTriggerZone.ZoneType.Trigger3_TopMount:     _inTrigger3 = true; break;
            case LadderTriggerZone.ZoneType.Trigger4_TopGround:    _inTrigger4 = true; break;
        }
    }

    public void OnPlayerExitZone(LadderTriggerZone.ZoneType type)
    {
        switch (type)
        {
            case LadderTriggerZone.ZoneType.Trigger1_BottomGround: _inTrigger1 = false; break;
            case LadderTriggerZone.ZoneType.Trigger2_BottomMount:  _inTrigger2 = false; break;
            case LadderTriggerZone.ZoneType.Trigger3_TopMount:     _inTrigger3 = false; break;
            case LadderTriggerZone.ZoneType.Trigger4_TopGround:    _inTrigger4 = false; break;
        }
    }

    // =========================================================================
    // Coordinate & Direction Calculations
    // =========================================================================
    public Vector3 GetTriggerPos(Transform t)
    {
        if (t == null) return Vector3.zero;

        try
        {
            BoxCollider box = t.GetComponent<BoxCollider>();
            if (box != null)
            {
                // World position of the BoxCollider center, perfectly taking all parent scales into account
                return t.TransformPoint(box.center);
            }

            Collider col = t.GetComponent<Collider>();
            if (col != null)
            {
                return col.bounds.center;
            }

            return t.position;
        }
        catch (System.Exception)
        {
            return Vector3.zero;
        }
    }

    /// <summary>
    /// Returns the exact platform deck floor position for Trigger 4.
    /// Uses Trigger 4's BoxCollider bottom bounds (min.y) as the exact floor surface.
    /// </summary>
    public Vector3 GetDeckFloorPos()
    {
        if (trigger4_TopGround != null)
        {
            BoxCollider box = trigger4_TopGround.GetComponent<BoxCollider>();
            if (box != null)
            {
                Vector3 center = GetTriggerPos(trigger4_TopGround);
                float halfHeight = (box.size.y * trigger4_TopGround.lossyScale.y) * 0.5f;
                float floorY = center.y - halfHeight;
                return new Vector3(center.x, floorY + 0.15f, center.z);
            }
        }

        Vector3 t4Center = GetTriggerPos(trigger4_TopGround);
        float fallbackY = (trigger3_TopMount != null) ? GetTriggerPos(trigger3_TopMount).y : t4Center.y;
        return new Vector3(t4Center.x, fallbackY + 0.15f, t4Center.z);
    }

    /// <summary>
    /// Direction vector pointing directly INTO the ladder rungs (horizontal).
    /// </summary>
    public Vector3 GetLadderFacing()
    {
        Vector3 t1 = GetTriggerPos(trigger1_BottomGround);
        Vector3 t2 = GetTriggerPos(trigger2_BottomMount);
        Vector3 dir = (t2 - t1);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
        {
            return Vector3.left;
        }
        return dir.normalized;
    }

    /// <summary>
    /// Direction vector stepping onto the top platform deck from ladder top (horizontal).
    /// </summary>
    public Vector3 GetPlatformFacing()
    {
        Vector3 t3 = GetTriggerPos(trigger3_TopMount);
        Vector3 t4 = GetTriggerPos(trigger4_TopGround);
        Vector3 dir = (t4 - t3);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
        {
            return GetLadderFacing();
        }
        return dir.normalized;
    }

    // =========================================================================
    // Idle — Proximity detection & [C] interaction
    // =========================================================================
    private void HandleIdleInput()
    {
        float playerY = _pt.position.y;

        Vector3 t1Pos = (trigger1_BottomGround != null) ? GetTriggerPos(trigger1_BottomGround) : Vector3.zero;
        Vector3 t2Pos = (trigger2_BottomMount  != null) ? GetTriggerPos(trigger2_BottomMount)  : Vector3.zero;
        Vector3 t3Pos = (trigger3_TopMount     != null) ? GetTriggerPos(trigger3_TopMount)     : Vector3.zero;
        Vector3 t4Pos = (trigger4_TopGround    != null) ? GetTriggerPos(trigger4_TopGround)    : Vector3.zero;

        // 2D horizontal distances
        float distToT1 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t1Pos.x, t1Pos.z));
        float distToT2 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t2Pos.x, t2Pos.z));
        float distToT3 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t3Pos.x, t3Pos.z));
        float distToT4 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t4Pos.x, t4Pos.z));

        // Bottom Ground detection: Player is on ground level (Y < 50m) and near T1 or T2
        bool nearBottom = (playerY < 50f) && (
            _inTrigger1 || _inTrigger2 ||
            distToT1 <= Mathf.Max(proximityRadius, 25f) ||
            distToT2 <= Mathf.Max(proximityRadius, 25f)
        );

        // Top Platform detection: Player is on top deck (Y >= 50m) and near T4 or T3
        bool nearTop = (playerY >= 50f) && (
            _inTrigger4 || _inTrigger3 ||
            distToT4 <= Mathf.Max(proximityRadius, 25f) ||
            distToT3 <= Mathf.Max(proximityRadius, 25f)
        );

        // Interaction Key [C]
        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.C))
        {
            if (nearBottom)
            {
                // Mount to Trigger 2 (WatchTower Start Trigger) at ladder base rung
                StartCoroutine(MountUpCoroutine());
            }
            else if (nearTop)
            {
                // Mount to Trigger 3 (Watch Tower End Trigger) at ladder top rung
                StartCoroutine(MountDownCoroutine());
            }
        }
    }

    // =========================================================================
    // Mount Coroutines
    // =========================================================================
    /// <summary>
    /// Mounts from Ground (Trigger 1) directly to Trigger 2 (Ladder Base Rung).
    /// </summary>
    private IEnumerator MountUpCoroutine()
    {
        currentState = ClimbState.MountingUp;
        _isDismounting = false;
        _timeOnLadder = 0f;

        // 1. Freeze player physics and CharacterController
        if (_pc != null) _pc.SetFrozen(true);
        if (_cc != null) _cc.enabled = false;

        // 2. Set climbing animation pose
        if (_anim != null)
        {
            _anim.applyRootMotion = false;
            if (_anim.layerCount > 1)
            {
                _prevShootingWeight = _anim.GetLayerWeight(1);
                _anim.SetLayerWeight(1, 0f);
            }
            _anim.SetBool(HashIsClimbing, true);
            _anim.SetFloat(HashLadderClimbSpeed, 0f);
            _anim.CrossFadeInFixedTime("LadderClimb", 0.12f);
        }

        PlayAudio(mountSound, 440f);

        Vector3 startPos = _pt.position;
        Quaternion startRot = _pt.rotation;

        Vector3 forwardOffset = GetLadderFacing() * ladderForwardOffset;
        Vector3 heightOffset  = Vector3.up * ladderHeightOffset;

        Vector3 targetPos = (trigger2_BottomMount != null)
            ? (GetTriggerPos(trigger2_BottomMount) + forwardOffset + heightOffset)
            : (startPos + Vector3.up * 14f);
        // Player must FACE the ladder rungs (into the ladder), so negate the T1→T2 direction
        Quaternion targetRot = Quaternion.LookRotation(-GetLadderFacing(), Vector3.up);

        float elapsed = 0f;
        while (elapsed < mountDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / mountDuration));
            _pt.position = Vector3.Lerp(startPos, targetPos, t);
            _pt.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        _pt.position = targetPos;
        _pt.rotation = targetRot;
        climbProgress = 0f;
        _timeOnLadder = 0f;
        _isDismounting = false;
        currentState = ClimbState.Climbing;

        // ── Camera lock: player ke peeche ──────────────────────────────────────
        if (_cam == null) _cam = CameraController.Instance ?? FindFirstObjectByType<CameraController>();
        _cam?.EnterLadderMode(_pt);

        Debug.Log($"[LadderClimbSystem] >>> Mounted successfully to Trigger 2 @ {targetPos}!");
    }

    /// <summary>
    /// Mounts from Platform (Trigger 4) directly onto Ladder Top Rungs (Trigger 3 zone).
    /// </summary>
    private IEnumerator MountDownCoroutine()
    {
        currentState = ClimbState.MountingDown;
        _isDismounting = false;
        _timeOnLadder = 0f;

        if (_pc != null) _pc.SetFrozen(true);
        if (_cc != null) _cc.enabled = false;

        if (_anim != null)
        {
            _anim.applyRootMotion = false;
            if (_anim.layerCount > 1)
            {
                _prevShootingWeight = _anim.GetLayerWeight(1);
                _anim.SetLayerWeight(1, 0f);
            }
            _anim.SetBool(HashIsClimbing, true);
            _anim.SetFloat(HashLadderClimbSpeed, 0f);
            _anim.CrossFadeInFixedTime("LadderClimb", 0.12f);
        }

        PlayAudio(mountSound, 440f);

        Vector3 startPos = _pt.position;
        Quaternion startRot = _pt.rotation;

        Transform bottomTarget = trigger2_BottomMount ?? trigger1_BottomGround;
        Transform topTarget    = trigger3_TopMount ?? trigger4_TopGround;

        Vector3 bottomPos = (bottomTarget != null) ? GetTriggerPos(bottomTarget) : startPos;
        Vector3 topPos    = (topTarget != null) ? GetTriggerPos(topTarget) : (startPos + Vector3.down * 2f);

        Vector3 forwardOffset = GetLadderFacing() * ladderForwardOffset;
        Vector3 heightOffset  = Vector3.up * ladderHeightOffset;

        // Position player on the ladder rungs at topMountProgress (0.82f) facing INTO ladder
        Vector3 targetPos = Vector3.Lerp(bottomPos, topPos, topMountProgress) + forwardOffset + heightOffset;
        Quaternion targetRot = Quaternion.LookRotation(-GetLadderFacing(), Vector3.up);

        float elapsed = 0f;
        while (elapsed < mountDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / mountDuration));
            _pt.position = Vector3.Lerp(startPos, targetPos, t);
            _pt.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        _pt.position = targetPos;
        _pt.rotation = targetRot;
        climbProgress = topMountProgress;
        _timeOnLadder = 0f;
        _isDismounting = false;
        currentState = ClimbState.Climbing;

        // ── Camera lock: player ke peeche ──────────────────────────────────────
        if (_cam == null) _cam = CameraController.Instance ?? FindFirstObjectByType<CameraController>();
        _cam?.EnterLadderMode(_pt);

        Debug.Log($"[LadderClimbSystem] >>> Mounted successfully to Top of Ladder (Trigger 3) @ {targetPos} (progress={topMountProgress:F2})!");
    }

    // =========================================================================
    // Climbing Loop — [W] Up, [S] Down, Release freezes in place
    // =========================================================================
    private void HandleClimbing()
    {
        Transform bottomTarget = trigger2_BottomMount ?? trigger1_BottomGround;
        Transform topTarget    = trigger3_TopMount ?? trigger4_TopGround;

        if (bottomTarget == null || topTarget == null) return;
        if (_isDismounting) return;

        _timeOnLadder += Time.deltaTime;

        Vector3 bottomPos = GetTriggerPos(bottomTarget);
        Vector3 topPos    = GetTriggerPos(topTarget);

        float ladderLen = Vector3.Distance(bottomPos, topPos);
        if (ladderLen < 0.01f) ladderLen = 1f;

        float vInput    = Input.GetAxisRaw("Vertical");
        bool  pressUp   = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)   || vInput > 0.2f;
        bool  pressDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || vInput < -0.2f;

        if (pressUp)
        {
            climbProgress += (climbSpeed / ladderLen) * Time.deltaTime;
            SetAnimSpeed(1f);
            PlayRungSound();
        }
        else if (pressDown)
        {
            climbProgress -= (climbSpeed / ladderLen) * Time.deltaTime;
            SetAnimSpeed(-1f);
            PlayRungSound();
        }
        else
        {
            // Freeze in place on current rung
            SetAnimSpeed(0f);
        }

        climbProgress = Mathf.Clamp(climbProgress, 0f, 1f);

        // Position player along ladder between T2 (bottom rung) and T3 (top rung)
        Vector3 basePos = Vector3.Lerp(bottomPos, topPos, climbProgress);
        Vector3 forwardOffset = GetLadderFacing() * ladderForwardOffset;
        Vector3 heightOffset  = Vector3.up * ladderHeightOffset;

        _pt.position = basePos + forwardOffset + heightOffset;
        // Player must FACE INTO the ladder rungs (negate direction so player looks AT the rungs)
        _pt.rotation = Quaternion.LookRotation(-GetLadderFacing(), Vector3.up);

        // ── Auto Dismount Checks ─────────────────────────────────────────────
        // 1. TOUCH TRIGGER 3's BOX COLLIDER / REACH TOP WHILE CLIMBING UP -> AUTO DISMOUNT DIRECTLY ONTO TRIGGER 4 (Platform)!
        //    CRITICAL GUARD: _timeOnLadder > 0.35f prevents instant-bounce when MountDown sets climbProgress=1.0 on mount.
        bool reachedTop = false;

        if (_timeOnLadder > 0.35f)
        {
            // A. BoxCollider bounds check on Trigger 3
            if (trigger3_TopMount != null)
            {
                BoxCollider box3 = trigger3_TopMount.GetComponent<BoxCollider>();
                if (box3 != null)
                {
                    Bounds b3 = box3.bounds;
                    b3.Expand(2.5f);
                    if (b3.Contains(_pt.position) || _pt.position.y >= (box3.bounds.min.y - 0.5f))
                    {
                        reachedTop = true;
                    }
                }
            }

            // B. Height & progress thresholds
            if (_pt.position.y >= (topPos.y - 1.2f) || climbProgress >= 0.88f || Vector3.Distance(_pt.position, topPos) <= 2.5f)
            {
                reachedTop = true;
            }
        }

        // ONLY auto-dismount to top when:
        //  - reachedTop is true (position/progress condition met)
        //  - AND _timeOnLadder > 0.35f (not a fresh MountDown spawn)
        //  - AND player is ACTIVELY pressing [W] upward (never auto-fire passively)
        if (reachedTop && _timeOnLadder > 0.35f && pressUp)
        {
            _isDismounting = true;
            SetAnimSpeed(0f);
            StartCoroutine(DismountToTopCoroutine());
            return;
        }

        // 2. REACH TRIGGER 2's BOX COLLIDER / REACH BOTTOM WHILE ACTIVELY CLIMBING DOWN -> AUTO DISMOUNT DIRECTLY ONTO TRIGGER 1 (Ground)!
        // (CRITICAL: MUST REQUIRE ACTIVE pressDown / downward movement so mounting does NOT false-trigger dismount!)
        if (_timeOnLadder > 0.40f && pressDown)
        {
            bool reachedBottom = false;

            if (trigger2_BottomMount != null)
            {
                BoxCollider box2 = trigger2_BottomMount.GetComponent<BoxCollider>();
                if (box2 != null)
                {
                    Bounds b2 = box2.bounds;
                    b2.Expand(2.0f);
                    if (b2.Contains(_pt.position) || _pt.position.y <= (box2.bounds.max.y + 0.5f))
                    {
                        reachedBottom = true;
                    }
                }
            }

            if (_pt.position.y <= (bottomPos.y + 0.8f) || climbProgress <= 0.03f || Vector3.Distance(_pt.position, bottomPos) <= 2.0f)
            {
                reachedBottom = true;
            }

            if (reachedBottom)
            {
                _isDismounting = true;
                SetAnimSpeed(0f);
                StartCoroutine(DismountToBottomCoroutine());
                return;
            }
        }
    }

    private void SetAnimSpeed(float speed)
    {
        if (_anim != null) _anim.SetFloat(HashLadderClimbSpeed, speed);
    }

    // =========================================================================
    // Dismount Coroutines
    // =========================================================================
    /// <summary>
    /// Smoothly dismounts from Trigger 3 directly onto Trigger 4 (Platform Deck Floor) into normal Idle locomotion.
    /// </summary>
    private IEnumerator DismountToTopCoroutine()
    {
        currentState = ClimbState.DismountingToTop;
        SetAnimSpeed(0f);
        PlayAudio(dismountSound, 320f);

        Vector3    startPos = _pt.position;
        Quaternion startRot = _pt.rotation;

        Vector3    targetPos = GetDeckFloorPos();
        Quaternion targetRot = Quaternion.LookRotation(GetPlatformFacing(), Vector3.up);

        float elapsed = 0f;
        while (elapsed < dismountDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dismountDuration));
            _pt.position = Vector3.Lerp(startPos, targetPos, t);
            _pt.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        _pt.position = targetPos;
        _pt.rotation = targetRot;

        // Restore Animator to normal locomotion
        if (_anim != null)
        {
            _anim.SetBool(HashIsClimbing, false);
            _anim.SetFloat(HashLadderClimbSpeed, 0f);
            _anim.CrossFadeInFixedTime("UnarmedLocomotion", 0.15f);

            if (_anim.layerCount > 1)
                _anim.SetLayerWeight(1, _prevShootingWeight);
        }

        // Restore CharacterController and unfreeze player
        if (_cc != null) _cc.enabled = true;
        if (_pc != null) _pc.SetFrozen(false);

        // ── Camera unlock: free-look wapas ────────────────────────────────────
        _cam?.ExitLadderMode();

        climbProgress  = 0f;
        _timeOnLadder  = 0f;
        _isDismounting = false;
        currentState   = ClimbState.Idle;

        Debug.Log("[LadderClimbSystem] >>> Successfully dismounted onto Platform (Trigger 4)!");
    }

    /// <summary>
    /// Smoothly dismounts from Trigger 2 directly onto Trigger 1 (Ground Level) into normal Idle locomotion.
    /// </summary>
    private IEnumerator DismountToBottomCoroutine()
    {
        currentState = ClimbState.DismountingToBottom;
        SetAnimSpeed(0f);
        PlayAudio(dismountSound, 320f);

        Vector3    startPos = _pt.position;
        Quaternion startRot = _pt.rotation;

        Vector3 targetPos = (trigger1_BottomGround != null) ? GetTriggerPos(trigger1_BottomGround) : (startPos - Vector3.up * 14f);
        // Face away from ladder towards open ground
        Quaternion targetRot = Quaternion.LookRotation(-GetLadderFacing(), Vector3.up);

        float elapsed = 0f;
        while (elapsed < dismountDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dismountDuration));
            _pt.position = Vector3.Lerp(startPos, targetPos, t);
            _pt.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        _pt.position = targetPos;
        _pt.rotation = targetRot;

        // Restore Animator to normal locomotion
        if (_anim != null)
        {
            _anim.SetBool(HashIsClimbing, false);
            _anim.SetFloat(HashLadderClimbSpeed, 0f);
            _anim.CrossFadeInFixedTime("UnarmedLocomotion", 0.15f);

            if (_anim.layerCount > 1)
                _anim.SetLayerWeight(1, _prevShootingWeight);
        }

        // Restore CharacterController and unfreeze player
        if (_cc != null) _cc.enabled = true;
        if (_pc != null) _pc.SetFrozen(false);

        // ── Camera unlock: free-look wapas ────────────────────────────────────
        _cam?.ExitLadderMode();

        climbProgress  = 0f;
        _timeOnLadder  = 0f;
        _isDismounting = false;
        currentState   = ClimbState.Idle;

        Debug.Log("[LadderClimbSystem] >>> Successfully dismounted onto Ground (Trigger 1)!");
    }

    // =========================================================================
    // OnGUI — Single Sleek Gold-Bordered Yellow HUD Banner at Lower Third
    // =========================================================================
    private void OnGUI()
    {
        if (_pt == null) return;

        float playerY = _pt.position.y;
        Vector3 t1Pos = (trigger1_BottomGround != null) ? GetTriggerPos(trigger1_BottomGround) : Vector3.zero;
        Vector3 t2Pos = (trigger2_BottomMount  != null) ? GetTriggerPos(trigger2_BottomMount)  : Vector3.zero;
        Vector3 t3Pos = (trigger3_TopMount     != null) ? GetTriggerPos(trigger3_TopMount)     : Vector3.zero;
        Vector3 t4Pos = (trigger4_TopGround    != null) ? GetTriggerPos(trigger4_TopGround)    : Vector3.zero;

        float distToT1 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t1Pos.x, t1Pos.z));
        float distToT2 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t2Pos.x, t2Pos.z));
        float distToT3 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t3Pos.x, t3Pos.z));
        float distToT4 = Vector2.Distance(new Vector2(_pt.position.x, _pt.position.z), new Vector2(t4Pos.x, t4Pos.z));

        bool nearBottom = (playerY < 50f) && (_inTrigger1 || _inTrigger2 || distToT1 <= Mathf.Max(proximityRadius, 25f) || distToT2 <= Mathf.Max(proximityRadius, 25f));
        bool nearTop    = (playerY >= 50f) && (_inTrigger4 || _inTrigger3 || distToT4 <= Mathf.Max(proximityRadius, 25f) || distToT3 <= Mathf.Max(proximityRadius, 25f));

        bool isClimbingState = (currentState == ClimbState.Climbing || currentState == ClimbState.MountingUp || currentState == ClimbState.MountingDown);
        bool inClimbPosition = isClimbingState || (_inTrigger2 || _inTrigger3 || distToT2 <= 2.5f || distToT3 <= 2.5f);

        if (inClimbPosition)
        {
            // Jab player W ya S press karke climb kare tab UI chhip jaye (hide)
            // Aur jaise hi W ya S press karna bandh karde tab turant wapas dikhe
            bool isPressingClimbKeys = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                                       Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                                       Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.15f;

            if (!isPressingClimbKeys)
            {
                DrawCustomPromptBox("[W] Climb Up  |  [S] Climb Down");
            }
        }
        else if (currentState == ClimbState.Idle)
        {
            if (nearBottom)
            {
                DrawCustomPromptBox("[ C ] Climb Up");
            }
            else if (nearTop)
            {
                DrawCustomPromptBox("[ C ] Climb Down");
            }
        }
    }

    private void DrawCustomPromptBox(string msg)
    {
        float w = 340f;
        float h = 48f;
        float x = (Screen.width - w) * 0.5f;
        // Position at bottom center of the screen
        float y = Screen.height - h - 35f;

        // 1. Semi-transparent dark background
        GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.88f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);

        // 2. High-contrast golden yellow border accents
        GUI.color = new Color(1f, 0.84f, 0.1f, 1f);
        GUI.Box(new Rect(x, y, w, 3f), GUIContent.none);
        GUI.Box(new Rect(x, y + h - 3f, w, 3f), GUIContent.none);

        // 3. Crisp bold golden yellow text
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 20,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(1f, 0.88f, 0.15f, 1f);

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), msg, style);
    }

    // =========================================================================
    // Disable Old Canvas UI
    // =========================================================================
    private void DisableOldCanvasUI()
    {
        try
        {
            var canvasObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in canvasObjects)
            {
                if (go == null) continue;
                string n = go.name.ToLower();
                if (n.Contains("climbing hint") || n.Contains("press c to climb") || n.Contains("climb up prompt") || n.Contains("climb down prompt"))
                {
                    if (go.activeSelf) go.SetActive(false);
                }
            }
        }
        catch (System.Exception)
        {
        }
    }

    // =========================================================================
    // Audio
    // =========================================================================
    private void SetupAudio()
    {
        _audio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0.75f;
    }

    private void PlayRungSound()
    {
        if (Time.time < _nextRungSound) return;
        _nextRungSound = Time.time + rungSoundInterval;
        PlayAudio(rungStepSound, 520f);
    }

    private void PlayAudio(AudioClip clip, float freq)
    {
        if (_audio == null) return;
        if (clip != null)
        {
            _audio.PlayOneShot(clip, audioVolume);
        }
        else
        {
            AudioClip proc = MakeClickClip(freq);
            _audio.PlayOneShot(proc, audioVolume * 0.35f);
        }
    }

    private static AudioClip MakeClickClip(float freq)
    {
        int sr = 44100, len = (int)(sr * 0.055f);
        float[] s = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 85f);
        }
        AudioClip c = AudioClip.Create("LadderRung", len, 1, sr, false);
        c.SetData(s, 0);
        return c;
    }

    // =========================================================================
    // Scene Gizmos
    // =========================================================================
    private void OnDrawGizmos()
    {
        if (trigger1_BottomGround != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f);
            Vector3 p = GetTriggerPos(trigger1_BottomGround);
            Gizmos.DrawWireSphere(p, 0.6f);
        }

        if (trigger2_BottomMount != null)
        {
            Gizmos.color = new Color(0.1f, 1f, 0.2f, 0.9f);
            Vector3 p = GetTriggerPos(trigger2_BottomMount);
            Gizmos.DrawWireSphere(p, 0.5f);
        }

        if (trigger3_TopMount != null)
        {
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.9f);
            Vector3 p = GetTriggerPos(trigger3_TopMount);
            Gizmos.DrawWireSphere(p, 0.5f);
        }

        if (trigger4_TopGround != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.8f);
            Vector3 p = GetDeckFloorPos();
            Gizmos.DrawWireSphere(p, 0.6f);
        }

        if (trigger2_BottomMount != null && trigger3_TopMount != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(GetTriggerPos(trigger2_BottomMount), GetTriggerPos(trigger3_TopMount));
        }
    }
}
