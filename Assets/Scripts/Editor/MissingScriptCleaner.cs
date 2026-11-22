#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class MissingScriptCleaner
{
    static MissingScriptCleaner()
    {
        EditorApplication.delayCall += AutoClean;
    }

    private static void AutoClean()
    {
        CleanSceneObjects();
    }

    [MenuItem("Tools/Fix/Clean Missing Scripts From All Prefabs & Scenes %#m")]
    public static void CleanAllNow()
    {
        int totalRemoved = 0;
        totalRemoved += CleanSceneObjects();
        totalRemoved += CleanAllPrefabs();
        totalRemoved += CleanOpenPrefabStage();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#00FF88><b>[MissingScriptCleaner]</b></color> Cleanup complete! Total missing scripts removed: {totalRemoved}");
        EditorUtility.DisplayDialog("Missing Script Cleaner", $"Successfully removed {totalRemoved} missing scripts from Prefabs and Scenes!", "OK");
    }

    public static int CleanSceneObjects()
    {
        int removedCount = 0;
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            removedCount += CleanGameObjectRecursive(root);
        }
        return removedCount;
    }

    public static int CleanOpenPrefabStage()
    {
        int count = 0;
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot != null)
        {
            count = CleanGameObjectRecursive(stage.prefabContentsRoot);
            if (count > 0)
            {
                EditorUtility.SetDirty(stage.prefabContentsRoot);
            }
        }
        return count;
    }

    public static int CleanAllPrefabs()
    {
        int totalRemoved = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefeb" });

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                if (contents != null)
                {
                    int removed = CleanGameObjectRecursive(contents);
                    if (removed > 0)
                    {
                        try
                        {
                            PrefabUtility.SaveAsPrefabAsset(contents, path);
                            totalRemoved += removed;
                            Debug.Log($"<color=#00FF88><b>[MissingScriptCleaner]</b></color> Cleaned {removed} missing script(s) in Prefab: {path}");
                        }
                        catch (System.Exception saveEx)
                        {
                            Debug.LogWarning($"[MissingScriptCleaner] Could not save prefab {path}: {saveEx.Message}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MissingScriptCleaner] Could not process prefab at {path}: {ex.Message}");
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        return totalRemoved;
    }

    private static int CleanGameObjectRecursive(GameObject go)
    {
        if (go == null) return 0;

        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

        for (int i = 0; i < go.transform.childCount; i++)
        {
            Transform child = go.transform.GetChild(i);
            if (child != null)
            {
                count += CleanGameObjectRecursive(child.gameObject);
            }
        }

        return count;
    }
}
#endif
