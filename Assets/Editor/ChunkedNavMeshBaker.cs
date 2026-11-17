#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Debug = UnityEngine.Debug;

/// <summary>
/// ChunkedNavMeshBaker — One-Click & Chunk-based NavMesh Baking for Large Maze Scenes.
/// 
/// ─── KEY FEATURES ───────────────────────────────────────────────────────────────
/// 1. ⚡ ONE-CLICK FULL AUTO BAKE:
///    - Auto-finds Maze and Maze terrain geometry.
///    - Disables old full-scene NavMeshSurfaces that freeze the editor.
///    - Auto-marks torches, props, and decorative vegetation as Ignored.
///    - Auto-calculates optimal grid columns/rows from maze dimensions.
///    - Creates volume chunks with seamless boundary overlap.
///    - Asynchronously bakes chunk-by-chunk with progress feedback and zero editor freezing!
/// 
/// 2. ADVANCED MANUAL CONTROLS:
///    - Customizable Grid Columns/Rows, Chunk Overlap, Voxel Size, and Ignore LayerMask.
/// </summary>
public class ChunkedNavMeshBaker : EditorWindow
{
    // ─────────────────────────────────────────────────────────────────────────────
    // FIELDS & SETTINGS
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Maze Geometry Settings")]
    [Tooltip("The root transform containing the maze geometry (walls, floors, obstacles).")]
    public Transform mazeRoot;

    [Tooltip("Number of chunk columns along the X axis.")]
    [Range(1, 16)]
    public int gridColumns = 2;

    [Tooltip("Number of chunk rows along the Z axis.")]
    [Range(1, 16)]
    public int gridRows = 2;

    [Tooltip("Overlap distance (in meters) added to each chunk border to prevent pathfinding seams.")]
    [Range(0.5f, 10f)]
    public float chunkOverlap = 2.5f;

    [Tooltip("Override voxel size for high-resolution baking without overflowing memory (0.1 to 0.25 recommended).")]
    [Range(0.05f, 0.5f)]
    public float voxelSizeOverride = 0.15f;

    [Header("Filtering & Decorations")]
    [Tooltip("LayerMask for props, decorations, torches, trees, and grass that should be ignored during bake.")]
    public LayerMask ignoreLayers = 0;

    // ─────────────────────────────────────────────────────────────────────────────
    // INTERNAL STATE FOR ASYNC BAKING
    // ─────────────────────────────────────────────────────────────────────────────
    private const string ChunksContainerName = "NavMesh_Chunks";
    private const string NavMeshSaveDirectory = "Assets/NavMeshData";

    private Queue<NavMeshSurface> _bakeQueue = new Queue<NavMeshSurface>();
    private int _totalChunksToBake = 0;
    private int _bakedCount = 0;
    private bool _isBaking = false;
    private Stopwatch _totalStopwatch;

    private Vector2 _scrollPos;

    // ─────────────────────────────────────────────────────────────────────────────
    // MENU ITEMS
    // ─────────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Maze/⚡ One-Click Bake Maze NavMesh", false, 0)]
    public static void OneClickMenuAction()
    {
        ChunkedNavMeshBaker window = GetWindow<ChunkedNavMeshBaker>("Chunked NavMesh Baker");
        window.minSize = new Vector2(420, 560);
        window.Show();
        window.OneClickAutoBake();
    }

    [MenuItem("Tools/Maze/Chunked NavMesh Baker Window", false, 100)]
    public static void ShowWindow()
    {
        ChunkedNavMeshBaker window = GetWindow<ChunkedNavMeshBaker>("Chunked NavMesh Baker");
        window.minSize = new Vector2(420, 560);
        window.Show();
    }

    private void OnEnable()
    {
        if (mazeRoot == null)
        {
            AutoDetectMazeRoot();
        }
    }

    private void OnDisable()
    {
        if (_isBaking)
        {
            CancelBake("Window closed");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ⚡ 100% ONE-CLICK FULL AUTOMATION
    // ─────────────────────────────────────────────────────────────────────────────
    public void OneClickAutoBake()
    {
        Debug.Log("<color=cyan>══════════════════════════════════════════════════════════════</color>");
        Debug.Log("<color=cyan><b>[ChunkedNavMeshBaker] ⚡ STARTING ONE-CLICK AUTO BAKE...</b></color>");

        // Step 1: Auto-Detect Maze Root
        if (mazeRoot == null)
        {
            AutoDetectMazeRoot();
        }

        if (mazeRoot == null)
        {
            Debug.LogError("[ChunkedNavMeshBaker] Could not find 'Maze' in the scene! Please ensure the Maze GameObject exists.");
            EditorUtility.DisplayDialog("Maze Not Found", "Could not automatically locate 'Maze' in the active scene. Please open the scene and try again.", "OK");
            return;
        }

        // Step 2: Disable any old full-scene NavMeshSurface that causes editor freezes
        DisableOldFreezingSurfaces();

        // Step 3: Auto-Mark Torches, Props, and Foliage to Ignore
        AutoMarkCommonDecorations();

        // Step 4: Calculate Optimal Grid Size from Maze Bounds
        Bounds totalBounds;
        if (!CalculateMazeBounds(mazeRoot, out totalBounds))
        {
            Debug.LogError($"[ChunkedNavMeshBaker] Failed to calculate geometry bounds for '{mazeRoot.name}'!");
            EditorUtility.DisplayDialog("No Geometry", $"Could not find any Colliders or Renderers under '{mazeRoot.name}'.", "OK");
            return;
        }

        float maxHorizSpan = Mathf.Max(totalBounds.size.x, totalBounds.size.z);
        if (maxHorizSpan > 1800f)
        {
            gridColumns = 4;
            gridRows = 4;
        }
        else if (maxHorizSpan > 900f)
        {
            gridColumns = 3;
            gridRows = 3;
        }
        else
        {
            gridColumns = 2;
            gridRows = 2;
        }

        chunkOverlap = 2.5f;
        voxelSizeOverride = 0.15f;

        Debug.Log($"<color=cyan>[ChunkedNavMeshBaker]</color> Auto-configured optimal grid: {gridColumns}x{gridRows} chunks (Maze Span: {maxHorizSpan:F0}m, Overlap: {chunkOverlap}m, Voxel: {voxelSizeOverride}).");

        // Step 5: Create Chunks
        CreateChunksInternal(false);

        // Step 6: Bake All Chunks Asynchronously
        BakeAllChunks();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GUI RENDERING
    // ─────────────────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(6);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.2f, 0.85f, 1f);
        EditorGUILayout.LabelField("CHUNKED NAVMESH BAKER", titleStyle);

        EditorGUILayout.HelpBox(
            "Large maze scenes freeze Unity when baked in one go.\n" +
            "This tool bakes the maze in seamless volume chunks safely and without freezing.",
            MessageType.Info
        );
        EditorGUILayout.Space(6);

        // ═════════════════════════════════════════════════════════════════════════
        // ⚡ ONE-CLICK BIG ACTION BUTTON
        // ═════════════════════════════════════════════════════════════════════════
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚡ One-Click Complete Automation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Ek single click mein: Maze dhoondhega, purane freezing surfaces band karega, decorations ignore karega, chunks banayega aur bake complete karega!",
            MessageType.None
        );

        EditorGUI.BeginDisabledGroup(_isBaking);
        GUI.backgroundColor = new Color(0.15f, 0.9f, 0.4f);
        if (GUILayout.Button("⚡ ONE-CLICK AUTO BAKE (SAB KUCH AUTOMATIC)", GUILayout.Height(42)))
        {
            OneClickAutoBake();
        }
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // ═════════════════════════════════════════════════════════════════════════
        // MANUAL / ADVANCED CONTROLS
        // ═════════════════════════════════════════════════════════════════════════
        EditorGUILayout.LabelField("─── Advanced / Manual Settings ───", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(4);

        // 1. Scene Reference Settings
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("1. Maze Root Reference", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        mazeRoot = (Transform)EditorGUILayout.ObjectField("Maze Root", mazeRoot, typeof(Transform), true);
        if (GUILayout.Button("Auto-Find", GUILayout.Width(75)))
        {
            AutoDetectMazeRoot();
        }
        EditorGUILayout.EndHorizontal();

        if (mazeRoot == null)
        {
            EditorGUILayout.HelpBox("Assign the parent GameObject of your maze (e.g. 'Maze').", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"Active Children: {mazeRoot.childCount}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 2. Grid & Chunk Settings
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("2. Chunk Grid & Resolution", EditorStyles.boldLabel);

        gridColumns = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Grid Columns (X)", "Number of chunks along the X axis."), gridColumns));
        gridRows    = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Grid Rows (Z)", "Number of chunks along the Z axis."), gridRows));
        chunkOverlap = EditorGUILayout.FloatField(new GUIContent("Chunk Overlap (m)", "Padding overlap on chunk boundaries to prevent seams (default 2.5)."), chunkOverlap);
        voxelSizeOverride = EditorGUILayout.FloatField(new GUIContent("Voxel Size Override", "Voxel resolution override (default 0.15 for high quality)."), voxelSizeOverride);

        int totalChunks = gridColumns * gridRows;
        EditorGUILayout.LabelField($"Total Chunks: {totalChunks} ({gridColumns} x {gridRows})", EditorStyles.boldLabel);

        if (mazeRoot != null)
        {
            Bounds b;
            if (CalculateMazeBounds(mazeRoot, out b))
            {
                float chunkW = (b.size.x / gridColumns) + chunkOverlap;
                float chunkL = (b.size.z / gridRows) + chunkOverlap;
                EditorGUILayout.HelpBox(
                    $"Maze Total Bounds: {b.size.x:F0}m (X) x {b.size.z:F0}m (Z) x {b.size.y:F0}m (Height)\n" +
                    $"Approx Chunk Size: {chunkW:F0}m x {chunkL:F0}m (Overlap: {chunkOverlap:F1}m)",
                    MessageType.None
                );
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 3. Ignore Layers & Decorations
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("3. Ignore Decorations & Props", EditorStyles.boldLabel);

        ignoreLayers = LayerMaskField("Ignore Layers", ignoreLayers);

        EditorGUI.BeginDisabledGroup(mazeRoot == null || _isBaking);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Auto-Mark Props & Torches", "Scans names for torches, props, foliage and marks them Ignored."), GUILayout.Height(26)))
        {
            AutoMarkCommonDecorations();
        }

        EditorGUI.BeginDisabledGroup(ignoreLayers.value == 0);
        if (GUILayout.Button(new GUIContent("Mark Selected Layers", "Adds NavMeshModifier with 'Ignore From Build' to objects in selected layers."), GUILayout.Height(26)))
        {
            MarkDecorationsAsIgnored();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 4. Action Buttons
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("4. Chunk Management & Manual Bake", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(_isBaking);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Create Chunks", "Splits the maze bounds into NavMeshSurface volume chunks."), GUILayout.Height(30)))
        {
            CreateChunks();
        }

        if (GUILayout.Button(new GUIContent("Clear Chunks", "Deletes existing chunk GameObjects and removes their NavMesh data."), GUILayout.Height(30)))
        {
            ClearChunks();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(4);

        if (!_isBaking)
        {
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
            if (GUILayout.Button(new GUIContent("Bake All Chunks", "Asynchronously bakes all chunks one by one with progress feedback."), GUILayout.Height(34)))
            {
                BakeAllChunks();
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = new Color(1f, 0.35f, 0.3f);
            if (GUILayout.Button("Cancel Baking", GUILayout.Height(34)))
            {
                CancelBake("Cancelled by user via button");
            }
            GUI.backgroundColor = Color.white;

            float progress = _totalChunksToBake > 0 ? (float)_bakedCount / _totalChunksToBake : 0f;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Baking in progress: {_bakedCount} / {_totalChunksToBake} chunks completed...", EditorStyles.boldLabel);
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progress, $"{progress * 100f:F0}%");
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. CREATE CHUNKS
    // ─────────────────────────────────────────────────────────────────────────────
    public void CreateChunks()
    {
        CreateChunksInternal(true);
    }

    private void CreateChunksInternal(bool showDialog)
    {
        if (mazeRoot == null)
        {
            Debug.LogWarning("[ChunkedNavMeshBaker] Cannot create chunks: Maze Root is not assigned!");
            if (showDialog) EditorUtility.DisplayDialog("Missing Maze Root", "Please assign the 'Maze Root' Transform before creating chunks.", "OK");
            return;
        }

        Bounds totalBounds;
        if (!CalculateMazeBounds(mazeRoot, out totalBounds))
        {
            Debug.LogWarning($"[ChunkedNavMeshBaker] Could not find any Colliders or Renderers under '{mazeRoot.name}' to compute bounds.");
            if (showDialog) EditorUtility.DisplayDialog("No Geometry Found", $"Could not find any Colliders or Renderers under '{mazeRoot.name}'. Ensure your maze has geometry with active colliders or renderers.", "OK");
            return;
        }

        // Remove old chunks to prevent duplicates
        ClearChunksInternal(false);

        // Create container GameObject under Maze Root
        GameObject container = new GameObject(ChunksContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Create NavMesh Chunks Container");
        container.transform.SetParent(mazeRoot, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        int cols = Mathf.Max(1, gridColumns);
        int rows = Mathf.Max(1, gridRows);

        float minX = totalBounds.min.x;
        float minZ = totalBounds.min.z;
        float stepX = totalBounds.size.x / cols;
        float stepZ = totalBounds.size.z / rows;

        // Ensure vertical bounds are generous enough to capture any multi-level hills/bridges/platforms
        float verticalPadding = Mathf.Max(totalBounds.size.y * 0.25f, 25f);
        float chunkHeight = totalBounds.size.y + verticalPadding * 2f;
        float centerY = totalBounds.center.y;

        int humanoidAgentTypeId = GetHumanoidAgentTypeId();

        int createdCount = 0;
        for (int col = 0; col < cols; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                float cMinX = minX + col * stepX;
                float cMaxX = cMinX + stepX;

                float cMinZ = minZ + row * stepZ;
                float cMaxZ = cMinZ + stepZ;

                // Add overlap padding on boundaries to prevent seams
                float halfOverlap = chunkOverlap * 0.5f;
                cMinX -= halfOverlap;
                cMaxX += halfOverlap;
                cMinZ -= halfOverlap;
                cMaxZ += halfOverlap;

                Vector3 chunkCenter = new Vector3((cMinX + cMaxX) * 0.5f, centerY, (cMinZ + cMaxZ) * 0.5f);
                Vector3 chunkSize = new Vector3(cMaxX - cMinX, chunkHeight, cMaxZ - cMinZ);

                string chunkName = $"NavChunk_{col}_{row}";
                GameObject chunkGO = new GameObject(chunkName);
                Undo.RegisterCreatedObjectUndo(chunkGO, "Create NavMesh Chunk");
                chunkGO.transform.SetParent(container.transform, true);
                chunkGO.transform.position = chunkCenter;
                chunkGO.transform.rotation = Quaternion.identity;

                // Configure NavMeshSurface
                NavMeshSurface surface = Undo.AddComponent<NavMeshSurface>(chunkGO);
                surface.collectObjects = CollectObjects.Volume;
                surface.center = Vector3.zero; // Local center (transform is already placed at chunkCenter)
                surface.size = chunkSize;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize = true;
                surface.voxelSize = Mathf.Max(0.05f, voxelSizeOverride);
                surface.agentTypeID = humanoidAgentTypeId;

                if (ignoreLayers.value != 0)
                {
                    surface.layerMask = ~ignoreLayers.value;
                }

                EditorUtility.SetDirty(surface);
                createdCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(mazeRoot.gameObject.scene);
        Debug.Log($"<color=green>[ChunkedNavMeshBaker]</color> Successfully created {createdCount} NavMesh chunk surfaces under '{mazeRoot.name}/{ChunksContainerName}'. Total Bounds: {totalBounds.size}.");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Chunks Created", $"Created {createdCount} NavMesh volume chunks successfully!\nClick 'Bake All Chunks' to begin baking.", "OK");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. BAKE ALL CHUNKS (ASYNC via EditorApplication.delayCall)
    // ─────────────────────────────────────────────────────────────────────────────
    public void BakeAllChunks()
    {
        if (mazeRoot == null)
        {
            Debug.LogWarning("[ChunkedNavMeshBaker] Cannot bake: Maze Root is not assigned!");
            EditorUtility.DisplayDialog("Missing Maze Root", "Please assign the Maze Root Transform first.", "OK");
            return;
        }

        List<NavMeshSurface> surfaces = FindChunkSurfaces();
        if (surfaces == null || surfaces.Count == 0)
        {
            Debug.LogWarning("[ChunkedNavMeshBaker] No NavMeshSurface chunks found! Creating chunks first...");
            CreateChunksInternal(false);
            surfaces = FindChunkSurfaces();

            if (surfaces == null || surfaces.Count == 0)
            {
                Debug.LogError("[ChunkedNavMeshBaker] Failed to generate chunks!");
                EditorUtility.DisplayDialog("Error", "Could not create chunks. Please verify your Maze Root has colliders.", "OK");
                return;
            }
        }

        EnsureNavMeshFolderExists();

        _bakeQueue.Clear();
        foreach (var s in surfaces)
        {
            if (s != null) _bakeQueue.Enqueue(s);
        }

        _totalChunksToBake = _bakeQueue.Count;
        _bakedCount = 0;
        _isBaking = true;
        _totalStopwatch = Stopwatch.StartNew();

        Debug.Log($"<color=cyan>[ChunkedNavMeshBaker]</color> Starting asynchronous chunked bake for {_totalChunksToBake} chunks...");

        // Kick off the queue on the next editor tick
        EditorApplication.delayCall += ProcessBakeQueueStep;
    }

    private void ProcessBakeQueueStep()
    {
        if (!_isBaking)
        {
            EditorUtility.ClearProgressBar();
            return;
        }

        try
        {
            if (_bakeQueue.Count == 0)
            {
                FinishBaking();
                return;
            }

            NavMeshSurface currentSurface = _bakeQueue.Dequeue();
            if (currentSurface == null)
            {
                _bakedCount++;
                EditorApplication.delayCall += ProcessBakeQueueStep;
                return;
            }

            string chunkName = currentSurface.gameObject.name;
            float progress = (float)_bakedCount / _totalChunksToBake;

            // Display cancelable progress bar
            bool userCancelled = EditorUtility.DisplayCancelableProgressBar(
                "Baking NavMesh Chunks (Zero Freeze)",
                $"Baking {chunkName} ({_bakedCount + 1}/{_totalChunksToBake})... Please wait.",
                progress
            );

            if (userCancelled)
            {
                CancelBake("User pressed Cancel on progress bar");
                return;
            }

            // Bake this single surface
            Stopwatch sw = Stopwatch.StartNew();
            currentSurface.BuildNavMesh();
            sw.Stop();

            double elapsedSec = sw.Elapsed.TotalSeconds;
            Debug.Log($"<color=green>[ChunkedNavMeshBaker]</color> Baked '{chunkName}' in {elapsedSec:F2} seconds ({_bakedCount + 1}/{_totalChunksToBake}).");

            // Save the generated NavMeshData asset to disk
            SaveNavMeshDataAsset(currentSurface);

            _bakedCount++;

            Repaint();

            // Schedule the next chunk bake on the next frame so Editor can repaint
            EditorApplication.delayCall += ProcessBakeQueueStep;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChunkedNavMeshBaker] Exception during chunk bake: {ex.Message}\n{ex.StackTrace}");
            CancelBake("Exception encountered during bake");
        }
        finally
        {
            if (!_isBaking || _bakeQueue.Count == 0)
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }

    private void FinishBaking()
    {
        _isBaking = false;
        _bakeQueue.Clear();
        EditorUtility.ClearProgressBar();

        if (_totalStopwatch != null)
        {
            _totalStopwatch.Stop();
            Debug.Log($"<color=cyan>══════════════════════════════════════════════════════════════</color>");
            Debug.Log($"<color=green><b>[ChunkedNavMeshBaker] ✔ ALL {_totalChunksToBake} CHUNKS BAKED SUCCESSFULLY!</b></color> Total time: {_totalStopwatch.Elapsed.TotalSeconds:F2} seconds.");
        }

        AssetDatabase.SaveAssets();

        if (mazeRoot != null)
        {
            EditorSceneManager.MarkSceneDirty(mazeRoot.gameObject.scene);
        }

        Repaint();
        EditorUtility.DisplayDialog(
            "⚡ NavMesh Bake Complete!",
            $"All {_totalChunksToBake} NavMesh chunks baked successfully without freezing the editor!\n\n" +
            $"• NavMeshData assets saved under '{NavMeshSaveDirectory}/'.\n" +
            $"• Scene saved & marked dirty.\n" +
            $"• All NavMeshAgents (player, zombies, spiders) can now navigate the full maze seamlessly!",
            "Awesome!"
        );
    }

    private void CancelBake(string reason)
    {
        _isBaking = false;
        _bakeQueue.Clear();
        EditorUtility.ClearProgressBar();

        if (_totalStopwatch != null) _totalStopwatch.Stop();

        Debug.LogWarning($"[ChunkedNavMeshBaker] Baking was cancelled ({reason}). Completed {_bakedCount}/{_totalChunksToBake} chunks.");
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. CLEAR CHUNKS
    // ─────────────────────────────────────────────────────────────────────────────
    public void ClearChunks()
    {
        if (mazeRoot == null)
        {
            Debug.LogWarning("[ChunkedNavMeshBaker] Maze Root is not assigned!");
            return;
        }

        ClearChunksInternal(true);
    }

    private void ClearChunksInternal(bool showDialog)
    {
        int removedCount = 0;

        Transform container = mazeRoot.Find(ChunksContainerName);
        if (container != null)
        {
            NavMeshSurface[] surfaces = container.GetComponentsInChildren<NavMeshSurface>(true);
            foreach (var s in surfaces)
            {
                if (s != null)
                {
                    s.RemoveData();
                    removedCount++;
                }
            }
            Undo.DestroyObjectImmediate(container.gameObject);
        }

        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in mazeRoot)
        {
            if (child != null && child.name.StartsWith("NavChunk_"))
            {
                toDestroy.Add(child.gameObject);
            }
        }

        foreach (var go in toDestroy)
        {
            NavMeshSurface s = go.GetComponent<NavMeshSurface>();
            if (s != null) s.RemoveData();
            Undo.DestroyObjectImmediate(go);
            removedCount++;
        }

        EditorSceneManager.MarkSceneDirty(mazeRoot.gameObject.scene);

        if (showDialog)
        {
            Debug.Log($"<color=yellow>[ChunkedNavMeshBaker]</color> Cleared {removedCount} chunk surfaces from '{mazeRoot.name}'.");
            EditorUtility.DisplayDialog("Chunks Cleared", $"Removed {removedCount} NavMesh chunks from '{mazeRoot.name}'.", "OK");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. SMART DECORATION & OLD SURFACE CLEANUP
    // ─────────────────────────────────────────────────────────────────────────────
    private void DisableOldFreezingSurfaces()
    {
        // Find any full-scene NavMeshSurface that collects everything
        NavMeshSurface[] allSurfaces = Resources.FindObjectsOfTypeAll<NavMeshSurface>();
        int disabledCount = 0;

        foreach (var s in allSurfaces)
        {
            if (s == null || !s.gameObject.scene.isLoaded) continue;

            // Don't disable our chunk surfaces
            if (s.name.StartsWith("NavChunk_")) continue;

            // If it's a global surface (CollectObjects.All)
            if (s.collectObjects == CollectObjects.All)
            {
                Undo.RecordObject(s, "Disable Old Full Scene NavMeshSurface");
                s.enabled = false;
                EditorUtility.SetDirty(s);
                disabledCount++;
                Debug.Log($"<color=yellow>[ChunkedNavMeshBaker]</color> Disabled old full-scene NavMeshSurface on '{s.gameObject.name}' to prevent freezing.");
            }
        }

        if (disabledCount > 0)
        {
            Debug.Log($"<color=green>[ChunkedNavMeshBaker]</color> Disabled {disabledCount} old global surface(s).");
        }
    }

    private void AutoMarkCommonDecorations()
    {
        if (mazeRoot == null) return;

        Transform[] allTransforms = mazeRoot.GetComponentsInChildren<Transform>(true);
        int markedCount = 0;

        string[] ignoreKeywords = new string[]
        {
            "torch", "mashal", "fire", "light", "particle", "smoke", "spark",
            "tree", "grass", "bush", "fern", "flower", "foliage",
            "prop", "decoration", "rubble", "rock_small", "pebble"
        };

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Auto Mark Decorations As Ignored");

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            string lowName = t.name.ToLowerInvariant();
            bool matchesKeyword = false;

            for (int i = 0; i < ignoreKeywords.Length; i++)
            {
                if (lowName.Contains(ignoreKeywords[i]))
                {
                    matchesKeyword = true;
                    break;
                }
            }

            // Check if it's a trigger collider or particle system
            bool hasCollider = t.GetComponent<Collider>() != null;
            bool isParticle = t.GetComponent<ParticleSystem>() != null;

            if ((matchesKeyword || isParticle) && (hasCollider || isParticle))
            {
                NavMeshModifier mod = t.GetComponent<NavMeshModifier>();
                if (mod == null)
                {
                    mod = Undo.AddComponent<NavMeshModifier>(t.gameObject);
                }
                else
                {
                    Undo.RecordObject(mod, "Auto Mark Ignored");
                }
                mod.ignoreFromBuild = true;
                EditorUtility.SetDirty(mod);
                markedCount++;
            }
        }

        if (markedCount > 0)
        {
            Debug.Log($"<color=green>[ChunkedNavMeshBaker]</color> Auto-marked {markedCount} props/torches/foliage with NavMeshModifier (Ignore From Build = true).");
        }
    }

    public void MarkDecorationsAsIgnored()
    {
        if (mazeRoot == null)
        {
            Debug.LogWarning("[ChunkedNavMeshBaker] Maze Root is not assigned!");
            EditorUtility.DisplayDialog("Missing Maze Root", "Please assign the Maze Root Transform first.", "OK");
            return;
        }

        if (ignoreLayers.value == 0)
        {
            Debug.LogWarning("[ChunkedNavMeshBaker] No Ignore Layers selected! Please select layers to ignore first.");
            EditorUtility.DisplayDialog("No Ignore Layers", "Please select at least one layer in the 'Ignore Layers' mask.", "OK");
            return;
        }

        Transform[] allTransforms = mazeRoot.GetComponentsInChildren<Transform>(true);
        int markedCount = 0;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Mark Decorations As Ignored From NavMesh");

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            if (((1 << t.gameObject.layer) & ignoreLayers.value) != 0)
            {
                NavMeshModifier mod = t.GetComponent<NavMeshModifier>();
                if (mod == null)
                {
                    mod = Undo.AddComponent<NavMeshModifier>(t.gameObject);
                }
                else
                {
                    Undo.RecordObject(mod, "Set Ignore From Build");
                }

                mod.ignoreFromBuild = true;
                EditorUtility.SetDirty(mod);
                markedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(mazeRoot.gameObject.scene);

        Debug.Log($"<color=green>[ChunkedNavMeshBaker]</color> Successfully marked {markedCount} decoration object(s) with NavMeshModifier (Ignore From Build = true).");
        EditorUtility.DisplayDialog("Decorations Marked", $"Successfully marked {markedCount} decoration object(s) under '{mazeRoot.name}' to be ignored during NavMesh baking.", "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ASSET DATABASE & PERSISTENCE HELPERS
    // ─────────────────────────────────────────────────────────────────────────────
    private static void EnsureNavMeshFolderExists()
    {
        if (!Directory.Exists(NavMeshSaveDirectory))
        {
            Directory.CreateDirectory(NavMeshSaveDirectory);
            AssetDatabase.Refresh();
        }
    }

    private static void SaveNavMeshDataAsset(NavMeshSurface surface)
    {
        if (surface == null || surface.navMeshData == null) return;

        EnsureNavMeshFolderExists();

        string sceneName = surface.gameObject.scene.name;
        if (string.IsNullOrEmpty(sceneName)) sceneName = "MazeScene";

        string assetPath = $"{NavMeshSaveDirectory}/{sceneName}_{surface.gameObject.name}.asset";

        if (!AssetDatabase.Contains(surface.navMeshData))
        {
            NavMeshData oldAsset = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (oldAsset != null && oldAsset != surface.navMeshData)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
            Debug.Log($"<color=cyan>[ChunkedNavMeshBaker]</color> Created NavMeshData asset at: {assetPath}");
        }
        else
        {
            EditorUtility.SetDirty(surface.navMeshData);
        }

        EditorUtility.SetDirty(surface);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // UTILITY METHODS
    // ─────────────────────────────────────────────────────────────────────────────
    private List<NavMeshSurface> FindChunkSurfaces()
    {
        if (mazeRoot == null) return new List<NavMeshSurface>();

        List<NavMeshSurface> list = new List<NavMeshSurface>();

        Transform container = mazeRoot.Find(ChunksContainerName);
        if (container != null)
        {
            list.AddRange(container.GetComponentsInChildren<NavMeshSurface>(true));
        }

        foreach (Transform child in mazeRoot)
        {
            if (child != null && child.name.StartsWith("NavChunk_"))
            {
                NavMeshSurface s = child.GetComponent<NavMeshSurface>();
                if (s != null && !list.Contains(s)) list.Add(s);
            }
        }

        return list;
    }

    private static bool CalculateMazeBounds(Transform root, out Bounds combinedBounds)
    {
        combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null) return false;

        bool hasBounds = false;

        // 1. Encapsulate colliders under root
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col == null || col.isTrigger) continue;

            if (!hasBounds)
            {
                combinedBounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(col.bounds);
            }
        }

        // 2. Also check if "Maze terrain" exists separately in the scene and encapsulate it
        GameObject mazeTerrain = GameObject.Find("Maze terrain");
        if (mazeTerrain != null && mazeTerrain.transform != root)
        {
            Collider[] terrainCols = mazeTerrain.GetComponentsInChildren<Collider>(true);
            foreach (var col in terrainCols)
            {
                if (col == null || col.isTrigger) continue;
                if (!hasBounds)
                {
                    combinedBounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(col.bounds);
                }
            }
        }

        // 3. Encapsulate renderers under root
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer) continue;

            if (!hasBounds)
            {
                combinedBounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(r.bounds);
            }
        }

        return hasBounds;
    }

    private static int GetHumanoidAgentTypeId()
    {
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            string name = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
            if (string.Equals(name, "Humanoid", StringComparison.OrdinalIgnoreCase))
            {
                return settings.agentTypeID;
            }
        }
        return count > 0 ? NavMesh.GetSettingsByIndex(0).agentTypeID : 0;
    }

    private void AutoDetectMazeRoot()
    {
        GameObject mazeObj = GameObject.Find("Maze");
        if (mazeObj != null)
        {
            mazeRoot = mazeObj.transform;
            return;
        }

        GameObject mazeTerrain = GameObject.Find("Maze terrain");
        if (mazeTerrain != null)
        {
            mazeRoot = mazeTerrain.transform;
            return;
        }

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var t in allTransforms)
        {
            if (t != null && t.gameObject.scene.isLoaded && t.name.Equals("Maze", StringComparison.OrdinalIgnoreCase))
            {
                mazeRoot = t;
                return;
            }
        }
    }

    private static LayerMask LayerMaskField(string label, LayerMask layerMask)
    {
        List<string> layers = new List<string>();
        List<int> layerNumbers = new List<int>();

        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                layers.Add(layerName);
                layerNumbers.Add(i);
            }
        }

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & layerMask.value) > 0)
                maskWithoutEmpty |= (1 << i);
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

        int mask = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
                mask |= (1 << layerNumbers[i]);
        }

        layerMask.value = mask;
        return layerMask;
    }
}
#endif
