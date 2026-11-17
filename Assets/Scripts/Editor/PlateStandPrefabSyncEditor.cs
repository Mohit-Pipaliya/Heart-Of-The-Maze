#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility to copy and synchronize CameraStandpoint, PlayerStandPoint,
/// and Secret Room Door across Object 1 Stand.prefab through Object 8 Stand.prefab
/// with exact local position, rotation, scale, and serialized references in PlateStandInteraction.
/// </summary>
public static class PlateStandPrefabSyncEditor
{
    [MenuItem("Tools/Maze/Full Sync All Stands & Scene Prompts %#k")]
    public static void FullSyncAllStandsAndScene()
    {
        SyncCameraStandpointToAllStands();
        EnsurePromptInScene();
        LinkSecretRoomDoorsToAllStands();
        PositionAllStandsAtSecretDoors();
        AddStandsToMazePrefabs();
        Debug.Log("<color=#00FF88><b>[PlateStandPrefabSync] Complete Sync Finished! All 8 Stand prefabs & Scene UI are 100% ready!</b></color>");
    }

    [MenuItem("Tools/Maze/Sync CameraStandpoint To All Stands")]
    public static void SyncCameraStandpointToAllStands()
    {
        string stand1Path = "Assets/Prefeb/final object prefeb/Object 1 Stand.prefab";
        GameObject stand1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stand1Path);
        if (stand1Prefab == null)
        {
            Debug.LogError($"[PlateStandPrefabSync] Cannot find Object 1 Stand at '{stand1Path}'");
            return;
        }

        Transform cam1 = stand1Prefab.transform.Find("CameraStandpoint") ??
                         stand1Prefab.transform.Find("CameraStandPoint");

        if (cam1 == null)
        {
            Debug.LogError("[PlateStandPrefabSync] CameraStandpoint child not found on Object 1 Stand!");
            return;
        }

        Vector3 localPos = cam1.localPosition;
        Quaternion localRot = cam1.localRotation;
        Vector3 localScale = cam1.localScale;

        Debug.Log($"[PlateStandPrefabSync] Master CameraStandpoint from Object 1 Stand: Pos={localPos}, Rot={localRot.eulerAngles}, Scale={localScale}");

        int updatedCount = 0;
        for (int i = 1; i <= 8; i++)
        {
            string standPath = $"Assets/Prefeb/final object prefeb/Object {i} Stand.prefab";
            GameObject prefabObj = PrefabUtility.LoadPrefabContents(standPath);
            if (prefabObj == null)
            {
                Debug.LogWarning($"[PlateStandPrefabSync] Could not load prefab at '{standPath}'");
                continue;
            }

            Transform cam = prefabObj.transform.Find("CameraStandpoint") ??
                            prefabObj.transform.Find("CameraStandPoint");

            if (cam == null)
            {
                GameObject newCamGO = new GameObject("CameraStandpoint");
                newCamGO.transform.SetParent(prefabObj.transform, false);
                cam = newCamGO.transform;
            }

            cam.name = "CameraStandpoint";
            cam.localPosition = localPos;
            cam.localRotation = localRot;
            cam.localScale = localScale;

            // Link in PlateStandInteraction component
            PlateStandInteraction psi = prefabObj.GetComponentInChildren<PlateStandInteraction>(true);
            if (psi != null)
            {
                psi.cameraStandPoint = cam;
                psi.startUnderground = false;
                EditorUtility.SetDirty(psi);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabObj, standPath);
            PrefabUtility.UnloadPrefabContents(prefabObj);
            updatedCount++;
            Debug.Log($"[PlateStandPrefabSync] Synced Stand {i} to {standPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=#00FF88><b>[PlateStandPrefabSync] Successfully synchronized CameraStandpoint across all {updatedCount} stand prefabs!</b></color>");
    }

    [MenuItem("Tools/Maze/Ensure 'Press P to Place Object' In Game UI")]
    public static void EnsurePromptInScene()
    {
        Canvas gameUICanvas = null;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.name.Equals("Game UI", System.StringComparison.OrdinalIgnoreCase))
            {
                gameUICanvas = c;
                break;
            }
        }
        if (gameUICanvas == null && canvases.Length > 0) gameUICanvas = canvases[0];
        if (gameUICanvas == null)
        {
            Debug.LogError("[PlateStandPrefabSync] No Canvas found in scene!");
            return;
        }

        Transform existing = gameUICanvas.transform.Find("Press P to Place Object");
        if (existing == null)
        {
            // Find valid TMP Font
            TMP_FontAsset validFont = null;
            var tmps = gameUICanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t.font != null) { validFont = t.font; break; }
            }
            if (validFont == null) validFont = TMP_Settings.defaultFontAsset;

            GameObject promptObj = new GameObject("Press P to Place Object", typeof(RectTransform), typeof(Image));
            promptObj.transform.SetParent(gameUICanvas.transform, false);

            RectTransform rt = promptObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 65f);
            rt.sizeDelta = new Vector2(440f, 52f);

            Image bg = promptObj.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.06f, 0.92f);
            bg.raycastTarget = false;

            // Top Gold Border
            GameObject topBar = new GameObject("GoldBorderTop", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(promptObj.transform, false);
            RectTransform topRt = topBar.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = Vector2.zero;
            topRt.sizeDelta = new Vector2(0f, 3f);
            Image topImg = topBar.GetComponent<Image>();
            topImg.color = new Color(1f, 0.84f, 0.1f, 1f);
            topImg.raycastTarget = false;

            // Bottom Gold Border
            GameObject botBar = new GameObject("GoldBorderBottom", typeof(RectTransform), typeof(Image));
            botBar.transform.SetParent(promptObj.transform, false);
            RectTransform botRt = botBar.GetComponent<RectTransform>();
            botRt.anchorMin = new Vector2(0f, 0f);
            botRt.anchorMax = new Vector2(1f, 0f);
            botRt.pivot = new Vector2(0.5f, 0f);
            botRt.anchoredPosition = Vector2.zero;
            botRt.sizeDelta = new Vector2(0f, 3f);
            Image botImg = botBar.GetComponent<Image>();
            botImg.color = new Color(1f, 0.84f, 0.1f, 1f);
            botImg.raycastTarget = false;

            // Text
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(promptObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (validFont != null) tmp.font = validFont;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.88f, 0.15f, 1f);
            tmp.raycastTarget = false;
            tmp.text = "[P] Place Object";

            promptObj.SetActive(false);
            EditorUtility.SetDirty(promptObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#00FF88><b>[PlateStandPrefabSync] Created 'Press P to Place Object' in Game UI Canvas!</b></color>");
        }
    }

    [MenuItem("Tools/Maze/Link Secret Room Doors To All Stands")]
    public static void LinkSecretRoomDoorsToAllStands()
    {
        var stands = Object.FindObjectsByType<PlateStandInteraction>(FindObjectsSortMode.None);
        if (stands == null || stands.Length == 0)
        {
            Debug.LogWarning("[PlateStandPrefabSync] No PlateStandInteraction instances found in scene.");
            return;
        }

        int linkedCount = 0;
        foreach (var stand in stands)
        {
            stand.InitializeTargetDoor();
            if (stand.targetDoor != null)
            {
                EditorUtility.SetDirty(stand);
                linkedCount++;
                Debug.Log($"<color=#00FF88>[PlateStandPrefabSync] Stand {stand.standID} linked to door '{stand.targetDoor.name}' in '{stand.targetDoor.transform.parent?.name}'</color>");
            }
            else
            {
                Debug.LogWarning($"[PlateStandPrefabSync] Could not find Secret Room Door for Stand {stand.standID}");
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#00FF88><b>[PlateStandPrefabSync] Successfully linked {linkedCount} stands to their Secret Room Doors and saved scene!</b></color>");
    }

    [MenuItem("Tools/Maze/Position All Stands At Secret Room Doors %#l")]
    public static void PositionAllStandsAtSecretDoors()
    {
        var stands = Object.FindObjectsByType<PlateStandInteraction>(FindObjectsSortMode.None);
        if (stands == null || stands.Length == 0)
        {
            Debug.LogWarning("[PlateStandPrefabSync] No PlateStandInteraction instances found in scene.");
            return;
        }

        int positionedCount = 0;
        foreach (var stand in stands)
        {
            int id = stand.standID;
            if (id <= 0)
            {
                var match = System.Text.RegularExpressions.Regex.Match(stand.name, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int pid)) id = pid;
            }
            if (id <= 0) id = 1;

            stand.InitializeTargetDoor();
            if (stand.targetDoor == null)
            {
                Debug.LogWarning($"[PlateStandPrefabSync] Stand {id}: Could not find Secret Room Door!");
                continue;
            }

            Transform doorTr = stand.targetDoor.transform;

            // Offset in door local space: X = +40.4f (Right side), Z = +67.5f (Outside / in front)
            Vector3 doorLocalOffset = new Vector3(40.4f, 0f, 67.5f);
            Vector3 worldPos = doorTr.position + doorTr.TransformDirection(doorLocalOffset);

            float targetY = 4.1f;
            switch (id)
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

            // Rotation facing approach corridor (door euler Y + 90)
            float rotY = (doorTr.eulerAngles.y + 90f) % 360f;
            Quaternion worldRot = Quaternion.Euler(0f, rotY, 0f);

            Transform root = stand.transform;
            while (root.parent != null && root.parent.name.ToLower().Contains("stand") && !root.parent.name.ToLower().Contains("maze stands"))
            {
                root = root.parent;
            }

            Undo.RecordObject(root, "Position Stand At Secret Door");
            root.position = worldPos;
            root.rotation = worldRot;
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(stand);
            positionedCount++;

            Debug.Log($"<color=#00FF88>[PlateStandPrefabSync] Stand {id} ('{root.name}') positioned at {worldPos} (Rot={rotY}°) outside '{stand.targetDoor.name}'</color>");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#00FF88><b>[PlateStandPrefabSync] Successfully positioned {positionedCount} stands at their Secret Room Doors and saved scene!</b></color>");
    }

    [MenuItem("Tools/Maze/Add All Stands Directly Into Maze Prefabs %#m")]
    public static void AddStandsToMazePrefabs()
    {
        Vector3[] standLocalPositions = new Vector3[]
        {
            new Vector3(279.3f, 4.1f, 513.6f), // Maze 1
            new Vector3(631.7f, 4.0f, 513.8f), // Maze 2
            new Vector3(489.5f, 4.3f, 604.9f), // Maze 3
            new Vector3(488.9f, 5.3f, 261.1f), // Maze 4
            new Vector3(390.5f, 4.8f, 472.7f), // Maze 5
            new Vector3(736.2f, 4.2f, 472.9f), // Maze 6
            new Vector3(642.6f, 4.7f, 515.8f), // Maze 7
            new Vector3(636.4f, 4.5f, 534.2f)  // Maze 8
        };

        float[] standRotationsY = new float[]
        {
            0f,   // Maze 1
            0f,   // Maze 2
            270f, // Maze 3
            270f, // Maze 4
            180f, // Maze 5
            180f, // Maze 6
            0f,   // Maze 7
            0f    // Maze 8
        };

        int successCount = 0;
        for (int i = 1; i <= 8; i++)
        {
            string mazePath = $"Assets/Prefeb/Maze/Maze {i}.prefab";
            string standPath = $"Assets/Prefeb/final object prefeb/Object {i} Stand.prefab";

            GameObject standPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(standPath);
            if (standPrefab == null)
            {
                Debug.LogError($"[PlateStandPrefabSync] Stand prefab not found: {standPath}");
                continue;
            }

            GameObject mazeContents = PrefabUtility.LoadPrefabContents(mazePath);
            if (mazeContents == null)
            {
                Debug.LogError($"[PlateStandPrefabSync] Could not load maze prefab: {mazePath}");
                continue;
            }

            Transform existingStand = mazeContents.transform.Find($"Object {i} Stand");
            if (existingStand != null)
            {
                Object.DestroyImmediate(existingStand.gameObject);
            }

            GameObject standInst = (GameObject)PrefabUtility.InstantiatePrefab(standPrefab, mazeContents.transform);
            standInst.name = $"Object {i} Stand";
            standInst.transform.localPosition = standLocalPositions[i - 1];
            standInst.transform.localRotation = Quaternion.Euler(0f, standRotationsY[i - 1], 0f);

            PlateStandInteraction psi = standInst.GetComponentInChildren<PlateStandInteraction>(true);
            if (psi != null)
            {
                psi.standID = i;
                psi.displayName = $"Object {i} Stand";
                psi.startUnderground = false;

                // Also link targetDoor directly inside the maze prefab
                DoubleSlidingDoor dsd = mazeContents.GetComponentInChildren<DoubleSlidingDoor>(true);
                if (dsd != null)
                {
                    psi.targetDoor = dsd;
                }

                EditorUtility.SetDirty(psi);
            }

            PrefabUtility.SaveAsPrefabAsset(mazeContents, mazePath);
            PrefabUtility.UnloadPrefabContents(mazeContents);
            successCount++;
            Debug.Log($"<color=#00FF88>[PlateStandPrefabSync] Successfully added Object {i} Stand into '{mazePath}' at local {standLocalPositions[i - 1]}!</color>");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=#00FF88><b>[PlateStandPrefabSync] Finished! Added all {successCount} stands directly into Maze 1..8 prefabs!</b></color>");
    }

    [InitializeOnLoadMethod]
    private static void AutoSyncStandsIntoMazePrefabs()
    {
        EditorApplication.delayCall += () =>
        {
            string mazePath = "Assets/Prefeb/Maze/Maze 1.prefab";
            GameObject mazeContents = PrefabUtility.LoadPrefabContents(mazePath);
            if (mazeContents != null)
            {
                Transform stand = mazeContents.transform.Find("Object 1 Stand");
                PrefabUtility.UnloadPrefabContents(mazeContents);
                if (stand == null)
                {
                    Debug.Log("<color=#00FF88>[PlateStandPrefabSync] Auto-syncing stands into Maze 1..8 prefabs on editor reload...</color>");
                    AddStandsToMazePrefabs();
                }
            }
        };
    }
}
#endif
