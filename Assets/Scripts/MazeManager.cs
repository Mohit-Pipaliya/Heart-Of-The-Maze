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
/// 3. Nightly Rotation (Mutual Position Swap):
///    Block 8 <-> Block 1 -> Block 1 <-> Block 2 -> ... -> Block 7 <-> Block 8.
///    The outgoing block takes the incoming block's position, and the incoming block takes the Glade entrance slot!
/// 4. 100% Opaque Fog Wall:
///    When night arrives, an impenetrable, thick opaque fog wall covers the entrance opening
///    so that player standing in the Glade cannot see through to the outside or see blocks moving.
/// 5. Day/Night Doors:
///    Day starts -> Active block entrance door opens (and stays open all day).
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
    [Tooltip("Scene me 'maze enterence fog' empty GameObject ya BoxCollider yahan drag karein. (Agar empty chhodenge toh script automatically dhund legi ya create kar degi).")]
    public Transform mazeEntranceFog;

    [Tooltip("Fog visual effect script khud generate karegi")]
    public bool autoGenerateFog = true;

    [Tooltip("Fog wall fade hone ki speed (seconds)")]
    [Range(0.5f, 4f)] public float fogFadeDuration = 1.5f;

    [Tooltip("Fog wall ka color (mystical misty white/smoke)")]
    public Color fogColor = new Color(0.88f, 0.90f, 0.94f, 1.0f);

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
    private Vector3 _activeSlotPosition;
    private Quaternion _activeSlotRotation;
    private Vector3[] _homePositions = new Vector3[8];
    private Quaternion[] _homeRotations = new Quaternion[8];
    private bool _slotsCaptured = false;

    // ─── Fog Internal References ───
    private GameObject _fogEffectRoot;
    private ParticleSystem _fogParticleSystem;
    private MeshRenderer[] _fogCurtainRenderers;
    private Material _fogCurtainMaterial;
    private float _currentFogAlpha = 0f;
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
        yield return new WaitForSeconds(1.0f);

        // 2. Fade in dense 100% opaque entrance fog wall
        PlayAudio(fogSound);
        yield return FadeFog(1f, fogFadeDuration);

        // At 1.0 alpha, the fog wall is 100% opaque. The entire doorway is concealed.
        // 3. Play rumble audio and perform the mutual block swap behind the opaque fog wall
        PlayAudio(swapRumbleSound);
        yield return new WaitForSeconds(0.4f);

        AdvanceToNextBlock();

        yield return new WaitForSeconds(0.8f);

        // 4. Fade out the entrance fog wall, revealing the newly arrived maze block
        yield return FadeFog(0f, fogFadeDuration);

        isSwapping = false;
        _swapRoutine = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BLOCK ROTATION LOGIC (MUTUAL POSITION SWAP)
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
            // Close and reset all doors on outgoing block
            ReleaseAndCloseBlockDoors(outgoingBlock);

            // Close and reset all doors on incoming block
            ReleaseAndCloseBlockDoors(incomingBlock);

            // Read the two blocks' current positions and rotations
            Vector3 outgoingPos = outgoingBlock.transform.position;
            Quaternion outgoingRot = outgoingBlock.transform.rotation;

            Vector3 incomingPos = incomingBlock.transform.position;
            Quaternion incomingRot = incomingBlock.transform.rotation;

            // Direct mutual swap:
            // Purana vala naye vale ki position me chala jaye
            outgoingBlock.transform.SetPositionAndRotation(incomingPos, incomingRot);

            // Naye vala glade entrance ki position me aa jaye
            incomingBlock.transform.SetPositionAndRotation(outgoingPos, outgoingRot);

            currentActiveBlockNumber = incomingBlockNum;

            // Lock incoming block entrance door closed for the night
            DoubleSlidingDoor incomingDoor = FindActiveEntranceDoor();
            if (incomingDoor != null)
            {
                incomingDoor.CloseByManager();
            }

            Debug.Log($"[MazeManager] MUTUAL SWAP SUCCESS: Block {outgoingBlockNum} moved to reserve ({incomingPos.x:F0}, {incomingPos.z:F0}). Block {incomingBlockNum} is now ACTIVE at Glade entrance ({outgoingPos.x:F0}, {outgoingPos.z:F0})!");
        }
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
            door.OpenByManager();
            Debug.Log($"[MazeManager] Opened entrance door '{door.gameObject.name}' on active Block {currentActiveBlockNumber}");
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
    /// Finds the entrance door on the currently active block that faces the Glade / mazeEntranceFog.
    /// </summary>
    public DoubleSlidingDoor FindActiveEntranceDoor()
    {
        GameObject active = GetActiveBlock();
        if (active == null) return null;

        var doors = active.GetComponentsInChildren<DoubleSlidingDoor>(true);
        if (doors == null || doors.Length == 0) return null;

        // Position where the Glade entrance gate is located
        Vector3 entranceTarget = (mazeEntranceFog != null)
            ? mazeEntranceFog.position
            : new Vector3(4335.4f, 0f, 1552.6f);

        DoubleSlidingDoor closest = null;
        float minDist = float.MaxValue;

        foreach (var d in doors)
        {
            if (d == null) continue;
            float dist = Vector3.Distance(d.transform.position, entranceTarget);
            if (dist < minDist)
            {
                minDist = dist;
                closest = d;
            }
        }

        return closest ?? doors[0];
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  100% OPAQUE PROCEDURAL FOG WALL SYSTEM
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
            _fogCurtainRenderers = _fogEffectRoot.GetComponentsInChildren<MeshRenderer>();
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

        // Determine doorway volume size from BoxCollider
        Vector3 fogSize = new Vector3(25f, 45f, 75f);
        BoxCollider box = mazeEntranceFog.GetComponent<BoxCollider>();
        if (box != null)
        {
            fogSize = box.size;
            _fogEffectRoot.transform.localPosition = box.center;
        }

        // Material with Universal Render Pipeline / Unlit (or fallback)
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Transparent");

        Material curtainMat = new Material(unlitShader);
        curtainMat.name = "M_OpaqueFogWall";
        curtainMat.SetFloat("_Surface", 1f); // Transparent
        curtainMat.SetFloat("_Blend", 0f);   // Alpha blend
        curtainMat.SetFloat("_Cull", 0f);    // Double-sided (Cull Off)!
        curtainMat.SetInt("_ZWrite", 0);
        curtainMat.renderQueue = 3050;
        curtainMat.SetColor("_BaseColor", new Color(fogColor.r, fogColor.g, fogColor.b, 0f));
        _fogCurtainMaterial = curtainMat;

        List<MeshRenderer> renderers = new List<MeshRenderer>();

        // Create double-sided dual barrier across doorway opening (facing along X axis, covering Z-Y plane)
        float curtainWidth = Mathf.Max(fogSize.z, fogSize.x) * 1.25f;
        float curtainHeight = fogSize.y * 1.25f;

        // Front Curtain
        GameObject curtainFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
        curtainFront.name = "FogCurtain_Front";
        curtainFront.transform.SetParent(_fogEffectRoot.transform, false);
        curtainFront.transform.localPosition = new Vector3(2.0f, 0f, 0f);
        curtainFront.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        curtainFront.transform.localScale = new Vector3(curtainWidth, curtainHeight, 1f);

        Collider colF = curtainFront.GetComponent<Collider>();
        if (colF != null) DestroyImmediate(colF);

        MeshRenderer mrF = curtainFront.GetComponent<MeshRenderer>();
        mrF.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrF.receiveShadows = false;
        mrF.material = curtainMat;
        renderers.Add(mrF);

        // Back Curtain
        GameObject curtainBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
        curtainBack.name = "FogCurtain_Back";
        curtainBack.transform.SetParent(_fogEffectRoot.transform, false);
        curtainBack.transform.localPosition = new Vector3(-2.0f, 0f, 0f);
        curtainBack.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        curtainBack.transform.localScale = new Vector3(curtainWidth, curtainHeight, 1f);

        Collider colB = curtainBack.GetComponent<Collider>();
        if (colB != null) DestroyImmediate(colB);

        MeshRenderer mrB = curtainBack.GetComponent<MeshRenderer>();
        mrB.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrB.receiveShadows = false;
        mrB.material = curtainMat;
        renderers.Add(mrB);

        _fogCurtainRenderers = renderers.ToArray();

        // 3. Dense Billowing Fog Particle System
        GameObject psGo = new GameObject("FogParticles");
        psGo.transform.SetParent(_fogEffectRoot.transform, false);
        psGo.transform.localPosition = Vector3.zero;

        _fogParticleSystem = psGo.AddComponent<ParticleSystem>();
        var main = _fogParticleSystem.main;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
        main.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.95f);
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = true;
        main.playOnAwake = false;

        var emission = _fogParticleSystem.emission;
        emission.rateOverTime = 40f;
        emission.enabled = false;

        var shape = _fogParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(fogSize.x, fogSize.y, fogSize.z);

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = Resources.Load<Material>("VFX/M_ParticleAlpha") 
            ?? Resources.Load<Material>("M_ParticleAlpha")
            ?? curtainMat;

        var colOverLife = _fogParticleSystem.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(fogColor, 0f), new GradientColorKey(fogColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.35f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLife.color = grad;
    }

    public IEnumerator FadeFog(float targetAlpha, float duration)
    {
        EnsureEntranceFogVisuals();

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
            c.a = _currentFogAlpha * 0.95f;
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
        createdFogGo.transform.position = new Vector3(4335.4f, 20f, 1552.6f);
        BoxCollider box = createdFogGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(25f, 45f, 75f);
        mazeEntranceFog = createdFogGo.transform;
        Debug.Log($"[MazeManager] Created default 'maze enterence fog' BoxCollider at {createdFogGo.transform.position}");
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
