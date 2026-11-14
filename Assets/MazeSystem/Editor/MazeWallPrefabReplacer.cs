using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MazeSystem.EditorTools
{
    /// <summary>
    /// Replaces the scene's eight Maze N block instances with the authored
    /// "Maze 1..8" prefabs, keeping their slot pose and rewiring doors,
    /// slots, entrance marker, fog and gate.
    /// </summary>
    public static class MazeWallPrefabReplacer
    {
        private const string WallFolder = "Assets/Prefeb/Maze";
        // One-shot request files written by tooling; each is deleted once handled.
        private const string RequestPath = "Assets/MazeSystem/Generated/ReplaceMazeBlocks.request";
        private const string SaveRequestPath = "Assets/MazeSystem/Generated/SaveMazeScene.request";

        [MenuItem("Tools/Heart Of The Maze/Replace Maze Blocks With Wall Prefabs (1..8)", false, 22)]
        public static void ReplaceFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Replace Maze Blocks",
                    "Exit Play Mode before replacing the maze blocks.", "OK");
                return;
            }

            ReplaceBlocks();
        }

        [InitializeOnLoadMethod]
        private static void AutoRunRequestedReplacement()
        {
            if (!File.Exists(GetRequestAbsolutePath()))
                return;

            EditorApplication.delayCall += TryAutoRun;
        }

        private static double _lastRequestPollTime;

        /// <summary>
        /// Also polls while the editor is open so a request file written after the last
        /// domain reload is still handled without asking the user to touch the menu.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void HookRequestPolling()
        {
            EditorApplication.update -= PollRequests;
            EditorApplication.update += PollRequests;
        }

        private static void PollRequests()
        {
            if (EditorApplication.timeSinceStartup - _lastRequestPollTime < 1.0)
                return;

            _lastRequestPollTime = EditorApplication.timeSinceStartup;

            if (File.Exists(GetRequestAbsolutePath()))
                TryAutoRun();
            else if (File.Exists(GetSaveRequestAbsolutePath()))
                TryAutoSaveScene();
        }

        private static void TryAutoRun()
        {
            if (!File.Exists(GetRequestAbsolutePath()))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AssetDatabase.DeleteAsset(RequestPath);
            ReplaceBlocks();
        }

        private static string GetRequestAbsolutePath()
        {
            return Path.Combine(Application.dataPath, "MazeSystem/Generated/ReplaceMazeBlocks.request");
        }

        [InitializeOnLoadMethod]
        private static void AutoRunSaveRequested()
        {
            if (!File.Exists(GetSaveRequestAbsolutePath()))
                return;

            EditorApplication.delayCall += TryAutoSaveScene;
        }

        private static void TryAutoSaveScene()
        {
            if (!File.Exists(GetSaveRequestAbsolutePath()))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AssetDatabase.DeleteAsset(SaveRequestPath);

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isDirty && !string.IsNullOrEmpty(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[MazeSystem] Saved '{scene.path}' with the Maze wall 1..8 blocks.");
            }
        }

        private static string GetSaveRequestAbsolutePath()
        {
            return Path.Combine(Application.dataPath, "MazeSystem/Generated/SaveMazeScene.request");
        }

        private static void ReplaceBlocks()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded)
                return;

            MazeBlockManager manager = Object.FindFirstObjectByType<MazeBlockManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogError("[MazeSystem] Replace Maze Blocks: no MazeBlockManager in the active scene.");
                return;
            }

            var prefabs = new GameObject[MazeBlockManager.BlockCount];
            for (int n = 1; n <= MazeBlockManager.BlockCount; n++)
            {
                string path = ResolvePrefabPath(n);
                if (path == null)
                {
                    Debug.LogError($"[MazeSystem] Replace Maze Blocks: no prefab found for Maze {n} in '{WallFolder}'. " +
                                   $"Expected 'Maze wall {n}.prefab' or 'Maze {n}.prefab'. Nothing was changed.");
                    return;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"[MazeSystem] Replace Maze Blocks: could not load '{path}'. Nothing was changed.");
                    return;
                }

                if (prefab.GetComponentInChildren<MazeMainDoor>(true) == null)
                {
                    Debug.LogWarning($"[MazeSystem] '{path}' has no MazeMainDoor. Its block will be wired without a main door.");
                }

                prefabs[n - 1] = prefab;
            }

            MazeBlock[] existing = Object.FindObjectsByType<MazeBlock>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var current = new MazeBlock[MazeBlockManager.BlockCount];
            for (int i = 0; i < existing.Length; i++)
            {
                MazeBlock block = existing[i];
                if (block == null || block.gameObject.scene != scene)
                    continue;

                int index = block.BlockNumber - 1;
                if (index < 0 || index >= current.Length)
                    continue;

                if (current[index] != null && current[index] != block)
                {
                    Debug.LogError($"[MazeSystem] Replace Maze Blocks: two blocks claim Maze {block.BlockNumber}. Nothing was changed.");
                    return;
                }

                current[index] = block;
            }

            for (int i = 0; i < current.Length; i++)
                if (current[i] == null)
                {
                    Debug.LogError($"[MazeSystem] Replace Maze Blocks: Maze {i + 1} was not found. Nothing was changed.");
                    return;
                }

            var newBlocks = new MazeBlock[MazeBlockManager.BlockCount];
            int replaced = 0;
            bool sceneWasDirty = scene.isDirty;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Maze Blocks With Wall Prefabs");

            for (int i = 0; i < current.Length; i++)
            {
                MazeBlock oldBlock = current[i];
                int number = oldBlock.BlockNumber;
                string expectedPath = AssetDatabase.GetAssetPath(prefabs[number - 1]);

                if (GetPrefabPath(oldBlock.gameObject) == expectedPath)
                {
                    newBlocks[i] = oldBlock;
                    continue;
                }

                GameObject oldGo = oldBlock.gameObject;
                Transform parent = oldGo.transform.parent;
                if (parent == null)
                    parent = ResolveBlocksParent(manager);
                else if (parent.GetComponentInParent<MazeBlockManager>(true) != null)
                    parent = ResolveBlocksParent(manager);

                int siblingIndex = oldGo.transform.GetSiblingIndex();
                Vector3 worldPosition = oldGo.transform.position;
                Quaternion worldRotation = oldGo.transform.rotation;

                MazeSlot slot = manager.Slots != null && i < manager.Slots.Length ? manager.Slots[i] : null;
                if (slot != null && slot.footprintSize.sqrMagnitude > 0.0001f)
                {
                    worldPosition = slot.position;
                    worldRotation = slot.rotation;
                }

                GameObject newGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[number - 1], scene);
                Undo.RegisterCreatedObjectUndo(newGo, "Create Maze Wall Block");
                newGo.transform.SetParent(parent, false);
                newGo.transform.SetPositionAndRotation(worldPosition, worldRotation);
                newGo.transform.SetSiblingIndex(siblingIndex);
                newGo.name = $"Maze {number}";
                GameObjectUtility.SetStaticEditorFlags(newGo, 0);

                MazeMainDoor door = newGo.GetComponentInChildren<MazeMainDoor>(true);
                MazeBlock newBlock = newGo.GetComponent<MazeBlock>();
                if (newBlock == null)
                    newBlock = Undo.AddComponent<MazeBlock>(newGo);

                Undo.RecordObject(newBlock, "Configure Maze Block");
                newBlock.Configure(number, door != null ? door.transform : null, door);
                newBlock.CaptureOriginalState();
                EditorUtility.SetDirty(newBlock);

                Undo.DestroyObjectImmediate(oldGo);

                newBlocks[i] = newBlock;
                replaced++;
            }

            Undo.RecordObject(manager, "Reassign Maze Blocks");
            manager.EditorSetBlocks(newBlocks);
            manager.SnapBlocksToSlots();
            manager.CaptureSlotsFromCurrentLayout();

            MoveGladeAnchorsToActiveDoor(manager);
            EditorUtility.SetDirty(manager);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!sceneWasDirty)
                EditorSceneManager.SaveScene(scene);

            Debug.Log($"[MazeSystem] Maze blocks now use the Maze 1..8 prefabs. " +
                      $"Replaced {replaced}/8 instance(s). " +
                      (sceneWasDirty ? "Scene left dirty (save it when ready)." : "Scene saved."));

            Selection.activeGameObject = manager.gameObject;
        }

        private static string GetPrefabPath(GameObject go)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return source != null ? AssetDatabase.GetAssetPath(source) : null;
        }

        /// <summary>
        /// Finds the authored block prefab for Maze n. Accepts both the legacy
        /// "Maze wall n" naming and the current "Maze n" naming.
        /// </summary>
        private static string ResolvePrefabPath(int number)
        {
            string[] candidates =
            {
                $"{WallFolder}/Maze wall {number}.prefab",
                $"{WallFolder}/Maze {number}.prefab",
                $"{WallFolder}/MazeWall {number}.prefab",
                $"{WallFolder}/Maze Wall {number}.prefab"
            };

            for (int i = 0; i < candidates.Length; i++)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(candidates[i]) != null)
                    return candidates[i];

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { WallFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                string normalized = fileName.Replace(" ", string.Empty).ToLowerInvariant();
                if (normalized == $"mazewall{number}" || normalized == $"maze{number}" || normalized == $"wall{number}")
                    return path;
            }

            return null;
        }

        private static Transform ResolveBlocksParent(MazeBlockManager manager)
        {
            GameObject named = GameObject.Find("Maze");
            if (named != null && named.scene == manager.gameObject.scene && named != manager.gameObject)
                return named.transform;

            Transform parent = manager.transform.parent;
            return parent != null ? parent : manager.transform;
        }

        private static void MoveGladeAnchorsToActiveDoor(MazeBlockManager manager)
        {
            MazeBlock active = manager.GetBlock(MazeBlockManager.BlockCount);
            Transform marker = manager.GladeEntranceMarker;
            if (active == null || active.MainDoor == null || marker == null)
                return;

            Vector3 doorPosition = active.MainDoor.transform.position;
            Vector3 worldForward = -active.MainDoor.transform.forward;
            Quaternion markerRotation = Quaternion.LookRotation(worldForward, Vector3.up);
            Vector3 delta = doorPosition - marker.position;

            Undo.RecordObject(marker, "Move Glade Entrance Marker");
            marker.SetPositionAndRotation(doorPosition, markerRotation);

            if (manager.Fog != null)
            {
                Undo.RecordObject(manager.Fog.transform, "Move Glade Fog");
                manager.Fog.transform.position += delta;
            }

            if (manager.Gate != null)
            {
                Undo.RecordObject(manager.Gate.transform, "Move Glade Gate");
                manager.Gate.transform.position += delta;
            }
        }
    }
}
