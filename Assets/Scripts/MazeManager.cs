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
///    - Sequence: Block 8 -> Block 1 -> Block 2 -> Block 3 -> Block 4 -> Block 5 -> Block 6 -> Block 7 -> Block 8.
///    - Exactly ONE block is at the Glade entrance at any given time (no overlapping ever).
///    - Whichever block arrives at the Glade entrance is automatically rotated so its entrance door ALWAYS faces the Glade!
/// 4. Complete BoxCollider Smoke VFX (Dhuva sari jagah se nikal kar BoxCollider ko pura dhak deta hai):
///    - When night begins, smoke billows up from the ground across the entire BoxCollider area, fading in smoothly.
///    - In the morning (day), the smoke stops and smoothly fades out!
/// 5. Day/Night Doors:
///    - Day starts -> ONLY the single entrance door facing the Glade side opens. All other doors remain closed.
///    - Night starts -> Active entrance door closes (and stays closed all night).
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

    [Tooltip("Smoke visual effect script khud generate karegi")]
    public bool autoGenerateFog = true;

    [Tooltip("Smoke fade in / fade out duration (seconds)")]
    [Range(0.5f, 4f)] public float fogFadeDuration = 2.0f;

    [Tooltip("Smoke color (mystical pale smoke gray)")]
    public Color fogColor = new Color(0.85f, 0.88f, 0.94f, 0.88f);

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
    private ParticleSystem _groundMistParticleSystem;
    private GameObject _fogWallBarrier;
    private Material _fogWallMaterial;
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

    private void OnDestroy()
    {
        if (_fogWallMaterial != null)
        {
            if (Application.isPlaying) Destroy(_fogWallMaterial);
            else DestroyImmediate(_fogWallMaterial);
            _fogWallMaterial = null;
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

        // 2. Night shuru hote hi BoxCollider se dhuva (smoke) nikalna chalu ho aur fade in ho
        PlayAudio(fogSound);
        yield return FadeFog(1f, fogFadeDuration);

        // 3. Play rumble audio and perform block swap behind the dense smoke
        PlayAudio(swapRumbleSound);
        AdvanceToNextBlock();

        // Smoke stays active and billowing throughout the night!
        isSwapping = false;
        _swapRoutine = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BLOCK ROTATION & MUTUAL POSITION SWAP (EXACTLY ONE BLOCK AT GLADE)
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

            // Read incoming block's current position (where it is currently resting in reserve)
            Vector3 reservePos = incomingBlock.transform.position;

            // Failsafe: If reservePos is somehow at or near the Glade entrance, send to safe home slot
            if (Vector3.Distance(reservePos, _activeSlotCenter) < 700f)
            {
                int safeIdx = (outgoingBlockNum - 1 < 7) ? outgoingBlockNum - 1 : 0;
                reservePos = _homePositions[safeIdx];
            }

            // 1. Outgoing block moves to incoming block's reserve position with identity rotation
            outgoingBlock.transform.SetPositionAndRotation(reservePos, Quaternion.identity);

            // 2. Incoming block moves to the Glade entrance slot and rotates so its door ALWAYS faces Glade
            int optimalAngle = GetOptimalGladeRotation(incomingBlockNum);
            PositionBlockAtGladeSlot(incomingBlock, optimalAngle);

            currentActiveBlockNumber = incomingBlockNum;

            // 3. Strict Failsafe: Ensure that GLADE KE SAMNE HAMESHA EK HI BLOCK HO!
            //    No other block is ever allowed to overlap or remain near the Glade entrance.
            for (int i = 0; i < mazeBlocks.Length; i++)
            {
                GameObject b = mazeBlocks[i];
                if (b != null && b != incomingBlock)
                {
                    if (Vector3.Distance(b.transform.position, _activeSlotCenter) < 700f)
                    {
                        int fallbackSlot = (i < 7) ? i : 0;
                        b.transform.SetPositionAndRotation(_homePositions[fallbackSlot], Quaternion.identity);
                        Debug.LogWarning($"[MazeManager] Failsafe: Cleared overlapping block {i + 1} away to reserve slot {fallbackSlot + 1}!");
                    }
                }
            }

            // Lock incoming Glade entrance door closed for the night
            DoubleSlidingDoor incomingGladeDoor = FindActiveEntranceDoor();
            if (incomingGladeDoor != null)
            {
                incomingGladeDoor.CloseByManager();
            }

            Debug.Log($"[MazeManager] SWAP COMPLETE: Block {incomingBlockNum} is now the ONLY block at the Glade entrance (rot={optimalAngle}°, door facing Glade). Block {outgoingBlockNum} moved to reserve ({reservePos.x:F0}, {reservePos.z:F0}).");
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

        // Ensure other blocks are in their reserve slots (never at Glade entrance)
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
    //  BOXCOLLIDER COVERING SMOKE VFX (Dhuva sari jagah se nikal kar BoxCollider ko dhak leta hai)
    // ═══════════════════════════════════════════════════════════════════════════

    public void EnsureEntranceFogVisuals(bool forceRecreate = false)
    {
        if (mazeEntranceFog == null)
        {
            AutoFindEntranceFog();
            if (mazeEntranceFog == null) return;
        }

        if (!autoGenerateFog) return;

        // Clean up stale or incomplete objects
        Transform existing = mazeEntranceFog.Find("_GeneratedFogEffect");
        if (existing != null && (forceRecreate || _fogEffectRoot == null || _fogParticleSystem == null || _groundMistParticleSystem == null || _fogWallBarrier == null))
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
            existing = null;
        }

        if (existing == null)
        {
            CreateBoxColliderSmokeEffect();
        }
        else
        {
            _fogEffectRoot = existing.gameObject;
            var systems = _fogEffectRoot.GetComponentsInChildren<ParticleSystem>();
            if (systems.Length > 0) _fogParticleSystem = systems[0];
            if (systems.Length > 1) _groundMistParticleSystem = systems[1];

            Transform barrier = _fogEffectRoot.transform.Find("FogWallOpaqueBarrier");
            if (barrier != null) _fogWallBarrier = barrier.gameObject;
        }

        SetFogAlpha(_currentFogAlpha);
    }

    private void CreateBoxColliderSmokeEffect()
    {
        _fogEffectRoot = new GameObject("_GeneratedFogEffect");
        _fogEffectRoot.transform.SetParent(mazeEntranceFog, false);

        BoxCollider box = mazeEntranceFog.GetComponent<BoxCollider>();
        Vector3 smokeCenter = Vector3.zero;
        Vector3 smokeBoxSize = new Vector3(30f, 65f, 160f);

        if (box != null)
        {
            smokeCenter = box.center;
            // Match the exact dimensions of user's BoxCollider so the ENTIRE collider is covered
            smokeBoxSize = new Vector3(
                Mathf.Max(25f, box.size.x),
                Mathf.Max(35f, box.size.y),
                Mathf.Max(80f, box.size.z)
            );
        }

        _fogEffectRoot.transform.localPosition = smokeCenter;
        _fogEffectRoot.transform.localRotation = Quaternion.identity;

        Material smokeMat = Resources.Load<Material>("VFX/M_ParticleAlpha")
            ?? Resources.Load<Material>("VFX/M_Smoke")
            ?? Resources.Load<Material>("M_ParticleAlpha");

        // ─────────────────────────────────────────────────────────────────
        // 0. OPAQUE FOG WALL BARRIER (100% Sight Blocker: block cannot be seen)
        // ─────────────────────────────────────────────────────────────────
        _fogWallBarrier = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fogWallBarrier.name = "FogWallOpaqueBarrier";
        _fogWallBarrier.transform.SetParent(_fogEffectRoot.transform, false);
        _fogWallBarrier.transform.localPosition = Vector3.zero;
        // Rotate 90 degrees around Y so the barrier faces along X between Glade and Maze
        _fogWallBarrier.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        _fogWallBarrier.transform.localScale = new Vector3(smokeBoxSize.z, smokeBoxSize.y, 1f);

        Collider barrierCol = _fogWallBarrier.GetComponent<Collider>();
        if (barrierCol != null) Destroy(barrierCol);

        MeshRenderer barrierRend = _fogWallBarrier.GetComponent<MeshRenderer>();
        if (smokeMat != null)
        {
            _fogWallMaterial = new Material(smokeMat);
            _fogWallMaterial.name = "M_FogWallBarrier_Runtime";
            barrierRend.material = _fogWallMaterial;
        }

        float floorY = -smokeBoxSize.y * 0.48f;

        // ─────────────────────────────────────────────────────────────────
        // 1. CONTINUOUS GROUND-TO-TOP RISING SMOKE WALL ("continue niche se upar")
        // ─────────────────────────────────────────────────────────────────
        GameObject psGo = new GameObject("ContinuousRisingSmokeWall");
        psGo.transform.SetParent(_fogEffectRoot.transform, false);
        psGo.transform.localPosition = new Vector3(0f, floorY, 0f);

        _fogParticleSystem = psGo.AddComponent<ParticleSystem>();
        var main = _fogParticleSystem.main;
        // Long lifetime so particles reach far past the top of the wall
        main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 8.5f);
        // Fast upward speed so smoke continuously surges upward
        main.startSpeed = new ParticleSystem.MinMaxCurve(10.0f, 18.0f);
        // Large overlapping plumes to form a continuous joint wall (no gaps)
        main.startSize = new ParticleSystem.MinMaxCurve(35f, 65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.95f);
        main.maxParticles = 3500;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = true;
        main.playOnAwake = false;

        // Shape: Emits strictly from the floor line across the entire collider length
        var shape = _fogParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Min(smokeBoxSize.x, 25f), 2f, smokeBoxSize.z);

        // Upward surging velocity with 3D rolling turbulence
        var vel = _fogParticleSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.y = new ParticleSystem.MinMaxCurve(12.0f, 22.0f);
        vel.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        vel.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        // 3D Curl Noise for organic fluid eddies and billowing clouds
        var noise = _fogParticleSystem.noise;
        noise.enabled = true;
        noise.frequency = 0.03f;
        noise.strength = 2.5f;
        noise.scrollSpeed = 0.3f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.octaveCount = 2;
        noise.octaveMultiplier = 0.5f;
        noise.octaveScale = 2.0f;

        // Expanding size as it ascends from floor to top
        var sizeOverLife = _fogParticleSystem.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.2f, 1.0f);
        sizeCurve.AddKey(1.0f, 1.8f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Dense alpha curve: instant ramp at ground, stays solid and opaque, fades at top
        var colOverLife = _fogParticleSystem.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(fogColor, 0f), new GradientColorKey(fogColor, 1f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0f),
                new GradientAlphaKey(0.96f, 0.08f),
                new GradientAlphaKey(0.95f, 0.75f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colOverLife.color = grad;

        var rotOverLife = _fogParticleSystem.rotationOverLifetime;
        rotOverLife.enabled = true;
        rotOverLife.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        if (smokeMat != null) psRenderer.material = smokeMat;

        var emission = _fogParticleSystem.emission;
        emission.rateOverTime = 450f;
        emission.enabled = false;

        // ─────────────────────────────────────────────────────────────────
        // 2. DENSE FLOOR MIST LAYER (Crawling ground fog across doorway)
        // ─────────────────────────────────────────────────────────────────
        GameObject mistGo = new GameObject("DenseGroundMistParticles");
        mistGo.transform.SetParent(_fogEffectRoot.transform, false);
        mistGo.transform.localPosition = new Vector3(0f, floorY, 0f);

        _groundMistParticleSystem = mistGo.AddComponent<ParticleSystem>();
        var mistMain = _groundMistParticleSystem.main;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(6.0f, 9.0f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        mistMain.startSize = new ParticleSystem.MinMaxCurve(25f, 50f);
        mistMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        mistMain.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.90f);
        mistMain.maxParticles = 800;
        mistMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        mistMain.loop = true;
        mistMain.playOnAwake = false;

        var mistShape = _groundMistParticleSystem.shape;
        mistShape.shapeType = ParticleSystemShapeType.Box;
        mistShape.scale = new Vector3(Mathf.Min(smokeBoxSize.x * 1.5f, 40f), 2f, smokeBoxSize.z);

        var mistVel = _groundMistParticleSystem.velocityOverLifetime;
        mistVel.enabled = true;
        mistVel.space = ParticleSystemSimulationSpace.Local;
        mistVel.y = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        mistVel.x = new ParticleSystem.MinMaxCurve(-3.0f, 3.0f);
        mistVel.z = new ParticleSystem.MinMaxCurve(-2.0f, 2.0f);

        var mistNoise = _groundMistParticleSystem.noise;
        mistNoise.enabled = true;
        mistNoise.frequency = 0.025f;
        mistNoise.strength = 1.5f;
        mistNoise.scrollSpeed = 0.2f;
        mistNoise.damping = true;
        mistNoise.quality = ParticleSystemNoiseQuality.High;

        var mistSizeOverLife = _groundMistParticleSystem.sizeOverLifetime;
        mistSizeOverLife.enabled = true;
        AnimationCurve mistCurve = new AnimationCurve();
        mistCurve.AddKey(0f, 0.6f);
        mistCurve.AddKey(0.4f, 1.2f);
        mistCurve.AddKey(1.0f, 1.6f);
        mistSizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, mistCurve);

        var mistCol = _groundMistParticleSystem.colorOverLifetime;
        mistCol.enabled = true;
        Gradient mistGrad = new Gradient();
        mistGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(fogColor, 0f), new GradientColorKey(fogColor, 1f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0f),
                new GradientAlphaKey(0.92f, 0.15f),
                new GradientAlphaKey(0.85f, 0.70f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        mistCol.color = mistGrad;

        var mistRot = _groundMistParticleSystem.rotationOverLifetime;
        mistRot.enabled = true;
        mistRot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var mistRenderer = mistGo.GetComponent<ParticleSystemRenderer>();
        if (smokeMat != null) mistRenderer.material = smokeMat;

        var mistEmission = _groundMistParticleSystem.emission;
        mistEmission.rateOverTime = 180f;
        mistEmission.enabled = false;
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
            if (_groundMistParticleSystem != null)
            {
                var em = _groundMistParticleSystem.emission;
                em.enabled = true;
                _groundMistParticleSystem.Play();
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
            if (_groundMistParticleSystem != null)
            {
                var em = _groundMistParticleSystem.emission;
                em.enabled = false;
            }
            if (_fogWallBarrier != null) _fogWallBarrier.SetActive(false);
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
            if (_fogWallBarrier != null)
                _fogWallBarrier.SetActive(false);
            return;
        }

        if (_fogEffectRoot != null && !_fogEffectRoot.activeSelf)
            _fogEffectRoot.SetActive(true);

        Color pc = fogColor;
        pc.a = _currentFogAlpha * fogColor.a;

        if (_fogWallBarrier != null && _fogWallMaterial != null)
        {
            _fogWallBarrier.SetActive(true);
            Color wallCol = fogColor;
            wallCol.a = _currentFogAlpha * 0.98f;
            _fogWallMaterial.color = wallCol;
            if (_fogWallMaterial.HasProperty("_BaseColor"))
                _fogWallMaterial.SetColor("_BaseColor", wallCol);
        }

        if (_fogParticleSystem != null)
        {
            var main = _fogParticleSystem.main;
            main.startColor = pc;

            var em = _fogParticleSystem.emission;
            em.rateOverTime = 450f * _currentFogAlpha;
        }

        if (_groundMistParticleSystem != null)
        {
            var main = _groundMistParticleSystem.main;
            main.startColor = pc;

            var em = _groundMistParticleSystem.emission;
            em.rateOverTime = 180f * _currentFogAlpha;
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

        Debug.Log("[MazeManager] One-Click Setup finished! 8 Blocks, DayNight Controller, and BoxCollider Smoke successfully linked.");
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
        box.size = new Vector3(30f, 65f, 160f);
        mazeEntranceFog = createdFogGo.transform;
        Debug.Log($"[MazeManager] Created default 'maze enterence fog' BoxCollider at {createdFogGo.transform.position}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EDITOR CONTEXT MENU TESTING TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Simulate Day (Open Door & Smoke Off)")]
    public void TestOpenDoor() => StartCoroutine(DayStartRoutine());

    [ContextMenu("Simulate Night (Close Door, Smoke On & Swap)")]
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
