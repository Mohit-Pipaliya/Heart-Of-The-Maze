#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameUIThemeStyler
{
    static GameUIThemeStyler()
    {
        EditorApplication.delayCall += ApplyThemeToScenePrompts;
    }

    [MenuItem("Tools/UI/Apply Golden HUD Theme To All Prompts %#u")]
    public static void ApplyThemeMenu()
    {
        ApplyThemeToScenePrompts();
        Debug.Log("<color=#FFD700><b>[GameUIThemeStyler] All HUD interaction prompts updated to sleek golden yellow theme!</b></color>");
    }

    public static void ApplyThemeToScenePrompts()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        string[] promptNames = new string[]
        {
            "Press E to Pickup Sword",
            "Press E to Pick Up Oar",
            "Press B to Drive Boat",
            "Press E to Pick Up Magazine",
            "Collect the Oar First",
            "Press E to Pickup M4",
            "Press C to Join The Meeting",
            "Press E to Pickup Mashal",
            "[ C ] Ladder Chadhne ke liye Dabao",
            "Drive Boat UI",
            "Collect Oar UI"
        };

        bool anyModified = false;

        foreach (string pName in promptNames)
        {
            Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.name.Equals(pName, System.StringComparison.OrdinalIgnoreCase))
                {
                    StylePromptObject(t.gameObject);
                    anyModified = true;
                }
            }
        }

        if (anyModified)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }
    }

    private static void StylePromptObject(GameObject obj)
    {
        Undo.RecordObject(obj, "Style HUD Prompt");

        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            rt.sizeDelta = new Vector2(Mathf.Max(380f, rt.sizeDelta.x), 48f);
        }

        // 1. Dark semi-transparent background box
        Image bgImage = obj.GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = obj.AddComponent<Image>();
        }
        bgImage.color = new Color(0.04f, 0.04f, 0.06f, 0.90f);
        bgImage.raycastTarget = false;

        // 2. Gold top and bottom accent bars
        Transform topBorder = obj.transform.Find("GoldBorderTop");
        if (topBorder == null)
        {
            GameObject tbObj = new GameObject("GoldBorderTop", typeof(RectTransform), typeof(Image));
            tbObj.transform.SetParent(obj.transform, false);
            topBorder = tbObj.transform;
        }
        RectTransform topRt = topBorder.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = Vector2.zero;
        topRt.sizeDelta = new Vector2(0f, 3f);
        Image topImg = topBorder.GetComponent<Image>();
        topImg.color = new Color(1f, 0.84f, 0.1f, 1f);
        topImg.raycastTarget = false;

        Transform bottomBorder = obj.transform.Find("GoldBorderBottom");
        if (bottomBorder == null)
        {
            GameObject bbObj = new GameObject("GoldBorderBottom", typeof(RectTransform), typeof(Image));
            bbObj.transform.SetParent(obj.transform, false);
            bottomBorder = bbObj.transform;
        }
        RectTransform bottomRt = bottomBorder.GetComponent<RectTransform>();
        bottomRt.anchorMin = new Vector2(0f, 0f);
        bottomRt.anchorMax = new Vector2(1f, 0f);
        bottomRt.pivot = new Vector2(0.5f, 0f);
        bottomRt.anchoredPosition = Vector2.zero;
        bottomRt.sizeDelta = new Vector2(0f, 3f);
        Image bottomImg = bottomBorder.GetComponent<Image>();
        bottomImg.color = new Color(1f, 0.84f, 0.1f, 1f);
        bottomImg.raycastTarget = false;

        // 3. Golden Yellow bold text
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
        }
        if (tmp != null)
        {
            tmp.color = new Color(1f, 0.88f, 0.15f, 1f);
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }
    }
}
#endif
