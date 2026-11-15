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
/// 4. 100% Opaque 3D Fog Wall:
///    When night arrives, a thick, impenetrable 3D opaque fog wall covers the entrance opening
///    so that player standing in the Glade cannot see through to the outside or see blocks moving.
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

        // 2. Fade in dense 100% opaque 3D entrance fog wall
        PlayAudio(fogSound);
        yield return FadeFog(1f, fogFadeDuration);

        // Short pause while opaque fog completely seals the entrance
        yield return new WaitForSeconds(0.3f);

        // 3. Play rumble audio and perform the mutual block swap behind the opaque fog wall
        PlayAudio(swapRumbleSound);
        AdvanceToNextBlock();

        // Keep the thick fog wall up for 1.0s after swap so movement is 100% hidden
        yield return new WaitForSeconds(1.0f);

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
            // Purana block naye block ki position me chala jaye
            outgoingBlock.transform.SetPositionAndRotation(incomingPos, incomingRot);

            // Naya block glade entrance ki position me aa jaye
            incomingBlock.transform.SetPositionAndRotation(outgoingPos, outgoingRot);

            currentActiveBlockNumber = incomingBlockNum;

            // Ensure ONLY the Glade-facing door on incoming block is locked closed for the night
            DoubleSlidingDoor incomingGladeDoor = FindActiveEntranceDoor();
            if (incomingGladeDoor != null)
            {
                incomingGladeDoor.CloseByManager();
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
    //  DOOR CONTROL (ONLY GLADE-FACING DOOR OPENS!)
    // ═══════════════════════════════════════════════════════════════════════════

    public void OpenActiveDoor()
    {
        GameObject active = GetActiveBlock();
        if (active == null) return;

        var allDoors = active.GetComponentsInChildren<DoubleSlidingDoor>(true);
        DoubleSlidingDoor gladeFacingDoor = FindActiveEntranceDoor();

        // 1. Ensure all interior/non-glade doors on this block remain CLOSED
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
    /// Finds the specific door on the active block that faces the Glade side.
    /// The Glade is located in the +X direction (world X >= 4300).
    /// Therefore, the door with the MAXIMUM world X (transform.position.x) is strictly the Glade-facing door!
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
    //  100% OPAQUE 3D PROCEDURAL FOG WALL SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public void EnsureEntranceFogVisuals(bool forceRecreate = false)
    {
        if (mazeEntranceFog == null)
        {
            AutoFindEntranceFog();
            if (mazeEntranceFog == null) return;
        }

        if (!autoGenerateFog) return;

        // Clean up stale or broken effect root
        Transform existing = mazeEntranceFog.Find("_GeneratedFogEffect");
        if (existing != null && (forceRecreate || _fogCurtainRenderers == null || _fogCurtainRenderers.Length == 0 || _fogCurtainMaterial == null))
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
            existing = null;
        }

        if (existing == null)
        {
            CreateFogVisualObjects();
        }
        else
        {
            _fogEffectRoot = existing.gameObject;
            _fogParticleSystem = _fogEffectRoot.GetComponentInChildren<ParticleSystem>();
            _fogCurtainRenderers = _fogEffectRoot.GetComponentsInChildren<MeshRenderer>();
            if (_fogCurtainRenderers != null && _fogCurtainRenderers.Length > 0)
                _fogCurtainMaterial = _fogCurtainRenderers[0].material;
        }

        SetFogAlpha(_currentFogAlpha);
    }

    private void CreateFogVisualObjects()
    {
        _fogEffectRoot = new GameObject("_GeneratedFogEffect");
        _fogEffectRoot.transform.SetParent(mazeEntranceFog, false);
        _fogEffectRoot.transform.localPosition = Vector3.zero;
        _fogEffectRoot.transform.localRotation = Quaternion.identity;

        BoxCollider box = mazeEntranceFog.GetComponent<BoxCollider>();
        Vector3 fogCenter = Vector3.zero;
        Vector3 fogSize = new Vector3(30f, 65f, 160f);

        if (box != null)
        {
            fogCenter = box.center;
            // Guarantee full archway coverage: at least 30m thick, 65m high, 160m wide
            fogSize = new Vector3(
                Mathf.Max(30f, box.size.x),
                Mathf.Max(65f, box.size.y),
                Mathf.Max(160f, box.size.z)
            );
        }

        _fogEffectRoot.transform.localPosition = fogCenter;

        // Create URP Unlit Material with Cull Off (double-sided)
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Sprites/Default");

        Material fogMat = new Material(unlitShader);
        fogMat.name = "M_OpaqueFogWall_Mat";
        fogMat.SetFloat("_Surface", 1f); // Transparent
        fogMat.SetFloat("_Blend", 0f);   // Alpha blending
        fogMat.SetFloat("_Cull", 0f);    // Two-sided (Render both front and back faces)
        fogMat.SetInt("_ZWrite", 0);
        fogMat.renderQueue = 3100;
        fogMat.SetColor("_BaseColor", new Color(fogColor.r, fogColor.g, fogColor.b, 0f));
        _fogCurtainMaterial = fogMat;

        List<MeshRenderer> renderers = new List<MeshRenderer>();

        // 1. 3D Fog Barrier Volume (Cube Primitive - renders from ALL 6 sides!)
        GameObject fogVolume = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fogVolume.name = "FogVolume_3D";
        fogVolume.transform.SetParent(_fogEffectRoot.transform, false);
        fogVolume.transform.localPosition = Vector3.zero;
        fogVolume.transform.localRotation = Quaternion.identity;
        fogVolume.transform.localScale = fogSize;

        Collider cubeCol = fogVolume.GetComponent<Collider>();
        if (cubeCol != null)
        {
            if (Application.isPlaying) Destroy(cubeCol);
            else DestroyImmediate(cubeCol);
        }

        MeshRenderer cubeMr = fogVolume.GetComponent<MeshRenderer>();
        cubeMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cubeMr.receiveShadows = false;
        cubeMr.material = fogMat;
        renderers.Add(cubeMr);

        // 2. High-Density Front & Back Dual Passage Quads
        GameObject quadFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadFront.name = "FogCurtain_FacingGlade";
        quadFront.transform.SetParent(_fogEffectRoot.transform, false);
        quadFront.transform.localPosition = new Vector3(fogSize.x * 0.45f, 0f, 0f);
        quadFront.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        quadFront.transform.localScale = new Vector3(fogSize.z * 1.15f, fogSize.y * 1.15f, 1f);

        Collider qcF = quadFront.GetComponent<Collider>();
        if (qcF != null)
        {
            if (Application.isPlaying) Destroy(qcF);
            else DestroyImmediate(qcF);
        }

        MeshRenderer mrF = quadFront.GetComponent<MeshRenderer>();
        mrF.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrF.receiveShadows = false;
        mrF.material = fogMat;
        renderers.Add(mrF);

        GameObject quadBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadBack.name = "FogCurtain_FacingMaze";
        quadBack.transform.SetParent(_fogEffectRoot.transform, false);
        quadBack.transform.localPosition = new Vector3(-fogSize.x * 0.45f, 0f, 0f);
        quadBack.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        quadBack.transform.localScale = new Vector3(fogSize.z * 1.15f, fogSize.y * 1.15f, 1f);

        Collider qcB = quadBack.GetComponent<Collider>();
        if (qcB != null)
        {
            if (Application.isPlaying) Destroy(qcB);
            else DestroyImmediate(qcB);
        }

        MeshRenderer mrB = quadBack.GetComponent<MeshRenderer>();
        mrB.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrB.receiveShadows = false;
        mrB.material = fogMat;
        renderers.Add(mrB);

        _fogCurtainRenderers = renderers.ToArray();

        // 3. Dense Billowing Mist Particle System
        GameObject psGo = new GameObject("MistParticles");
        psGo.transform.SetParent(_fogEffectRoot.transform, false);
        psGo.transform.localPosition = Vector3.zero;

        _fogParticleSystem = psGo.AddComponent<ParticleSystem>();
        var main = _fogParticleSystem.main;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(16f, 32f);
        main.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.95f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = true;
        main.playOnAwake = false;

        var emission = _fogParticleSystem.emission;
        emission.rateOverTime = 50f;
        emission.enabled = false;

        var shape = _fogParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = fogSize;

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = Resources.Load<Material>("VFX/M_ParticleAlpha") 
            ?? Resources.Load<Material>("M_ParticleAlpha") 
            ?? fogMat;

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

        Color c = fogColor;
        c.a = _currentFogAlpha;

        // Apply alpha to all fog wall mesh renderers directly
        if (_fogCurtainRenderers != null)
        {
            for (int i = 0; i < _fogCurtainRenderers.Length; i++)
            {
                if (_fogCurtainRenderers[i] != null && _fogCurtainRenderers[i].material != null)
                {
                    Material m = _fogCurtainRenderers[i].material;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                }
            }
        }

        if (_fogParticleSystem != null)
        {
            var main = _fogParticleSystem.main;
            Color pc = fogColor;
            pc.a = _currentFogAlpha * 0.95f;
            main.startColor = pc;
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
        box.size = new Vector3(30f, 65f, 160f);
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
        EnsureEntranceFogVisuals();
        float target = _currentFogAlpha > 0.5f ? 0f : 1f;
        if (!Application.isPlaying)
        {
            SetFogAlpha(target);
        }
        else
        {
            StartCoroutine(FadeFog(target, 1.2f));
        }
    }

    [ContextMenu("Jump To Next Block Immediately")]
    public void TestJumpNext() => AdvanceToNextBlock();
}
