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
/// 3. Nightly Rotation: Block 8 -> Block 1 -> Block 2 -> Block 3 -> Block 4 -> Block 5 -> Block 6 -> Block 7 -> Block 8.
/// 4. Entrance Fog: When night arrives, thick mysterious fog automatically covers the entrance so block swapping is hidden.
/// 5. Day/Night Doors: Day starts -> Active block door opens. Night starts -> Active door closes.
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
    [Tooltip("Scene me 'maze enterence fog' empty GameObject ya BoxCollider yahan drag karein.")]
    public Transform mazeEntranceFog;

    [Tooltip("Fog visual effect script khud generate karegi")]
    public bool autoGenerateFog = true;

    [Tooltip("Fog fade hone ki speed (seconds)")]
    [Range(0.5f, 4f)] public float fogFadeDuration = 1.6f;

    [Tooltip("Fog ka color (mystical misty white/blue)")]
    public Color fogColor = new Color(0.85f, 0.88f, 0.96f, 0.98f);

    [Header("─── 3. Day / Night Door Automation ─────────────────────")]
    [Tooltip("Automatically connect to DayNightController in scene")]
    public bool syncWithDayNight = true;

    [Tooltip("Day hone ke kitne second baad door open ho")]
    [Range(0f, 3f)] public float doorOpenDelay = 0.5f;

    [Header("─── 4. Rotation Sequence ───────────────────────────────")]
    [Tooltip("Block order: Day 1 starts with Block 8, then 1, 2, 3, 4, 5, 6, 7, then returns to 8!")]
    public int[] blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

    [Header("─── 5. Audio (Optional) ────────────────────────────────")]
    public AudioClip fogSound;
    public AudioClip swapRumbleSound;
    [Range(0f, 1f)] public float soundVolume = 0.75f;

    [Header("─── 6. Live Status (Read Only) ─────────────────────────")]
    [SerializeField] private int currentActiveBlockNumber = 8;
    [SerializeField] private int currentSequenceIndex = 0;
    [SerializeField] private bool isSwapping = false;

    // ─── Slot Memory ───
    private Vector3 _activeSlotPosition;
    private Quaternion _activeSlotRotation;
    private Vector3[] _homePositions = new Vector3[8];
    private Quaternion[] _homeRotations = new Quaternion[8];
    private bool _slotsCaptured = false;

    // ─── Fog Internal References ───
    private GameObject _fogEffectRoot;
    private ParticleSystem _fogParticleSystem;
    private MeshRenderer _fogCurtainRenderer;
    private Material _fogCurtainMaterial;
    private float _currentFogAlpha = 0f;
    private Coroutine _fogFadeRoutine;
    private Coroutine _swapRoutine;
    private AudioSource _audioSource;

    // ─── Day/Night Tracking ───
    private bool _wasDay = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        InitializeAudio();
        CaptureInitialSlots();
        EnsureEntranceFogVisuals();
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
        if (doorOpenDelay > 0f)
            yield return new WaitForSeconds(doorOpenDelay);

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
        yield return new WaitForSeconds(1.2f);

        // 2. Fade in dense mysterious entrance fog to hide the doorway
        PlayAudio(fogSound);
        yield return FadeFog(1f, fogFadeDuration);

        // 3. Play rumble audio and swap the blocks behind the fog curtain
        PlayAudio(swapRumbleSound);
        yield return new WaitForSeconds(0.4f);

        AdvanceToNextBlock();

        yield return new WaitForSeconds(0.8f);

        // 4. Fade out the entrance fog, revealing the newly arrived maze block
        yield return FadeFog(0f, fogFadeDuration);

        isSwapping = false;
        _swapRoutine = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BLOCK ROTATION LOGIC
    // ═══════════════════════════════════════════════════════════════════════════

    public void AdvanceToNextBlock()
    {
        CaptureInitialSlots();

        if (blockSequence == null || blockSequence.Length == 0)
            blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

        int outgoingBlockNum = blockSequence[currentSequenceIndex];
        currentSequenceIndex = (currentSequenceIndex + 1) % blockSequence.Length;
        int incomingBlockNum = blockSequence[currentSequenceIndex];

        currentActiveBlockNumber = incomingBlockNum;

        // Return outgoing block to its home slot
        GameObject outgoingBlock = GetBlock(outgoingBlockNum);
        if (outgoingBlock != null)
        {
            int outIdx = outgoingBlockNum - 1;
            outgoingBlock.transform.position = _homePositions[outIdx];
            outgoingBlock.transform.rotation = _homeRotations[outIdx];
            CloseBlockDoorsInstant(outgoingBlock);
        }

        // Move incoming block into the active Glade entrance slot
        GameObject incomingBlock = GetBlock(incomingBlockNum);
        if (incomingBlock != null)
        {
            incomingBlock.transform.position = _activeSlotPosition;
            incomingBlock.transform.rotation = _activeSlotRotation;
            CloseBlockDoorsInstant(incomingBlock);
        }

        Debug.Log($"[MazeManager] Night swap complete: Block {outgoingBlockNum} moved to reserve. Block {incomingBlockNum} is now ACTIVE at the Glade entrance!");
    }

    private void ApplyInitialActiveBlock()
    {
        CaptureInitialSlots();

        if (blockSequence == null || blockSequence.Length == 0)
            blockSequence = new int[] { 8, 1, 2, 3, 4, 5, 6, 7 };

        currentSequenceIndex = 0;
        currentActiveBlockNumber = blockSequence[0]; // Day 1 = Block 8

        // Ensure Block 8 is at the active slot
        GameObject startBlock = GetBlock(currentActiveBlockNumber);
        if (startBlock != null)
        {
            startBlock.transform.position = _activeSlotPosition;
            startBlock.transform.rotation = _activeSlotRotation;
        }

        // Ensure other blocks are in their home slots
        for (int i = 0; i < 8; i++)
        {
            int blockNum = i + 1;
            if (blockNum == currentActiveBlockNumber) continue;

            GameObject b = GetBlock(blockNum);
            if (b != null)
            {
                b.transform.position = _homePositions[i];
                b.transform.rotation = _homeRotations[i];
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

        // Ensure all 8 blocks are found if not assigned
        AutoFindBlocks();

        // Slot for Block 8 is the active slot directly facing the Glade entrance
        GameObject block8 = GetBlock(8);
        if (block8 != null)
        {
            _activeSlotPosition = block8.transform.position;
            _activeSlotRotation = block8.transform.rotation;
        }

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
    //  DOOR CONTROL
    // ═══════════════════════════════════════════════════════════════════════════

    public void OpenActiveDoor()
    {
        DoubleSlidingDoor door = FindActiveEntranceDoor();
        if (door != null)
        {
            door.OpenDoor();
            Debug.Log($"[MazeManager] Opened entrance door on active Block {currentActiveBlockNumber}");
        }
    }

    public void CloseActiveDoor()
    {
        DoubleSlidingDoor door = FindActiveEntranceDoor();
        if (door != null)
        {
            door.CloseDoor();
            Debug.Log($"[MazeManager] Closed entrance door on active Block {currentActiveBlockNumber}");
        }
    }

    public void CloseAllDoorsInstant()
    {
        for (int i = 0; i < mazeBlocks.Length; i++)
        {
            if (mazeBlocks[i] != null)
                CloseBlockDoorsInstant(mazeBlocks[i]);
        }
    }

    private void CloseBlockDoorsInstant(GameObject block)
    {
        if (block == null) return;
        var doors = block.GetComponentsInChildren<DoubleSlidingDoor>(true);
        foreach (var d in doors)
        {
            if (d != null) d.SetStateInstant(false);
        }
    }

    /// <summary>
    /// Finds the entrance door on the currently active block that faces the Glade / mazeEntranceFog.
    /// </summary>
    public DoubleSlidingDoor FindActiveEntranceDoor()
    {
        GameObject active = GetActiveBlock();
        if (active == null) return null;

        var doors = active.GetComponentsInChildren<DoubleSlidingDoor>(true);
        if (doors == null || doors.Length == 0) return null;

        // If fog marker is assigned, choose the door closest to the entrance
        if (mazeEntranceFog != null)
        {
            DoubleSlidingDoor closest = null;
            float minDist = float.MaxValue;
            Vector3 target = mazeEntranceFog.position;

            foreach (var d in doors)
            {
                if (d == null) continue;
                float dist = Vector3.Distance(d.transform.position, target);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = d;
                }
            }
            if (closest != null) return closest;
        }

        return doors[0];
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROCEDURAL ENTRANCE FOG SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public void EnsureEntranceFogVisuals()
    {
        if (mazeEntranceFog == null)
        {
            AutoFindEntranceFog();
            if (mazeEntranceFog == null) return;
        }

        if (!autoGenerateFog) return;

        // Check if effect already exists
        Transform existing = mazeEntranceFog.Find("_GeneratedFogEffect");
        if (existing != null)
        {
            _fogEffectRoot = existing.gameObject;
            _fogParticleSystem = _fogEffectRoot.GetComponentInChildren<ParticleSystem>();
            _fogCurtainRenderer = _fogEffectRoot.GetComponentInChildren<MeshRenderer>();
        }
        else
        {
            CreateFogVisualObjects();
        }

        SetFogAlpha(0f);
    }

    private void CreateFogVisualObjects()
    {
        _fogEffectRoot = new GameObject("_GeneratedFogEffect");
        _fogEffectRoot.transform.SetParent(mazeEntranceFog, false);
        _fogEffectRoot.transform.localPosition = Vector3.zero;
        _fogEffectRoot.transform.localRotation = Quaternion.identity;

        // Determine doorway volume size from BoxCollider if present
        Vector3 fogSize = new Vector3(16f, 18f, 6f);
        BoxCollider box = mazeEntranceFog.GetComponent<BoxCollider>();
        if (box != null)
        {
            fogSize = box.size;
            _fogEffectRoot.transform.localPosition = box.center;
        }

        // 1. Opaque Billboard Fog Curtain
        GameObject curtain = GameObject.CreatePrimitive(PrimitiveType.Quad);
        curtain.name = "FogCurtain";
        curtain.transform.SetParent(_fogEffectRoot.transform, false);
        curtain.transform.localPosition = Vector3.zero;
        curtain.transform.localRotation = Quaternion.identity;
        curtain.transform.localScale = new Vector3(fogSize.x * 1.15f, fogSize.y * 1.15f, 1f);

        Collider col = curtain.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        _fogCurtainRenderer = curtain.GetComponent<MeshRenderer>();
        _fogCurtainRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _fogCurtainRenderer.receiveShadows = false;

        Material curtainMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        curtainMat.name = "M_DynamicFogCurtain";
        curtainMat.SetFloat("_Surface", 1f); // Transparent
        curtainMat.SetFloat("_Blend", 0f);   // Alpha
        curtainMat.SetInt("_ZWrite", 0);
        curtainMat.renderQueue = 3050;
        curtainMat.SetColor("_BaseColor", new Color(fogColor.r, fogColor.g, fogColor.b, 0f));
        _fogCurtainRenderer.material = curtainMat;
        _fogCurtainMaterial = curtainMat;

        // 2. Billowing Mist Particle System
        GameObject psGo = new GameObject("MistParticles");
        psGo.transform.SetParent(_fogEffectRoot.transform, false);
        psGo.transform.localPosition = Vector3.zero;

        _fogParticleSystem = psGo.AddComponent<ParticleSystem>();
        var main = _fogParticleSystem.main;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.75f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = true;
        main.playOnAwake = false;

        var emission = _fogParticleSystem.emission;
        emission.rateOverTime = 30f;
        emission.enabled = false;

        var shape = _fogParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = fogSize;

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = Resources.Load<Material>("VFX/M_ParticleAlpha") 
            ?? Resources.Load<Material>("M_ParticleAlpha");

        var colOverLife = _fogParticleSystem.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(fogColor, 0f), new GradientColorKey(fogColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLife.color = grad;
    }

    public IEnumerator FadeFog(float targetAlpha, float duration)
    {
        EnsureEntranceFogVisuals();

        float startAlpha = _currentFogAlpha;
        float elapsed = 0f;

        if (targetAlpha > 0.05f && _fogParticleSystem != null)
        {
            var em = _fogParticleSystem.emission;
            em.enabled = true;
            _fogParticleSystem.Play();
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

        if (targetAlpha <= 0.05f && _fogParticleSystem != null)
        {
            var em = _fogParticleSystem.emission;
            em.enabled = false;
        }
    }

    public void SetFogAlpha(float alpha)
    {
        _currentFogAlpha = Mathf.Clamp01(alpha);

        if (_fogCurtainMaterial == null && _fogCurtainRenderer != null)
            _fogCurtainMaterial = _fogCurtainRenderer.material;

        if (_fogCurtainMaterial != null)
        {
            Color c = fogColor;
            c.a = _currentFogAlpha;
            if (_fogCurtainMaterial.HasProperty("_BaseColor"))
                _fogCurtainMaterial.SetColor("_BaseColor", c);
            if (_fogCurtainMaterial.HasProperty("_Color"))
                _fogCurtainMaterial.SetColor("_Color", c);
        }

        if (_fogParticleSystem != null)
        {
            var main = _fogParticleSystem.main;
            Color c = fogColor;
            c.a = _currentFogAlpha * 0.8f;
            main.startColor = c;
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
        EnsureEntranceFogVisuals();
        _slotsCaptured = false;
        CaptureInitialSlots();
        ApplyInitialActiveBlock();

        Debug.Log("[MazeManager] One-Click Setup finished! 8 Blocks, DayNight Controller, and Entrance Fog successfully linked.");
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

        // Search for user's object
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
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EDITOR CONTEXT MENU TESTING TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Simulate Day (Open Door)")]
    public void TestOpenDoor() => OpenActiveDoor();

    [ContextMenu("Simulate Night (Close Door & Swap)")]
    public void TestNightSwap() => StartNightSwap();

    [ContextMenu("Toggle Fog (On / Off)")]
    public void TestToggleFog()
    {
        float target = _currentFogAlpha > 0.5f ? 0f : 1f;
        StartCoroutine(FadeFog(target, 1.2f));
    }

    [ContextMenu("Jump To Next Block Immediately")]
    public void TestJumpNext() => AdvanceToNextBlock();
}
