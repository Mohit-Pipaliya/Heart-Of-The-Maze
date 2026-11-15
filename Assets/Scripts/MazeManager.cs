using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple & Robust Maze System Manager.
/// 
/// FEATURES:
/// 1. 8 Maze Blocks (Maze 1 to Maze 8).
/// 2. Day 1 starts with Block 8 directly facing the Glade entrance.
/// 3. Nightly Rotation (Mutual Position Swap with Automatic Glade Alignment):
///    Every time a block swaps into the Glade entrance slot, it is automatically rotated
///    so that its main entrance door is ALWAYS facing the Glade!
/// 4. Ground Smoke VFX (Dhuva niche se nikalna):
///    When night begins, mystical smoke billows up from the ground across the doorway archway,
///    fading in smoothly. In the morning (day), the smoke stops and smoothly fades out!
/// 5. Day/Night Doors:
///    Day starts -> ONLY the single entrance door facing the Glade side opens. All other doors remain closed.
///    Night starts -> Active entrance door closes (and stays closed all night).
/// 6. One-Click Setup in Inspector!
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
public class MazeManager : MonoBehaviour
{
    public static MazeManager Instance { get; private set; }

    [Header("─── 1. Maze Blocks (1 to 8) ───────────────────────────")]
    [Tooltip("Assign Blocks 1 to 8 in order. (Index 0 = Block 1, ..., Index 7 = Block 8)")]
    public GameObject[] mazeBlocks = new GameObject[8];

    [Header("─── 2. Entrance Fog Object ─────────────────────────────")]
    [Tooltip("Scene me 'maze enterence fog' empty GameObject ya BoxCollider yahan drag karein. (Empty chhodne par auto-find kar lega).")]
    public Transform mazeEntranceFog;

    [Tooltip("Ground smoke visual effect script khud generate karegi")]
    public bool autoGenerateFog = true;

    [Tooltip("Smoke fade in / fade out duration (seconds)")]
    [Range(0.5f, 4f)] public float fogFadeDuration = 2.0f;

    [Tooltip("Smoke ka color (mystical misty white/smoke gray)")]
    public Color fogColor = new Color(0.85f, 0.88f, 0.94f, 0.85f);

    [Header("─── 3. Day / Night Door Automation ─────────────────────")]
    [Tooltip("Automatically connect to DayNightController in scene")]
    public bool syncWithDayNight = true;

    [Tooltip("Day hone ke kitne second baad door open ho")]
    [Range(0f, 3f)] public float doorOpenDelay = 0.5f;

    [Header("─── 4. Rotation Sequence ───────────────────────────────")]
    [Tooltip("Block sequence: Day 1 starts with Block 8, then 1, 2, 3, 4, 5, 6, 7, then returns to 8!")]
    public int[] blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

    [Header("─── 5. Audio (Optional) ────────────────────────────────")]
    public AudioClip fogSound;
    public AudioClip swapRumbleSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    [Header("─── 6. Live Status (Read Only) ─────────────────────────")]
    [SerializeField] private int currentActiveBlockNumber = 8;
    [SerializeField] private int currentSequenceIndex = 0;
    [SerializeField] private bool isSwapping = false;

    // ─── Slot Memory ───
    private Vector3 _activeSlotCenter = new Vector3(3838f, 0f, 1500f);
    private Vector3 _activeSlotBasePosition = new Vector3(3338f, 0f, 1000f);
    private Vector3[] _homePositions = new Vector3[8];
    private Quaternion[] _homeRotations = new Quaternion[8];
    private bool _slotsCaptured = false;

    // ─── Ground Smoke Internal References ───
    private GameObject _fogEffectRoot;
    private ParticleSystem _fogParticleSystem;
    private float _currentFogAlpha = 0f;
    private Coroutine _swapRoutine;
    private Coroutine _fogFadeRoutine;
    private AudioSource _audioSource;

    // ─── Day/Night Tracking ───
    private bool _wasDay = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        InitializeAudio();
        CaptureInitialSlots();
        EnsureEntranceFogVisuals(true);
    }

    private void Start()
    {
        if (DayNightController.Instance != null)
        {
            _wasDay = DayNightController.Instance.IsDay;
        }

        // Apply Day 1 initial active block (Block 8)
        ApplyInitialActiveBlock();

        if (DayNightController.Instance != null && DayNightController.Instance.IsDay)
        {
            SetFogAlpha(0f);
            OpenActiveDoor();
        }
        else
        {
            SetFogAlpha(1f);
            CloseAllDoorsInstant();
        }
    }

    private void OnEnable()
    {
        if (DayNightController.Instance != null)
        {
            DayNightController.Instance.OnDayStart += OnDayStartSignal;
            DayNightController.Instance.OnNightStart += OnNightStartSignal;
        }
    }

    private void OnDisable()
    {
        if (DayNightController.Instance != null)
        {
            DayNightController.Instance.OnDayStart -= OnDayStartSignal;
            DayNightController.Instance.OnNightStart -= OnNightStartSignal;
        }
    }

    private void Update()
    {
        // Failsafe polling for DayNightController state changes
        if (syncWithDayNight && DayNightController.Instance != null && !isSwapping)
        {
            bool isDay = DayNightController.Instance.IsDay;
            if (isDay && !_wasDay)
            {
                _wasDay = true;
                OnDayStartSignal();
            }
            else if (!isDay && _wasDay)
            {
                _wasDay = false;
                OnNightStartSignal();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DAY / NIGHT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    public void OnDayStartSignal()
    {
        if (isSwapping) return;
        StartCoroutine(DayStartRoutine());
    }

    public void OnNightStartSignal()
    {
        if (isSwapping) return;
        StartNightSwap();
    }

    private IEnumerator DayStartRoutine()
    {
        // Subah (Day) hote hi smoke fade out hokar bandh ho jana chahiye
        yield return FadeFog(0f, fogFadeDuration);

        if (doorOpenDelay > 0f)
            yield return new WaitForSeconds(doorOpenDelay);

        // Open strictly ONLY the door facing the Glade!
        OpenActiveDoor();
    }

    public void StartNightSwap()
    {
        if (isSwapping) return;
        if (_swapRoutine != null) StopCoroutine(_swapRoutine);
        _swapRoutine = StartCoroutine(NightSwapRoutine());
    }

    private IEnumerator NightSwapRoutine()
    {
        isSwapping = true;

        // 1. Immediately command the active block entrance door to close
        CloseActiveDoor();
        yield return new WaitForSeconds(0.8f);

        // 2. Night shuru hote hi niche se dhuva (smoke) nikalna chalu ho aur fade in ho
        PlayAudio(fogSound);
        yield return FadeFog(1f, fogFadeDuration);

        // 3. Play rumble audio and perform the mutual block swap behind the rising smoke
        PlayAudio(swapRumbleSound);
        AdvanceToNextBlock();

        // Smoke stays active and billowing throughout the night!
        isSwapping = false;
        _swapRoutine = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BLOCK ROTATION & MUTUAL POSITION SWAP (DOOR ALWAYS FACES GLADE)
    // ═══════════════════════════════════════════════════════════════════════════

    public void AdvanceToNextBlock()
    {
        CaptureInitialSlots();

        if (blockSequence == null || blockSequence.Length == 0)
            blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

        int outgoingBlockNum = blockSequence[currentSequenceIndex];
        currentSequenceIndex = (currentSequenceIndex + 1) % blockSequence.Length;
        int incomingBlockNum = blockSequence[currentSequenceIndex];

        GameObject outgoingBlock = GetBlock(outgoingBlockNum);
        GameObject incomingBlock = GetBlock(incomingBlockNum);

        if (outgoingBlock != null && incomingBlock != null)
        {
            // Close and reset all doors on both blocks
            ReleaseAndCloseBlockDoors(outgoingBlock);
            ReleaseAndCloseBlockDoors(incomingBlock);

            // Read the incoming block's current reserve position
            int inHomeIdx = incomingBlockNum - 1;
            Vector3 reservePos = _homePositions[inHomeIdx];
            Quaternion reserveRot = _homeRotations[inHomeIdx];

            // 1. Purana (outgoing) block naye block ki reserve position aur normal rotation par jaye
            outgoingBlock.transform.SetPositionAndRotation(reservePos, reserveRot);

            // 2. Naya (incoming) block active Glade entrance slot par aaye,
            //    AND is rotated so that its main entrance door is ALWAYS facing the Glade side!
            int optimalAngle = GetOptimalGladeRotation(incomingBlockNum);
            PositionBlockAtGladeSlot(incomingBlock, optimalAngle);

            currentActiveBlockNumber = incomingBlockNum;

            // Ensure ONLY the Glade-facing door on incoming block is locked closed for the night
            DoubleSlidingDoor incomingGladeDoor = FindActiveEntranceDoor();
            if (incomingGladeDoor != null)
            {
                incomingGladeDoor.CloseByManager();
            }

            Debug.Log($"[MazeManager] MUTUAL SWAP SUCCESS: Block {outgoingBlockNum} moved to reserve ({reservePos.x:F0}, {reservePos.z:F0}). Block {incomingBlockNum} is now ACTIVE at Glade entrance with rotation {optimalAngle}°! Door facing Glade.");
        }
    }

    /// <summary>
    /// Positions and rotates a 1000x1000 block around its center so its footprint
    /// matches the active slot and its entrance door faces directly into the Glade!
    /// </summary>
    private void PositionBlockAtGladeSlot(GameObject block, int angleDegrees)
    {
        if (block == null) return;

        Quaternion rot = Quaternion.Euler(0f, angleDegrees, 0f);
        Vector3 localCenterOffset = rot * new Vector3(500f, 0f, 500f);
        Vector3 targetPivotPos = _activeSlotCenter - localCenterOffset;

        block.transform.SetPositionAndRotation(targetPivotPos, rot);
    }

    /// <summary>
    /// Optimal cardinal rotation angle (0, 90, 180, 270) for each of the 8 blocks
    /// so that its main entrance door is ALWAYS situated on the East edge (X ~ 4338) facing the Glade!
    /// </summary>
    public int GetOptimalGladeRotation(int blockNumber)
    {
        switch (blockNumber)
        {
            case 1: return 180; // West door (X=6) rotates 180° to East edge (X=994)
            case 2: return 0;   // East door (X=1002) is already facing Glade
            case 3: return 270; // South door (Z=0) rotates 270° to East edge (X=1000)
            case 4: return 180; // West door (X=23) rotates 180° to East edge (X=976)
            case 5: return 270; // South door (Z=12) rotates 270° to East edge (X=988)
            case 6: return 0;   // East door (X=991) is already facing Glade
            case 7: return 90;  // North door (Z=992) rotates 90° to East edge (X=992)
            case 8: return 0;   // East door (X=997) is already facing Glade
            default: return 0;
        }
    }

    private void ApplyInitialActiveBlock()
    {
        CaptureInitialSlots();

        if (blockSequence == null || blockSequence.Length == 0)
            blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

        currentSequenceIndex = 0;
        currentActiveBlockNumber = blockSequence[0]; // Day 1 = Block 8

        // Place Block 8 at the active slot facing the Glade
        GameObject startBlock = GetBlock(currentActiveBlockNumber);
        if (startBlock != null)
        {
            PositionBlockAtGladeSlot(startBlock, GetOptimalGladeRotation(currentActiveBlockNumber));
        }

        // Ensure other blocks are in their reserve slots
        for (int i = 0; i < 8; i++)
        {
            int blockNum = i + 1;
            if (blockNum == currentActiveBlockNumber) continue;

            GameObject b = GetBlock(blockNum);
            if (b != null)
            {
                b.transform.SetPositionAndRotation(_homePositions[i], _homeRotations[i]);
            }
        }
    }

    public GameObject GetActiveBlock() => GetBlock(currentActiveBlockNumber);

    public GameObject GetBlock(int number)
    {
        int idx = number - 1;
        if (idx >= 0 && idx < mazeBlocks.Length)
            return mazeBlocks[idx];
        return null;
    }

    public void CaptureInitialSlots()
    {
        if (_slotsCaptured) return;

        AutoFindBlocks();

        // Calculate slot center from Maze parent
        GameObject mazeParent = GameObject.Find("Maze");
        Vector3 mazeOrigin = (mazeParent != null) ? mazeParent.transform.position : new Vector3(3338f, 0f, 0f);

        _activeSlotBasePosition = mazeOrigin + new Vector3(0f, 0f, 1000f);
        _activeSlotCenter = _activeSlotBasePosition + new Vector3(500f, 0f, 500f);

        // Capture home positions for all 8 blocks
        for (int i = 0; i < 8; i++)
        {
            GameObject b = mazeBlocks[i];
            if (b != null)
            {
                _homePositions[i] = b.transform.position;
                _homeRotations[i] = b.transform.rotation;
            }
        }

        _slotsCaptured = true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DOOR CONTROL (ONLY GLADE-FACING DOOR OPENS!)
    // ═══════════════════════════════════════════════════════════════════════════

    public void OpenActiveDoor()
    {
        GameObject active = GetActiveBlock();
        if (active == null) return;

        var allDoors = active.GetComponentsInChildren<DoubleSlidingDoor>(true);
        DoubleSlidingDoor gladeFacingDoor = FindActiveEntranceDoor();

        // 1. Ensure all interior/non-glade doors on this block remain strictly CLOSED
        foreach (var d in allDoors)
        {
            if (d != null && d != gladeFacingDoor)
            {
                d.ReleaseManagerOverride();
                d.CloseDoor();
            }
        }

        // 2. Open strictly ONLY the door facing the Glade side!
        if (gladeFacingDoor != null)
        {
            gladeFacingDoor.OpenByManager();
            Debug.Log($"[MazeManager] SUCCESS: Opened ONLY the Glade-facing entrance door '{gladeFacingDoor.gameObject.name}' (world X: {gladeFacingDoor.transform.position.x:F1}) on active Block {currentActiveBlockNumber}");
        }
    }

    public void CloseActiveDoor()
    {
        DoubleSlidingDoor door = FindActiveEntranceDoor();
        if (door != null)
        {
            door.CloseByManager();
            Debug.Log($"[MazeManager] Closed entrance door '{door.gameObject.name}' on active Block {currentActiveBlockNumber}");
        }
    }

    public void CloseAllDoorsInstant()
    {
        for (int i = 0; i < mazeBlocks.Length; i++)
        {
            if (mazeBlocks[i] != null)
                ReleaseAndCloseBlockDoors(mazeBlocks[i]);
        }
    }

    public void ReleaseAndCloseBlockDoors(GameObject block)
    {
        if (block == null) return;
        var doors = block.GetComponentsInChildren<DoubleSlidingDoor>(true);
        foreach (var d in doors)
        {
            if (d != null)
            {
                d.ReleaseManagerOverride();
                d.SetStateInstant(false);
            }
        }
    }

    /// <summary>
    /// Finds the specific door on the active block that faces into the Glade.
    /// Because the Glade is in the +X direction (world X >= 4300),
    /// the door with the MAXIMUM world X (transform.position.x) is strictly the Glade-facing door!
    /// </summary>
    public DoubleSlidingDoor FindActiveEntranceDoor()
    {
        GameObject active = GetActiveBlock();
        if (active == null) return null;

        var doors = active.GetComponentsInChildren<DoubleSlidingDoor>(true);
        if (doors == null || doors.Length == 0) return null;

        DoubleSlidingDoor bestDoor = null;
        float maxX = float.MinValue;

        foreach (var d in doors)
        {
            if (d == null) continue;
            float wx = d.transform.position.x;
            if (wx > maxX)
            {
                maxX = wx;
                bestDoor = d;
            }
        }

        return bestDoor ?? doors[0];
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  GROUND SMOKE VFX (Dhuva niche se nikalna - Night ON / Day OFF)
    // ═══════════════════════════════════════════════════════════════════════════

    public void EnsureEntranceFogVisuals(bool forceRecreate = false)
    {
        if (mazeEntranceFog == null)
        {
            AutoFindEntranceFog();
            if (mazeEntranceFog == null) return;
        }

        if (!autoGenerateFog) return;

        // Clean up stale or solid box objects
        Transform existing = mazeEntranceFog.Find("_GeneratedFogEffect");
        if (existing != null && (forceRecreate || _fogEffectRoot == null || _fogParticleSystem == null))
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
            existing = null;
        }

        if (existing == null)
        {
            CreateGroundSmokeEffect();
        }
        else
        {
            _fogEffectRoot = existing.gameObject;
            _fogParticleSystem = _fogEffectRoot.GetComponentInChildren<ParticleSystem>();
        }

        SetFogAlpha(_currentFogAlpha);
    }

    private void CreateGroundSmokeEffect()
    {
        _fogEffectRoot = new GameObject("_GeneratedFogEffect");
        _fogEffectRoot.transform.SetParent(mazeEntranceFog, false);

        // Position ground emitter right at ground level (Y = 0) spanning the doorway
        BoxCollider box = mazeEntranceFog.GetComponent<BoxCollider>();
        Vector3 smokePosition = Vector3.zero;
        Vector3 groundArea = new Vector3(25f, 1f, 90f);

        if (box != null)
        {
            smokePosition = new Vector3(box.center.x, 0.5f, box.center.z);
            groundArea = new Vector3(Mathf.Max(25f, box.size.x), 1.2f, Mathf.Max(90f, box.size.z));
        }

        _fogEffectRoot.transform.localPosition = smokePosition;
        _fogEffectRoot.transform.localRotation = Quaternion.identity;

        // Billowing Ground Smoke Particle System (Dhuva niche se nikalne wala)
        GameObject psGo = new GameObject("GroundSmokeParticles");
        psGo.transform.SetParent(_fogEffectRoot.transform, false);
        psGo.transform.localPosition = Vector3.zero;

        _fogParticleSystem = psGo.AddComponent<ParticleSystem>();
        var main = _fogParticleSystem.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 5.5f); // Upward rising speed
        main.startSize = new ParticleSystem.MinMaxCurve(8f, 16f);     // Starts at ground size
        main.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.85f);
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = true;
        main.playOnAwake = false;

        // Emission from ground strip across doorway
        var emission = _fogParticleSystem.emission;
        emission.rateOverTime = 45f;
        emission.enabled = false;

        // Shape: Flat box at ground level
        var shape = _fogParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = groundArea;
        shape.rotation = new Vector3(-90f, 0f, 0f); // Emits upwards (+Y) from ground

        // Velocity: Rising upward with natural drift
        var vel = _fogParticleSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.y = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
        vel.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        // Size over lifetime: Expands as smoke rises up into the archway
        var sizeOverLife = _fogParticleSystem.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);  // Starts smaller at ground
        sizeCurve.AddKey(0.4f, 1.2f); // Rapidly expands as it rises
        sizeCurve.AddKey(1.0f, 2.5f); // Billows into massive rolling smoke clouds
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color & Alpha: Fades in quickly from ground, stays thick, dissipates at top
        var colOverLife = _fogParticleSystem.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(fogColor, 0f), new GradientColorKey(fogColor, 1f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0f),
                new GradientAlphaKey(0.9f, 0.15f), // Billows thick right off the ground
                new GradientAlphaKey(0.85f, 0.65f),
                new GradientAlphaKey(0.0f, 1.0f)   // Smoothly dissipates near top
            }
        );
        colOverLife.color = grad;

        // Material with soft particle blending
        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = Resources.Load<Material>("VFX/M_ParticleAlpha")
            ?? Resources.Load<Material>("M_ParticleAlpha");

        // Rotation over lifetime for rolling smoke look
        var rotOverLife = _fogParticleSystem.rotationOverLifetime;
        rotOverLife.enabled = true;
        rotOverLife.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
    }

    public IEnumerator FadeFog(float targetAlpha, float duration)
    {
        EnsureEntranceFogVisuals();

        if (_fogFadeRoutine != null) StopCoroutine(_fogFadeRoutine);

        float startAlpha = _currentFogAlpha;
        float elapsed = 0f;

        if (targetAlpha > 0.05f)
        {
            if (_fogEffectRoot != null) _fogEffectRoot.SetActive(true);
            if (_fogParticleSystem != null)
            {
                var em = _fogParticleSystem.emission;
                em.enabled = true;
                _fogParticleSystem.Play();
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = Mathf.SmoothStep(startAlpha, targetAlpha, t);
            SetFogAlpha(smooth);
            yield return null;
        }

        SetFogAlpha(targetAlpha);

        if (targetAlpha <= 0.05f)
        {
            if (_fogParticleSystem != null)
            {
                var em = _fogParticleSystem.emission;
                em.enabled = false;
            }
            if (_fogEffectRoot != null) _fogEffectRoot.SetActive(false);
        }

        _fogFadeRoutine = null;
    }

    public void SetFogAlpha(float alpha)
    {
        _currentFogAlpha = Mathf.Clamp01(alpha);

        if (_currentFogAlpha <= 0.001f)
        {
            if (_fogEffectRoot != null && _fogEffectRoot.activeSelf)
                _fogEffectRoot.SetActive(false);
            return;
        }

        if (_fogEffectRoot != null && !_fogEffectRoot.activeSelf)
            _fogEffectRoot.SetActive(true);

        if (_fogParticleSystem != null)
        {
            var main = _fogParticleSystem.main;
            Color pc = fogColor;
            pc.a = _currentFogAlpha * fogColor.a;
            main.startColor = pc;

            var em = _fogParticleSystem.emission;
            em.rateOverTime = 45f * _currentFogAlpha;
        }
    }

    private void InitializeAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && (fogSound != null || swapRumbleSound != null))
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0.5f;
            _audioSource.playOnAwake = false;
        }
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  AUTO-DISCOVERY & ONE-CLICK SETUP
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("One-Click Setup")]
    public void OneClickSetup()
    {
        AutoFindBlocks();
        AutoFindEntranceFog();
        EnsureEntranceFogVisuals(true);
        _slotsCaptured = false;
        CaptureInitialSlots();
        ApplyInitialActiveBlock();

        Debug.Log("[MazeManager] One-Click Setup finished! 8 Blocks, DayNight Controller, and Ground Smoke successfully linked.");
    }

    public void AutoFindBlocks()
    {
        bool needsSearch = false;
        for (int i = 0; i < 8; i++)
        {
            if (mazeBlocks[i] == null) { needsSearch = true; break; }
        }

        if (!needsSearch) return;

        // Look for Maze parent in scene
        GameObject mazeParent = GameObject.Find("Maze");
        for (int i = 1; i <= 8; i++)
        {
            GameObject found = null;
            if (mazeParent != null)
            {
                Transform t = mazeParent.transform.Find($"Maze {i}");
                if (t != null) found = t.gameObject;
            }

            if (found == null)
            {
                found = GameObject.Find($"Maze {i}");
            }

            if (found != null)
            {
                mazeBlocks[i - 1] = found;
            }
        }
    }

    public void AutoFindEntranceFog()
    {
        if (mazeEntranceFog != null) return;

        // Search for user's GameObject
        string[] searchNames = new string[] {
            "maze enterence fog",
            "maze entrance fog",
            "Maze Entrance Fog",
            "Maze Enterence Fog",
            "Entrance Fog",
            "Glade Entrance Marker",
            "Glade Entrance Gate"
        };

        foreach (var name in searchNames)
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
            {
                mazeEntranceFog = go.transform;
                Debug.Log($"[MazeManager] Auto-linked Entrance Fog to '{go.name}'");
                return;
            }
        }

        // If not found in scene, automatically instantiate an empty entrance fog marker at the gateway
        GameObject createdFogGo = new GameObject("maze enterence fog");
        createdFogGo.transform.position = new Vector3(4335.4f, 0f, 1552.6f);
        BoxCollider box = createdFogGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(25f, 40f, 100f);
        mazeEntranceFog = createdFogGo.transform;
        Debug.Log($"[MazeManager] Created default 'maze enterence fog' BoxCollider at {createdFogGo.transform.position}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EDITOR CONTEXT MENU TESTING TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Simulate Day (Open Door & Fog Off)")]
    public void TestOpenDoor() => StartCoroutine(DayStartRoutine());

    [ContextMenu("Simulate Night (Close Door, Fog On & Swap)")]
    public void TestNightSwap() => StartNightSwap();

    [ContextMenu("Toggle Entrance Smoke")]
    public void TestToggleFog()
    {
        EnsureEntranceFogVisuals();
        float target = _currentFogAlpha > 0.5f ? 0f : 1f;
        if (!Application.isPlaying)
        {
            SetFogAlpha(target);
        }
        else
        {
            StartCoroutine(FadeFog(target, 1.5f));
        }
    }

    [ContextMenu("Jump To Next Block Immediately")]
    public void TestJumpNext() => AdvanceToNextBlock();
}
