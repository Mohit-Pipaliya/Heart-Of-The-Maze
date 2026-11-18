#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HealthBarAutoSetup
/// =================
/// 1-Click Setup for Themed Health Bars matching the SVG reference mockup:
/// - Player Bar (Top-Left, 600x80)
/// - Zombie Bar (Middle-Left, 400x44)
/// - Spider Bar (Middle-Right, 480x80)
/// - Boss Bar (Bottom-Center, 700x96 with Title Plate "NAGA — GUARDIAN OF THE HEART")
/// </summary>
public static class HealthBarAutoSetup
{
    private const string CanvasName = "HealthBars_ShowcaseCanvas";

    [MenuItem("Tools/Heart Of The Maze/⚡ 1-Click Setup Game Health Bars (Player HUD + Floating Enemy Bars)", false, 0)]
    public static void OneClickSetupGameHealthBars()
    {
        // 1. Clean up any test showcase canvas that blocks the view
        GameObject oldShowcase = GameObject.Find(CanvasName);
        if (oldShowcase != null)
        {
            Undo.DestroyObjectImmediate(oldShowcase);
            Debug.Log("<color=yellow>[HealthBarAutoSetup] Removed test showcase canvas.</color>");
        }

        // 2. Locate or create the main Game UI Canvas
        Canvas gameCanvas = null;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.renderMode != RenderMode.WorldSpace && c.gameObject.name == "Game UI")
            {
                gameCanvas = c;
                break;
            }
        }
        if (gameCanvas == null)
        {
            foreach (Canvas c in canvases)
            {
                if (c.renderMode != RenderMode.WorldSpace)
                {
                    gameCanvas = c;
                    break;
                }
            }
        }

        if (gameCanvas == null)
        {
            GameObject canvasObj = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            gameCanvas = canvasObj.GetComponent<Canvas>();
            gameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameCanvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Game UI Canvas");
        }

        // 3. Setup Player Health Bar on Game UI (Top-Left 600x80)
        Transform existingPlayerBar = gameCanvas.transform.Find("PlayerHealthBar") ?? gameCanvas.transform.Find("Player Health Bar");
        GameObject playerBarGO;
        if (existingPlayerBar != null)
        {
            playerBarGO = existingPlayerBar.gameObject;
            Undo.RecordObject(playerBarGO, "Update Player Health Bar");
        }
        else
        {
            playerBarGO = new GameObject("PlayerHealthBar", typeof(RectTransform));
            playerBarGO.transform.SetParent(gameCanvas.transform, false);
            Undo.RegisterCreatedObjectUndo(playerBarGO, "Create Player Health Bar");
        }

        RectTransform pRect = (RectTransform)playerBarGO.transform;
        pRect.anchorMin = new Vector2(0f, 1f);
        pRect.anchorMax = new Vector2(0f, 1f);
        pRect.pivot = new Vector2(0f, 1f);
        pRect.anchoredPosition = new Vector2(24f, -24f);
        pRect.sizeDelta = new Vector2(600f, 80f);

        HealthBar pBar = playerBarGO.GetComponent<HealthBar>();
        if (pBar == null) pBar = playerBarGO.AddComponent<HealthBar>();
        pBar.barType = BarType.Player;

        // 4. Debug Input for testing D/H/R keys
        HealthBarDebugInput debug = gameCanvas.GetComponent<HealthBarDebugInput>();
        if (debug == null) debug = gameCanvas.gameObject.AddComponent<HealthBarDebugInput>();

        // 5. Connect Scene Enemies
        ZombieAI[] zombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        foreach (var z in zombies)
        {
            Undo.RecordObject(z, "Enable Zombie Health Bar");
            z.showFloatingHealthBar = true;
        }

        SpiderEnemy[] spiders = Object.FindObjectsByType<SpiderEnemy>(FindObjectsSortMode.None);
        foreach (var s in spiders)
        {
            Undo.RecordObject(s, "Enable Spider Health Bar");
            s.showFloatingHealthBar = true;
        }

        HumanSnakeEnemy[] snakes = Object.FindObjectsByType<HumanSnakeEnemy>(FindObjectsSortMode.None);
        foreach (var sn in snakes)
        {
            Undo.RecordObject(sn, "Enable Boss Snake Health Bar");
            sn.showFloatingHealthBar = true;
        }

        EditorSceneManager.MarkSceneDirty(gameCanvas.gameObject.scene);

        string report = $"🎉 Game Health Bars Connected Successfully!\n\n" +
                        $"• Player Health Bar: Positioned at Top-Left (600x80) on {gameCanvas.gameObject.name}\n" +
                        $"• Zombie Enemies Connected: {zombies.Length} (Floating Rust Skull Bar)\n" +
                        $"• Spider Enemies Connected: {spiders.Length} (Floating Chitin Hex Bar)\n" +
                        $"• Boss Snake Enemies Connected: {snakes.Length} (Floating Naga Gold Bar)\n\n" +
                        $"🎮 In Play Mode:\n" +
                        $"  - Approach enemies to see their 3D health bars float above them!\n" +
                        $"  - Press [D] to damage, [H] to heal, [R] to reset full health.";

        EditorUtility.DisplayDialog("Themed Health Bars Setup", report, "OK");
        Debug.Log($"<color=#00FF88><b>[HealthBarAutoSetup] {report}</b></color>");
    }

    [MenuItem("Tools/Heart Of The Maze/Preview 4-Bar Showcase Sheet (Canvas Grid)", false, 1)]
    [MenuItem("Tools/Health Bar/Preview 4-Bar Showcase Sheet (Canvas Grid)", false, 1)]
    public static void OneClickSetupShowcase()
    {
        // 1. Find or create Showcase Canvas
        GameObject canvasObj = GameObject.Find(CanvasName);
        if (canvasObj == null)
        {
            canvasObj = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Themed Health Bars Showcase");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(canvasObj, "Update Themed Health Bars Showcase");
            // Clear existing children for a clean rebuild
            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }
        }

        canvasObj.layer = LayerMask.NameToLayer("UI");

        // 2. Configure Canvas & Scaler (1920x1080 match 0.5)
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
        if (raycaster == null) canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Dark Backdrop matching reference image (#0b0c10)
        GameObject bgObj = new GameObject("Background_DarkBackdrop", typeof(RectTransform), typeof(Image));
        bgObj.layer = canvasObj.layer;
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = (RectTransform)bgObj.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.043f, 0.047f, 0.063f, 0.96f); // #0b0c10
        bgImg.raycastTarget = false;

        // 4. Global Debug Input Handler on Canvas
        HealthBarDebugInput debugInput = canvasObj.GetComponent<HealthBarDebugInput>();
        if (debugInput == null) debugInput = canvasObj.AddComponent<HealthBarDebugInput>();

        // 5. Build Bar 1: Player (Top-Left)
        CreatePlayerBar(canvasObj.transform);

        // 6. Build Bar 2: Zombie (Middle-Left)
        CreateZombieBar(canvasObj.transform);

        // 7. Build Bar 3: Spider (Middle-Right)
        CreateSpiderBar(canvasObj.transform);

        // 8. Build Bar 4: Boss (Bottom-Center)
        CreateBossBar(canvasObj.transform);

        // 9. Top Info Banner
        CreateTopInfoBanner(canvasObj.transform);

        // Selection & Scene Dirty
        Selection.activeGameObject = canvasObj;
        EditorGUIUtility.PingObject(canvasObj);
        EditorSceneManager.MarkSceneDirty(canvasObj.scene);

        string msg = "🎉 Themed Health Bars Setup 100% Complete!\n\n" +
                     "✅ Exact reference geometry matches your SVG mockup:\n" +
                     "   • 1. Player Bar (Top-Left): 600x80 steel frame, carbon-fiber pattern, cyan accents, left bolt.\n" +
                     "   • 2. Zombie Bar (Middle-Left): 400x44 jagged rust/wood, skull icon, cracks, falling blood.\n" +
                     "   • 3. Spider Bar (Middle-Right): 480x80 chitin hex, spider icon, fangs below, web lines.\n" +
                     "   • 4. Boss Bar (Bottom-Center): 700x96 dual snake heads, gold scales, red jewel, emerald fill, Title Plate.\n\n" +
                     "🎮 PLAY MODE TESTING:\n" +
                     "   Press [D] = Damage 15% (smooth exp ease + ghost bar + sparks + shake)\n" +
                     "   Press [H] = Heal 25% (green pulse + rising particles)\n" +
                     "   Press [R] = Reset 100%\n" +
                     "   Under 25% HP = Scale pulse + outer glow oscillation + screen vignette!";

        EditorUtility.DisplayDialog("Themed Health Bars Setup", msg, "Awesome!");
        Debug.Log("<color=#00FF88><b>[HealthBarAutoSetup] Successfully generated themed health bars matching SVG mockup!</b></color>");
    }

    private static void CreatePlayerBar(Transform parent)
    {
        GameObject root = new GameObject("PlayerHealthBar", typeof(RectTransform));
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(60f, -50f);
        rect.sizeDelta = new Vector2(600f, 80f);

        HealthBar bar = root.AddComponent<HealthBar>();
        bar.barType = BarType.Player;
        root.AddComponent<HealthBarDebugInput>();

        // Label below: "Player, top-left"
        CreateLabel(root.transform, "Player, top-left", new Vector2(0f, -92f), new Vector2(250f, 30f), TextAlignmentOptions.TopLeft);
    }

    private static void CreateZombieBar(Transform parent)
    {
        GameObject root = new GameObject("ZombieHealthBar", typeof(RectTransform));
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-420f, 90f);
        rect.sizeDelta = new Vector2(400f, 44f);

        HealthBar bar = root.AddComponent<HealthBar>();
        bar.barType = BarType.Zombie;
        root.AddComponent<HealthBarDebugInput>();

        // Label below: "Zombie, world space"
        CreateLabel(root.transform, "Zombie, world space", new Vector2(0f, -55f), new Vector2(300f, 30f), TextAlignmentOptions.Center);
    }

    private static void CreateSpiderBar(Transform parent)
    {
        GameObject root = new GameObject("SpiderHealthBar", typeof(RectTransform));
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(380f, 90f);
        rect.sizeDelta = new Vector2(480f, 80f);

        HealthBar bar = root.AddComponent<HealthBar>();
        bar.barType = BarType.Spider;
        root.AddComponent<HealthBarDebugInput>();

        // Label below: "Spider, world space"
        CreateLabel(root.transform, "Spider, world space", new Vector2(0f, -65f), new Vector2(300f, 30f), TextAlignmentOptions.Center);
    }

    private static void CreateBossBar(Transform parent)
    {
        GameObject root = new GameObject("BossHealthBar", typeof(RectTransform));
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 75f);
        rect.sizeDelta = new Vector2(700f, 96f);

        HealthBar bar = root.AddComponent<HealthBar>();
        bar.barType = BarType.Boss;
        root.AddComponent<HealthBarDebugInput>();
    }

    private static void CreateLabel(Transform parent, string text, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions alignment)
    {
        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.layer = parent.gameObject.layer;
        labelObj.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)labelObj.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 17;
        tmp.color = new Color(0.55f, 0.60f, 0.68f, 1f); // #8c99ad
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
    }

    private static void CreateTopInfoBanner(Transform parent)
    {
        GameObject bannerObj = new GameObject("TopControlsBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
        bannerObj.layer = parent.gameObject.layer;
        bannerObj.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)bannerObj.transform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -16f);
        rect.sizeDelta = new Vector2(900f, 32f);

        TextMeshProUGUI tmp = bannerObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "⚡ THEMED HEALTH BARS — Press [D] Damage 15%  |  [H] Heal 25%  |  [R] Reset";
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.184f, 0.847f, 1f, 0.95f); // #2fd8ff
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }
}
#endif
