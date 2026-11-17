using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple & Robust Maze System Manager.
/// 
/// 100% REALISTIC & LOGICAL (No Magic, No Slabs, No Cartoon Smoke):
/// 1. 8 Maze Blocks (Maze 1 to Maze 8).
/// 2. Day 1 starts with Block 8 facing the Glade entrance.
/// 3. Real Mechanical Double Sliding Door (Maze Door):
///    - When Night falls, the mechanical double sliding doors slide shut and latch with a heavy mechanical sound.
///    - Once the doors are closed, the player sees the real closed door.
///    - Behind the closed doors, the ground rumbles and stone gears grind as the maze blocks rearrange in secrecy.
///    - When Morning (Day) comes, the doors slide open, revealing the new maze layout for the day!
/// 4. Mutual Position Swap:
///    - Sequence: Block 8 -> Block 1 -> Block 2 -> Block 3 -> Block 4 -> Block 5 -> Block 6 -> Block 7 -> Block 8.
///    - Exactly ONE block is at the Glade entrance at any given time (no overlapping ever).
///    - Whichever block arrives at the Glade entrance is automatically rotated so its entrance corridor ALWAYS faces the Glade!
/// 5. One-Click Setup in Inspector!
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
public class MazeManager : MonoBehaviour
{
    public static MazeManager Instance { get; private set; }

    [Header("─── 1. Maze Blocks (1 to 8) ───────────────────────────")]
    [Tooltip("Assign Blocks 1 to 8 in order. (Index 0 = Block 1, ..., Index 7 = Block 8)")]
    public GameObject[] mazeBlocks = new GameObject[8];

    [Header("─── 2. Glade Entrance Door (The Main Maze Gate) ────────")]
    [Tooltip("Scene ka main sliding entrance door (DoubleSlidingDoor). Empty chhodne par auto-find ya auto-create kar lega.")]
    public DoubleSlidingDoor mazeEntranceDoor;

    [Tooltip("Scene me entrance marker ya collider (optional).")]
    public Transform mazeEntranceFog;

    [Header("─── 3. Day / Night Door Automation ─────────────────────")]
    [Tooltip("Automatically connect to DayNightController in scene")]
    public bool syncWithDayNight = true;

    [Tooltip("Day hone ke kitne second baad door open ho")]
    [Range(0f, 3f)] public float doorOpenDelay = 0.5f;

    [Header("─── 4. Rotation Sequence ───────────────────────────────")]
    [Tooltip("Block sequence: Day 1 starts with Block 8, then 1, 2, 3, 4, 5, 6, 7, then returns to 8!")]
    public int[] blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

    [Header("─── 5. Audio (Optional) ────────────────────────────────")]
    public AudioClip swapRumbleSound;
    public AudioClip doorCloseSound;
    public AudioClip doorOpenSound;
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

    // ─── Door Tracking ───
    private Coroutine _swapRoutine;
    private AudioSource _audioSource;
    private AudioClip _proceduralRumbleClip;
    private bool _wasDay = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        InitializeAudio();
        CaptureInitialSlots();
        CleanupOldEffects();
        EnsureEntranceDoor();
    }

    private void Start()
    {
        if (DayNightController.Instance != null)
        {
            _wasDay = DayNightController.Instance.IsDay;
        }

        // Apply Day 1 initial active block (Block 8)
        ApplyInitialActiveBlock();

        EnsureEntranceDoor();

        if (DayNightController.Instance != null && DayNightController.Instance.IsDay)
        {
            OpenActiveDoor();
        }
        else
        {
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
    //  DAY / NIGHT HANDLERS (LOGICAL MECHANICAL FLOW)
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
        EnsureEntranceDoor();

        if (doorOpenDelay > 0f)
            yield return new WaitForSeconds(doorOpenDelay);

        // Subah (Day) hote hi real mechanical sliding doors open ho jayein!
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

        EnsureEntranceDoor();

        // 1. Raat hote hi real mechanical sliding doors bandh hone lagti hain
        CloseActiveDoor();

        // Wait for the mechanical doors to slide completely shut (realistic timing)
        yield return new WaitForSeconds(1.6f);

        // 2. Darwaza poori tarah bandh hone ke baad:
        //    Piche shifting stone rumbling sound bajti hai
        PlayAudio(swapRumbleSound != null ? swapRumbleSound : GetOrCreateGateRumbleClip());

        // 3. Bandh darwaze ke piche maze blocks swap aur rotate hote hain
        //    (Player ko bandh darwaza dikhta hai, isliye koi pop ya teleport nahi dikhega!)
        AdvanceToNextBlock();

        // Darwaza poori raat bandh rehta hai taaki maze ke khatre bahar na aayein
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
            // Close all doors on both blocks
            ReleaseAndCloseBlockDoors(outgoingBlock);
            ReleaseAndCloseBlockDoors(incomingBlock);

            // Read incoming block's current position (where it is resting in reserve)
            Vector3 reservePos = incomingBlock.transform.position;

            // Failsafe: If reservePos is near Glade entrance, send to safe home slot
            if (Vector3.Distance(reservePos, _activeSlotCenter) < 700f)
            {
                int safeIdx = (outgoingBlockNum - 1 < 7) ? outgoingBlockNum - 1 : 0;
                reservePos = _homePositions[safeIdx];
            }

            // 1. Outgoing block moves to reserve slot with identity rotation
            outgoingBlock.transform.SetPositionAndRotation(reservePos, Quaternion.identity);
            SyncStandWithBlock(outgoingBlockNum);

            // 2. Incoming block moves to active Glade slot and rotates so its corridor faces Glade
            int optimalAngle = GetOptimalGladeRotation(incomingBlockNum);
            PositionBlockAtGladeSlot(incomingBlock, optimalAngle);
            SyncStandWithBlock(incomingBlockNum);

            currentActiveBlockNumber = incomingBlockNum;

            // 3. Strict Failsafe: Ensure that GLADE KE SAMNE HAMESHA EK HI BLOCK HO!
            for (int i = 0; i < mazeBlocks.Length; i++)
            {
                GameObject b = mazeBlocks[i];
                if (b != null && b != incomingBlock)
                {
                    if (Vector3.Distance(b.transform.position, _activeSlotCenter) < 700f)
                    {
                        int fallbackSlot = (i < 7) ? i : 0;
                        b.transform.SetPositionAndRotation(_homePositions[fallbackSlot], Quaternion.identity);
                        Debug.LogWarning($"[MazeManager] Failsafe: Cleared overlapping block {i + 1} to reserve slot {fallbackSlot + 1}!");
                    }
                }
            }

            Debug.Log($"[MazeManager] SWAP COMPLETE: Block {incomingBlockNum} is now active at Glade (rot={optimalAngle}°). Block {outgoingBlockNum} moved to reserve.");
        }
    }

    /// <summary>
    /// Positions and rotates a 1000x1000 block around its center so its footprint
    /// matches the active slot and its entrance corridor faces directly into the Glade!
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
    /// Synchronizes the position and rotation of the Stand corresponding to blockNum
    /// so that it always remains placed outside the secret room door on the right side!
    /// </summary>
    private void SyncStandWithBlock(int blockNum)
    {
        var stands = UnityEngine.Object.FindObjectsByType<PlateStandInteraction>(FindObjectsSortMode.None);
        if (stands == null) return;

        foreach (var stand in stands)
        {
            if (stand.standID == blockNum)
            {
                stand.InitializeTargetDoor();
                if (stand.targetDoor != null)
                {
                    Transform dt = stand.targetDoor.transform;
                    Vector3 worldPos = dt.position + dt.TransformDirection(new Vector3(40.4f, 0f, 67.5f));
                    float targetY = 4.1f;
                    switch (blockNum)
                    {
                        case 1: targetY = 4.1f; break;
                        case 2: targetY = 4.0f; break;
                        case 3: targetY = 4.3f; break;
                        case 4: targetY = 5.3f; break;
                        case 5: targetY = 4.8f; break;
                        case 6: targetY = 4.2f; break;
                        case 7: targetY = 4.7f; break;
                        case 8: targetY = 4.5f; break;
                    }
                    worldPos.y = targetY;
                    float rotY = (dt.eulerAngles.y + 90f) % 360f;

                    Transform root = stand.transform;
                    while (root.parent != null && root.parent.name.ToLower().Contains("stand") && !root.parent.name.ToLower().Contains("maze stands"))
                    {
                        root = root.parent;
                    }
                    root.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, rotY, 0f));
                }
                break;
            }
        }
    }

    /// <summary>
    /// Optimal cardinal rotation angle (0, 90, 180, 270) for each of the 8 blocks
    /// so that its main entrance is ALWAYS situated on the East edge facing the Glade!
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
            SyncStandWithBlock(initialBlockNum);

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
    //  REAL MECHANICAL DOUBLE SLIDING DOOR CONTROL
    // ═══════════════════════════════════════════════════════════════════════════

    public void EnsureEntranceDoor()
    {
        CleanupOldEffects();

        if (mazeEntranceDoor != null) return;

        // 1. Search scene near entrance (4333, 0, 1554)
        DoubleSlidingDoor[] allDoors = UnityEngine.Object.FindObjectsByType<DoubleSlidingDoor>(FindObjectsSortMode.None);
        foreach (var d in allDoors)
        {
            if (d == null) continue;
            float dist = Vector3.Distance(d.transform.position, new Vector3(4333f, 0f, 1554f));
            if (dist < 120f)
            {
                mazeEntranceDoor = d;
                Debug.Log($"[MazeManager] Linked Glade Entrance Door to '{d.gameObject.name}' at {d.transform.position}");
                return;
            }
        }

        // 2. Fallback to active block's Glade-facing door
        mazeEntranceDoor = FindActiveEntranceDoor();
        if (mazeEntranceDoor != null) return;

        // 3. Auto-instantiate the authentic Maze Door prefab at the Glade gateway
        GameObject prefab = Resources.Load<GameObject>("Maze Door");
        if (prefab != null)
        {
            GameObject doorGo = Instantiate(prefab, new Vector3(4333f, 0f, 1554f), Quaternion.identity);
            doorGo.name = "Glade Entrance Door";
            mazeEntranceDoor = doorGo.GetComponent<DoubleSlidingDoor>();
            Debug.Log("[MazeManager] Created permanent Glade Entrance Door at (4333, 0, 1554) from Maze Door prefab.");
        }
    }

    public void OpenActiveDoor()
    {
        EnsureEntranceDoor();

        if (mazeEntranceDoor != null)
        {
            mazeEntranceDoor.OpenByManager();
            PlayAudio(doorOpenSound != null ? doorOpenSound : mazeEntranceDoor.openSound);
            Debug.Log($"[MazeManager] Glade Entrance Door opened at {mazeEntranceDoor.transform.position}!");
        }

        // Also open block's Glade-facing door if separate
        DoubleSlidingDoor blockDoor = FindActiveEntranceDoor();
        if (blockDoor != null && blockDoor != mazeEntranceDoor)
        {
            blockDoor.OpenByManager();
        }
    }

    public void CloseActiveDoor()
    {
        EnsureEntranceDoor();

        if (mazeEntranceDoor != null)
        {
            mazeEntranceDoor.CloseByManager();
            PlayAudio(doorCloseSound != null ? doorCloseSound : mazeEntranceDoor.closeSound);
            Debug.Log($"[MazeManager] Glade Entrance Door closed at {mazeEntranceDoor.transform.position}!");
        }

        DoubleSlidingDoor blockDoor = FindActiveEntranceDoor();
        if (blockDoor != null && blockDoor != mazeEntranceDoor)
        {
            blockDoor.CloseByManager();
        }
    }

    public void CloseAllDoorsInstant()
    {
        EnsureEntranceDoor();

        if (mazeEntranceDoor != null)
        {
            mazeEntranceDoor.CloseByManager();
        }

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

        return bestDoor;
    }

    public void CleanupOldEffects()
    {
        if (mazeEntranceFog != null)
        {
            Transform oldFog = mazeEntranceFog.Find("_GeneratedFogEffect");
            if (oldFog != null)
            {
                if (Application.isPlaying) Destroy(oldFog.gameObject);
                else DestroyImmediate(oldFog.gameObject);
            }

            Transform oldGate = mazeEntranceFog.Find("_GladeEntranceGate");
            if (oldGate != null)
            {
                if (Application.isPlaying) Destroy(oldGate.gameObject);
                else DestroyImmediate(oldGate.gameObject);
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
            float low1 = Mathf.Sin(t * Mathf.PI * 2f * 30f);
            float low2 = Mathf.Sin(t * Mathf.PI * 2f * 52f);
            float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.3f;
            float env = Mathf.Sin(Mathf.Clamp01(t / 2f) * Mathf.PI);
            data[i] = (low1 * 0.5f + low2 * 0.3f + noise) * env * 0.7f;
        }
        _proceduralRumbleClip = AudioClip.Create("MazeStoneRumble", length, 1, sampleRate, false);
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

    // ─── Backwards Compatibility & Public Actions ───
    public void ToggleEntranceDoor()
    {
        EnsureEntranceDoor();
        if (mazeEntranceDoor != null)
        {
            if (mazeEntranceDoor.isOpen) CloseActiveDoor();
            else OpenActiveDoor();
        }
    }

    public void EnsureEntranceGateVisuals(bool forceRecreate = false) => EnsureEntranceDoor();
    public void EnsureEntranceFogVisuals(bool forceRecreate = false) => EnsureEntranceDoor();

    // ═══════════════════════════════════════════════════════════════════════════
    //  AUTO-DISCOVERY & ONE-CLICK SETUP
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("One-Click Setup")]
    public void OneClickSetup()
    {
        AutoFindBlocks();
        AutoFindEntranceFog();
        EnsureEntranceDoor();
        _slotsCaptured = false;
        CaptureInitialSlots();
        ApplyInitialActiveBlock();

        Debug.Log("[MazeManager] One-Click Setup finished! 8 Blocks, DayNight Controller, and Glade Entrance Door successfully linked.");
    }

    public void AutoFindBlocks()
    {
        bool needsSearch = false;
        for (int i = 0; i < 8; i++)
        {
            if (mazeBlocks[i] == null) { needsSearch = true; break; }
        }

        if (!needsSearch) return;

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
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EDITOR CONTEXT MENU TESTING TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Simulate Day (Open Door)")]
    public void TestDayStart() => StartCoroutine(DayStartRoutine());

    [ContextMenu("Simulate Night (Close Door & Swap)")]
    public void TestNightSwap() => StartNightSwap();

    [ContextMenu("Toggle Entrance Door")]
    public void TestToggleDoor() => ToggleEntranceDoor();

    [ContextMenu("Jump To Next Block Immediately")]
    public void TestJumpNext() => AdvanceToNextBlock();
}
