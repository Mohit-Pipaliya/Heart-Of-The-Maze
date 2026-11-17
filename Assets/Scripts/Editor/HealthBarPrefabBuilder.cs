#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility to generate AAA Theme Health Bar prefabs
/// (Player, Zombie, Spider, Boss) in Assets/Prefeb/UI/
/// and bake them directly into enemy prefabs.
/// </summary>
public static class HealthBarPrefabBuilder
{
    private const string UI_PREFAB_DIR = "Assets/Prefeb/UI";

    [MenuItem("Tools/Theme Health Bar/Create All 4 Health Bar Prefabs %#h")]
    public static void CreateAllPrefabs()
    {
        EnsureDirectory(UI_PREFAB_DIR);

        CreatePlayerPrefab();
        CreateZombiePrefab();
        CreateSpiderPrefab();
        CreateBossPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#00FF88><b>[HealthBarPrefabBuilder] Successfully generated Player, Zombie, Spider, and Boss Health Bar prefabs in Assets/Prefeb/UI!</b></color>");
    }

    [MenuItem("Tools/Theme Health Bar/Bake Health Bars Into Enemy Prefabs")]
    public static void BakeHealthBarsIntoEnemyPrefabs()
    {
        string[] zombiePrefabs = new string[]
        {
            "Assets/Prefeb/Enemy/FemaleZombie_Enemy.prefab",
            "Assets/Prefeb/Enemy/Zombie Police.prefab"
        };

        string spiderPrefab = "Assets/Prefeb/Enemy/SpiderSkin1_1 Variant.prefab";

        int bakedCount = 0;

        foreach (var path in zombiePrefabs)
        {
            GameObject enemyContents = PrefabUtility.LoadPrefabContents(path);
            if (enemyContents != null)
            {
                HealthBar existing = enemyContents.GetComponentInChildren<HealthBar>(true);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                HealthBar hb = HealthBar.AttachToEnemy(enemyContents, HealthBar.BarType.Zombie, 1000f);
                if (hb != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(enemyContents, path);
                    bakedCount++;
                    Debug.Log($"<color=#00FF88>[HealthBarPrefabBuilder] Baked Zombie Health Bar into '{path}'</color>");
                }
                PrefabUtility.UnloadPrefabContents(enemyContents);
            }
        }

        GameObject spiderContents = PrefabUtility.LoadPrefabContents(spiderPrefab);
        if (spiderContents != null)
        {
            HealthBar existing = spiderContents.GetComponentInChildren<HealthBar>(true);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            HealthBar hb = HealthBar.AttachToEnemy(spiderContents, HealthBar.BarType.Spider, 1000f);
            if (hb != null)
            {
                PrefabUtility.SaveAsPrefabAsset(spiderContents, spiderPrefab);
                bakedCount++;
                Debug.Log($"<color=#00FF88>[HealthBarPrefabBuilder] Baked Spider Health Bar into '{spiderPrefab}'</color>");
            }
            PrefabUtility.UnloadPrefabContents(spiderContents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=#00FF88><b>[HealthBarPrefabBuilder] Finished baking Health Bars into {bakedCount} enemy prefabs!</b></color>");
    }

    [InitializeOnLoadMethod]
    private static void AutoGenerateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            string testPath = $"{UI_PREFAB_DIR}/ZombieHealthBar.prefab";
            if (!System.IO.File.Exists(testPath))
            {
                Debug.Log("[HealthBarPrefabBuilder] Generating missing UI Health Bar prefabs...");
                CreateAllPrefabs();
                BakeHealthBarsIntoEnemyPrefabs();
            }
        };
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path);
            string folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureDirectory(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void CreatePlayerPrefab()
    {
        string path = $"{UI_PREFAB_DIR}/PlayerHealthBar.prefab";
        GameObject go = new GameObject("PlayerHealthBar", typeof(RectTransform), typeof(HealthBar));
        HealthBar hb = go.GetComponent<HealthBar>();
        hb.SetupPlayerBarInEditor();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void CreateZombiePrefab()
    {
        string path = $"{UI_PREFAB_DIR}/ZombieHealthBar.prefab";
        GameObject go = new GameObject("ZombieHealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(HealthBar));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.transform.localScale = Vector3.one * 0.0075f;

        HealthBar hb = go.GetComponent<HealthBar>();
        hb.SetupZombieBarInEditor();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void CreateSpiderPrefab()
    {
        string path = $"{UI_PREFAB_DIR}/SpiderHealthBar.prefab";
        GameObject go = new GameObject("SpiderHealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(HealthBar));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.transform.localScale = Vector3.one * 0.0075f;

        HealthBar hb = go.GetComponent<HealthBar>();
        hb.SetupSpiderBarInEditor();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void CreateBossPrefab()
    {
        string path = $"{UI_PREFAB_DIR}/BossHealthBar.prefab";
        GameObject go = new GameObject("BossHealthBar", typeof(RectTransform), typeof(HealthBar));
        HealthBar hb = go.GetComponent<HealthBar>();
        hb.type = HealthBar.BarType.Boss;
        hb.EnsureHierarchy();
        hb.ApplyThemeStyling();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 50f);
        rt.sizeDelta = new Vector2(700f, 96f);

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }
}
#endif
