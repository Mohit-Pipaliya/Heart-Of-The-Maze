using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PlateStandInteraction / ObjectStandInteraction
/// -------------------------------------------------------------------------
/// 1. Stand terrain par rehta hai (startUnderground = false by default).
/// 2. PlayerStandPoint par ek AAA Glowing VFX Marker (Golden Flat Pad + Neon Rings + 20m Light Beams + Light + Sparkles)
///    chamkata hai jisse player ko door se hi saaf pata chale ki yahan khada rehna hai.
/// 3. Player jab PlayerStandPoint par khada ho jaye:
///    - Prompt dikhta hai: "[P] Place Object"
/// 4. Player 'P' dabaye:
///    - Weapon/Shield pehle unequip hota hai taaki haath khali ho.
///    - Player turant PlayerStandPoint par socket ki taraf smoothly face karta hai.
///    - Glowing VFX Marker extinguish (sparkle burst) ho jata hai.
///    - 'PlaceObject' trigger / 'PlacedObject' animation chalti hai.
///    - Socket tak pahuchte hi Object socket me snap hota hai (exact scale preserved).
///      (Agar testing me object pehle na uthaya ho toh script automatically matching Object {standID} attach kar leta hai).
///    - Animation 'RotateObject' pose me enter hoti hai.
///    - CAMERA: Cinematic Side View (CameraStandpoint) me shift ho jata hai (Stand + Placed Object aur Player half body dikhe).
/// 5. Rotation Mechanic:
///    - Prompt: "[A / D] Rotate to Align & Open Door"
///    - A dabane par left rotate, D dabane par right rotate.
///    - PlateArrow rotate hote huye StandTargetArrow ke samne exact 2nd time aane par puzzle solve hota hai!
/// 6. AAA VFX (Lighting / Golden aura) play hota hai, matching Maze sector ka Secret Room Door open hota hai,
///    camera wapas gameplay view me aata hai, aur player unfreeze ho jata hai.
/// </summary>
[SelectionBase]
[AddComponentMenu("Maze/Plate Stand Interaction")]
public class PlateStandInteraction : MonoBehaviour
{
    // ── Static Registry ──────────────────────────────────────────────────────
    private static readonly HashSet<int> s_CollectedPlateIDs = new HashSet<int>();

    public static void MarkPlateCollected(int id)
    {
        if (id > 0)
        {
            s_CollectedPlateIDs.Add(id);
            Debug.Log($"[PlateStandInteraction] Plate {id} marked collected globally.");
        }
    }

    public static bool IsPlateCollected(int id) => s_CollectedPlateIDs.Contains(id);

    public enum RotationAxisMode
    {
        Y_Axis, // Standard dial rotation (up)
        X_Axis, // Pitch axis
        Z_Axis  // Roll / facing axis
    }

    [Header("── Stand Identity ──────────────────────────────────────────")]
    [Tooltip("Stand ID (1 to 8). Agar 0 ho toh GameObject ya parent name se auto-detect hoga.")]
    public int standID = 0;

    [Tooltip("Stand / Object display name")]
    public string displayName = "";

    [Header("── References (Auto-Detected if Null) ───────────────────────")]
    [Tooltip("Stand par plate rakhne ka empty socket")]
    public Transform standPlateSocket;

    [Tooltip("Stand par target arrow")]
    public Transform standTargetArrow;

    [Tooltip("Player ke khade hone ka point (BoxCollider trigger is par hota hai)")]
    public Transform playerStandPoint;

    [Tooltip("Object placed hone par camera is point par shift hoga (Side view). Auto-detected if null.")]
    public Transform cameraStandPoint;

    [Header("── Underground & Emergence ──────────────────────────────────")]
    [Tooltip("Game start par terrain ke neeche buried rahega (default true so stand emerges when plate is collected)")]
    public bool startUnderground = true;

    [Tooltip("Game start par terrain ke neeche kitna meter buried rahega")]
    public float undergroundDepth = 15f;

    [Tooltip("Plate collect hone par stand ke upar aane ka time (seconds)")]
    public float riseDuration = 2.5f;

    [Tooltip("Rise animation curve")]
    public AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Stand ke zameen se nikalne par sound")]
    public AudioClip riseSound;

    [Header("── Player Placement & Animation ─────────────────────────────")]
    [Tooltip("Placement trigger parameter name in Animator (Trigger: PlaceObject)")]
    public string placeAnimTrigger = "PlaceObject";

    [Tooltip("Placement state name in Animator (State: PlacedObject)")]
    public string placeAnimState = "PlacedObject";

    [Tooltip("Rotate animation bool parameter name")]
    public string rotateAnimBool = "RotateObject";

    [Tooltip("Rotate speed float parameter name")]
    public string rotateSpeedFloat = "RotateSpeed";

    [Tooltip("PlacedObject animation mein normalized time jab plate socket me snap ho")]
    [Range(0.2f, 0.8f)]
    public float placeNormalizedTime = 0.44f;

    [Tooltip("Plate socket me snap hone par local offset")]
    public Vector3 platePlacementLocalOffset = Vector3.zero;

    [Tooltip("Plate socket me snap hone par local rotation Euler")]
    public Vector3 platePlacementLocalRotation = Vector3.zero;

    [Header("── Rotation Puzzle Settings ─────────────────────────────────")]
    [Tooltip("Kis axis par object rotate hoga")]
    public RotationAxisMode rotationAxis = RotationAxisMode.Y_Axis;

    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 65f;

    [Tooltip("Agar direction ulti karni ho toh tick karein")]
    public bool invertRotationDirection = false;

    [Tooltip("Kitni baar target arrow ke samne exact aane par door open ho (default 1)")]
    public int targetPassCount = 1;

    [Tooltip("Arrow alignment angle threshold (degrees)")]
    public float alignmentAngleThreshold = 8f;

    [Header("── Secret Room Door Integration ─────────────────────────────")]
    [Tooltip("Target door to open upon successful alignment. Auto-finds 'Secret Room Door' in matching sector if null.")]
    public DoubleSlidingDoor targetDoor;

    [Tooltip("Alignment ke kitne second baad door open ho")]
    public float doorOpenDelay = 0.4f;

    [Header("── Audio & AAA VFX ──────────────────────────────────────────")]
    public AudioClip rotateTickSound;
    public AudioClip successSound;
    public Color vfxPrimaryColor = new Color(1f, 0.85f, 0.2f, 1f); // Golden lighting
    public Color vfxSecondaryColor = new Color(0.2f, 0.9f, 1f, 1f); // Cyan electricity
    public GameObject customSuccessVFXPrefab;

    [Header("── HUD Prompts ──────────────────────────────────────────────")]
    public string placePromptText = "[P] Place Object";
    public string rotatePromptText = "[A / D] Rotate to Align & Open Door";

    // ── Runtime State ────────────────────────────────────────────────────────
    private Transform _standRoot;
    private Vector3 _surfacePosition;
    private Vector3 _buriedPosition;
    private bool _hasInitializedPositions = false;
    private bool _isUnderground = false;
    private bool _isRising = false;
    private bool _playerAtStandPoint = false;
    private int _colliderEnterCount = 0;
    private bool _isPlacing = false;
    private bool _isPlaced = false;
    private bool _isRotatingPuzzle = false;
    private bool _isRotatingKeyPressed = false;
    private bool _isCompleted = false;

    private PlatePickup _placedPlate;
    private Transform _plateArrow;
    private PlayerController _playerController;
    private Animator _playerAnimator;

    // ── Stand Point Glowing VFX Marker ───────────────────────────────────────
    private GameObject _standPointMarkerObj;
    private GameObject _hoverCrystal;
    private GameObject[] _orbitCrystals;
    private Light _standPointLight;
    private LineRenderer _standPointOuterRing;
    private LineRenderer _standPointInnerRing;
    private LineRenderer _standPointCompassRays;
    private LineRenderer[] _standPointPillars;
    private ParticleSystem _standPointParticles;
    private bool _glowMarkerActive = false;
    private Vector3 _cachedGroundPos = Vector3.zero;

    // ── Puzzle Alignment HUD & In-World Guide State ──────────────────────────
    private float _currentPuzzleAngle = 0f;
    private float _signedPuzzleAngle = 0f;
    private float _lastPuzzleAngle = 180f;
    private bool _isMovingCloser = false;
    private float _nextTickTime = 0f;
    private GameObject _puzzleTargetMarkerObj;
    private GameObject _puzzleDialPointerObj;
    private LineRenderer _puzzleResonanceArc;
    private Light _puzzleMarkerLight;
    private Light _puzzleDialLight;
    private MeshRenderer _puzzleTargetMeshRend;
    private MeshRenderer _puzzleDialMeshRend;

    // ── Shared Canvas UI Prompt ──────────────────────────────────────────────
    private static GameObject _sharedCanvasPromptObj;
    private static TextMeshProUGUI _sharedPromptTMP;
    private static PlateStandInteraction s_ActivePromptOwner;

    public bool IsUnderground => _isUnderground;
    public bool IsRising => _isRising;
    public bool IsPlaced => _isPlaced;
    public bool IsCompleted => _isCompleted;
    public Transform StandRoot => _standRoot;

    public void ResolveStandRoot()
    {
        if (_standRoot != null && _standRoot != transform) return;

        // Agar script PlayerStandPoint par ho toh parent Stand Root hoga
        if (gameObject.name == "PlayerStandPoint" || gameObject.name.Contains("StandPoint"))
        {
            _standRoot = (transform.parent != null) ? transform.parent : transform;
        }
        else
        {
            _standRoot = transform;
        }

        // Agar parent FBX mesh ya wrapper ho, toh uske parent ko Stand Root check karein
        if (_standRoot != null && _standRoot.parent != null)
        {
            string pName = _standRoot.parent.name.ToLower();
            if (pName.Contains("stand") && !pName.Contains("maze stands"))
            {
                _standRoot = _standRoot.parent;
            }
        }
    }

    private void EnsurePositionsInitialized()
    {
        if (_hasInitializedPositions) return;
        ResolveStandRoot();
        if (_standRoot == null) _standRoot = transform;
        _surfacePosition = _standRoot.position;
        _buriedPosition = _surfacePosition + Vector3.down * Mathf.Abs(undergroundDepth);
        _hasInitializedPositions = true;
    }

    private void Awake()
    {
        ResolveStandRoot();

        // Auto detect ID from standRoot name or gameObject name
        var match = System.Text.RegularExpressions.Regex.Match(_standRoot != null ? _standRoot.name : gameObject.name, @"\d+");
        if (!match.Success && transform.parent != null)
            match = System.Text.RegularExpressions.Regex.Match(transform.parent.name, @"\d+");

        if (match.Success && int.TryParse(match.Value, out int parsedId) && parsedId > 0)
        {
            standID = parsedId;
        }

        if (string.IsNullOrEmpty(displayName))
        {
            displayName = $"Object {standID} Stand";
        }

        // Cache child references
        AutoFindReferences();

        // Calculate and cache surface and buried positions before any motion
        EnsurePositionsInitialized();

        // Core Rule: Stand MUST remain buried underground until its matching plate is collected!
        bool alreadyCollected = IsPlateCollected(standID) ||
            (PlatePickup.CurrentHeldPlate != null && (PlatePickup.CurrentHeldPlate.plateID == standID || standID == 0));

        if (!alreadyCollected)
        {
            _standRoot.position = _buriedPosition;
            _isUnderground = true;
            _isRising = false;
        }
        else
        {
            _standRoot.position = _surfacePosition;
            _isUnderground = false;
            _isRising = false;
        }
    }

    private void Start()
    {
        EnsurePositionsInitialized();

        // Pre-cache door and lock it from accidental proximity open
        InitializeTargetDoor();

        // Re-verify if matching plate is already collected
        bool alreadyCollected = IsPlateCollected(standID) ||
            (PlatePickup.CurrentHeldPlate != null && (PlatePickup.CurrentHeldPlate.plateID == standID || standID == 0));

        if (!alreadyCollected)
        {
            _standRoot.position = _buriedPosition;
            _isUnderground = true;
            _isRising = false;
        }
        else
        {
            _standRoot.position = _surfacePosition;
            _isUnderground = false;
            _isRising = false;
        }

        // Ensure trigger BoxCollider is on playerStandPoint
        EnsureStandPointCollider();

        // Subscribe to global pickup event
        PlatePickup.OnPlatePickedUp += HandlePlatePickedUp;
    }

    private void AutoFindReferences()
    {
        Transform searchRoot = _standRoot != null ? _standRoot : transform;

        if (standPlateSocket == null)
            standPlateSocket = FindChildRecursive(searchRoot, "StandPlateSocket");

        if (standTargetArrow == null)
            standTargetArrow = FindChildRecursive(searchRoot, "StandTargetArrow");

        if (playerStandPoint == null)
        {
            if (gameObject.name == "PlayerStandPoint")
                playerStandPoint = transform;
            else
                playerStandPoint = FindChildRecursive(searchRoot, "PlayerStandPoint");
        }

        if (cameraStandPoint == null)
        {
            cameraStandPoint = FindChildRecursive(searchRoot, "CameraStandpoint") ??
                               FindChildRecursive(searchRoot, "CameraStandPoint") ??
                               FindChildRecursive(searchRoot, "cameraStandPoint") ??
                               FindChildRecursive(searchRoot, "Camera Stand Point") ??
                               FindChildRecursive(searchRoot, "cameraStandpoint");
        }
    }

    public static Vector3 GetGroundPosition(Vector3 searchPos, float searchUp = 6f, float maxDist = 20f)
    {
        Vector3 rayStart = searchPos + Vector3.up * searchUp;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return searchPos;
    }

    private void EnsureStandPointCollider()
    {
        Transform psp = playerStandPoint != null ? playerStandPoint : transform;
        if (psp != null)
        {
            var col = psp.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = psp.gameObject.AddComponent<BoxCollider>();
            }

            col.isTrigger = true;
            // World size: 5.0m wide, 4.0m high, 5.0m deep
            float scaleFactor = psp.lossyScale.y > 0.001f ? psp.lossyScale.y : 1f;
            float worldWidth = 5.0f;
            float worldHeight = 4.0f;
            col.size = new Vector3(worldWidth / scaleFactor, worldHeight / scaleFactor, worldWidth / scaleFactor);
            col.center = new Vector3(0f, (worldHeight * 0.5f) / scaleFactor, 0f);

            // Ensure proxy if script is not on same GameObject
            if (psp.gameObject != this.gameObject)
            {
                var proxy = psp.GetComponent<StandTriggerZoneProxy>();
                if (proxy == null)
                    proxy = psp.gameObject.AddComponent<StandTriggerZoneProxy>();
                proxy.parentStand = this;
            }
        }
    }

    // ── Dedicated Canvas UI Integration ──────────────────────────────────────
    private static void EnsureCanvasPromptUI()
    {
        if (_sharedCanvasPromptObj != null) return;

        // 1. Find Game UI Canvas in scene
        Canvas targetCanvas = null;
        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in allCanvases)
        {
            if (c.name.Equals("Game UI", System.StringComparison.OrdinalIgnoreCase))
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && allCanvases.Length > 0)
        {
            targetCanvas = allCanvases[0];
        }
        if (targetCanvas == null) return;

        // 2. Check if already exists in scene
        Transform existing = targetCanvas.transform.Find("Press P to Place Object") ??
                             targetCanvas.transform.Find("Place Object UI");

        if (existing != null)
        {
            _sharedCanvasPromptObj = existing.gameObject;
            _sharedPromptTMP = _sharedCanvasPromptObj.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        // 3. Find valid TMP Font Asset from existing prompt (e.g. Press E to Pickup Sword)
        TMP_FontAsset validFont = null;
        TextMeshProUGUI[] existingTMPs = targetCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in existingTMPs)
        {
            if (t.font != null)
            {
                validFont = t.font;
                break;
            }
        }
        if (validFont == null)
        {
            validFont = TMP_Settings.defaultFontAsset;
        }

        // 4. Create Sleek AAA Prompt Object under Game UI
        _sharedCanvasPromptObj = new GameObject("Press P to Place Object", typeof(RectTransform), typeof(Image));
        _sharedCanvasPromptObj.transform.SetParent(targetCanvas.transform, false);

        RectTransform rt = _sharedCanvasPromptObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 65f);
        rt.sizeDelta = new Vector2(440f, 52f);

        Image bg = _sharedCanvasPromptObj.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.06f, 0.92f);
        bg.raycastTarget = true;

        Button btn = _sharedCanvasPromptObj.GetComponent<Button>();
        if (btn == null) btn = _sharedCanvasPromptObj.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => {
            if (s_ActivePromptOwner != null)
            {
                s_ActivePromptOwner.TriggerPlacement();
            }
        });

        // Golden top accent border
        GameObject topBar = new GameObject("GoldBorderTop", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(_sharedCanvasPromptObj.transform, false);
        RectTransform topRt = topBar.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = Vector2.zero;
        topRt.sizeDelta = new Vector2(0f, 3f);
        Image topImg = topBar.GetComponent<Image>();
        topImg.color = new Color(1f, 0.84f, 0.1f, 1f);
        topImg.raycastTarget = false;

        // Golden bottom accent border
        GameObject bottomBar = new GameObject("GoldBorderBottom", typeof(RectTransform), typeof(Image));
        bottomBar.transform.SetParent(_sharedCanvasPromptObj.transform, false);
        RectTransform botRt = bottomBar.GetComponent<RectTransform>();
        botRt.anchorMin = new Vector2(0f, 0f);
        botRt.anchorMax = new Vector2(1f, 0f);
        botRt.pivot = new Vector2(0.5f, 0f);
        botRt.anchoredPosition = Vector2.zero;
        botRt.sizeDelta = new Vector2(0f, 3f);
        Image botImg = bottomBar.GetComponent<Image>();
        botImg.color = new Color(1f, 0.84f, 0.1f, 1f);
        botImg.raycastTarget = false;

        // Text
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(_sharedCanvasPromptObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        _sharedPromptTMP = textObj.GetComponent<TextMeshProUGUI>();
        if (validFont != null) _sharedPromptTMP.font = validFont;
        _sharedPromptTMP.alignment = TextAlignmentOptions.Center;
        _sharedPromptTMP.fontSize = 22;
        _sharedPromptTMP.fontStyle = FontStyles.Bold;
        _sharedPromptTMP.color = new Color(1f, 0.88f, 0.15f, 1f);
        _sharedPromptTMP.raycastTarget = false;
        _sharedPromptTMP.text = "[P] Place Object";

        _sharedCanvasPromptObj.SetActive(false);
    }

    private void UpdateCanvasPrompt(bool show, string msg)
    {
        EnsureCanvasPromptUI();
        if (_sharedCanvasPromptObj == null) return;

        if (show)
        {
            s_ActivePromptOwner = this;
            if (_sharedPromptTMP != null && !string.IsNullOrEmpty(msg))
                _sharedPromptTMP.text = msg;
            if (!_sharedCanvasPromptObj.activeSelf)
                _sharedCanvasPromptObj.SetActive(true);
        }
        else
        {
            if (s_ActivePromptOwner == this)
            {
                s_ActivePromptOwner = null;
                if (_sharedCanvasPromptObj.activeSelf)
                    _sharedCanvasPromptObj.SetActive(false);
            }
        }
    }

    public void InitializeTargetDoor()
    {
        if (targetDoor != null)
        {
            targetDoor.openOnApproach = false;
            targetDoor.isManagedExternally = true;
            targetDoor.isOpen = false;
            return;
        }

        int targetSectorID = standID > 0 ? standID : 1;

        // 1. Search in Maze sector (Maze 1, Maze 2, ..., Maze 8)
        Transform mazeChild = null;
        var mazeRoot = GameObject.Find("Maze");
        if (mazeRoot != null)
        {
            for (int i = 0; i < mazeRoot.transform.childCount; i++)
            {
                var ch = mazeRoot.transform.GetChild(i);
                if (ch.name.Equals($"Maze {targetSectorID}", System.StringComparison.OrdinalIgnoreCase) ||
                    ch.name.Equals($"Maze{targetSectorID}", System.StringComparison.OrdinalIgnoreCase) ||
                    ch.name.Contains(targetSectorID.ToString()))
                {
                    mazeChild = ch;
                    break;
                }
            }
        }

        if (mazeChild == null)
        {
            var direct = GameObject.Find($"Maze {targetSectorID}") ?? GameObject.Find($"Maze/Maze {targetSectorID}");
            if (direct != null) mazeChild = direct.transform;
        }

        if (mazeChild != null)
        {
            var doors = mazeChild.GetComponentsInChildren<DoubleSlidingDoor>(true);
            foreach (var d in doors)
            {
                string n = d.name.ToLower();
                if (n.Contains("secret") || n.Contains("sceret") || n.Contains("secert") || n.Contains("room"))
                {
                    targetDoor = d;
                    break;
                }
            }
            if (targetDoor == null && doors.Length > 0)
                targetDoor = doors[0];
        }

        // 2. Search scene for sector-matching secret door
        if (targetDoor == null)
        {
            var allDoors = Object.FindObjectsByType<DoubleSlidingDoor>(FindObjectsSortMode.None);
            foreach (var d in allDoors)
            {
                string n = d.name.ToLower();
                if (n.Contains("secret") || n.Contains("sceret") || n.Contains("secert") || n.Contains("room"))
                {
                    Transform curr = d.transform;
                    while (curr != null)
                    {
                        if (curr.name.Equals($"Maze {targetSectorID}", System.StringComparison.OrdinalIgnoreCase) ||
                            curr.name.Contains(targetSectorID.ToString()))
                        {
                            targetDoor = d;
                            break;
                        }
                        curr = curr.parent;
                    }
                    if (targetDoor != null) break;
                }
            }
        }

        // 3. Fallback: Any secret room door anywhere in scene
        if (targetDoor == null)
        {
            var allDoors = Object.FindObjectsByType<DoubleSlidingDoor>(FindObjectsSortMode.None);
            foreach (var d in allDoors)
            {
                string n = d.name.ToLower();
                if (n.Contains("secret") || n.Contains("sceret") || n.Contains("secert") || n.Contains("room"))
                {
                    targetDoor = d;
                    break;
                }
            }
        }

        // Lock door from opening until puzzle is solved!
        if (targetDoor != null)
        {
            targetDoor.openOnApproach = false;
            targetDoor.isManagedExternally = true;
            targetDoor.isOpen = false;
            Debug.Log($"[PlateStandInteraction] Stand {standID} bound to target door: '{targetDoor.name}' in '{targetDoor.transform.parent?.name}'");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (standID <= 0)
        {
            var match = System.Text.RegularExpressions.Regex.Match(gameObject.name, @"\d+");
            if (!match.Success && transform.parent != null)
                match = System.Text.RegularExpressions.Regex.Match(transform.parent.name, @"\d+");
            if (!match.Success && _standRoot != null)
                match = System.Text.RegularExpressions.Regex.Match(_standRoot.name, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int parsedId))
                standID = parsedId;
        }

        if (string.IsNullOrEmpty(displayName))
            displayName = $"Object {standID} Stand";

        ResolveStandRoot();
        AutoFindReferences();

        if (targetDoor == null && !Application.isPlaying)
        {
            InitializeTargetDoor();
        }
    }
#endif

    private void HandlePlatePickedUp(PlatePickup plate)
    {
        if (plate == null) return;
        if (plate.plateID == standID || standID == 0)
        {
            TriggerEmergence();
        }
    }

    public void TriggerEmergence()
    {
        if (_isUnderground && !_isRising)
        {
            StartCoroutine(RiseFromGroundCoroutine());
        }
    }

    private IEnumerator RiseFromGroundCoroutine()
    {
        _isRising = true;
        Debug.Log($"[PlateStandInteraction] Stand {standID} emerging from terrain...");

        if (riseSound != null)
        {
            AudioSource.PlayClipAtPoint(riseSound, _surfacePosition, 0.9f);
        }

        // Ground emergence dust VFX
        VFXUtil.Burst(_surfacePosition + Vector3.up * 0.1f, Vector3.up, new Color(0.65f, 0.55f, 0.42f, 0.8f),
            20, 1.2f, 3.5f, 0.15f, 0.45f, 0.8f, 1.6f, false, false, 0.3f, 50f, 2.0f);

        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);
            float curveT = riseCurve != null ? riseCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
            if (_standRoot != null)
                _standRoot.position = Vector3.LerpUnclamped(_buriedPosition, _surfacePosition, curveT);
            yield return null;
        }

        if (_standRoot != null)
            _standRoot.position = _surfacePosition;
        _isUnderground = false;
        _isRising = false;
        Debug.Log($"[PlateStandInteraction] Stand {standID} fully emerged at surface.");
    }

    private void OnDisable()
    {
        if (s_ActivePromptOwner == this)
            UpdateCanvasPrompt(false, "");
    }

    private void OnDestroy()
    {
        PlatePickup.OnPlatePickedUp -= HandlePlatePickedUp;
        DestroyStandPointMarker();
        if (s_ActivePromptOwner == this)
            UpdateCanvasPrompt(false, "");
    }

    private void Update()
    {
        // 1. Check if object collected while buried
        if (_isUnderground && !_isRising)
        {
            bool hasCollected = IsPlateCollected(standID) ||
                (PlatePickup.CurrentHeldPlate != null && (PlatePickup.CurrentHeldPlate.plateID == standID || standID == 0));

            if (hasCollected)
            {
                TriggerEmergence();
                return;
            }
        }

        // 2. Manage StandPoint Glowing VFX Marker
        // Stand visible hone par glowing beacon chamkega jisse player ko door se hi saaf pata chale kahan khada hona hai!
        bool standVisible = !_isUnderground && !_isRising && !_isPlaced && !_isCompleted;
        UpdateStandPointGlow(standVisible);

        if (_isCompleted || _isPlacing || _isUnderground || _isRising)
        {
            if (s_ActivePromptOwner == this)
                UpdateCanvasPrompt(false, "");
            return;
        }

        // 3. Proximity safety helper
        UpdateDistanceHelper();

        // 4. Manage Canvas UI Prompt and Listen for [P] key when standing on PlayerStandPoint
        if (_isRotatingPuzzle)
        {
            // During rotation, GameHUDPrompt.DrawAlignmentHUD owns the HUD — keep canvas prompt hidden
            if (s_ActivePromptOwner == this)
                UpdateCanvasPrompt(false, "");
            return;
        }
        else if (_playerAtStandPoint && !_isPlaced && !_isRising)
        {
            PlatePickup heldPlate = GetCurrentOrChildPlate();
            bool isHolding = (heldPlate != null);
            bool isMatching = isHolding && (heldPlate.plateID == standID || standID == 0);

            string text = (isHolding && !isMatching)
                ? $"[Wrong Object] Object {heldPlate.plateID} does not fit {displayName}"
                : (!string.IsNullOrEmpty(placePromptText) ? placePromptText : "[P] Place Object");

            UpdateCanvasPrompt(true, text);

            bool pPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.pKey.isPressed) pPressed = true;
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) pPressed = true;
            }
#endif
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKey(KeyCode.P)) pPressed = true;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) pPressed = true;

            if (pPressed)
            {
                TriggerPlacement();
            }
        }
        else
        {
            if (s_ActivePromptOwner == this)
                UpdateCanvasPrompt(false, "");
        }
    }

    public void TriggerPlacement()
    {
        if (_isPlacing || _isPlaced || _isCompleted) return;

        PlatePickup heldPlate = GetCurrentOrChildPlate();
        bool isHolding = (heldPlate != null);
        bool isMatching = isHolding && (heldPlate.plateID == standID || standID == 0);

        if (isHolding && !isMatching)
        {
            Debug.Log($"[PlateStandInteraction] Player attempted to place wrong object {heldPlate.plateID} on Stand {standID}.");
            return;
        }

        Debug.Log($"[PlateStandInteraction] Placement triggered for Stand {standID}! Starting sequence...");
        UpdateCanvasPrompt(false, "");
        StartCoroutine(PlaceAndAlignSequence());
    }

    private PlatePickup GetCurrentOrChildPlate()
    {
        PlatePickup plate = PlatePickup.CurrentHeldPlate;
        if (plate == null && _playerController != null)
        {
            plate = _playerController.GetComponentInChildren<PlatePickup>(true);
        }
        return plate;
    }

    private bool IsClosestStandToPlayer()
    {
        if (_playerController == null) return true;
        Vector3 pPos = _playerController.transform.position;
        Vector3 myPsp = (playerStandPoint != null) ? playerStandPoint.position : transform.position;
        float myDistSq = (new Vector2(pPos.x - myPsp.x, pPos.z - myPsp.z)).sqrMagnitude;

        PlateStandInteraction[] allStands = Object.FindObjectsByType<PlateStandInteraction>(FindObjectsSortMode.None);
        foreach (var s in allStands)
        {
            if (s == this || s == null || s._isCompleted || s._isUnderground) continue;
            Vector3 otherPsp = (s.playerStandPoint != null) ? s.playerStandPoint.position : s.transform.position;
            float otherDistSq = (new Vector2(pPos.x - otherPsp.x, pPos.z - otherPsp.z)).sqrMagnitude;
            if (otherDistSq < myDistSq)
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateDistanceHelper()
    {
        if (_playerController == null)
            _playerController = PlayerController.Instance ?? Object.FindFirstObjectByType<PlayerController>();

        if (_playerController != null && !_isUnderground)
        {
            if (!IsClosestStandToPlayer())
            {
                if (_playerAtStandPoint)
                {
                    _playerAtStandPoint = false;
                    if (s_ActivePromptOwner == this)
                        UpdateCanvasPrompt(false, "");
                }
                return;
            }

            Vector3 pPos = _playerController.transform.position;
            Vector3 pspPos = playerStandPoint != null ? playerStandPoint.position : transform.position;

            float distToPsp = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(pspPos.x, pspPos.z));

            // Within 4.0 meters of standpoint
            if (distToPsp <= 4.0f)
            {
                _playerAtStandPoint = true;
                CachePlayerReferences(_playerController.gameObject);
            }
            else if (distToPsp > 5.5f && _colliderEnterCount <= 0)
            {
                _playerAtStandPoint = false;
            }
        }
    }

    // ── Trigger Detection (Player steps onto PlayerStandPoint) ────────────────
    private void OnTriggerEnter(Collider other)
    {
        OnZoneTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        OnZoneTriggerExit(other);
    }

    public void OnZoneTriggerEnter(Collider other)
    {
        if (_isCompleted) return;
        if (!IsPlayerCollider(other)) return;

        _colliderEnterCount++;
        _playerAtStandPoint = true;
        CachePlayerReferences(other.gameObject);
        Debug.Log($"[PlateStandInteraction] Player stepped on PlayerStandPoint for Stand {standID}.");
    }

    public void OnZoneTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        _colliderEnterCount = Mathf.Max(0, _colliderEnterCount - 1);
        if (_colliderEnterCount == 0)
        {
            if (_playerController != null)
            {
                Vector3 pPos = _playerController.transform.position;
                Vector3 pspPos = playerStandPoint != null ? playerStandPoint.position : transform.position;
                float distToPsp = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(pspPos.x, pspPos.z));
                if (distToPsp > 5.5f)
                {
                    _playerAtStandPoint = false;
                    Debug.Log($"[PlateStandInteraction] Player stepped off PlayerStandPoint for Stand {standID}.");
                }
            }
            else
            {
                _playerAtStandPoint = false;
            }
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

        if (_playerAnimator == null && _playerController != null)
            _playerAnimator = _playerController.Animator ?? _playerController.GetComponent<Animator>() ?? _playerController.GetComponentInChildren<Animator>();
    }

    private static Material CreateEmissiveMat(Color col, bool additive = true)
    {
        Shader s = VFXUtil.FindValidShader(transparent: true);
        Material m = new Material(s);

        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1); // 1 = Transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2 : 0); // 2 = Additive in URP
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        if (m.HasProperty("_Cull")) m.SetInt("_Cull", 0); // Double-sided rendering
        m.renderQueue = 3000;

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) m.EnableKeyword("_BLENDMODE_ADD");

        // CRITICAL: Bind white texture so URP unlit shaders do not sample empty black!
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture2D.whiteTexture);

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", col * 3.5f);
        }
        return m;
    }

    // ── Stand Theme Configuration & VFX Data ─────────────────────────────────
    [System.Serializable]
    public struct StandThemeData
    {
        public int standID;
        public string themeName;
        public Color primaryColor;      // Emissive beacon, outer ring, altar crest
        public Color secondaryColor;    // Orbitals, inner ring, particle gradient
        public Color dialPointerColor;  // Moving needle / pointer color
        public Color resonanceArcColor; // Electric arc color when close
        public int numPillars;          // Perimeter sky beam pillars (4, 5, 6, or 8)
        public float beaconHeight;      // Sky beam height (22m - 36m)
        public float ringRadius;        // Outer ring size (2.6m - 3.0m)
    }

    public static StandThemeData GetStandTheme(int id)
    {
        switch (id)
        {
            case 1:
                return new StandThemeData
                {
                    standID = 1,
                    themeName = "Solar Altar",
                    primaryColor = new Color(1.0f, 0.82f, 0.12f, 1f),      // Solar Gold (#FFD21E)
                    secondaryColor = new Color(1.0f, 0.45f, 0.05f, 1f),    // Sunfire Amber (#FF730D)
                    dialPointerColor = new Color(1.0f, 0.96f, 0.72f, 1f),  // Sunbeam White-Gold
                    resonanceArcColor = new Color(1.0f, 0.85f, 0.25f, 1f), // Radiant Solar Flare
                    numPillars = 4,
                    beaconHeight = 25f,
                    ringRadius = 2.6f
                };
            case 2:
                return new StandThemeData
                {
                    standID = 2,
                    themeName = "Abyssal Altar",
                    primaryColor = new Color(0.05f, 0.78f, 1.0f, 1f),      // Ocean Azure (#0DC7FF)
                    secondaryColor = new Color(0.0f, 0.35f, 0.95f, 1f),     // Cobalt Trench (#0059F2)
                    dialPointerColor = new Color(0.65f, 0.95f, 1.0f, 1f),  // Sea Foam White
                    resonanceArcColor = new Color(0.1f, 0.9f, 1.0f, 1f),   // Bioluminescent Arc
                    numPillars = 6,
                    beaconHeight = 24f,
                    ringRadius = 2.7f
                };
            case 3:
                return new StandThemeData
                {
                    standID = 3,
                    themeName = "Serpent Altar",
                    primaryColor = new Color(0.10f, 1.0f, 0.42f, 1f),      // Venom Emerald (#1AFF6B)
                    secondaryColor = new Color(0.02f, 0.65f, 0.25f, 1f),   // Ancient Jade (#05A640)
                    dialPointerColor = new Color(0.77f, 1.0f, 0.54f, 1f),  // Viper Lime
                    resonanceArcColor = new Color(0.2f, 1.0f, 0.5f, 1f),   // Acid Spark Arc
                    numPillars = 4,
                    beaconHeight = 26f,
                    ringRadius = 2.6f
                };
            case 4:
                return new StandThemeData
                {
                    standID = 4,
                    themeName = "Blood Altar",
                    primaryColor = new Color(1.0f, 0.12f, 0.25f, 1f),      // Crimson Blood (#FF1E40)
                    secondaryColor = new Color(1.0f, 0.42f, 0.02f, 1f),    // Hellfire Ember (#FF6B05)
                    dialPointerColor = new Color(1.0f, 0.7f, 0.7f, 1f),    // Ruby Shard
                    resonanceArcColor = new Color(1.0f, 0.25f, 0.15f, 1f), // Magma Lightning
                    numPillars = 5,
                    beaconHeight = 28f,
                    ringRadius = 2.8f
                };
            case 5:
                return new StandThemeData
                {
                    standID = 5,
                    themeName = "Void Altar",
                    primaryColor = new Color(0.75f, 0.15f, 1.0f, 1f),      // Astral Amethyst (#BF26FF)
                    secondaryColor = new Color(0.35f, 0.10f, 0.95f, 1f),   // Cosmic Indigo (#591AF2)
                    dialPointerColor = new Color(0.88f, 0.75f, 1.0f, 1f),  // Starlight Magenta
                    resonanceArcColor = new Color(0.85f, 0.35f, 1.0f, 1f), // Warp Spark Arc
                    numPillars = 8,
                    beaconHeight = 30f,
                    ringRadius = 2.9f
                };
            case 6:
                return new StandThemeData
                {
                    standID = 6,
                    themeName = "Frost Altar",
                    primaryColor = new Color(0.55f, 0.90f, 1.0f, 1f),      // Diamond Ice (#8CE6FF)
                    secondaryColor = new Color(0.85f, 0.96f, 1.0f, 1f),    // Glacial Frost (#D9F5FF)
                    dialPointerColor = new Color(0.95f, 0.98f, 1.0f, 1f),  // Cryo Crystal White
                    resonanceArcColor = new Color(0.6f, 0.95f, 1.0f, 1f),  // Blizzard Ice Arc
                    numPillars = 6,
                    beaconHeight = 26f,
                    ringRadius = 2.7f
                };
            case 7:
                return new StandThemeData
                {
                    standID = 7,
                    themeName = "Storm Altar",
                    primaryColor = new Color(0.20f, 0.85f, 1.0f, 1f),      // Arc Plasma (#33D9FF)
                    secondaryColor = new Color(0.85f, 0.35f, 1.0f, 1f),    // Voltage Violet (#D959FF)
                    dialPointerColor = new Color(1.0f, 1.0f, 0.4f, 1f),    // Lightning Gold
                    resonanceArcColor = new Color(0.3f, 0.9f, 1.0f, 1f),   // Tesla Crackle Arc
                    numPillars = 4,
                    beaconHeight = 32f,
                    ringRadius = 2.8f
                };
            case 8:
            default:
                return new StandThemeData
                {
                    standID = id > 0 ? id : 8,
                    themeName = "Heart Altar",
                    primaryColor = new Color(1.0f, 0.55f, 0.05f, 1f),      // Primordial Gold (#FF8C0D)
                    secondaryColor = new Color(1.0f, 0.15f, 0.35f, 1f),    // Heart Ruby (#FF2659)
                    dialPointerColor = new Color(1.0f, 0.95f, 0.85f, 1f),  // Iridescent Divine
                    resonanceArcColor = new Color(1.0f, 0.75f, 0.3f, 1f),  // Mythic Radiance Arc
                    numPillars = 8,
                    beaconHeight = 36f,
                    ringRadius = 3.0f
                };
        }
    }

    // ── StandPoint Glowing VFX Marker System ──────────────────────────────────
    private void UpdateStandPointGlow(bool active)
    {
        if (active)
        {
            if (!_glowMarkerActive || _standPointMarkerObj == null)
            {
                CreateStandPointMarker();
            }

            if (_standPointMarkerObj != null && playerStandPoint != null)
            {
                // CRITICAL: Ground flush position, STRICTLY WORLD HORIZONTAL ROTATION (NO TILT)
                _cachedGroundPos = GetGroundPosition(playerStandPoint.position);
                _standPointMarkerObj.transform.position = _cachedGroundPos;
                _standPointMarkerObj.transform.rotation = Quaternion.identity;

                PlatePickup heldPlate = GetCurrentOrChildPlate();
                bool holdingKey = (heldPlate != null && (heldPlate.plateID == standID || standID == 0));
                var theme = GetStandTheme(standID);

                // Pulsing radiant light
                if (_standPointLight != null)
                {
                    float baseInt = holdingKey ? 11.5f : 8.0f;
                    float pulse = Mathf.PingPong(Time.time * (holdingKey ? 4.5f : 2.5f), 3.5f);
                    _standPointLight.intensity = baseInt + pulse;
                    _standPointLight.color = holdingKey ? theme.primaryColor : Color.Lerp(theme.primaryColor, theme.secondaryColor, 0.35f);
                }

                // Breathing ring pulse
                float pulseW = Mathf.Sin(Time.time * 3f) * 0.08f;
                if (_standPointOuterRing != null)
                {
                    _standPointOuterRing.startWidth = 0.38f + pulseW;
                    _standPointOuterRing.endWidth = 0.38f + pulseW;
                }
                if (_standPointInnerRing != null)
                {
                    _standPointInnerRing.startWidth = 0.26f + pulseW * 0.5f;
                    _standPointInnerRing.endWidth = 0.26f + pulseW * 0.5f;
                }

                // Spin and bob floating central sacred relic
                if (_hoverCrystal != null)
                {
                    _hoverCrystal.transform.Rotate(Vector3.up, (holdingKey ? 85f : 55f) * Time.deltaTime, Space.World);
                    float bob = Mathf.Sin(Time.time * 2.8f) * 0.25f;
                    _hoverCrystal.transform.localPosition = new Vector3(0f, 1.65f + bob, 0f);
                }

                // Orbit 3 satellite rune shards in a tilted dynamic orbit
                if (_orbitCrystals != null)
                {
                    float orbitSpeed = holdingKey ? 90f : 60f;
                    for (int i = 0; i < _orbitCrystals.Length; i++)
                    {
                        if (_orbitCrystals[i] == null) continue;
                        float a = (Time.time * orbitSpeed + (i * 120f)) * Mathf.Deg2Rad;
                        float r = 0.95f;
                        float ox = Mathf.Cos(a) * r;
                        float oz = Mathf.Sin(a) * r;
                        float oy = 1.6f + Mathf.Sin(a * 1.5f + Time.time * 2.5f) * 0.25f;
                        _orbitCrystals[i].transform.localPosition = new Vector3(ox, oy, oz);
                        _orbitCrystals[i].transform.Rotate(Vector3.up, 100f * Time.deltaTime, Space.Self);
                    }
                }
            }
        }
        else
        {
            if (_glowMarkerActive)
            {
                DestroyStandPointMarker();
            }
        }
    }

    private void CreateStandPointMarker()
    {
        DestroyStandPointMarker();

        Transform psp = playerStandPoint != null ? playerStandPoint : transform;
        _cachedGroundPos = GetGroundPosition(psp.position);

        var theme = GetStandTheme(standID);

        _standPointMarkerObj = new GameObject($"Stand_{standID}_GlowMarker");
        // CRITICAL: Strictly unscaled root and WORLD HORIZONTAL (Quaternion.identity)
        _standPointMarkerObj.transform.position = _cachedGroundPos;
        _standPointMarkerObj.transform.rotation = Quaternion.identity;
        _standPointMarkerObj.transform.localScale = Vector3.one;

        // 1. Glowing 3D Sacred Ground Disc (Flush with terrain, themed color)
        float padDiameter = theme.ringRadius * 1.8f;
        GameObject discObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discObj.name = "GroundPad_Disc";
        discObj.transform.SetParent(_standPointMarkerObj.transform, false);
        discObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        discObj.transform.localRotation = Quaternion.identity;
        discObj.transform.localScale = new Vector3(padDiameter, 0.02f, padDiameter);
        var discCol = discObj.GetComponent<Collider>();
        if (discCol != null) Destroy(discCol);

        var discRenderer = discObj.GetComponent<MeshRenderer>();
        Color padColor = new Color(theme.primaryColor.r, theme.primaryColor.g, theme.primaryColor.b, 0.65f);
        discRenderer.material = CreateEmissiveMat(padColor, true);

        // 2. High-Visibility Outer Astrolabe Neon Ring
        GameObject outerRingObj = new GameObject("OuterNeonRing");
        outerRingObj.transform.SetParent(_standPointMarkerObj.transform, false);
        _standPointOuterRing = outerRingObj.AddComponent<LineRenderer>();
        _standPointOuterRing.useWorldSpace = false;
        _standPointOuterRing.loop = true;
        _standPointOuterRing.positionCount = 48;
        _standPointOuterRing.startWidth = 0.42f;
        _standPointOuterRing.endWidth = 0.42f;
        _standPointOuterRing.startColor = Color.white;
        _standPointOuterRing.endColor = Color.white;
        _standPointOuterRing.numCapVertices = 4;
        _standPointOuterRing.numCornerVertices = 4;

        Material outerMat = CreateEmissiveMat(theme.primaryColor, true);
        _standPointOuterRing.material = outerMat;

        float outerRadius = theme.ringRadius;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * (360f / 48) * Mathf.Deg2Rad;
            _standPointOuterRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * outerRadius, 0.09f, Mathf.Sin(angle) * outerRadius));
        }

        // 3. Inner Mystic Seal Ring
        GameObject innerRingObj = new GameObject("InnerNeonRing");
        innerRingObj.transform.SetParent(_standPointMarkerObj.transform, false);
        _standPointInnerRing = innerRingObj.AddComponent<LineRenderer>();
        _standPointInnerRing.useWorldSpace = false;
        _standPointInnerRing.loop = true;
        _standPointInnerRing.positionCount = 36;
        _standPointInnerRing.startWidth = 0.30f;
        _standPointInnerRing.endWidth = 0.30f;
        _standPointInnerRing.startColor = Color.white;
        _standPointInnerRing.endColor = Color.white;
        _standPointInnerRing.numCapVertices = 4;
        _standPointInnerRing.numCornerVertices = 4;

        Material innerMat = CreateEmissiveMat(theme.secondaryColor, true);
        _standPointInnerRing.material = innerMat;

        float innerRadius = theme.ringRadius * 0.52f;
        for (int i = 0; i < 36; i++)
        {
            float angle = i * (360f / 36) * Mathf.Deg2Rad;
            _standPointInnerRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * innerRadius, 0.10f, Mathf.Sin(angle) * innerRadius));
        }

        // 4. Sacred 8-Point Runic Compass Star (connecting outer & inner seal)
        GameObject compassObj = new GameObject("SacredCompassStar");
        compassObj.transform.SetParent(_standPointMarkerObj.transform, false);
        _standPointCompassRays = compassObj.AddComponent<LineRenderer>();
        _standPointCompassRays.useWorldSpace = false;
        _standPointCompassRays.loop = true;
        _standPointCompassRays.positionCount = 16;
        _standPointCompassRays.startWidth = 0.20f;
        _standPointCompassRays.endWidth = 0.20f;
        _standPointCompassRays.startColor = Color.white;
        _standPointCompassRays.endColor = Color.white;
        _standPointCompassRays.numCapVertices = 4;
        _standPointCompassRays.numCornerVertices = 4;
        _standPointCompassRays.material = CreateEmissiveMat(Color.Lerp(theme.primaryColor, theme.secondaryColor, 0.5f), true);

        for (int i = 0; i < 16; i++)
        {
            float rad = (i % 2 == 0) ? outerRadius * 0.96f : innerRadius * 0.72f;
            float ang = i * 22.5f * Mathf.Deg2Rad;
            _standPointCompassRays.SetPosition(i, new Vector3(Mathf.Cos(ang) * rad, 0.095f, Mathf.Sin(ang) * rad));
        }

        // 5. Vertical Light Beam Ribbons (theme.numPillars beams rising theme.beaconHeight into sky!)
        int pillarCount = Mathf.Max(4, theme.numPillars);
        _standPointPillars = new LineRenderer[pillarCount];
        for (int p = 0; p < pillarCount; p++)
        {
            GameObject pillarObj = new GameObject($"Pillar_{p}");
            pillarObj.transform.SetParent(_standPointMarkerObj.transform, false);
            var lr = pillarObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.startWidth = 0.40f;
            lr.endWidth = 0.06f;
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.numCapVertices = 4;
            lr.material = outerMat;

            float pAngle = p * (360f / pillarCount) * Mathf.Deg2Rad;
            Vector3 basePt = new Vector3(Mathf.Cos(pAngle) * outerRadius, 0.11f, Mathf.Sin(pAngle) * outerRadius);
            Vector3 topPt = basePt + Vector3.up * theme.beaconHeight;
            lr.SetPosition(0, basePt);
            lr.SetPosition(1, topPt);
            _standPointPillars[p] = lr;
        }

        // 6. Central Floating Sacred Diamond Core (hovering at Y = 1.65m)
        GameObject hoverObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hoverObj.name = "HoverDiamondCore";
        hoverObj.transform.SetParent(_standPointMarkerObj.transform, false);
        hoverObj.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        hoverObj.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        hoverObj.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
        var hoverCol = hoverObj.GetComponent<Collider>();
        if (hoverCol != null) Destroy(hoverCol);
        var hoverRend = hoverObj.GetComponent<MeshRenderer>();
        hoverRend.material = CreateEmissiveMat(theme.primaryColor * 2.8f, true);
        _hoverCrystal = hoverObj;

        // 7. 3 Orbiting Satellite Rune Shards
        _orbitCrystals = new GameObject[3];
        for (int s = 0; s < 3; s++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = $"OrbitShard_{s}";
            shard.transform.SetParent(_standPointMarkerObj.transform, false);
            shard.transform.localScale = new Vector3(0.26f, 0.38f, 0.26f);
            shard.transform.localRotation = Quaternion.Euler(30f, s * 60f, 45f);
            var sc = shard.GetComponent<Collider>();
            if (sc != null) Destroy(sc);
            shard.GetComponent<MeshRenderer>().material = CreateEmissiveMat(theme.secondaryColor * 2.5f, true);
            _orbitCrystals[s] = shard;
        }

        // 8. Powerful Radiant Point Light
        GameObject lightObj = new GameObject("StandPoint_Light");
        lightObj.transform.SetParent(_standPointMarkerObj.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        _standPointLight = lightObj.AddComponent<Light>();
        _standPointLight.type = LightType.Point;
        _standPointLight.color = theme.primaryColor;
        _standPointLight.range = 22.0f;
        _standPointLight.intensity = 9.0f;
        _standPointLight.shadows = LightShadows.None;

        // 9. Upward Particle Beacon
        CreateParticleBeacon(_standPointMarkerObj.transform, theme.primaryColor, theme.secondaryColor);

        _glowMarkerActive = true;
    }

    private void CreateParticleBeacon(Transform parent, Color colPrimary, Color colSecondary)
    {
        GameObject psObj = new GameObject("StandPoint_BeaconParticles");
        psObj.SetActive(false); // Inactive prevents autoplay warnings when setting duration
        psObj.transform.SetParent(parent, false);
        psObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        psObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        _standPointParticles = psObj.AddComponent<ParticleSystem>();
        var main = _standPointParticles.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        main.startColor = colPrimary;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        var emission = _standPointParticles.emission;
        emission.rateOverTime = 30f;

        var shape = _standPointParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2.0f;

        var colOverLifetime = _standPointParticles.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(colPrimary, 0f), new GradientColorKey(colSecondary, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;

        var pRenderer = psObj.GetComponent<ParticleSystemRenderer>();
        pRenderer.material = CreateEmissiveMat(colPrimary, true);
        psObj.SetActive(true);
        _standPointParticles.Play();
    }

    private void DestroyStandPointMarker()
    {
        if (_standPointMarkerObj != null)
        {
            Destroy(_standPointMarkerObj);
            _standPointMarkerObj = null;
        }
        _hoverCrystal = null;
        _orbitCrystals = null;
        _standPointLight = null;
        _standPointOuterRing = null;
        _standPointInnerRing = null;
        _standPointCompassRays = null;
        _standPointPillars = null;
        _standPointParticles = null;
        _glowMarkerActive = false;
    }

    // ── Main Placement & Rotation Sequence ───────────────────────────────────
    private IEnumerator PlaceAndAlignSequence()
    {
        _isPlacing = true;
        _playerAtStandPoint = false;
        CachePlayerReferences(null);

        // STEP 0: Pehle weapon/shield unequip karo agar player ke hath mein ho
        if (_playerController != null && (_playerController.IsHoldingWeapon || _playerController.WeaponIndex != 0))
        {
            Debug.Log("[PlateStandInteraction] Weapon in hand detected. Ensuring unarmed before placement...");
            float unequipTimer = 0f;
            StartCoroutine(_playerController.UnequipCurrentWeaponForPickup());
            while (_playerController.IsHoldingWeapon && unequipTimer < 1.0f)
            {
                unequipTimer += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.1f);
        }

        // STEP 1: Freeze Player
        if (_playerController != null)
        {
            _playerController.SetFrozen(true, lockWorldPosition: false);
        }

        Vector3 groundPos = (_cachedGroundPos != Vector3.zero) ? _cachedGroundPos : (playerStandPoint != null ? playerStandPoint.position : transform.position);

        // STEP 2: Snap player position and align to face StandPlateSocket
        if (_playerController != null)
        {
            _playerController.transform.position = new Vector3(groundPos.x, _playerController.transform.position.y, groundPos.z);

            Vector3 lookTarget = standPlateSocket != null ? standPlateSocket.position : _standRoot.position;
            Vector3 lookDir = lookTarget - _playerController.transform.position;
            lookDir.y = 0f;
            Quaternion targetFacingRot = (lookDir.sqrMagnitude > 0.001f)
                ? Quaternion.LookRotation(lookDir.normalized)
                : Quaternion.identity;

            _playerController.transform.rotation = targetFacingRot;
        }

        // STEP 3: Extinguish the glowing stand point marker with a sparkle flash
        VFXUtil.Burst(groundPos + Vector3.up * 0.2f, Vector3.up, new Color(1f, 0.85f, 0.3f, 0.95f),
            25, 1.2f, 3.5f, 0.08f, 0.22f, 0.4f, 0.8f, true, false, 0.1f, 60f, 1.5f);
        DestroyStandPointMarker();

        // Resolve Player Animator
        if (_playerAnimator == null && _playerController != null)
        {
            _playerAnimator = _playerController.Animator ?? _playerController.GetComponent<Animator>() ?? _playerController.GetComponentInChildren<Animator>();
        }

        // Disable upper body layer weights so full body plays PlacedObject cleanly
        if (_playerAnimator != null)
        {
            _playerAnimator.applyRootMotion = false;
            for (int i = 1; i < _playerAnimator.layerCount; i++)
            {
                _playerAnimator.SetLayerWeight(i, 0f);
            }
            _playerAnimator.SetInteger("WeaponState", 0);
            _playerAnimator.SetFloat("Speed", 0f);

            // Reset all conflicting pickup triggers so they never fire
            _playerAnimator.ResetTrigger("PickupObject");
            _playerAnimator.ResetTrigger("PickupSword");
            _playerAnimator.ResetTrigger("PickupGun");
            _playerAnimator.ResetTrigger("PickupOar");
        }

        // STEP 4: Play PlacedObject animation
        Debug.Log("[PlateStandInteraction] Firing PlacedObject animation on player animator...");
        if (_playerAnimator != null)
        {
            _playerAnimator.ResetTrigger(placeAnimTrigger);
            _playerAnimator.SetTrigger(placeAnimTrigger);
            _playerAnimator.CrossFadeInFixedTime(placeAnimState, 0.12f, 0, 0f);
        }

        // STEP 5: Wait for hands to reach the stand socket (~0.85s, hands placing the object down)
        yield return new WaitForSeconds(0.85f);

        // STEP 6: Snap plate onto StandPlateSocket with exact scale preservation
        PlatePickup plateToPlace = GetCurrentOrChildPlate();
        if (plateToPlace == null)
        {
            // Auto search scene for Object {standID}
            var allPlates = FindObjectsByType<PlatePickup>(FindObjectsSortMode.None);
            foreach (var p in allPlates)
            {
                if (p.plateID == standID || standID == 0)
                {
                    plateToPlace = p;
                    break;
                }
            }
        }

        if (plateToPlace == null)
        {
            // Auto instantiate from prefab if missing (e.g. testing directly in front of stand)
            string prefabPath = $"Assets/Prefeb/final object prefeb/Object {standID}.prefab";
#if UNITY_EDITOR
            var prefabObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabObj != null)
            {
                var spawned = Instantiate(prefabObj);
                spawned.name = $"Object {standID}";
                plateToPlace = spawned.GetComponent<PlatePickup>();
            }
#endif
        }

        if (plateToPlace != null && standPlateSocket != null)
        {
            plateToPlace.PlaceOnStand(standPlateSocket, platePlacementLocalOffset, platePlacementLocalRotation);
            _placedPlate = plateToPlace;
            _isPlaced = true;
            _plateArrow = FindPlateArrow(plateToPlace.transform);
            Debug.Log($"[PlateStandInteraction] Object successfully snapped onto socket: {standPlateSocket.name}");

            // Placement snap sparkle burst VFX
            VFXUtil.Burst(standPlateSocket.position + Vector3.up * 0.08f, Vector3.up, new Color(1f, 0.88f, 0.35f, 1f),
                18, 1.0f, 2.2f, 0.05f, 0.15f, 0.2f, 0.5f, true, true, 0.1f, 40f, 1.2f);
        }

        // STEP 7: Wait for placing animation to finish and player to stand upright into Idle (~0.95s)
        yield return new WaitForSeconds(0.95f);

        // Player stays standing idle and frozen in place (no rotate animation on character)
        if (_playerAnimator != null)
        {
            _playerAnimator.CrossFadeInFixedTime("UnarmedLocomotion", 0.15f, 0, 0f);
            _playerAnimator.SetFloat("Speed", 0f);
        }

        // STEP 8: Switch camera to CameraStandpoint (x axis downward view of the stand)
        if (cameraStandPoint == null)
        {
            AutoFindReferences();
        }

        var cam = CameraController.Instance ?? FindFirstObjectByType<CameraController>() ?? (Camera.main != null ? Camera.main.GetComponent<CameraController>() : null);
        if (cam != null)
        {
            cam.EnterStandMode(standPlateSocket, _playerController != null ? _playerController.transform : null, cameraStandPoint);
        }

        yield return new WaitForSeconds(0.2f);

        // STEP 9: Enter Interactive Direct Object Rotation Puzzle (A & D rotate object directly)
        yield return StartCoroutine(RotatePuzzleRoutine());

        // STEP 10: Success Sequence & Door Opening (Camera returns behind player and player unfreezes)
        yield return StartCoroutine(SuccessSequenceRoutine());
    }

    private IEnumerator RotatePuzzleRoutine()
    {
        _isRotatingPuzzle = true;
        _isRotatingKeyPressed = false;
        _lastPuzzleAngle = 180f;

        // Ensure dial starts visibly misaligned so player has to rotate it
        float initialAngle = GetArrowAngle();
        if (initialAngle <= alignmentAngleThreshold + 15f)
        {
            Vector3 axis = GetAxisVector(rotationAxis);
            if (standPlateSocket != null)
            {
                standPlateSocket.Rotate(axis, 115f, Space.Self);
            }
            initialAngle = GetArrowAngle();
        }

        _currentPuzzleAngle = initialAngle;
        _signedPuzzleAngle = GetSignedArrowAngle();
        _lastPuzzleAngle = initialAngle;

        // Create 3D In-World Glowing Alignment Markers on Rim & Dial
        CreatePuzzleAlignmentMarkers();

        bool wasAligned = false;
        Debug.Log($"<color=#00FF88>[PlateStandInteraction] Stand {standID}: Direct Rotation Puzzle active. Initial Angle: {initialAngle:F1}°. Use A / D to rotate.</color>");

        while (true)
        {
            // Read A / D inputs
            bool aPressed = false;
            bool dPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) aPressed = true;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) dPressed = true;
            }
#endif
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) aPressed = true;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dPressed = true;

            _isRotatingKeyPressed = aPressed || dPressed;

            float rotationInput = 0f;
            if (aPressed) rotationInput -= 1f;
            if (dPressed) rotationInput += 1f;

            if (invertRotationDirection) rotationInput = -rotationInput;

            // Direct Object Rotation (player stays frozen in idle, no character rotate animation)
            if (Mathf.Abs(rotationInput) > 0.01f && standPlateSocket != null)
            {
                Vector3 axis = GetAxisVector(rotationAxis);
                float deltaAngle = rotationInput * rotationSpeed * Time.deltaTime;
                standPlateSocket.Rotate(axis, deltaAngle, Space.Self);

                // Ratchet tick audio with proximity pitch modulation
                if (Time.time >= _nextTickTime)
                {
                    _nextTickTime = Time.time + 0.075f;
                    AudioClip clip = (rotateTickSound != null) ? rotateTickSound : GetProceduralTickClip();
                    if (clip != null)
                    {
                        float pitch = Mathf.Lerp(1.28f, 0.88f, _currentPuzzleAngle / 180f);
                        PlayAudioWithPitch(clip, standPlateSocket.position, 0.45f, pitch);
                    }
                }
            }

            // Real-time angle & alignment tracking
            _currentPuzzleAngle = GetArrowAngle();
            _signedPuzzleAngle = GetSignedArrowAngle();

            if (_isRotatingKeyPressed)
            {
                if (_currentPuzzleAngle < _lastPuzzleAngle - 0.02f)
                    _isMovingCloser = true;
                else if (_currentPuzzleAngle > _lastPuzzleAngle + 0.02f)
                    _isMovingCloser = false;
            }
            _lastPuzzleAngle = _currentPuzzleAngle;

            // Update in-world markers glow & color proximity
            UpdatePuzzleMarkersVisuals(_currentPuzzleAngle);

            bool isAligned = (_currentPuzzleAngle <= alignmentAngleThreshold);

            if (isAligned && !wasAligned)
            {
                Debug.Log($"<color=#00FF88>[PlateStandInteraction] Stand {standID}: Target Arrow ALIGNED! (Angle: {_currentPuzzleAngle:F1}°). Opening Door...</color>");

                // Micro sparkle feedback on alignment pass
                var theme = GetStandTheme(standID);
                Vector3 burstPos = (standTargetArrow != null) ? standTargetArrow.position : standPlateSocket.position;
                VFXUtil.Burst(burstPos, Vector3.up, theme.primaryColor,
                    25, 1.2f, 3.0f, 0.06f, 0.16f, 0.25f, 0.6f, true, true, 0.1f, 40f, 1.4f);

                break; // PUZZLE SOLVED! Exit immediately!
            }

            wasAligned = isAligned;
            yield return null;
        }

        // Clean up 3D guide markers
        DestroyPuzzleAlignmentMarkers();
        _isRotatingPuzzle = false;
        _isRotatingKeyPressed = false;
    }

    private IEnumerator SuccessSequenceRoutine()
    {
        Debug.Log($"<color=#00FF88>[PlateStandInteraction] Stand {standID}: Arrow puzzle SOLVED! Triggering AAA VFX & Door Opening...</color>");

        // 1. Snap plate to 100% exact alignment
        SnapPlateToExactTarget();

        // 2. Play AAA VFX Sequence (Golden lighting, electric sparks, glowing core)
        TriggerSuccessVFX();

        if (successSound != null)
        {
            AudioSource.PlayClipAtPoint(successSound, standPlateSocket != null ? standPlateSocket.position : transform.position, 1.0f);
        }

        // 3. Open Secret Room Door!
        if (targetDoor == null)
        {
            InitializeTargetDoor();
        }

        if (targetDoor != null)
        {
            Debug.Log($"<color=#00FF88>[PlateStandInteraction] Opening Secret Room Door: '{targetDoor.name}'</color>");
            targetDoor.OpenFromPlatePuzzle();
            targetDoor.OpenByManager();
        }
        else
        {
            Debug.LogWarning($"[PlateStandInteraction] Target door was null! Searching scene for any Secret Room Door...");
            var allDoors = Object.FindObjectsByType<DoubleSlidingDoor>(FindObjectsSortMode.None);
            foreach (var d in allDoors)
            {
                string n = d.name.ToLower();
                if (n.Contains("secret") || n.Contains("sceret") || n.Contains("secert") || n.Contains("room"))
                {
                    targetDoor = d;
                    targetDoor.OpenFromPlatePuzzle();
                    targetDoor.OpenByManager();
                    break;
                }
            }
        }

        // Small delay (0.5s) to view the golden burst and see door begin sliding open
        yield return new WaitForSeconds(0.5f);

        // 4. Restore Gameplay Camera (Exit Stand Mode - smoothly returns camera behind player)
        var cam = CameraController.Instance ?? Object.FindFirstObjectByType<CameraController>() ?? (Camera.main != null ? Camera.main.GetComponent<CameraController>() : null);
        if (cam != null)
        {
            cam.ExitStandMode();
        }

        // 5. Restore Player Control (Unfreeze)
        if (_playerController != null)
        {
            _playerController.SetFrozen(false);
        }

        if (_playerAnimator != null)
        {
            _playerAnimator.applyRootMotion = false;
            _playerAnimator.CrossFadeInFixedTime("UnarmedLocomotion", 0.15f, 0, 0f);
            _playerAnimator.SetFloat("Speed", 0f);

            // Restore Shooting Layer weight if multi-layer
            if (_playerAnimator.layerCount > 1)
            {
                _playerAnimator.SetLayerWeight(1, 1f);
            }
        }

        _isPlacing = false;
        _isRotatingPuzzle = false;
        _isCompleted = true;
        Debug.Log($"<color=#00FF88><b>[PlateStandInteraction]</b> Stand {standID} interaction completed! Door opened, camera restored, player unfrozen.</color>");
    }

    // ── Arrow Alignment Geometry ─────────────────────────────────────────────
    public float GetArrowAngle()
    {
        Transform plateA = _plateArrow;
        if (plateA == null && _placedPlate != null)
        {
            plateA = FindPlateArrow(_placedPlate.transform);
            _plateArrow = plateA;
        }

        if (plateA == null || standTargetArrow == null)
        {
            return 0f;
        }

        Vector3 fwdPlate = plateA.forward;
        Vector3 fwdTarget = standTargetArrow.forward;

        switch (rotationAxis)
        {
            case RotationAxisMode.Y_Axis:
                fwdPlate.y = 0f;
                fwdTarget.y = 0f;
                break;
            case RotationAxisMode.X_Axis:
                fwdPlate.x = 0f;
                fwdTarget.x = 0f;
                break;
            case RotationAxisMode.Z_Axis:
                fwdPlate.z = 0f;
                fwdTarget.z = 0f;
                break;
        }

        if (fwdPlate.sqrMagnitude < 0.001f || fwdTarget.sqrMagnitude < 0.001f)
            return 0f;

        return Vector3.Angle(fwdPlate.normalized, fwdTarget.normalized);
    }

    public float GetSignedArrowAngle()
    {
        Transform plateA = _plateArrow;
        if (plateA == null && _placedPlate != null)
        {
            plateA = FindPlateArrow(_placedPlate.transform);
            _plateArrow = plateA;
        }

        if (plateA == null || standTargetArrow == null)
            return 0f;

        Vector3 axis = GetAxisVector(rotationAxis);
        Vector3 fwdPlate = plateA.forward;
        Vector3 fwdTarget = standTargetArrow.forward;

        Vector3 normal = (standPlateSocket != null) ? standPlateSocket.TransformDirection(axis) : axis;
        return Vector3.SignedAngle(fwdPlate, fwdTarget, normal);
    }

    // ── 3D In-World Alignment Guide Visuals ──────────────────────────────────
    private void CreatePuzzleAlignmentMarkers()
    {
        DestroyPuzzleAlignmentMarkers();

        if (standPlateSocket == null) return;

        // Measure rim radius from placed plate
        float rimRadius = 0.65f;
        if (_placedPlate != null)
        {
            var rends = _placedPlate.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                float ext = Mathf.Max(r.bounds.extents.x, r.bounds.extents.z);
                if (ext > 0.05f)
                {
                    rimRadius = Mathf.Clamp(ext * 0.96f, 0.40f, 3.5f);
                    break;
                }
            }
        }

        var theme = GetStandTheme(standID);

        Vector3 targetFwd = (standTargetArrow != null) ? standTargetArrow.forward : _standRoot.forward;
        targetFwd.y = 0f;
        if (targetFwd.sqrMagnitude < 0.01f) targetFwd = Vector3.forward;
        targetFwd.Normalize();

        // 1. Fixed Elemental Altar Crest on Rim (Pointing inward toward center) - STRICTLY ROOT OBJECT SCALE 1
        _puzzleTargetMarkerObj = new GameObject($"Stand_{standID}_AltarCrest");
        _puzzleTargetMarkerObj.transform.position = standPlateSocket.position + (targetFwd * (rimRadius + 0.12f)) + (Vector3.up * 0.06f);
        _puzzleTargetMarkerObj.transform.rotation = Quaternion.LookRotation(-targetFwd, Vector3.up);
        _puzzleTargetMarkerObj.transform.localScale = Vector3.one;

        // A. Central Relic Diamond
        GameObject crestDiamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crestDiamond.name = "CrestDiamond";
        crestDiamond.transform.SetParent(_puzzleTargetMarkerObj.transform, false);
        crestDiamond.transform.localPosition = Vector3.zero;
        crestDiamond.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        crestDiamond.transform.localScale = new Vector3(0.24f, 0.09f, 0.24f);
        Destroy(crestDiamond.GetComponent<Collider>());
        _puzzleTargetMeshRend = crestDiamond.GetComponent<MeshRenderer>();
        _puzzleTargetMeshRend.material = CreateEmissiveMat(theme.primaryColor * 2.8f, true);

        // B. Left Wing
        GameObject leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWing.name = "LeftWing";
        leftWing.transform.SetParent(_puzzleTargetMarkerObj.transform, false);
        leftWing.transform.localPosition = new Vector3(-0.16f, 0.01f, -0.06f);
        leftWing.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        leftWing.transform.localScale = new Vector3(0.18f, 0.06f, 0.08f);
        Destroy(leftWing.GetComponent<Collider>());
        leftWing.GetComponent<MeshRenderer>().material = CreateEmissiveMat(theme.secondaryColor * 2.2f, true);

        // C. Right Wing
        GameObject rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWing.name = "RightWing";
        rightWing.transform.SetParent(_puzzleTargetMarkerObj.transform, false);
        rightWing.transform.localPosition = new Vector3(0.16f, 0.01f, -0.06f);
        rightWing.transform.localRotation = Quaternion.Euler(0f, -25f, 0f);
        rightWing.transform.localScale = new Vector3(0.18f, 0.06f, 0.08f);
        Destroy(rightWing.GetComponent<Collider>());
        rightWing.GetComponent<MeshRenderer>().material = CreateEmissiveMat(theme.secondaryColor * 2.2f, true);

        // D. Inward Target Pointer Spear
        GameObject pointerSpear = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pointerSpear.name = "PointerSpear";
        pointerSpear.transform.SetParent(_puzzleTargetMarkerObj.transform, false);
        pointerSpear.transform.localPosition = new Vector3(0f, 0.015f, 0.14f);
        pointerSpear.transform.localRotation = Quaternion.identity;
        pointerSpear.transform.localScale = new Vector3(0.06f, 0.05f, 0.20f);
        Destroy(pointerSpear.GetComponent<Collider>());
        pointerSpear.GetComponent<MeshRenderer>().material = CreateEmissiveMat(theme.primaryColor * 3.2f, true);

        // E. Altar Crest Point Light
        _puzzleMarkerLight = _puzzleTargetMarkerObj.AddComponent<Light>();
        _puzzleMarkerLight.type = LightType.Point;
        _puzzleMarkerLight.color = theme.primaryColor;
        _puzzleMarkerLight.range = 3.0f;
        _puzzleMarkerLight.intensity = 6.5f;

        // 2. Rotating Elemental Needle (STRICTLY ROOT OBJECT SCALE 1, syncs with plate arrow)
        _puzzleDialPointerObj = new GameObject($"Dial_Needle_{standID}");
        _puzzleDialPointerObj.transform.localScale = Vector3.one;

        Transform pArrow = _plateArrow != null ? _plateArrow : FindPlateArrow(standPlateSocket);
        Vector3 worldDialFwd = (pArrow != null) ? pArrow.forward : (standPlateSocket != null ? standPlateSocket.forward : Vector3.forward);
        worldDialFwd.y = 0f;
        if (worldDialFwd.sqrMagnitude < 0.01f) worldDialFwd = Vector3.forward;
        worldDialFwd.Normalize();

        _puzzleDialPointerObj.transform.position = standPlateSocket.position + Vector3.up * 0.05f;
        _puzzleDialPointerObj.transform.rotation = Quaternion.LookRotation(worldDialFwd, Vector3.up);

        // A. Full Radial Needle Bar (Extending across the plate surface)
        GameObject needleBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        needleBar.name = "NeedleBar";
        needleBar.transform.SetParent(_puzzleDialPointerObj.transform, false);
        needleBar.transform.localPosition = new Vector3(0f, 0.01f, rimRadius * 0.48f);
        needleBar.transform.localRotation = Quaternion.identity;
        needleBar.transform.localScale = new Vector3(0.07f, 0.035f, rimRadius * 0.94f);
        Destroy(needleBar.GetComponent<Collider>());
        _puzzleDialMeshRend = needleBar.GetComponent<MeshRenderer>();
        _puzzleDialMeshRend.material = CreateEmissiveMat(theme.dialPointerColor * 2.8f, true);

        // B. Needle Arrow Head (Pointing outward to meet Altar Crest)
        GameObject needleHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        needleHead.name = "NeedleHead";
        needleHead.transform.SetParent(_puzzleDialPointerObj.transform, false);
        needleHead.transform.localPosition = new Vector3(0f, 0.02f, rimRadius * 0.96f);
        needleHead.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        needleHead.transform.localScale = new Vector3(0.18f, 0.05f, 0.18f);
        Destroy(needleHead.GetComponent<Collider>());
        needleHead.GetComponent<MeshRenderer>().material = CreateEmissiveMat(theme.dialPointerColor * 3.5f, true);

        // C. Needle Center Relic Pivot
        GameObject needlePivot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        needlePivot.name = "NeedlePivot";
        needlePivot.transform.SetParent(_puzzleDialPointerObj.transform, false);
        needlePivot.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        needlePivot.transform.localScale = new Vector3(0.16f, 0.03f, 0.16f);
        Destroy(needlePivot.GetComponent<Collider>());
        needlePivot.GetComponent<MeshRenderer>().material = _puzzleDialMeshRend.material;

        // D. Needle Tracking Point Light
        _puzzleDialLight = _puzzleDialPointerObj.AddComponent<Light>();
        _puzzleDialLight.type = LightType.Point;
        _puzzleDialLight.color = theme.dialPointerColor;
        _puzzleDialLight.range = 2.6f;
        _puzzleDialLight.intensity = 5.5f;

        // 3. Dynamic Resonance Lightning Arc (Connects needle tip to Altar Crest when close)
        GameObject arcObj = new GameObject("ResonanceArc");
        _puzzleResonanceArc = arcObj.AddComponent<LineRenderer>();
        _puzzleResonanceArc.useWorldSpace = true;
        _puzzleResonanceArc.positionCount = 14;
        _puzzleResonanceArc.startWidth = 0.08f;
        _puzzleResonanceArc.endWidth = 0.08f;
        _puzzleResonanceArc.startColor = Color.white;
        _puzzleResonanceArc.endColor = Color.white;
        _puzzleResonanceArc.material = CreateEmissiveMat(theme.resonanceArcColor * 2.5f, true);
        _puzzleResonanceArc.enabled = false;
    }

    private void UpdatePuzzleMarkersVisuals(float currentAngle)
    {
        if (_puzzleDialPointerObj == null || _puzzleTargetMarkerObj == null) return;

        // 1. Sync Dial Needle world position and rotation with plate arrow
        Transform pArrow = _plateArrow != null ? _plateArrow : FindPlateArrow(standPlateSocket);
        Vector3 worldDialFwd = (pArrow != null) ? pArrow.forward : (standPlateSocket != null ? standPlateSocket.forward : Vector3.forward);
        worldDialFwd.y = 0f;
        if (worldDialFwd.sqrMagnitude < 0.01f) worldDialFwd = Vector3.forward;
        worldDialFwd.Normalize();

        if (standPlateSocket != null)
        {
            _puzzleDialPointerObj.transform.position = standPlateSocket.position + Vector3.up * 0.05f;
            _puzzleDialPointerObj.transform.rotation = Quaternion.LookRotation(worldDialFwd, Vector3.up);
        }

        var theme = GetStandTheme(standID);
        float t = Mathf.Clamp01(currentAngle / 45f); // 0 = aligned, 1 = far
        Color dialColor = Color.Lerp(theme.primaryColor, theme.dialPointerColor, t);

        if (_puzzleDialMeshRend != null && _puzzleDialMeshRend.material != null)
        {
            _puzzleDialMeshRend.material.color = dialColor;
            if (_puzzleDialMeshRend.material.HasProperty("_EmissionColor"))
                _puzzleDialMeshRend.material.SetColor("_EmissionColor", dialColor * (2.2f + (1f - t) * 3.0f));
        }

        if (_puzzleDialLight != null)
        {
            _puzzleDialLight.color = dialColor;
            _puzzleDialLight.intensity = 4.0f + (1f - t) * 4.5f;
        }

        if (_puzzleMarkerLight != null)
        {
            float pulse = Mathf.PingPong(Time.time * (3.5f + (1f - t) * 8f), 2.0f);
            _puzzleMarkerLight.intensity = 5.0f + pulse;
        }

        // Resonance Electric Arc (< 55 degrees)
        if (_puzzleResonanceArc != null)
        {
            if (currentAngle < 55f)
            {
                _puzzleResonanceArc.enabled = true;
                float prox = 1f - (currentAngle / 55f);
                float arcWidth = Mathf.Lerp(0.05f, 0.20f, prox);
                _puzzleResonanceArc.startWidth = arcWidth;
                _puzzleResonanceArc.endWidth = arcWidth;

                Vector3 startPt = _puzzleDialPointerObj.transform.position + _puzzleDialPointerObj.transform.forward * 0.65f;
                Vector3 endPt = _puzzleTargetMarkerObj.transform.position;

                int count = _puzzleResonanceArc.positionCount;
                Vector3 dir = endPt - startPt;
                Vector3 perp = Vector3.Cross(dir.normalized, Vector3.up);

                for (int i = 0; i < count; i++)
                {
                    float frac = (float)i / (count - 1);
                    Vector3 basePt = Vector3.Lerp(startPt, endPt, frac);
                    float turbulence = Mathf.Sin(frac * Mathf.PI) * (1f - prox * 0.5f);
                    float jitterX = Mathf.Sin(Time.time * 28f + i * 2.5f) * 0.08f * turbulence;
                    float jitterY = Mathf.Cos(Time.time * 32f + i * 2.0f) * 0.05f * turbulence;
                    _puzzleResonanceArc.SetPosition(i, basePt + perp * jitterX + Vector3.up * jitterY);
                }
            }
            else
            {
                _puzzleResonanceArc.enabled = false;
            }
        }

        // Micro elemental sparks when very close (< 15 degrees) and rotating
        if (currentAngle < 15f && _isRotatingKeyPressed && Random.value < 0.28f)
        {
            Vector3 midPoint = (_puzzleDialPointerObj.transform.position + _puzzleTargetMarkerObj.transform.position) * 0.5f;
            VFXUtil.Burst(midPoint, Vector3.up, theme.resonanceArcColor,
                4, 0.4f, 1.2f, 0.03f, 0.08f, 0.10f, 0.25f, true, true, 0.08f, 40f, 0.7f);
        }
    }

    private void DestroyPuzzleAlignmentMarkers()
    {
        if (_puzzleTargetMarkerObj != null) Destroy(_puzzleTargetMarkerObj);
        if (_puzzleDialPointerObj != null) Destroy(_puzzleDialPointerObj);
        if (_puzzleResonanceArc != null) Destroy(_puzzleResonanceArc.gameObject);
        _puzzleTargetMarkerObj = null;
        _puzzleDialPointerObj = null;
        _puzzleResonanceArc = null;
        _puzzleMarkerLight = null;
        _puzzleDialLight = null;
        _puzzleTargetMeshRend = null;
        _puzzleDialMeshRend = null;
    }

    // ── Procedural Sound Synthesis ───────────────────────────────────────────
    private static AudioClip s_ProceduralTickClip;
    private static AudioClip GetProceduralTickClip()
    {
        if (s_ProceduralTickClip != null) return s_ProceduralTickClip;
        int samples = 750;
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env = Mathf.Exp(-t * 14f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 1500f * (i / 44100f)) * env * 0.45f;
        }
        s_ProceduralTickClip = AudioClip.Create("DialTick", samples, 1, 44100, false);
        s_ProceduralTickClip.SetData(data, 0);
        return s_ProceduralTickClip;
    }

    private static AudioClip s_ProceduralLockClip;
    private static AudioClip GetProceduralLockClip()
    {
        if (s_ProceduralLockClip != null) return s_ProceduralLockClip;
        int samples = 15000;
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env1 = Mathf.Exp(-t * 8f);
            float env2 = Mathf.Exp(-t * 2.5f);
            float tone = Mathf.Sin(2f * Mathf.PI * 523.25f * (i / 44100f)) * 0.35f * env2
                       + Mathf.Sin(2f * Mathf.PI * 659.25f * (i / 44100f)) * 0.30f * env2
                       + Mathf.Sin(2f * Mathf.PI * 783.99f * (i / 44100f)) * 0.35f * env2
                       + Mathf.Sin(2f * Mathf.PI * 180f * (i / 44100f)) * 0.5f * env1;
            data[i] = Mathf.Clamp(tone, -1f, 1f);
        }
        s_ProceduralLockClip = AudioClip.Create("VaultLockChime", samples, 1, 44100, false);
        s_ProceduralLockClip.SetData(data, 0);
        return s_ProceduralLockClip;
    }

    private static void PlayAudioWithPitch(AudioClip clip, Vector3 pos, float volume, float pitch)
    {
        if (clip == null) return;
        GameObject go = new GameObject("OneShotAudio");
        go.transform.position = pos;
        AudioSource aSource = go.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume;
        aSource.pitch = pitch;
        aSource.spatialBlend = 0.5f;
        aSource.Play();
        Destroy(go, (clip.length / Mathf.Max(0.1f, pitch)) + 0.1f);
    }

    public void SnapPlateToExactTarget()
    {
        if (standPlateSocket == null || standTargetArrow == null) return;
        Transform plateA = _plateArrow != null ? _plateArrow : FindPlateArrow(standPlateSocket);
        if (plateA == null) return;

        Vector3 axis = GetAxisVector(rotationAxis);
        Vector3 fwdPlate = plateA.forward;
        Vector3 fwdTarget = standTargetArrow.forward;

        float signedAngle = Vector3.SignedAngle(fwdPlate, fwdTarget, standPlateSocket.TransformDirection(axis));
        if (Mathf.Abs(signedAngle) > 0.01f)
        {
            standPlateSocket.Rotate(axis, signedAngle, Space.Self);
            Debug.Log($"[PlateStandInteraction] Snapped plate rotation by {signedAngle:F2}° for exact target match.");
        }
    }

    // ── AAA VFX Generation ───────────────────────────────────────────────────
    private void TriggerSuccessVFX()
    {
        Vector3 center = (standPlateSocket != null) ? standPlateSocket.position : transform.position;
        Vector3 arrowPos = (standTargetArrow != null) ? standTargetArrow.position : center;

        var theme = GetStandTheme(standID);

        if (customSuccessVFXPrefab != null)
        {
            Instantiate(customSuccessVFXPrefab, center, Quaternion.identity);
        }

        // 1. Radiant pulsing lights in theme colors
        VFXUtil.SpawnFadingLight(center + Vector3.up * 0.2f, theme.primaryColor, 9.0f, 6.0f, 2.2f);
        VFXUtil.SpawnFadingLight(arrowPos + Vector3.up * 0.1f, theme.secondaryColor, 7.0f, 4.5f, 1.6f);

        // 2. High-energy elemental stretch spark burst
        VFXUtil.Burst(center + Vector3.up * 0.05f, Vector3.up, theme.primaryColor,
            45, 3.0f, 7.5f, 0.08f, 0.25f, 0.40f, 0.85f, true, true, 0.25f, 90f, 2.0f);

        // 3. Arrow alignment arc burst
        VFXUtil.Burst(arrowPos, Vector3.up, theme.secondaryColor,
            30, 2.0f, 5.5f, 0.06f, 0.20f, 0.30f, 0.65f, true, true, 0.15f, 65f, 1.8f);

        // 4. Soft theme ambient dust
        Color ambientCol = Color.Lerp(theme.primaryColor, Color.white, 0.4f);
        ambientCol.a = 0.65f;
        VFXUtil.Burst(center, Vector3.up, ambientCol,
            20, 1.0f, 2.8f, 0.15f, 0.40f, 0.65f, 1.4f, true, false, -0.1f, 60f, 2.0f);
    }

    // ── Helper Utilities ─────────────────────────────────────────────────────
    public Vector3 GetAxisVector(RotationAxisMode mode)
    {
        switch (mode)
        {
            case RotationAxisMode.X_Axis: return Vector3.right;
            case RotationAxisMode.Z_Axis: return Vector3.forward;
            case RotationAxisMode.Y_Axis:
            default: return Vector3.up;
        }
    }

    public static Transform FindPlateArrow(Transform root)
    {
        if (root == null) return null;
        Transform arrow = FindChildRecursive(root, "PlateArrow");
        if (arrow != null) return arrow;

        // Fallback: search for child containing "Arrow"
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.ToLower().Contains("arrow"))
                return child;
        }

        return root;
    }

    private static Transform FindChildRecursive(Transform current, string targetName)
    {
        if (current == null) return null;
        if (current.name == targetName) return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindChildRecursive(current.GetChild(i), targetName);
            if (found != null) return found;
        }

        return null;
    }

    // ── OnGUI Prompt Rendering (Double Safety Guarantee) ─────────────────────
    private void OnGUI()
    {
        // 1. Rotation Puzzle: Always render the interactive Alignment HUD Gauge
        if (_isRotatingPuzzle)
        {
            bool isAligned = (_currentPuzzleAngle <= alignmentAngleThreshold);
            var theme = GetStandTheme(standID);
            GameHUDPrompt.DrawAlignmentHUD(_currentPuzzleAngle, _signedPuzzleAngle, alignmentAngleThreshold, isAligned, _isMovingCloser, _isRotatingKeyPressed, theme.themeName, theme.primaryColor);
            return;
        }

        // If Canvas UI is already displaying prompt cleanly, do NOT double-render in OnGUI!
        if (_sharedCanvasPromptObj != null && _sharedCanvasPromptObj.activeInHierarchy)
            return;

        // Only closest stand draws prompt
        if (!IsClosestStandToPlayer())
            return;

        // 2. Stand Point Prompts:
        if (_playerAtStandPoint && !_isPlaced && !_isPlacing && !_isCompleted)
        {
            if (_isRising)
            {
                GameHUDPrompt.DrawPromptBox("Stand is awakening from the terrain...");
                return;
            }

            PlatePickup heldPlate = GetCurrentOrChildPlate();
            bool isHolding = (heldPlate != null);
            bool isMatching = isHolding && (heldPlate.plateID == standID || standID == 0);

            string text = (isHolding && !isMatching)
                ? $"[Wrong Object] Object {heldPlate.plateID} does not fit {displayName}"
                : (!string.IsNullOrEmpty(placePromptText) ? placePromptText : "[P] Place Object");

            GameHUDPrompt.DrawPromptBox(text);

            // Allow clicking the prompt area in OnGUI
            Rect clickRect = new Rect((Screen.width - 440f) * 0.5f, Screen.height - 110f, 440f, 50f);
            if (Event.current.type == EventType.MouseDown && clickRect.Contains(Event.current.mousePosition))
            {
                TriggerPlacement();
            }
        }
    }
}

/// <summary>
/// Helper proxy attached to PlayerStandPoint when PlateStandInteraction is on another GameObject,
/// forwarding trigger events to the parent stand.
/// </summary>
public class StandTriggerZoneProxy : MonoBehaviour
{
    public PlateStandInteraction parentStand;

    private void OnTriggerEnter(Collider other)
    {
        if (parentStand != null)
            parentStand.OnZoneTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentStand != null)
            parentStand.OnZoneTriggerExit(other);
    }
}

