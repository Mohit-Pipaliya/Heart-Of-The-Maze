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
/// 4. Glade Entrance Stone Blast Gate & Shadow Void Occlusion (The Maze Runner Style):
///    - Zero smoke, zero cartoon transparency: 100% Opaque solid stone blast gate + interior shadow void.
///    - Night starts: Massive stone blast gate drops down / slams shut, completely sealing the doorway so nothing can be seen across ("aar-paar kuch na dikhe")!
///    - Behind the closed gate, block swap executes secretly in total isolation.
///    - Morning (Day) starts: Massive stone blast gate smoothly rises up into the archway ceiling, revealing the new maze!
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

    [Header("─── 2. Glade Entrance Occlusion Gate (The Maze Gate) ────")]
    [Tooltip("Scene me 'Maze Enterence Fog' ya entrance GameObject/BoxCollider.")]
    public Transform mazeEntranceFog;

    [Tooltip("Entrance stone blast gate enable karein (100% opaque solid stone, zero see-through)")]
    public bool useEntranceGate = true;

    [Tooltip("Gate open / close animation duration in seconds")]
    [Range(0.5f, 4f)] public float gateAnimationDuration = 1.8f;

    [Tooltip("Custom material for the Stone Blast Gate (empty chhodne par auto maze wall material load karega)")]
    public Material customGateMaterial;

    [Header("─── 3. Day / Night Door Automation ─────────────────────")]
    [Tooltip("Automatically connect to DayNightController in scene")]
    public bool syncWithDayNight = true;

    [Tooltip("Day hone ke kitne second baad door open ho")]
    [Range(0f, 3f)] public float doorOpenDelay = 0.5f;

    [Header("─── 4. Rotation Sequence ───────────────────────────────")]
    [Tooltip("Block sequence: Day 1 starts with Block 8, then 1, 2, 3, 4, 5, 6, 7, then returns to 8!")]
    public int[] blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

    [Header("─── 5. Audio (Optional) ────────────────────────────────")]
    public AudioClip gateCloseSound;
    public AudioClip gateOpenSound;
    public AudioClip swapRumbleSound;
    [Range(0f, 1f)] public float soundVolume = 0.85f;

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

    // ─── Entrance Gate Internal References ───
    private GameObject _entranceGateRoot;
    private Transform _blastGateMover;
    private GameObject _shadowVoidVolume;
    private Vector3 _gateOpenLocalPos;
    private Vector3 _gateClosedLocalPos;
    private bool _isGateOpen = false;
    private Coroutine _swapRoutine;
    private Coroutine _gateAnimRoutine;
    private AudioSource _audioSource;
    private AudioClip _proceduralRumbleClip;

    // ─── Day/Night Tracking ───
    private bool _wasDay = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        InitializeAudio();
        CaptureInitialSlots();
        EnsureEntranceGateVisuals(true);
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
            SetGateState(open: true, instant: true);
            OpenActiveDoor();
        }
        else
        {
            SetGateState(open: false, instant: true);
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
        // Subah (Day) hote hi Stone Blast Gate smoothly open ho kar ceiling me raise ho jaye
        yield return AnimateEntranceGate(open: true, gateAnimationDuration);

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

        // 1. Active block entrance door ko close hone ka command dein
        CloseActiveDoor();
        yield return new WaitForSeconds(0.4f);

        // 2. Colossal Stone Blast Gate slams shut, completely sealing the doorway!
        //    (Ab aar-paar bilkul kuch nahi dikhega - 100% solid stone wall)
        yield return AnimateEntranceGate(open: false, gateAnimationDuration);

        // 3. Play rumble audio and perform block swap behind the sealed solid gate
        PlayAudio(swapRumbleSound != null ? swapRumbleSound : GetOrCreateGateRumbleClip());
        AdvanceToNextBlock();

        // Blast gate poori raat bandh rahegi!
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
    public static int GetOptimalGladeRotation(int blockNumber)
    {
        switch (blockNumber)
        {
            case 1: return 180;
            case 2: return 0;
            case 3: return 270;
            case 4: return 180;
            case 5: return 270;
            case 6: return 0;
            case 7: return 90;
            case 8: return 0;
            default: return 0;
        }
    }

    private void ApplyInitialActiveBlock()
    {
        int initialBlockNum = (blockSequence != null && blockSequence.Length > 0) ? blockSequence[0] : 8;
        currentSequenceIndex = 0;
        currentActiveBlockNumber = initialBlockNum;

        GameObject activeBlock = GetBlock(initialBlockNum);
        if (activeBlock != null)
        {
            int optimalAngle = GetOptimalGladeRotation(initialBlockNum);
            PositionBlockAtGladeSlot(activeBlock, optimalAngle);

            for (int i = 0; i < mazeBlocks.Length; i++)
            {
                GameObject b = mazeBlocks[i];
                if (b != null && b != activeBlock)
                {
                    if (Vector3.Distance(b.transform.position, _activeSlotCenter) < 700f)
                    {
                        int fallbackSlot = (i < 7) ? i : 0;
                        b.transform.SetPositionAndRotation(_homePositions[fallbackSlot], Quaternion.identity);
                    }
                }
            }
        }
    }

    public void CaptureInitialSlots()
    {
        if (_slotsCaptured) return;

        for (int i = 0; i < 8; i++)
        {
            GameObject block = GetBlock(i + 1);
            if (block != null)
            {
                _homePositions[i] = block.transform.position;
                _homeRotations[i] = block.transform.rotation;
            }
            else
            {
                _homePositions[i] = new Vector3(3338f + (i * 1100f), 0f, 1000f);
                _homeRotations[i] = Quaternion.identity;
            }
        }

        GameObject block8 = GetBlock(8);
        if (block8 != null)
        {
            _activeSlotBasePosition = block8.transform.position;
            _activeSlotCenter = _activeSlotBasePosition + new Vector3(500f, 0f, 500f);
        }
        else
        {
            _activeSlotCenter = new Vector3(3838f, 0f, 1500f);
        }

        _slotsCaptured = true;
    }

    public GameObject GetBlock(int number)
    {
        int idx = number - 1;
        if (mazeBlocks != null && idx >= 0 && idx < mazeBlocks.Length)
        {
            return mazeBlocks[idx];
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DOOR CONTROL (STRICTLY ONLY GLADE-FACING DOOR OPENS)
    // ═══════════════════════════════════════════════════════════════════════════

    public void OpenActiveDoor()
    {
        CloseAllDoorsInstant();

        DoubleSlidingDoor gladeDoor = FindActiveEntranceDoor();
        if (gladeDoor != null)
        {
            gladeDoor.OpenByManager();
            Debug.Log($"[MazeManager] Glade-facing door opened on Block {currentActiveBlockNumber} at world position {gladeDoor.transform.position}!");
        }
        else
        {
            Debug.LogWarning($"[MazeManager] No Glade-facing entrance door found near Glade threshold on Block {currentActiveBlockNumber}!");
        }
    }

    public void CloseActiveDoor()
    {
        DoubleSlidingDoor gladeDoor = FindActiveEntranceDoor();
        if (gladeDoor != null)
        {
            gladeDoor.CloseByManager();
        }
    }

    public void CloseAllDoorsInstant()
    {
        for (int i = 1; i <= 8; i++)
        {
            GameObject b = GetBlock(i);
            if (b != null) ReleaseAndCloseBlockDoors(b);
        }
    }

    public void ReleaseAndCloseBlockDoors(GameObject block)
    {
        if (block == null) return;
        DoubleSlidingDoor[] doors = block.GetComponentsInChildren<DoubleSlidingDoor>(true);
        foreach (var door in doors)
        {
            if (door != null)
            {
                door.CloseByManager();
            }
        }
    }

    public DoubleSlidingDoor FindActiveEntranceDoor()
    {
        GameObject activeBlock = GetBlock(currentActiveBlockNumber);
        if (activeBlock == null) return null;

        DoubleSlidingDoor[] doors = activeBlock.GetComponentsInChildren<DoubleSlidingDoor>(true);
        if (doors == null || doors.Length == 0) return null;

        DoubleSlidingDoor bestDoor = null;
        float maxWorldX = float.MinValue;

        foreach (var d in doors)
        {
            if (d == null) continue;
            float worldX = d.transform.position.x;
            if (worldX > maxWorldX)
            {
                maxWorldX = worldX;
                bestDoor = d;
            }
        }

        if (bestDoor != null && maxWorldX >= 4200f)
        {
            return bestDoor;
        }

        return bestDoor;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  GLADE ENTRANCE STONE BLAST GATE & SHADOW VOID OCCLUSION
    //  (100% OPAQUE - AAR PAAR KUCH BHI NAHI DIKHEGA!)
    // ═══════════════════════════════════════════════════════════════════════════

    public void EnsureEntranceGateVisuals(bool forceRecreate = false)
    {
        if (mazeEntranceFog == null)
        {
            AutoFindEntranceFog();
            if (mazeEntranceFog == null) return;
        }

        // 1. Clean up legacy smoke particles if they exist
        Transform legacyFog = mazeEntranceFog.Find("_GeneratedFogEffect");
        if (legacyFog != null)
        {
            if (Application.isPlaying) Destroy(legacyFog.gameObject);
            else DestroyImmediate(legacyFog.gameObject);
        }

        if (!useEntranceGate) return;

        // 2. Check if gate already exists
        Transform existingGate = mazeEntranceFog.Find("_GladeEntranceGate");
        if (existingGate != null && (forceRecreate || _entranceGateRoot == null || _blastGateMover == null))
        {
            if (Application.isPlaying) Destroy(existingGate.gameObject);
            else DestroyImmediate(existingGate.gameObject);
            existingGate = null;
        }

        if (existingGate == null)
        {
            CreateGladeEntranceGate();
        }
        else
        {
            _entranceGateRoot = existingGate.gameObject;
            _blastGateMover = _entranceGateRoot.transform.Find("StoneBlastGateMover");
            Transform voidT = _entranceGateRoot.transform.Find("ArchwayShadowVoid");
            if (voidT != null) _shadowVoidVolume = voidT.gameObject;
        }
    }

    private void CreateGladeEntranceGate()
    {
        _entranceGateRoot = new GameObject("_GladeEntranceGate");
        _entranceGateRoot.transform.SetParent(mazeEntranceFog, false);

        BoxCollider box = mazeEntranceFog.GetComponent<BoxCollider>();
        Vector3 gateCenter = Vector3.zero;
        float gateWidth = 180f;
        float gateHeight = 160f;

        if (box != null)
        {
            gateCenter = box.center;
            gateHeight = Mathf.Max(120f, box.size.y);
            gateWidth = Mathf.Clamp(box.size.z, 160f, 600f);
        }

        _entranceGateRoot.transform.localPosition = gateCenter;
        _entranceGateRoot.transform.localRotation = Quaternion.identity;

        // Stone blast gate material (matches the ancient stone walls of the maze)
        Material gateMat = customGateMaterial
            ?? Resources.Load<Material>("Materials/M_StoneBlastGate")
            ?? Resources.Load<Material>("Materials/0")
            ?? GetWallMaterialFallback();

        // ─────────────────────────────────────────────────────────────
        // 1. MOVING STONE BLAST GATE (Drops down at night, raises at day)
        // ─────────────────────────────────────────────────────────────
        GameObject moverGo = new GameObject("StoneBlastGateMover");
        moverGo.transform.SetParent(_entranceGateRoot.transform, false);
        _blastGateMover = moverGo.transform;

        // Main massive stone slab
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "StoneBlastGateSlab";
        slab.transform.SetParent(_blastGateMover, false);
        slab.transform.localPosition = Vector3.zero;
        slab.transform.localScale = new Vector3(8f, gateHeight, gateWidth);

        Renderer slabRenderer = slab.GetComponent<Renderer>();
        if (slabRenderer != null && gateMat != null)
        {
            slabRenderer.sharedMaterial = gateMat;
        }

        // Solid collider so player cannot pass through while closed
        BoxCollider slabCol = slab.GetComponent<BoxCollider>();
        if (slabCol != null) slabCol.isTrigger = false;

        // Decorative stone reinforcement pillars on both ends
        float halfW = gateWidth * 0.48f;
        CreateReinforcementPillar("Pillar_Left", _blastGateMover, new Vector3(1f, 0f, -halfW), gateHeight, gateMat);
        CreateReinforcementPillar("Pillar_Right", _blastGateMover, new Vector3(1f, 0f, halfW), gateHeight, gateMat);

        // ─────────────────────────────────────────────────────────────
        // 2. ABYSSAL SHADOW VOID (Pitch black interior volume)
        // ─────────────────────────────────────────────────────────────
        _shadowVoidVolume = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _shadowVoidVolume.name = "ArchwayShadowVoid";
        _shadowVoidVolume.transform.SetParent(_entranceGateRoot.transform, false);
        _shadowVoidVolume.transform.localPosition = new Vector3(-12f, 0f, 0f);
        _shadowVoidVolume.transform.localScale = new Vector3(20f, gateHeight * 1.05f, gateWidth * 1.02f);

        Collider voidCol = _shadowVoidVolume.GetComponent<Collider>();
        if (voidCol != null) DestroyImmediate(voidCol);

        Material voidMat = Resources.Load<Material>("Materials/M_ArchwayVoid");
        if (voidMat == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                voidMat = new Material(unlit);
                voidMat.color = Color.black;
            }
        }
        Renderer voidRend = _shadowVoidVolume.GetComponent<Renderer>();
        if (voidRend != null && voidMat != null) voidRend.sharedMaterial = voidMat;

        // Calculate closed and open local positions
        _gateClosedLocalPos = Vector3.zero;
        _gateOpenLocalPos = new Vector3(0f, gateHeight + 15f, 0f);

        // Initial state
        _blastGateMover.localPosition = _gateClosedLocalPos;
        _isGateOpen = false;
        _shadowVoidVolume.SetActive(true);
    }

    private void CreateReinforcementPillar(string name, Transform parent, Vector3 localPos, float height, Material mat)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = name;
        pillar.transform.SetParent(parent, false);
        pillar.transform.localPosition = localPos;
        pillar.transform.localScale = new Vector3(11f, height * 1.02f, 14f);
        Renderer r = pillar.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
    }

    private Material GetWallMaterialFallback()
    {
        GameObject wall = GameObject.Find("wall (72)") ?? GameObject.Find("wall (1)") ?? GameObject.Find("Wall");
        if (wall != null)
        {
            Renderer r = wall.GetComponentInChildren<Renderer>();
            if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
        }
        return null;
    }

    public IEnumerator AnimateEntranceGate(bool open, float duration)
    {
        EnsureEntranceGateVisuals();
        if (_blastGateMover == null) yield break;

        if (_gateAnimRoutine != null) StopCoroutine(_gateAnimRoutine);

        Vector3 startPos = _blastGateMover.localPosition;
        Vector3 targetPos = open ? _gateOpenLocalPos : _gateClosedLocalPos;

        if (!open && _shadowVoidVolume != null)
        {
            _shadowVoidVolume.SetActive(true);
        }

        AudioClip clipToPlay = open ? gateOpenSound : (gateCloseSound != null ? gateCloseSound : swapRumbleSound);
        if (clipToPlay == null) clipToPlay = GetOrCreateGateRumbleClip();
        PlayAudio(clipToPlay);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            _blastGateMover.localPosition = Vector3.Lerp(startPos, targetPos, smoothT);
            yield return null;
        }

        _blastGateMover.localPosition = targetPos;
        _isGateOpen = open;

        if (open && _shadowVoidVolume != null)
        {
            _shadowVoidVolume.SetActive(false);
        }

        _gateAnimRoutine = null;
    }

    public void SetGateState(bool open, bool instant = false)
    {
        EnsureEntranceGateVisuals();
        if (_blastGateMover == null) return;

        if (instant)
        {
            if (_gateAnimRoutine != null) { StopCoroutine(_gateAnimRoutine); _gateAnimRoutine = null; }
            _blastGateMover.localPosition = open ? _gateOpenLocalPos : _gateClosedLocalPos;
            _isGateOpen = open;
            if (_shadowVoidVolume != null) _shadowVoidVolume.SetActive(!open);
        }
        else
        {
            if (Application.isPlaying)
            {
                StartCoroutine(AnimateEntranceGate(open, gateAnimationDuration));
            }
            else
            {
                _blastGateMover.localPosition = open ? _gateOpenLocalPos : _gateClosedLocalPos;
                _isGateOpen = open;
                if (_shadowVoidVolume != null) _shadowVoidVolume.SetActive(!open);
            }
        }
    }

    private AudioClip GetOrCreateGateRumbleClip()
    {
        if (_proceduralRumbleClip != null) return _proceduralRumbleClip;
        int sampleRate = 22050;
        int length = sampleRate * 2;
        float[] data = new float[length];
        System.Random rand = new System.Random(42);
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            float low1 = Mathf.Sin(t * Mathf.PI * 2f * 32f);
            float low2 = Mathf.Sin(t * Mathf.PI * 2f * 54f);
            float low3 = Mathf.Sin(t * Mathf.PI * 2f * 82f);
            float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.35f;
            float env = Mathf.Sin(Mathf.Clamp01(t / 2f) * Mathf.PI);
            data[i] = (low1 * 0.45f + low2 * 0.3f + low3 * 0.15f + noise) * env * 0.7f;
        }
        _proceduralRumbleClip = AudioClip.Create("StoneGateGrind", length, 1, sampleRate, false);
        _proceduralRumbleClip.SetData(data, 0);
        return _proceduralRumbleClip;
    }

    private void InitializeAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
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

    public void ToggleEntranceGate() => SetGateState(!_isGateOpen);

    // ─── Backwards Compatibility Stubs ───
    public void EnsureEntranceFogVisuals(bool forceRecreate = false) => EnsureEntranceGateVisuals(forceRecreate);
    public void TestOpenDoor() => StartCoroutine(DayStartRoutine());
    public void TestToggleFog() => ToggleEntranceGate();

    [System.Obsolete("Fog wall has been replaced by the Stone Blast Gate.")]
    public IEnumerator FadeFog(float targetAlpha, float duration)
    {
        bool open = targetAlpha <= 0.05f;
        yield return AnimateEntranceGate(open, duration);
    }

    [System.Obsolete("Fog wall has been replaced by the Stone Blast Gate.")]
    public void SetFogAlpha(float alpha)
    {
        bool open = alpha <= 0.05f;
        SetGateState(open, instant: true);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  AUTO-DISCOVERY & ONE-CLICK SETUP
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("One-Click Setup")]
    public void OneClickSetup()
    {
        AutoFindBlocks();
        AutoFindEntranceFog();
        EnsureEntranceGateVisuals(true);
        _slotsCaptured = false;
        CaptureInitialSlots();
        ApplyInitialActiveBlock();

        Debug.Log("[MazeManager] One-Click Setup finished! 8 Blocks, DayNight Controller, and Stone Blast Gate successfully linked.");
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
                Debug.Log($"[MazeManager] Auto-linked Block {i} -> '{found.name}'");
            }
        }
    }

    public void AutoFindEntranceFog()
    {
        if (mazeEntranceFog != null) return;

        string[] searchNames = new string[] {
            "Maze Enterence Fog",
            "maze enterence fog",
            "maze entrance fog",
            "Maze Entrance Fog",
            "EntranceFog",
            "Entrance Fog"
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

        GameObject createdFogGo = new GameObject("Maze Enterence Fog");
        createdFogGo.transform.position = new Vector3(4335.4f, 0f, 1552.6f);
        BoxCollider box = createdFogGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(30f, 65f, 160f);
        mazeEntranceFog = createdFogGo.transform;
        Debug.Log($"[MazeManager] Created default 'Maze Enterence Fog' BoxCollider at {createdFogGo.transform.position}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EDITOR CONTEXT MENU TESTING TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Simulate Day (Open Gate & Open Door)")]
    public void TestDayStart() => StartCoroutine(DayStartRoutine());

    [ContextMenu("Simulate Night (Close Door, Slam Gate & Swap)")]
    public void TestNightSwap() => StartNightSwap();

    [ContextMenu("Toggle Entrance Gate")]
    public void TestToggleGate()
    {
        SetGateState(!_isGateOpen);
    }

    [ContextMenu("Jump To Next Block Immediately")]
    public void TestJumpNext() => AdvanceToNextBlock();
}
